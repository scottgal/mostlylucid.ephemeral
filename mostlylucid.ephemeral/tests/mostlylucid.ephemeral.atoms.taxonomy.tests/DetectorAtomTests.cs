using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mostlylucid.Ephemeral;
using Mostlylucid.Ephemeral.Atoms.Taxonomy.Atoms;
using Mostlylucid.Ephemeral.Atoms.Taxonomy.Ledger;
using Xunit;

namespace Mostlylucid.Ephemeral.Atoms.Taxonomy.Tests;

public class DetectorAtomTests
{
    [Fact]
    public async Task DetectorAtomBase_SingleBot_ReturnsCorrectContribution()
    {
        // Arrange
        var detector = new TestBotDetector();
        var sink = new SignalSink();
        sink.Raise("request.present", "test-session");

        // Act
        var contributions = await detector.DetectAsync(sink, "test-session");

        // Assert
        Assert.Single(contributions);
        var contribution = contributions[0];
        Assert.Equal("TestBotDetector", contribution.DetectorName);
        Assert.Equal("test", contribution.Category);
        Assert.Equal(0.8, contribution.ConfidenceDelta);
        Assert.Equal("Test bot detected", contribution.Reason);
    }

    [Fact]
    public async Task DetectorAtomBase_Human_ReturnsNegativeConfidence()
    {
        // Arrange
        var detector = new TestHumanDetector();
        var sink = new SignalSink();

        // Act
        var contributions = await detector.DetectAsync(sink, "test-session");

        // Assert
        Assert.Single(contributions);
        var contribution = contributions[0];
        Assert.True(contribution.ConfidenceDelta < 0, "Human contribution should have negative confidence");
    }

    [Fact]
    public async Task DetectorAtomBase_None_ReturnsEmptyList()
    {
        // Arrange
        var detector = new TestNeutralDetector();
        var sink = new SignalSink();

        // Act
        var contributions = await detector.DetectAsync(sink, "test-session");

        // Assert
        Assert.Empty(contributions);
    }

    [Fact]
    public async Task DetectorAtomBase_Multiple_ReturnsAllContributions()
    {
        // Arrange
        var detector = new TestMultipleContributionDetector();
        var sink = new SignalSink();

        // Act
        var contributions = await detector.DetectAsync(sink, "test-session");

        // Assert
        Assert.Equal(2, contributions.Count);
    }

    [Fact]
    public void DetectorAtomBase_Properties_AreCorrect()
    {
        // Arrange
        var detector = new TestBotDetector();

        // Assert
        Assert.Equal("TestBotDetector", detector.Name);
        Assert.Equal("test", detector.Category);
        Assert.Equal(50, detector.Priority);
        Assert.True(detector.IsEnabled);
        Assert.False(detector.IsOptional);
    }

    [Fact]
    public async Task DetectorAtomBase_HasSignal_DetectsExistingSignal()
    {
        // Arrange
        var detector = new TestSignalCheckDetector();
        var sink = new SignalSink();
        sink.Raise("expected.signal", "test-session");

        // Act
        var contributions = await detector.DetectAsync(sink, "test-session");

        // Assert
        Assert.Single(contributions);
        Assert.Equal("signal_found", contributions[0].Reason);
    }

    [Fact]
    public async Task DetectorAtomBase_HasSignal_ReturnsNoneForMissingSignal()
    {
        // Arrange
        var detector = new TestSignalCheckDetector();
        var sink = new SignalSink();
        // Don't raise the expected signal

        // Act
        var contributions = await detector.DetectAsync(sink, "test-session");

        // Assert
        Assert.Empty(contributions);
    }

    [Fact]
    public async Task DetectorOrchestrator_RunsAllDetectors()
    {
        // Arrange
        var orchestrator = new DetectorOrchestrator();
        orchestrator.Register(new TestBotDetector());
        orchestrator.Register(new TestHumanDetector());

        var sink = new SignalSink();
        sink.Raise("request.present", "test-session");

        // Act
        var ledger = await orchestrator.DetectAsync(sink, "test-session");

        // Assert
        Assert.Equal(2, ledger.Contributions.Count);
    }

    [Fact]
    public async Task DetectorOrchestrator_EarlyExit_StopsProcessing()
    {
        // Arrange
        var options = new DetectorOrchestratorOptions
        {
            ParallelWaveExecution = false // Sequential to test early exit
        };
        var orchestrator = new DetectorOrchestrator(options);
        orchestrator.Register(new TestEarlyExitDetector());
        orchestrator.Register(new TestBotDetector());

        var sink = new SignalSink();
        sink.Raise("request.present", "test-session");

        // Act
        var ledger = await orchestrator.DetectAsync(sink, "test-session");

        // Assert
        Assert.True(ledger.EarlyExit);
        Assert.NotNull(ledger.EarlyExitContribution);
    }

    [Fact]
    public async Task DetectorOrchestrator_QuorumExit_TriggersAtThreshold()
    {
        // Arrange
        var options = new DetectorOrchestratorOptions
        {
            EnableQuorumExit = true,
            QuorumConfidenceThreshold = 0.8
        };
        var orchestrator = new DetectorOrchestrator(options);
        orchestrator.Register(new TestHighConfidenceDetector());
        orchestrator.Register(new TestBotDetector()); // Won't run if quorum reached

        var sink = new SignalSink();
        sink.Raise("request.present", "test-session");

        // Act
        var ledger = await orchestrator.DetectAsync(sink, "test-session");

        // Assert
        Assert.True(ledger.Confidence >= options.QuorumConfidenceThreshold);
    }

    [Fact]
    public async Task DetectorOrchestrator_Timeout_HandlesGracefully()
    {
        // Arrange
        var orchestrator = new DetectorOrchestrator();
        orchestrator.Register(new TestSlowDetector());

        var sink = new SignalSink();

        // Act
        var ledger = await orchestrator.DetectAsync(sink, "test-session");

        // Assert - slow detector should have timed out, but ledger should exist
        Assert.NotNull(ledger);
    }

    [Fact]
    public async Task DetectorOrchestrator_EmitsWaveSignals()
    {
        // Arrange
        var orchestrator = new DetectorOrchestrator();
        orchestrator.Register(new TestBotDetector());

        var sink = new SignalSink();
        sink.Raise("request.present", "test-session");

        // Act
        var ledger = await orchestrator.DetectAsync(sink, "test-session");

        // Assert - should have wave start/complete signals
        Assert.True(sink.Detect("detection.wave.0.started"));
        Assert.True(sink.Detect("detection.wave.0.completed"));
        Assert.True(sink.Detect("detection.completed"));
    }

    // Test implementations
    private class TestBotDetector : DetectorAtomBase
    {
        public TestBotDetector() : base("TestBotDetector", "test") { }

        public override Task<IReadOnlyList<DetectionContribution>> DetectAsync(
            SignalSink sink, string sessionId, CancellationToken ct = default)
        {
            return Task.FromResult(Single(Bot(0.8, "Test bot detected")));
        }
    }

    private class TestHumanDetector : DetectorAtomBase
    {
        public TestHumanDetector() : base("TestHumanDetector", "test") { }

        public override Task<IReadOnlyList<DetectionContribution>> DetectAsync(
            SignalSink sink, string sessionId, CancellationToken ct = default)
        {
            return Task.FromResult(Single(Human(0.7, "Human indicators found")));
        }
    }

    private class TestNeutralDetector : DetectorAtomBase
    {
        public TestNeutralDetector() : base("TestNeutralDetector", "test") { }

        public override Task<IReadOnlyList<DetectionContribution>> DetectAsync(
            SignalSink sink, string sessionId, CancellationToken ct = default)
        {
            return Task.FromResult(None());
        }
    }

    private class TestMultipleContributionDetector : DetectorAtomBase
    {
        public TestMultipleContributionDetector() : base("TestMultipleDetector", "test") { }

        public override Task<IReadOnlyList<DetectionContribution>> DetectAsync(
            SignalSink sink, string sessionId, CancellationToken ct = default)
        {
            return Task.FromResult(Multiple(
                Bot(0.5, "First signal"),
                Bot(0.3, "Second signal")));
        }
    }

    private class TestSignalCheckDetector : DetectorAtomBase
    {
        public TestSignalCheckDetector() : base("TestSignalCheckDetector", "test") { }

        public override Task<IReadOnlyList<DetectionContribution>> DetectAsync(
            SignalSink sink, string sessionId, CancellationToken ct = default)
        {
            if (HasSignal(sink, "expected.signal"))
            {
                return Task.FromResult(Single(Bot(0.5, "signal_found")));
            }
            return Task.FromResult(None());
        }
    }

    private class TestEarlyExitDetector : DetectorAtomBase
    {
        public TestEarlyExitDetector() : base("TestEarlyExitDetector", "test") { }
        public override int Priority => 1; // Run first

        public override Task<IReadOnlyList<DetectionContribution>> DetectAsync(
            SignalSink sink, string sessionId, CancellationToken ct = default)
        {
            var contribution = DetectionContribution.VerifiedBot(
                Name, "Verified bad bot", "badbot", "TestBot");
            return Task.FromResult<IReadOnlyList<DetectionContribution>>(new[] { contribution });
        }
    }

    private class TestHighConfidenceDetector : DetectorAtomBase
    {
        public TestHighConfidenceDetector() : base("TestHighConfidenceDetector", "test") { }
        public override int Priority => 1;

        public override Task<IReadOnlyList<DetectionContribution>> DetectAsync(
            SignalSink sink, string sessionId, CancellationToken ct = default)
        {
            return Task.FromResult(Single(Bot(0.95, "Very confident", weight: 5.0)));
        }
    }

    private class TestSlowDetector : DetectorAtomBase
    {
        public TestSlowDetector() : base("TestSlowDetector", "test") { }
        public override TimeSpan Timeout => TimeSpan.FromMilliseconds(50);
        public override bool IsOptional => true;

        public override async Task<IReadOnlyList<DetectionContribution>> DetectAsync(
            SignalSink sink, string sessionId, CancellationToken ct = default)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct); // Will timeout
            return Single(Bot(0.5, "Slow result"));
        }
    }
}
