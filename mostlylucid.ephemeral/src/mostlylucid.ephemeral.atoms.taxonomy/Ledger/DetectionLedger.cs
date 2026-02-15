using System.Collections.Concurrent;

namespace Mostlylucid.Ephemeral.Atoms.Taxonomy.Ledger;

/// <summary>
///     The evidence accumulator for detection systems.
///     This is the core ledger that BotDetection, ThreatDetection, etc. use directly.
/// </summary>
/// <remarks>
///     **Flow:**
///     ```
///     Request → SignalSink (shared) → Detectors → DetectionLedger → Verdict
///     ↓
///     (high salience signals)
///     ↓
///     Learning System
///     ```
///     The ledger is the single source of truth for detection evidence.
///     All detectors write contributions to the same ledger instance.
///     The ledger aggregates evidence and produces the final verdict.
/// </remarks>
public class DetectionLedger
{
    private readonly List<DetectionContribution> _contributions = new();
    private readonly HashSet<string> _failedDetectors = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<string, LedgerSignal> _signals = new(StringComparer.OrdinalIgnoreCase);

    public DetectionLedger(string requestId, string? fingerprint = null)
    {
        RequestId = requestId;
        Fingerprint = fingerprint;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    ///     Request/session identifier.
    /// </summary>
    public string RequestId { get; }

    /// <summary>
    ///     Privacy-preserving fingerprint (hashed composite of request attributes).
    /// </summary>
    public string? Fingerprint { get; }

    /// <summary>
    ///     When this ledger was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    ///     All evidence contributions from detectors.
    /// </summary>
    public IReadOnlyList<DetectionContribution> Contributions
    {
        get
        {
            lock (_lock)
            {
                return _contributions.ToList();
            }
        }
    }

    /// <summary>
    ///     Current aggregated bot probability (0.0 = human, 1.0 = bot).
    /// </summary>
    public double BotProbability { get; private set; } = 0.5;

    /// <summary>
    ///     Confidence in the probability (0.0 - 1.0).
    /// </summary>
    public double Confidence { get; private set; }

    /// <summary>
    ///     Whether early exit was triggered.
    /// </summary>
    public bool EarlyExit => EarlyExitContribution != null;

    /// <summary>
    ///     The contribution that triggered early exit.
    /// </summary>
    public DetectionContribution? EarlyExitContribution { get; private set; }

    /// <summary>
    ///     Primary detected bot type.
    /// </summary>
    public string? BotType { get; private set; }

    /// <summary>
    ///     Primary detected bot name.
    /// </summary>
    public string? BotName { get; private set; }

    /// <summary>
    ///     Total processing time across all contributions.
    /// </summary>
    public double TotalProcessingTimeMs { get; private set; }

    /// <summary>
    ///     Which detectors contributed evidence.
    /// </summary>
    public IReadOnlySet<string> ContributingDetectors
    {
        get
        {
            lock (_lock)
            {
                return _contributions.Select(c => c.DetectorName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>
    ///     Which detectors failed or timed out.
    /// </summary>
    public IReadOnlySet<string> FailedDetectors
    {
        get
        {
            lock (_lock)
            {
                return _failedDetectors.ToHashSet();
            }
        }
    }

    /// <summary>
    ///     All signals recorded in the ledger.
    /// </summary>
    public IReadOnlyDictionary<string, LedgerSignal> Signals => _signals;

    /// <summary>
    ///     Gets the merged signals from all contributions.
    /// </summary>
    public IReadOnlyDictionary<string, object> MergedSignals
    {
        get
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            lock (_lock)
            {
                foreach (var contrib in _contributions)
                foreach (var (key, value) in contrib.Signals)
                    result[key] = value;
            }

            return result;
        }
    }

    /// <summary>
    ///     Category breakdown for explainability.
    /// </summary>
    public IReadOnlyDictionary<string, CategoryScore> CategoryBreakdown
    {
        get
        {
            lock (_lock)
            {
                return _contributions
                    .GroupBy(c => c.Category, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        g => g.Key,
                        g => new CategoryScore
                        {
                            Category = g.Key,
                            Score = g.Sum(c => c.ConfidenceDelta * c.Weight),
                            TotalWeight = g.Sum(c => c.Weight),
                            ContributionCount = g.Count(),
                            Reasons = g.Select(c => c.Reason).ToList()
                        },
                        StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>
    ///     Records a signal in the ledger.
    /// </summary>
    public void Record(
        string key,
        object? value,
        double salience,
        string sourceAtom,
        string? sourceKind = null,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        var signal = new LedgerSignal
        {
            Key = key,
            Value = value,
            Salience = salience,
            SourceAtom = sourceAtom,
            SourceKind = sourceKind ?? "detector",
            Timestamp = DateTimeOffset.UtcNow,
            Metadata = metadata != null ? new Dictionary<string, object>(metadata) : null
        };

        _signals[key] = signal;
    }

    /// <summary>
    ///     Adds a detection contribution from a detector.
    /// </summary>
    public void AddContribution(DetectionContribution contribution)
    {
        lock (_lock)
        {
            _contributions.Add(contribution);
            TotalProcessingTimeMs += contribution.ProcessingTimeMs;

            // Check for early exit
            if (contribution.TriggerEarlyExit && EarlyExitContribution == null) EarlyExitContribution = contribution;

            // Update bot type if provided with higher confidence
            if (!string.IsNullOrEmpty(contribution.BotType) && contribution.ConfidenceDelta > 0)
                BotType = contribution.BotType;

            if (!string.IsNullOrEmpty(contribution.BotName) && contribution.ConfidenceDelta > 0)
                BotName = contribution.BotName;
        }

        // Record as signal
        Record(
            $"contribution.{contribution.DetectorName}",
            contribution.ConfidenceDelta,
            Math.Abs(contribution.ConfidenceDelta),
            contribution.DetectorName,
            contribution.Category);

        // Add detector-specific signals
        foreach (var (key, val) in contribution.Signals)
            Record(
                $"detector.{contribution.DetectorName}.{key}",
                val,
                contribution.Salience,
                contribution.DetectorName);

        // Recalculate aggregated probability
        Aggregate();
    }

    /// <summary>
    ///     Records a detector failure.
    /// </summary>
    public void RecordFailure(string detectorName)
    {
        lock (_lock)
        {
            _failedDetectors.Add(detectorName);
        }

        Record($"detector.failed.{detectorName}", true, 0.3, "orchestrator");
    }

    /// <summary>
    ///     Recalculates the aggregated bot probability using sigmoid function.
    /// </summary>
    protected virtual void Aggregate()
    {
        lock (_lock)
        {
            if (_contributions.Count == 0)
            {
                BotProbability = 0.5;
                Confidence = 0.0;
                return;
            }

            // Filter to contributions with weight
            var weighted = _contributions
                .Where(c => c.Weight > 0)
                .ToList();

            if (weighted.Count == 0)
            {
                BotProbability = 0.3; // No evidence = assume more likely human
                Confidence = 0.0;
                return;
            }

            // Weighted sum of contributions
            var weightedSum = weighted.Sum(c => c.ConfidenceDelta * c.Weight);
            var totalWeight = weighted.Sum(c => Math.Abs(c.Weight));

            // Sigmoid function: 1 / (1 + e^(-x))
            BotProbability = 1.0 / (1.0 + Math.Exp(-weightedSum));

            // Confidence is independent of bot probability — it reflects how much
            // evidence we collected and how strongly detectors agree.
            // You can be highly confident (0.95) that something is human (prob 0.1).

            // 1. Weight coverage: total evidence weight vs expected baseline
            var weightFactor = Math.Min(1.0, totalWeight / 5.0);

            // 2. Agreement: fraction of weighted evidence pointing in the majority direction
            var positiveWeight = weighted.Where(c => c.ConfidenceDelta > 0)
                .Sum(c => Math.Abs(c.ConfidenceDelta * c.Weight));
            var negativeWeight = weighted.Where(c => c.ConfidenceDelta < 0)
                .Sum(c => Math.Abs(c.ConfidenceDelta * c.Weight));
            var totalSignalWeight = positiveWeight + negativeWeight;
            var agreementFactor = totalSignalWeight > 0
                ? Math.Max(positiveWeight, negativeWeight) / totalSignalWeight
                : 0.0;

            // 3. Detector count: more distinct detectors = more confident
            var distinctDetectors = weighted.Select(c => c.DetectorName)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count();
            var countFactor = Math.Min(1.0, distinctDetectors / 4.0);

            // Combine: agreement (40%) + weight coverage (35%) + detector count (25%)
            Confidence = (agreementFactor * 0.40) + (weightFactor * 0.35) + (countFactor * 0.25);
        }
    }

    /// <summary>
    ///     Gets signals matching a pattern.
    /// </summary>
    public IReadOnlyList<LedgerSignal> GetSignals(string pattern)
    {
        if (pattern == "*")
            return _signals.Values.ToList();

        if (pattern.EndsWith("*"))
        {
            var prefix = pattern[..^1];
            return _signals.Values
                .Where(s => s.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (_signals.TryGetValue(pattern, out var signal))
            return new[] { signal };

        return Array.Empty<LedgerSignal>();
    }

    /// <summary>
    ///     Gets signals above a salience threshold (for escalation).
    /// </summary>
    public IReadOnlyList<LedgerSignal> GetHighSalienceSignals(double threshold = 0.8)
    {
        return _signals.Values
            .Where(s => s.Salience >= threshold)
            .OrderByDescending(s => s.Salience)
            .ToList();
    }

    /// <summary>
    ///     Creates a learning record for the heuristic system.
    ///     Only returns a record for high-confidence detections.
    /// </summary>
    public LearningRecord? ToLearningRecord(double confidenceThreshold = 0.85)
    {
        if (Confidence < confidenceThreshold)
            return null;

        var isBot = BotProbability >= 0.7;
        var isHuman = BotProbability <= 0.3;

        if (!isBot && !isHuman)
            return null; // Uncertain - don't learn

        return new LearningRecord
        {
            RequestId = RequestId,
            Fingerprint = Fingerprint,
            IsBot = isBot,
            BotProbability = BotProbability,
            Confidence = Confidence,
            BotType = BotType,
            BotName = BotName,
            Features = ExtractFeatures(),
            CategoryScores = CategoryBreakdown.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Score),
            ContributingDetectors = ContributingDetectors.ToList(),
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    ///     Extracts feature vector for heuristic learning.
    /// </summary>
    protected virtual Dictionary<string, double> ExtractFeatures()
    {
        var features = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        // Extract numeric signals as features
        foreach (var (key, signal) in _signals)
            if (signal.Value is double d)
                features[key] = d;
            else if (signal.Value is bool b)
                features[key] = b ? 1.0 : 0.0;
            else if (signal.Value is int i) features[key] = i;

        // Add contribution deltas as features
        lock (_lock)
        {
            foreach (var contribution in _contributions)
            {
                var featureKey = $"contribution.{contribution.DetectorName}.delta";
                features[featureKey] = contribution.ConfidenceDelta;
            }
        }

        return features;
    }

    public bool HasSignal(string key)
    {
        return _signals.ContainsKey(key);
    }

    public LedgerSignal? GetSignal(string key)
    {
        return _signals.TryGetValue(key, out var signal) ? signal : null;
    }

    public T? GetSignalValue<T>(string key)
    {
        return _signals.TryGetValue(key, out var signal) && signal.Value is T value ? value : default;
    }
}

/// <summary>
///     Score breakdown for a single category.
/// </summary>
public sealed class CategoryScore
{
    public string Category { get; init; } = string.Empty;
    public double Score { get; init; }
    public double TotalWeight { get; init; }
    public int ContributionCount { get; init; }
    public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();
}

/// <summary>
///     A contribution from a detector to the detection ledger.
/// </summary>
public sealed record DetectionContribution
{
    public string DetectorName { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public double ConfidenceDelta { get; init; } // -1.0 (human) to +1.0 (bot)
    public double Weight { get; init; } = 1.0;
    public double Salience { get; init; } = 0.5;
    public string Reason { get; init; } = string.Empty;
    public string? Detail { get; init; }
    public string? BotType { get; init; }
    public string? BotName { get; init; }
    public IReadOnlyDictionary<string, object> Signals { get; init; } = new Dictionary<string, object>();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public double ProcessingTimeMs { get; init; }
    public int Priority { get; init; }
    public bool TriggerEarlyExit { get; init; }
    public string? EarlyExitVerdict { get; init; }

    /// <summary>
    ///     Creates a bot-indicating contribution.
    /// </summary>
    public static DetectionContribution Bot(
        string detectorName,
        string category,
        double confidence,
        string reason,
        double weight = 1.0,
        string? botType = null,
        string? botName = null,
        Dictionary<string, object>? signals = null)
    {
        return new DetectionContribution
        {
            DetectorName = detectorName,
            Category = category,
            ConfidenceDelta = Math.Clamp(confidence, 0, 1),
            Weight = weight,
            Salience = Math.Abs(confidence),
            Reason = reason,
            BotType = botType,
            BotName = botName,
            Signals = signals ?? new Dictionary<string, object>()
        };
    }

    /// <summary>
    ///     Creates a human-indicating contribution.
    /// </summary>
    public static DetectionContribution Human(
        string detectorName,
        string category,
        double confidence,
        string reason,
        double weight = 1.0,
        Dictionary<string, object>? signals = null)
    {
        return new DetectionContribution
        {
            DetectorName = detectorName,
            Category = category,
            ConfidenceDelta = -Math.Clamp(confidence, 0, 1), // Negative = human
            Weight = weight,
            Salience = Math.Abs(confidence),
            Reason = reason,
            Signals = signals ?? new Dictionary<string, object>()
        };
    }

    /// <summary>
    ///     Creates a neutral/informational contribution.
    /// </summary>
    public static DetectionContribution Info(
        string detectorName,
        string category,
        string reason,
        Dictionary<string, object>? signals = null)
    {
        return new DetectionContribution
        {
            DetectorName = detectorName,
            Category = category,
            ConfidenceDelta = 0,
            Weight = 0,
            Salience = 0.1,
            Reason = reason,
            Signals = signals ?? new Dictionary<string, object>()
        };
    }

    /// <summary>
    ///     Creates an early exit contribution (verified bot).
    /// </summary>
    public static DetectionContribution VerifiedBot(
        string detectorName,
        string reason,
        string? botType = null,
        string? botName = null)
    {
        return new DetectionContribution
        {
            DetectorName = detectorName,
            Category = "verified",
            ConfidenceDelta = 1.0,
            Weight = 10.0,
            Salience = 1.0,
            Reason = reason,
            BotType = botType,
            BotName = botName,
            TriggerEarlyExit = true,
            EarlyExitVerdict = "VerifiedBadBot"
        };
    }

    /// <summary>
    ///     Creates an early exit contribution for verified good bots.
    /// </summary>
    public static DetectionContribution VerifiedGoodBot(
        string detectorName,
        string reason,
        string botName)
    {
        return new DetectionContribution
        {
            DetectorName = detectorName,
            Category = "verified_good",
            ConfidenceDelta = 0.0, // Neutral - it's a bot but allowed
            Weight = 0.0,
            Salience = 1.0,
            Reason = reason,
            BotType = "VerifiedGood",
            BotName = botName,
            TriggerEarlyExit = true,
            EarlyExitVerdict = "VerifiedGoodBot"
        };
    }
}

/// <summary>
///     A record for training the heuristic model.
/// </summary>
public sealed class LearningRecord
{
    public string? RequestId { get; init; }
    public string? Fingerprint { get; init; }
    public bool IsBot { get; init; }
    public double BotProbability { get; init; }
    public double Confidence { get; init; }
    public string? BotType { get; init; }
    public string? BotName { get; init; }
    public IReadOnlyDictionary<string, double> Features { get; init; } = new Dictionary<string, double>();
    public IReadOnlyDictionary<string, double> CategoryScores { get; init; } = new Dictionary<string, double>();
    public IReadOnlyList<string> ContributingDetectors { get; init; } = Array.Empty<string>();
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
///     Store for persisting detection ledgers and learning records.
/// </summary>
public interface IDetectionLedgerStore
{
    Task SaveLedgerAsync(DetectionLedger ledger, CancellationToken ct = default);
    Task SaveLearningRecordAsync(LearningRecord record, CancellationToken ct = default);

    Task<IReadOnlyList<LearningRecord>> GetLearningRecordsAsync(
        int limit = 1000,
        DateTimeOffset? since = null,
        CancellationToken ct = default);

    Task<DetectionLedger?> GetLedgerAsync(string requestId, CancellationToken ct = default);
}