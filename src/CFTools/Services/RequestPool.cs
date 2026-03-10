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

    private SemaphoreSlim _semaphore;
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
        double jitterFactor = 0.3)
    {
        _maxConcurrency = Math.Min(maxConcurrency, 8);
        _maxRetries = Math.Min(maxRetries, 5);
        _baseDelayMs = baseDelayMs;
        _maxDelayMs = maxDelayMs;
        _jitterFactor = jitterFactor;

        _semaphore = new SemaphoreSlim(_maxConcurrency, 8);
        _poolCts = new CancellationTokenSource();
    }

    /// <summary>
    /// Add a task to the pool. Returns when the task completes or fails after all retries.
    /// </summary>
    public Task<T> Add<T>(Func<CancellationToken, Task<T>> execute, CancellationToken cancellationToken = default)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            _poolCts?.Token ?? CancellationToken.None,
            cancellationToken);

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var task = new QueuedTask(
            async ct =>
            {
                var result = await execute(ct);
                tcs.TrySetResult(result);
            },
            linkedCts,
            SetException: ex => tcs.TrySetException(ex),
            SetCanceled: ct => tcs.TrySetCanceled(ct));

        lock (_lock)
        {
            _queue.Enqueue(task);
        }

        _ = ProcessQueue();

        return tcs.Task;
    }

    /// <summary>
    /// Pause processing. Running tasks complete, no new tasks start.
    /// </summary>
    public void Pause()
    {
        lock (_lock) { _paused = true; }
    }

    /// <summary>
    /// Resume processing after pause.
    /// </summary>
    public void Resume()
    {
        lock (_lock) { _paused = false; }
        _ = ProcessQueue();
    }

    /// <summary>
    /// Cancel all pending and running tasks.
    /// </summary>
    public void Cancel()
    {
        lock (_lock)
        {
            _poolCts?.Cancel();

            // Drain queue
            while (_queue.TryDequeue(out var task))
            {
                task.LinkedCts.Dispose();
            }

            // Reset CTS for future use
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
            _maxConcurrency = Math.Min(maxConcurrency, 8);
            // Semaphore replacement is complex; just update the cap for new tasks
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
        _semaphore.Dispose();
    }

    // ========================================================================
    // Private
    // ========================================================================

    private async Task ProcessQueue()
    {
        while (true)
        {
            QueuedTask? task;
            lock (_lock)
            {
                if (_paused || _queue.Count == 0)
                    return;

                task = _queue.Dequeue();
            }

            await _semaphore.WaitAsync(task.LinkedCts.Token).ConfigureAwait(false);
            Interlocked.Increment(ref _running);

            _ = ExecuteTask(task);
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
        }
        catch (OperationCanceledException)
        {
            task.SetCanceled?.Invoke(task.LinkedCts.Token);
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
                    // Re-enqueue at front (via temp queue swap to maintain order)
                    var items = _queue.ToArray();
                    _queue.Clear();
                    _queue.Enqueue(task);
                    foreach (var item in items)
                        _queue.Enqueue(item);
                }

                _ = ProcessQueue();
                return; // Don't release semaphore yet — ProcessQueue will re-acquire
            }
            else
            {
                task.SetException?.Invoke(ex);
                Interlocked.Increment(ref _failed);
            }
        }
        finally
        {
            Interlocked.Decrement(ref _running);
            _semaphore.Release();
            _ = ProcessQueue();
        }
    }

    private bool ShouldRetry(Exception error, int attempt)
    {
        if (attempt >= _maxRetries)
            return false;

        if (error is CfApiException cfEx)
            return cfEx.Normalized.Retryable;

        // Network errors are retryable
        if (error is HttpRequestException)
            return true;

        if (error is TaskCanceledException tce && tce.InnerException is TimeoutException)
            return true;

        return false;
    }

    private int CalculateDelay(Exception error, int attempt)
    {
        // Check for Retry-After
        if (error is CfApiException cfEx && cfEx.RetryAfterMs is > 0)
            return cfEx.RetryAfterMs.Value;

        // Exponential backoff: min(maxDelay, baseDelay * 2^attempt)
        var exponentialDelay = Math.Min(_maxDelayMs, _baseDelayMs * (int)Math.Pow(2, attempt));

        // Add jitter
        var jitter = _random.Next(0, (int)(_baseDelayMs * _jitterFactor));

        return exponentialDelay + jitter;
    }

    private record QueuedTask(
        Func<CancellationToken, Task> Execute,
        CancellationTokenSource LinkedCts,
        Action<Exception>? SetException = null,
        Action<CancellationToken>? SetCanceled = null)
    {
        public int Attempt { get; set; }
    }
}
