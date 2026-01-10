using Xunit;

namespace Mostlylucid.Ephemeral.Tests;

public class SignalSinkTests
{
    [Fact]
    public void Raise_AddsSignalToSink()
    {
        var sink = new SignalSink();
        sink.Raise("test.signal");

        var snapshot = sink.Sense();
        Assert.Single(snapshot);
        Assert.Equal("test.signal", snapshot[0].Signal);
    }

    [Fact]
    public void Sense_ReturnsAllSignals()
    {
        var sink = new SignalSink();
        sink.Raise("signal.1");
        sink.Raise("signal.2");
        sink.Raise("signal.3");

        var snapshot = sink.Sense();
        Assert.Equal(3, snapshot.Count);
    }

    [Fact]
    public void Sense_WithPredicate_FiltersSignals()
    {
        var sink = new SignalSink();
        sink.Raise("error.timeout");
        sink.Raise("error.connection");
        sink.Raise("warning.low");

        var errorSignals = sink.Sense(s => s.Signal.StartsWith("error."));
        Assert.Equal(2, errorSignals.Count);
    }

    [Fact]
    public void Detect_ReturnsTrueForExistingSignal()
    {
        var sink = new SignalSink();
        sink.Raise("test.signal");

        Assert.True(sink.Detect("test.signal"));
        Assert.False(sink.Detect("other.signal"));
    }

    [Fact]
    public void Detect_WithPredicate_MatchesSignal()
    {
        var sink = new SignalSink();
        sink.Raise("error.timeout");

        Assert.True(sink.Detect(s => s.Signal.StartsWith("error.")));
        Assert.False(sink.Detect(s => s.Signal.StartsWith("warning.")));
    }

    [Fact]
    public void Count_ReturnsNumberOfSignals()
    {
        var sink = new SignalSink();
        sink.Raise("signal.1");
        sink.Raise("signal.2");

        Assert.Equal(2, sink.Count);
    }

    [Fact]
    [Obsolete("SignalSink no longer manages cleanup - coordinators control signal lifetime")]
    public void Cleanup_LimitsIterations_PreventingUnboundedLoop()
    {
        // OBSOLETE TEST: SignalSink no longer performs cleanup.
        // Coordinators manage signal lifetime through operation eviction.
        var sink = new SignalSink();

        // Add many signals - sink no longer enforces capacity limits
        for (var i = 0; i < 5000; i++) sink.Raise($"signal.{i}");
        for (var i = 0; i < 1100; i++) sink.Raise($"cleanup.{i}");

        // Sink just holds all signals - no automatic cleanup
        Assert.Equal(6100, sink.Count);
    }

    [Fact]
    [Obsolete("SignalSink no longer manages cleanup - coordinators control signal lifetime")]
    public async Task Cleanup_HandlesExpiredSignals()
    {
        // OBSOLETE TEST: SignalSink no longer performs age-based cleanup.
        // Coordinators manage signal lifetime through operation eviction.
        var sink = new SignalSink();

        sink.Raise("signal.1");
        sink.Raise("signal.2");

        await Task.Delay(100);

        // Add more signals
        for (var i = 0; i < 10; i++) sink.Raise($"new.{i}");

        // Old signals remain - no automatic cleanup based on age
        var signals = sink.Sense();
        Assert.Contains(signals, s => s.Signal == "signal.1");
        Assert.Contains(signals, s => s.Signal == "signal.2");
    }

    [Fact]
    [Obsolete("SignalSink.UpdateWindowSize is obsolete - coordinators control signal lifetime")]
    public void UpdateWindowSize_UpdatesCapacityAndAge()
    {
        // OBSOLETE TEST: UpdateWindowSize is now a no-op.
        var sink = new SignalSink();

        sink.UpdateWindowSize(200, TimeSpan.FromMinutes(5));

        // Properties return 0 - no longer managed by sink
        Assert.Equal(0, sink.MaxCapacity);
        Assert.Equal(TimeSpan.Zero, sink.MaxAge);
    }

    [Fact]
    [Obsolete("SignalSink.UpdateWindowSize is obsolete - coordinators control signal lifetime")]
    public void UpdateWindowSize_OnlyUpdatesSpecifiedParameters()
    {
        // OBSOLETE TEST: UpdateWindowSize is now a no-op.
        var sink = new SignalSink();

        sink.UpdateWindowSize(200);

        // Properties return 0 - no longer managed by sink
        Assert.Equal(0, sink.MaxCapacity);
        Assert.Equal(TimeSpan.Zero, sink.MaxAge);
    }

    [Fact]
    [Obsolete("SignalSink.UpdateWindowSize is obsolete - coordinators control signal lifetime")]
    public void UpdateWindowSize_ClampsToMinimumOne()
    {
        // OBSOLETE TEST: UpdateWindowSize is now a no-op.
        var sink = new SignalSink();

        sink.UpdateWindowSize(-100);

        // Property returns 0 - no longer managed by sink
        Assert.Equal(0, sink.MaxCapacity);
    }

    [Fact]
    public void SignalRaised_Event_FiresForSubscribers()
    {
        var sink = new SignalSink();
        SignalEvent? receivedSignal = null;

        using var sub = sink.Subscribe(signal => receivedSignal = signal);
        sink.Raise("test.event");

        Assert.NotNull(receivedSignal);
        Assert.Equal("test.event", receivedSignal.Value.Signal);
    }

    [Fact]
    public void SignalRaised_Event_DoesNotThrowOnHandlerException()
    {
        var sink = new SignalSink();

        using var sub = sink.Subscribe(_ => throw new Exception("Handler failure"));

        // Should not throw
        sink.Raise("test.event");

        Assert.Equal(1, sink.Count);
    }

    [Fact]
    public void ConcurrentRaise_HandlesMultipleThreads()
    {
        var sink = new SignalSink(10000);
        var tasks = new List<Task>();

        for (var i = 0; i < 100; i++)
        {
            var taskId = i;
            tasks.Add(Task.Run(() =>
            {
                for (var j = 0; j < 100; j++) sink.Raise($"task.{taskId}.signal.{j}");
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Should have signals from concurrent operations
        Assert.True(sink.Count > 0);
    }

    [Fact]
    public void Sense_PredicateAllocation_MinimizesGC()
    {
        var sink = new SignalSink();

        // Add many signals
        for (var i = 0; i < 100; i++) sink.Raise($"signal.{i}");

        // Pre-sized list should reduce allocations
        var results = sink.Sense(s => s.Signal.StartsWith("signal.5"));

        Assert.NotEmpty(results);
        Assert.All(results, s => Assert.StartsWith("signal.5", s.Signal));
    }

    [Fact]
    public void Detect_ShortCircuits_OnFirstMatch()
    {
        var sink = new SignalSink();

        for (var i = 0; i < 1000; i++) sink.Raise($"signal.{i}");

        // Should find quickly without checking all 1000
        var found = sink.Detect("signal.0");

        Assert.True(found);
    }

    [Fact]
    public async Task HighVolumeStressTest_MaintainsStability()
    {
        var sink = new SignalSink();

        // Simulate high-volume scenario
        var tasks = Enumerable.Range(0, 10).Select(taskId =>
            Task.Run(async () =>
            {
                for (var i = 0; i < 1000; i++)
                {
                    sink.Raise($"task.{taskId}.msg.{i}");
                    if (i % 100 == 0) await Task.Delay(1); // Small delay
                }
            })
        ).ToArray();

        await Task.WhenAll(tasks);

        // SignalSink no longer enforces capacity limits - just stores all signals
        // Coordinators manage signal lifetime through operation eviction
        Assert.Equal(10_000, sink.Count);
    }
}