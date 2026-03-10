using CFTools.Models;

namespace CFTools.Services;

public record PoolStats(int Pending, int Running, int Completed, int Failed, bool Paused);

/// <summary>
/// Rate-limited request pool with concurrency control, exponential backoff, and pause/cancel.
/// </summary>
public sealed class RequestPool : IDisposable
{
    private readonly object _lock = new();
    private readonly Queue<QueuedTask> _queue = new();
    private readonly Random _random = new();

    private CancellationTokenSource? _poolCts;

    private int _maxConcurrency;
    private int _maxRetries;
    private int _baseDelayMs;
    private int _maxDelayMs;
    private double _jitterFactor;

    private int _running;
    private int _completed;
    private int _failed;
    private bool _paused;

    public RequestPool(
        int maxConcurrency = 4,
        int maxRetries = 3,
        int baseDelayMs = 500,
        int maxDelayMs = 20_000,
        double jitterFactor = 0.3
    )
    {
        _maxConcurrency = ClampConcurrency(maxConcurrency);
        _maxRetries = ClampRetries(maxRetries);
        _baseDelayMs = baseDelayMs;
        _maxDelayMs = maxDelayMs;
        _jitterFactor = jitterFactor;
        _poolCts = new CancellationTokenSource();
    }

    public Task<T> Add<T>(
        Func<CancellationToken, Task<T>> execute,
        CancellationToken cancellationToken = default
    )
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            _poolCts?.Token ?? CancellationToken.None,
            cancellationToken
        );

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var task = new QueuedTask(
            async ct =>
            {
                var result = await execute(ct);
                tcs.TrySetResult(result);
            },
            linkedCts,
            SetException: ex => tcs.TrySetException(ex),
            SetCanceled: ct => tcs.TrySetCanceled(ct)
        );

        lock (_lock)
        {
            _queue.Enqueue(task);
        }

        _ = ProcessQueue();
        return tcs.Task;
    }

    public void Pause()
    {
        lock (_lock)
        {
            _paused = true;
        }
    }

    public void Resume()
    {
        lock (_lock)
        {
            _paused = false;
        }
        _ = ProcessQueue();
    }

    public void Cancel()
    {
        lock (_lock)
        {
            _poolCts?.Cancel();

            while (_queue.TryDequeue(out var task))
                task.LinkedCts.Dispose();

            _poolCts?.Dispose();
            _poolCts = new CancellationTokenSource();
        }
    }

    public PoolStats GetStats()
    {
        lock (_lock)
        {
            return new PoolStats(_queue.Count, _running, _completed, _failed, _paused);
        }
    }

    public void UpdateConcurrency(int maxConcurrency)
    {
        lock (_lock)
        {
            _maxConcurrency = ClampConcurrency(maxConcurrency);
        }
        _ = ProcessQueue();
    }

    public void UpdateRetries(int maxRetries)
    {
        lock (_lock)
        {
            _maxRetries = ClampRetries(maxRetries);
        }
    }

    public void ResetStats()
    {
        lock (_lock)
        {
            _completed = 0;
            _failed = 0;
        }
    }

    public void Dispose()
    {
        _poolCts?.Cancel();
        _poolCts?.Dispose();
    }

    private async Task ProcessQueue()
    {
        while (true)
        {
            QueuedTask? task;
            lock (_lock)
            {
                if (_paused || _queue.Count == 0 || _running >= _maxConcurrency)
                    return;

                task = _queue.Dequeue();
                _running++;
            }

            _ = ExecuteTask(task);
            await Task.Yield();
        }
    }

    private async Task ExecuteTask(QueuedTask task)
    {
        try
        {
            var ct = task.LinkedCts.Token;
            ct.ThrowIfCancellationRequested();

            await task.Execute(ct).ConfigureAwait(false);
            Interlocked.Increment(ref _completed);
            task.LinkedCts.Dispose();
        }
        catch (OperationCanceledException)
        {
            task.SetCanceled?.Invoke(task.LinkedCts.Token);
            task.LinkedCts.Dispose();
        }
        catch (Exception ex)
        {
            if (ShouldRetry(ex, task.Attempt))
            {
                task.Attempt++;
                var delay = CalculateDelay(ex, task.Attempt);
                await Task.Delay(delay, task.LinkedCts.Token).ConfigureAwait(false);

                lock (_lock)
                {
                    var items = _queue.ToArray();
                    _queue.Clear();
                    _queue.Enqueue(task);
                    foreach (var item in items)
                        _queue.Enqueue(item);
                }

                _ = ProcessQueue();
                return;
            }

            task.SetException?.Invoke(ex);
            Interlocked.Increment(ref _failed);
            task.LinkedCts.Dispose();
        }
        finally
        {
            Interlocked.Decrement(ref _running);
            _ = ProcessQueue();
        }
    }

    private bool ShouldRetry(Exception error, int attempt)
    {
        if (attempt >= _maxRetries)
            return false;

        if (error is CfApiException cfEx)
            return cfEx.Normalized.Retryable;

        if (error is HttpRequestException)
            return true;

        if (error is TaskCanceledException tce && tce.InnerException is TimeoutException)
            return true;

        return false;
    }

    private int CalculateDelay(Exception error, int attempt)
    {
        if (error is CfApiException cfEx && cfEx.RetryAfterMs is > 0)
            return cfEx.RetryAfterMs.Value;

        var exponentialDelay = Math.Min(_maxDelayMs, _baseDelayMs * (int)Math.Pow(2, attempt));
        var jitter = _random.Next(0, (int)(_baseDelayMs * _jitterFactor));

        return exponentialDelay + jitter;
    }

    private static int ClampConcurrency(int value) => Math.Clamp(value, 1, 8);

    private static int ClampRetries(int value) => Math.Clamp(value, 0, 5);

    private sealed record QueuedTask(
        Func<CancellationToken, Task> Execute,
        CancellationTokenSource LinkedCts,
        Action<Exception>? SetException = null,
        Action<CancellationToken>? SetCanceled = null
    )
    {
        public int Attempt { get; set; }
    }
}
