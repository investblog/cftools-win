using CFTools.Models;
using CFTools.Services;
using Xunit;

namespace CFTools.Tests;

public class RequestPoolTests : IDisposable
{
    private readonly RequestPool _pool;

    public RequestPoolTests()
    {
        _pool = new RequestPool(maxConcurrency: 2, maxRetries: 2, baseDelayMs: 50, maxDelayMs: 200);
    }

    public void Dispose()
    {
        _pool.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Add_ExecutesTask_ReturnsResult()
    {
        var result = await _pool.Add(async ct =>
        {
            await Task.Delay(10, ct);
            return 42;
        });

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task Add_MultipleTasks_AllComplete()
    {
        var tasks = new List<Task<int>>();
        for (var i = 0; i < 5; i++)
        {
            var val = i;
            tasks.Add(
                _pool.Add(async ct =>
                {
                    await Task.Delay(10, ct);
                    return val;
                })
            );
        }

        var results = await Task.WhenAll(tasks);

        Assert.Equal(5, results.Length);
        var stats = _pool.GetStats();
        Assert.Equal(5, stats.Completed);
    }

    [Fact]
    public async Task Add_RespectsMaxConcurrency()
    {
        var maxConcurrent = 0;
        var currentConcurrent = 0;

        var tasks = new List<Task<int>>();
        for (var i = 0; i < 6; i++)
        {
            tasks.Add(
                _pool.Add(async ct =>
                {
                    var current = Interlocked.Increment(ref currentConcurrent);
                    int oldMax;
                    do
                    {
                        oldMax = maxConcurrent;
                    } while (
                        current > oldMax
                        && Interlocked.CompareExchange(ref maxConcurrent, current, oldMax) != oldMax
                    );

                    await Task.Delay(50, ct);
                    Interlocked.Decrement(ref currentConcurrent);
                    return current;
                })
            );
        }

        await Task.WhenAll(tasks);

        Assert.True(maxConcurrent <= 2, $"Max concurrent was {maxConcurrent}, expected <= 2");
    }

    [Fact]
    public async Task UpdateConcurrency_AppliesToFutureWork()
    {
        using var pool = new RequestPool(maxConcurrency: 1, maxRetries: 1, baseDelayMs: 10, maxDelayMs: 50);
        pool.UpdateConcurrency(3);

        var maxConcurrent = 0;
        var currentConcurrent = 0;
        var tasks = Enumerable.Range(0, 6).Select(_ =>
            pool.Add(async ct =>
            {
                var current = Interlocked.Increment(ref currentConcurrent);
                int oldMax;
                do
                {
                    oldMax = maxConcurrent;
                } while (
                    current > oldMax
                    && Interlocked.CompareExchange(ref maxConcurrent, current, oldMax) != oldMax
                );

                await Task.Delay(40, ct);
                Interlocked.Decrement(ref currentConcurrent);
                return current;
            })
        );

        await Task.WhenAll(tasks);

        Assert.True(maxConcurrent <= 3, $"Max concurrent was {maxConcurrent}, expected <= 3");
        Assert.True(maxConcurrent >= 2, $"Expected runtime concurrency update to increase throughput, got {maxConcurrent}");
    }

    [Fact]
    public async Task ZeroConcurrency_IsClampedAndStillProcessesWork()
    {
        using var pool = new RequestPool(maxConcurrency: 0);

        var result = await pool.Add(async ct =>
        {
            await Task.Delay(10, ct);
            return 7;
        });

        Assert.Equal(7, result);
    }

    [Fact]
    public async Task Add_RetryableError_Retries()
    {
        var attempts = 0;

        var result = await _pool.Add(ct =>
        {
            attempts++;
            if (attempts < 3)
                throw new CfApiException(ErrorNormalizer.Normalize(500, "Server error"));
            return Task.FromResult("ok");
        });

        Assert.Equal("ok", result);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task Add_NonRetryableError_FailsImmediately()
    {
        var attempts = 0;

        await Assert.ThrowsAsync<CfApiException>(async () =>
        {
            await _pool.Add<string>(ct =>
            {
                attempts++;
                throw new CfApiException(ErrorNormalizer.Normalize(10000, "Auth error"));
            });
        });

        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task Cancel_StopsProcessing()
    {
        var completed = 0;
        var tasks = new List<Task>();

        for (var i = 0; i < 10; i++)
        {
            tasks.Add(
                _pool.Add(async ct =>
                {
                    await Task.Delay(100, ct);
                    Interlocked.Increment(ref completed);
                    return 0;
                })
            );
        }

        await Task.Delay(50);
        _pool.Cancel();
        await Task.Delay(200);

        Assert.True(completed < 10, $"Expected fewer than 10 completions, got {completed}");
    }

    [Fact]
    public void GetStats_ReturnsCorrectState()
    {
        var stats = _pool.GetStats();

        Assert.Equal(0, stats.Pending);
        Assert.Equal(0, stats.Running);
        Assert.Equal(0, stats.Completed);
        Assert.Equal(0, stats.Failed);
        Assert.False(stats.Paused);
    }

    [Fact]
    public void Pause_SetsPausedFlag()
    {
        _pool.Pause();
        Assert.True(_pool.GetStats().Paused);

        _pool.Resume();
        Assert.False(_pool.GetStats().Paused);
    }
}
