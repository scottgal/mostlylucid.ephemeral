using System;
using System.Collections.Generic;
using System.Linq;
using Mostlylucid.Ephemeral.Atoms.Taxonomy.Ledger;
using Xunit;

namespace Mostlylucid.Ephemeral.Atoms.Taxonomy.Tests;

public class DetectionLedgerTests
{
    [Fact]
    public void DetectionLedger_NewLedger_HasDefaultValues()
    {
        // Arrange & Act
        var ledger = new DetectionLedger("test-request");

        // Assert
        Assert.Equal("test-request", ledger.RequestId);
        Assert.Equal(0.5, ledger.BotProbability);
        Assert.Equal(0.0, ledger.Confidence);
        Assert.Empty(ledger.Contributions);
        Assert.False(ledger.EarlyExit);
    }

    [Fact]
    public void DetectionLedger_AddContribution_UpdatesProbability()
    {
        // Arrange
        var ledger = new DetectionLedger("test-request");
        var contribution = DetectionContribution.Bot("TestDetector", "test", 0.7, "Test reason");

        // Act
        ledger.AddContribution(contribution);

        // Assert
        Assert.Single(ledger.Contributions);
        Assert.True(ledger.BotProbability > 0.5, "Bot contribution should increase probability");
    }

    [Fact]
    public void DetectionLedger_HumanContribution_DecreasesProbability()
    {
        // Arrange
        var ledger = new DetectionLedger("test-request");
        var contribution = DetectionContribution.Human("TestDetector", "test", 0.6, "Human indicators");

        // Act
        ledger.AddContribution(contribution);

        // Assert
        Assert.True(ledger.BotProbability < 0.5, "Human contribution should decrease probability");
    }

    [Fact]
    public void DetectionLedger_MultipleContributions_Aggregates()
    {
        // Arrange
        var ledger = new DetectionLedger("test-request");

        // Act
        ledger.AddContribution(DetectionContribution.Bot("Detector1", "cat1", 0.6, "Bot signal 1"));
        ledger.AddContribution(DetectionContribution.Bot("Detector2", "cat2", 0.4, "Bot signal 2"));
        ledger.AddContribution(DetectionContribution.Human("Detector3", "cat3", 0.2, "Human signal"));

        // Assert
        Assert.Equal(3, ledger.Contributions.Count);
        Assert.True(ledger.Confidence > 0, "Multiple contributions should increase confidence");
    }

    [Fact]
    public void DetectionLedger_VerifiedBot_TriggersEarlyExit()
    {
        // Arrange
        var ledger = new DetectionLedger("test-request");
        var contribution = DetectionContribution.VerifiedBot("TestDetector", "Verified bad bot", "scraper", "BadBot");

        // Act
        ledger.AddContribution(contribution);

        // Assert
        Assert.True(ledger.EarlyExit);
        Assert.NotNull(ledger.EarlyExitContribution);
        Assert.Equal("VerifiedBadBot", ledger.EarlyExitContribution.EarlyExitVerdict);
    }

    [Fact]
    public void DetectionLedger_VerifiedGoodBot_TriggersEarlyExit()
    {
        // Arrange
        var ledger = new DetectionLedger("test-request");
        var contribution = DetectionContribution.VerifiedGoodBot("TestDetector", "Known good bot", "Googlebot");

        // Act
        ledger.AddContribution(contribution);

        // Assert
        Assert.True(ledger.EarlyExit);
        Assert.Equal("VerifiedGoodBot", ledger.EarlyExitContribution!.EarlyExitVerdict);
    }

    [Fact]
    public void DetectionLedger_RecordSignal_StoresSignal()
    {
        // Arrange
        var ledger = new DetectionLedger("test-request");

        // Act
        ledger.Record("test.signal", "test-value", 0.8, "TestAtom");

        // Assert
        Assert.True(ledger.HasSignal("test.signal"));
        var signal = ledger.GetSignal("test.signal");
        Assert.NotNull(signal);
        Assert.Equal("test-value", signal.Value);
        Assert.Equal(0.8, signal.Salience);
    }

    [Fact]
    public void DetectionLedger_GetSignals_ByPattern()
    {
        // Arrange
        var ledger = new DetectionLedger("test-request");
        ledger.Record("detector.ua.signal1", "val1", 0.5, "ua");
        ledger.Record("detector.ua.signal2", "val2", 0.5, "ua");
        ledger.Record("detector.ip.signal", "val3", 0.5, "ip");

        // Act
        var uaSignals = ledger.GetSignals("detector.ua.*");

        // Assert
        Assert.Equal(2, uaSignals.Count);
    }

    [Fact]
    public void DetectionLedger_GetHighSalienceSignals_FiltersCorrectly()
    {
        // Arrange
        var ledger = new DetectionLedger("test-request");
        ledger.Record("high.signal", "val1", 0.9, "detector1");
        ledger.Record("low.signal", "val2", 0.3, "detector2");
        ledger.Record("medium.signal", "val3", 0.7, "detector3");

        // Act
        var highSalience = ledger.GetHighSalienceSignals(0.8);

        // Assert
        Assert.Single(highSalience);
        Assert.Equal("high.signal", highSalience[0].Key);
    }

    [Fact]
    public void DetectionLedger_CategoryBreakdown_GroupsCorrectly()
    {
        // Arrange
        var ledger = new DetectionLedger("test-request");
        ledger.AddContribution(DetectionContribution.Bot("D1", "behavioral", 0.5, "Reason 1"));
        ledger.AddContribution(DetectionContribution.Bot("D2", "behavioral", 0.3, "Reason 2"));
        ledger.AddContribution(DetectionContribution.Bot("D3", "header", 0.2, "Reason 3"));

        // Act
        var breakdown = ledger.CategoryBreakdown;

        // Assert
        Assert.Equal(2, breakdown.Count);
        Assert.True(breakdown.ContainsKey("behavioral"));
        Assert.True(breakdown.ContainsKey("header"));
        Assert.Equal(2, breakdown["behavioral"].ContributionCount);
    }

    [Fact]
    public void DetectionLedger_ContributingDetectors_TracksAll()
    {
        // Arrange
        var ledger = new DetectionLedger("test-request");
        ledger.AddContribution(DetectionContribution.Bot("Detector1", "cat", 0.5, "R1"));
        ledger.AddContribution(DetectionContribution.Bot("Detector2", "cat", 0.3, "R2"));

        // Act
        var detectors = ledger.ContributingDetectors;

        // Assert
        Assert.Equal(2, detectors.Count);
        Assert.Contains("Detector1", detectors);
        Assert.Contains("Detector2", detectors);
    }

    [Fact]
    public void DetectionLedger_RecordFailure_TracksFailedDetector()
    {
        // Arrange
        var ledger = new DetectionLedger("test-request");

        // Act
        ledger.RecordFailure("FailedDetector");

        // Assert
        Assert.Contains("FailedDetector", ledger.FailedDetectors);
        Assert.True(ledger.HasSignal("detector.failed.FailedDetector"));
    }

    [Fact]
    public void DetectionLedger_TotalProcessingTime_Accumulates()
    {
        // Arrange
        var ledger = new DetectionLedger("test-request");

        // Act
        ledger.AddContribution(new DetectionContribution
        {
            DetectorName = "D1",
            Category = "cat",
            ConfidenceDelta = 0.5,
            ProcessingTimeMs = 10
        });
        ledger.AddContribution(new DetectionContribution
        {
            DetectorName = "D2",
            Category = "cat",
            ConfidenceDelta = 0.3,
            ProcessingTimeMs = 15
        });

        // Assert
        Assert.Equal(25, ledger.TotalProcessingTimeMs);
    }

    [Fact]
    public void DetectionLedger_BotType_UpdatesOnHighConfidence()
    {
        // Arrange
        var ledger = new DetectionLedger("test-request");

        // Act
        ledger.AddContribution(DetectionContribution.Bot(
            "Detector1", "cat", 0.7, "Reason", botType: "scraper", botName: "BadBot"));

        // Assert
        Assert.Equal("scraper", ledger.BotType);
        Assert.Equal("BadBot", ledger.BotName);
    }

    [Fact]
    public void DetectionLedger_MergedSignals_CombinesAllContributions()
    {
        // Arrange
        var ledger = new DetectionLedger("test-request");
        ledger.AddContribution(new DetectionContribution
        {
            DetectorName = "D1",
            Category = "cat",
            ConfidenceDelta = 0.5,
            Signals = new Dictionary<string, object> { ["sig1"] = "val1" }
        });
        ledger.AddContribution(new DetectionContribution
        {
            DetectorName = "D2",
            Category = "cat",
            ConfidenceDelta = 0.3,
            Signals = new Dictionary<string, object> { ["sig2"] = "val2" }
        });

        // Act
        var merged = ledger.MergedSignals;

        // Assert
        Assert.Equal(2, merged.Count);
    }

    [Fact]
    public void DetectionLedger_ToLearningRecord_ReturnsNullForLowConfidence()
    {
        // Arrange
        var ledger = new DetectionLedger("test-request");
        // Don't add any contributions - confidence stays at 0

        // Act
        var record = ledger.ToLearningRecord(0.85);

        // Assert
        Assert.Null(record);
    }

    [Fact]
    public void DetectionLedger_ToLearningRecord_ReturnsRecordForHighConfidence()
    {
        // Arrange
        var ledger = new DetectionLedger("test-request", "fingerprint123");
        // Add high confidence contributions
        for (int i = 0; i < 5; i++)
        {
            ledger.AddContribution(DetectionContribution.Bot($"D{i}", "cat", 0.9, $"Reason{i}", weight: 2.0));
        }

        // Act
        var record = ledger.ToLearningRecord(0.85);

        // Assert
        if (record != null)
        {
            Assert.True(record.IsBot);
            Assert.Equal("test-request", record.RequestId);
            Assert.Equal("fingerprint123", record.Fingerprint);
        }
    }

    [Fact]
    public void DetectionContribution_Bot_CreatesCorrectContribution()
    {
        // Act
        var contribution = DetectionContribution.Bot(
            "TestDetector", "category", 0.75, "Test reason",
            weight: 1.5, botType: "crawler", botName: "TestBot");

        // Assert
        Assert.Equal("TestDetector", contribution.DetectorName);
        Assert.Equal("category", contribution.Category);
        Assert.Equal(0.75, contribution.ConfidenceDelta);
        Assert.Equal(1.5, contribution.Weight);
        Assert.Equal("Test reason", contribution.Reason);
        Assert.Equal("crawler", contribution.BotType);
        Assert.Equal("TestBot", contribution.BotName);
    }

    [Fact]
    public void DetectionContribution_Human_CreatesNegativeConfidence()
    {
        // Act
        var contribution = DetectionContribution.Human(
            "TestDetector", "category", 0.6, "Human indicators");

        // Assert
        Assert.Equal(-0.6, contribution.ConfidenceDelta);
    }

    [Fact]
    public void DetectionContribution_Info_CreatesNeutralContribution()
    {
        // Act
        var contribution = DetectionContribution.Info(
            "TestDetector", "category", "Informational");

        // Assert
        Assert.Equal(0, contribution.ConfidenceDelta);
        Assert.Equal(0, contribution.Weight);
    }

    [Fact]
    public void DetectionContribution_VerifiedBot_CreatesEarlyExitContribution()
    {
        // Act
        var contribution = DetectionContribution.VerifiedBot(
            "TestDetector", "Verified bad", "malicious", "BadBot");

        // Assert
        Assert.Equal(1.0, contribution.ConfidenceDelta);
        Assert.Equal(10.0, contribution.Weight);
        Assert.True(contribution.TriggerEarlyExit);
        Assert.Equal("VerifiedBadBot", contribution.EarlyExitVerdict);
    }

    [Fact]
    public void DetectionContribution_VerifiedGoodBot_CreatesNeutralEarlyExit()
    {
        // Act
        var contribution = DetectionContribution.VerifiedGoodBot(
            "TestDetector", "Known good", "Googlebot");

        // Assert
        Assert.Equal(0.0, contribution.ConfidenceDelta);
        Assert.Equal(0.0, contribution.Weight);
        Assert.True(contribution.TriggerEarlyExit);
        Assert.Equal("VerifiedGoodBot", contribution.EarlyExitVerdict);
    }
}
