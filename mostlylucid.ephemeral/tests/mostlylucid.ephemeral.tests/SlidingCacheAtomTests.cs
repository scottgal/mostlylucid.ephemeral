using Mostlylucid.Ephemeral.Atoms.SlidingCache;
using Xunit;

namespace Mostlylucid.Ephemeral.Tests;

public class SlidingCacheAtomTests
{
    [Fact]
    public async Task SlidingExpiration_IsResetOnAccess()
    {
        var computeCount = 0;

        await using var cache = new SlidingCacheAtom<string, int>(
            (_, _) =>
            {
                computeCount++;
                return Task.FromResult(42);
            },
            TimeSpan.FromMilliseconds(300),
            TimeSpan.FromSeconds(5));

        var first = await cache.GetOrComputeAsync("key");
        Assert.Equal(42, first);

        await Task.Delay(100); // below sliding window
        var second = await cache.GetOrComputeAsync("key");
        Assert.Equal(42, second);

        await Task.Delay(100); // still below refreshed sliding window
        var third = await cache.GetOrComputeAsync("key");
        Assert.Equal(42, third);

        Assert.Equal(1, computeCount);
    }

    [Fact]
    public async Task AbsoluteExpiration_IsEnforced()
    {
        var computeCount = 0;

        await using var cache = new SlidingCacheAtom<string, int>(
            (_, _) =>
            {
                computeCount++;
                return Task.FromResult(computeCount);
            },
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMilliseconds(150));

        var first = await cache.GetOrComputeAsync("key");
        Assert.Equal(1, first);

        await Task.Delay(220); // beyond absolute expiration
        var second = await cache.GetOrComputeAsync("key");

        Assert.Equal(2, computeCount); // recomputed
        Assert.Equal(2, second);
    }

    [Fact]
    public async Task ExpiredEntries_AreRemovedWhenOverCapacity()
    {
        var computeCount = 0;

        await using var cache = new SlidingCacheAtom<string, int>(
            (_, _) =>
            {
                computeCount++;
                return Task.FromResult(computeCount);
            },
            TimeSpan.FromMilliseconds(60),
            TimeSpan.FromMilliseconds(60),
            1);

        var first = await cache.GetOrComputeAsync("old");
        Assert.Equal(1, first);

        await Task.Delay(90); // allow "old" to expire
        var second = await cache.GetOrComputeAsync("new");
        Assert.Equal(2, second);

        // Adding "new" triggered cleanup; "old" should be gone
        Assert.False(cache.TryGet("old", out _));

        var stats = cache.GetStats();
        Assert.Equal(1, stats.TotalEntries);
        Assert.Equal(1, stats.ValidEntries);
        Assert.Equal(0, stats.ExpiredEntries);
    }

    [Fact]
    public async Task Eviction_WithRetentionScorer_KeepsHighRiskOverHighFrequency()
    {
        // Arrange: maxSize=2, one high-risk entry (1 access), one low-risk (10 accesses)
        await using var cache = new SlidingCacheAtom<string, (double risk, int dummy)>(
            (key, _) => Task.FromResult(key == "high-risk" ? (0.9, 0) : (0.0, 0)),
            maxSize: 2,
            cleanupInterval: TimeSpan.FromMilliseconds(50),
            retentionScorer: (_, v) => v.risk);

        await cache.GetOrComputeAsync("high-risk");
        for (var i = 0; i < 10; i++)
            await cache.GetOrComputeAsync("low-risk");  // boost AccessCount

        // Act: add third entry to trigger eviction
        await cache.GetOrComputeAsync("new-entry");
        await Task.Delay(200);  // let cleanup run at least once

        // Assert: high-risk must still be in cache
        Assert.True(cache.TryGet("high-risk", out _), "High-risk entry must survive over high-frequency low-risk");
    }

    [Fact]
    public async Task CleanupInterval_Tunable_ExpiredEntriesEvictedAtConfiguredRate()
    {
        await using var cache = new SlidingCacheAtom<string, int>(
            (_, _) => Task.FromResult(1),
            slidingExpiration: TimeSpan.FromMilliseconds(50),
            maxSize: 10,
            cleanupInterval: TimeSpan.FromMilliseconds(60));

        await cache.GetOrComputeAsync("k1");
        await Task.Delay(300);  // 3+ cleanup sweeps at 60ms interval

        // k1 should be expired and swept
        Assert.False(cache.TryGet("k1", out _), "Expired entry should be evicted by cleanup loop");
    }
}