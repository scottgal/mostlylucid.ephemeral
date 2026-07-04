using Mostlylucid.Ephemeral.Atoms.Taxonomy.Ledger;

namespace Mostlylucid.Ephemeral.Atoms.Taxonomy.Atoms;

/// <summary>
///     Base interface for detection atoms (bot detection, threat detection, etc.).
///     Reads from shared SignalSink, writes contributions to DetectionLedger.
/// </summary>
/// <remarks>
///     **Flow:**
///     ```
///     Request → SignalSink (shared by coordinator)
///     ↓
///     DetectorAtom (reads signals, runs detection)
///     ↓
///     DetectionLedger (accumulates evidence)
///     ↓
///     Escalator (persists high-salience for learning)
///     ```
/// </remarks>
public interface IDetectorAtom
{
    /// <summary>
    ///     Unique name of this detector.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     Category (e.g., "UserAgent", "IP", "Behavioral").
    /// </summary>
    string Category { get; }

    /// <summary>
    ///     Execution priority (lower = runs earlier).
    /// </summary>
    int Priority { get; }

    /// <summary>
    ///     Whether this detector is currently enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    ///     Maximum execution time.
    /// </summary>
    TimeSpan Timeout { get; }

    /// <summary>
    ///     Whether failure of this detector is acceptable.
    /// </summary>
    bool IsOptional { get; }

    /// <summary>
    ///     Signal patterns that must be present for this detector to run.
    ///     Uses glob patterns (e.g., "request.headers.*", "ip.geo.*").
    /// </summary>
    IReadOnlyList<string> RequiredSignals { get; }

    /// <summary>
    ///     Executes the detector against the current signals.
    /// </summary>
    /// <param name="sink">Shared signal sink for reading context.</param>
    /// <param name="sessionId">Session/request identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Detection contributions.</returns>
    Task<IReadOnlyList<DetectionContribution>> DetectAsync(
        SignalSink sink,
        string sessionId,
        CancellationToken ct = default);
}

/// <summary>
///     Base class for detector atoms with common functionality.
/// </summary>
public abstract class DetectorAtomBase : IDetectorAtom
{
    protected DetectorAtomBase(string name, string category)
    {
        Name = name;
        Category = category;
    }

    public string Name { get; }
    public string Category { get; }
    public virtual int Priority => 50;
    public virtual bool IsEnabled => true;
    public virtual TimeSpan Timeout => TimeSpan.FromSeconds(2);
    public virtual bool IsOptional => false;
    public virtual IReadOnlyList<string> RequiredSignals => Array.Empty<string>();

    public abstract Task<IReadOnlyList<DetectionContribution>> DetectAsync(
        SignalSink sink,
        string sessionId,
        CancellationToken ct = default);

    /// <summary>
    ///     Returns a single contribution.
    /// </summary>
    protected IReadOnlyList<DetectionContribution> Single(DetectionContribution contribution)
    {
        return new[] { contribution };
    }

    /// <summary>
    ///     Returns multiple contributions.
    /// </summary>
    protected IReadOnlyList<DetectionContribution> Multiple(params DetectionContribution[] contributions)
    {
        return contributions;
    }

    /// <summary>
    ///     Returns no contributions.
    /// </summary>
    protected IReadOnlyList<DetectionContribution> None()
    {
        return Array.Empty<DetectionContribution>();
    }

    /// <summary>
    ///     Raises a signal on the sink with the atom's <see cref="Name"/>
    ///     automatically prepended. Callers write the suffix only.
    /// </summary>
    /// <example>
    ///     <code>
    ///     Raise(sink, sessionId, "matched");       // fires "MyAtom.matched"
    ///     Raise(sink, sessionId, "score", 0.42);   // fires "MyAtom.score:0.42"
    ///     </code>
    /// </example>
    protected void Raise(SignalSink sink, string sessionId, string suffix)
    {
        sink.Raise($"{Name}.{suffix}", sessionId);
    }

    /// <summary>
    ///     Raises a Model-2 hint signal (<c>Name.suffix:value</c>) on the
    ///     sink with the atom's <see cref="Name"/> automatically prepended.
    ///     Value is converted invariantly so numeric locales don't drift.
    /// </summary>
    protected void Raise(SignalSink sink, string sessionId, string suffix, object value)
    {
        var formatted = value switch
        {
            double d => d.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
            float f => f.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            _ => value?.ToString() ?? string.Empty
        };
        sink.Raise($"{Name}.{suffix}:{formatted}", sessionId);
    }

    /// <summary>
    ///     Creates a bot-indicating contribution.
    /// </summary>
    protected DetectionContribution Bot(
        double confidence,
        string reason,
        double weight = 1.0,
        string? botType = null,
        string? botName = null,
        Dictionary<string, object>? signals = null)
    {
        return DetectionContribution.Bot(Name, Category, confidence, reason, weight, botType, botName, signals);
    }

    /// <summary>
    ///     Creates a human-indicating contribution.
    /// </summary>
    protected DetectionContribution Human(
        double confidence,
        string reason,
        double weight = 1.0,
        Dictionary<string, object>? signals = null)
    {
        return DetectionContribution.Human(Name, Category, confidence, reason, weight, signals);
    }

    /// <summary>
    ///     Checks if a signal pattern exists in the sink.
    /// </summary>
    protected bool HasSignal(SignalSink sink, string pattern)
    {
        if (pattern.Contains('*') || pattern.Contains('?'))
            // Pattern matching
            return sink.Sense(s => StringPatternMatcher.Matches(s.Signal, pattern)).Count > 0;
        return sink.Detect(pattern);
    }

    /// <summary>
    ///     Gets signals matching a pattern.
    /// </summary>
    protected IReadOnlyList<SignalEvent> GetSignals(SignalSink sink, string pattern)
    {
        return sink.Sense(s => StringPatternMatcher.Matches(s.Signal, pattern));
    }

    /// <summary>
    ///     Counts signals matching a pattern.
    /// </summary>
    protected int CountSignals(SignalSink sink, string pattern, TimeSpan? since = null)
    {
        var cutoff = since.HasValue ? DateTimeOffset.UtcNow - since.Value : DateTimeOffset.MinValue;
        return sink.Sense(s =>
            s.Timestamp >= cutoff &&
            StringPatternMatcher.Matches(s.Signal, pattern)).Count;
    }
}

/// <summary>
///     Orchestrates detector atoms against a detection ledger.
///     Manages wave-based execution and early exit.
/// </summary>
/// <remarks>
///     **Wave Execution:**
///     - Wave 0: Detectors with no required signals (can run immediately)
///     - Wave N: Detectors whose required signals are now satisfied
///     **Early Exit:**
///     - Verified bot/human triggers immediate exit
///     - Quorum-based exit when confidence exceeds threshold
/// </remarks>
public sealed class DetectorOrchestrator
{
    private readonly List<IDetectorAtom> _detectors = new();
    private readonly DetectorOrchestratorOptions _options;

    public DetectorOrchestrator(DetectorOrchestratorOptions? options = null)
    {
        _options = options ?? new DetectorOrchestratorOptions();
    }

    /// <summary>
    ///     Registers a detector.
    /// </summary>
    public void Register(IDetectorAtom detector)
    {
        _detectors.Add(detector);
    }

    /// <summary>
    ///     Registers multiple detectors.
    /// </summary>
    public void RegisterMany(IEnumerable<IDetectorAtom> detectors)
    {
        _detectors.AddRange(detectors);
    }

    /// <summary>
    ///     Runs all detectors against the shared signal sink.
    /// </summary>
    /// <param name="sink">Shared signal sink for the request.</param>
    /// <param name="sessionId">Session/request identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Detection ledger with all contributions.</returns>
    public async Task<DetectionLedger> DetectAsync(
        SignalSink sink,
        string sessionId,
        CancellationToken ct = default)
    {
        var ledger = new DetectionLedger(sessionId);

        // Group detectors by wave (based on required signals)
        var waves = GroupIntoWaves(sink);

        foreach (var (waveNumber, detectors) in waves)
        {
            ct.ThrowIfCancellationRequested();

            // Signal wave start
            sink.Raise($"detection.wave.{waveNumber}.started", sessionId);

            // Run wave
            await RunWaveAsync(waveNumber, detectors, sink, sessionId, ledger, ct);

            // Publish running bot-probability and confidence AFTER the wave
            // so downstream atoms in later waves see the running score as a
            // Model-2 hint. Same rationale as per-contribution signals: the
            // blackboard IS the SignalSink, no separate ledger-access is
            // needed. GeoChangeAtom already reads `risk.current_score` today.
            var runningProb = ledger.BotProbability.ToString(
                "F4", System.Globalization.CultureInfo.InvariantCulture);
            var runningConf = ledger.Confidence.ToString(
                "F4", System.Globalization.CultureInfo.InvariantCulture);
            sink.Raise($"risk.current_score:{runningProb}", sessionId);
            sink.Raise($"risk.current_confidence:{runningConf}", sessionId);

            // Signal wave complete
            sink.Raise($"detection.wave.{waveNumber}.completed", sessionId);

            // Check for early exit
            if (ledger.EarlyExit)
            {
                sink.Raise("detection.early_exit", sessionId);
                break;
            }

            // Check for quorum (enough confidence to stop)
            if (_options.EnableQuorumExit && ledger.Confidence >= _options.QuorumConfidenceThreshold)
            {
                sink.Raise("detection.quorum_exit", sessionId);
                break;
            }
        }

        // Signal detection complete
        sink.Raise("detection.completed", sessionId);

        return ledger;
    }

    private List<(int Wave, List<IDetectorAtom> Detectors)> GroupIntoWaves(SignalSink sink)
    {
        var wave0 = new List<IDetectorAtom>();
        var laterWaves = new List<IDetectorAtom>();

        foreach (var detector in _detectors.Where(d => d.IsEnabled).OrderBy(d => d.Priority))
            if (detector.RequiredSignals.Count == 0)
                // No dependencies - can run in wave 0
                wave0.Add(detector);
            else if (AllSignalsSatisfied(sink, detector.RequiredSignals))
                // Dependencies already satisfied - can run in wave 0
                wave0.Add(detector);
            else
                // Has unsatisfied dependencies - run in later wave
                laterWaves.Add(detector);

        var result = new List<(int, List<IDetectorAtom>)>();
        if (wave0.Count > 0)
            result.Add((0, wave0));

        if (laterWaves.Count > 0)
            result.Add((1, laterWaves));

        return result;
    }

    private bool AllSignalsSatisfied(SignalSink sink, IReadOnlyList<string> patterns)
    {
        foreach (var pattern in patterns)
            if (pattern.Contains('*') || pattern.Contains('?'))
            {
                if (sink.Sense(s => StringPatternMatcher.Matches(s.Signal, pattern)).Count == 0)
                    return false;
            }
            else
            {
                if (!sink.Detect(pattern))
                    return false;
            }

        return true;
    }

    private async Task RunWaveAsync(
        int waveNumber,
        List<IDetectorAtom> detectors,
        SignalSink sink,
        string sessionId,
        DetectionLedger ledger,
        CancellationToken ct)
    {
        var tasks = detectors.Select(d => RunDetectorAsync(d, sink, sessionId, ledger, ct));

        if (_options.ParallelWaveExecution)
            await Task.WhenAll(tasks);
        else
            foreach (var task in tasks)
            {
                await task;
                if (ledger.EarlyExit)
                    break;
            }
    }

    private async Task RunDetectorAsync(
        IDetectorAtom detector,
        SignalSink sink,
        string sessionId,
        DetectionLedger ledger,
        CancellationToken ct)
    {
        var startTime = DateTimeOffset.UtcNow;
        sink.Raise($"detector.{detector.Name}.started", sessionId);

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(detector.Timeout);

            var contributions = await detector.DetectAsync(sink, sessionId, timeoutCts.Token);

            var elapsed = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;

            // Publish each contribution as a signal on the sink -- the
            // blackboard IS the SignalSink, so late atoms (e.g. HeuristicLate,
            // Similarity, AI) read prior contributions via `sink.Sense(...)`
            // instead of needing a separate ledger-access contract. Signal
            // shape is stable and parseable:
            //   contribution.<detector>.<index>:<confidence>|<weight>|<botType>|<botName>
            // Rich reason strings stay on the ledger to keep the sink small
            // and PII-safe -- reasons can contain operator-authored prose.
            var index = 0;
            foreach (var contribution in contributions)
            {
                ledger.AddContribution(contribution with { ProcessingTimeMs = elapsed });

                var confidence = contribution.ConfidenceDelta.ToString(
                    "F4", System.Globalization.CultureInfo.InvariantCulture);
                var weight = contribution.Weight.ToString(
                    "F4", System.Globalization.CultureInfo.InvariantCulture);
                var botType = contribution.BotType ?? string.Empty;
                var botName = contribution.BotName ?? string.Empty;
                sink.Raise(
                    $"contribution.{detector.Name}.{index}:{confidence}|{weight}|{botType}|{botName}",
                    sessionId);
                index++;
            }

            sink.Raise($"detector.{detector.Name}.completed", sessionId);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Timeout
            var elapsed = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            sink.Raise($"detector.{detector.Name}.timeout", sessionId);

            if (!detector.IsOptional) ledger.Record($"detector.failed.{detector.Name}", "timeout", 0.1, "orchestrator");
        }
        catch (Exception ex)
        {
            sink.Raise($"detector.{detector.Name}.error", sessionId);

            if (!detector.IsOptional) ledger.Record($"detector.error.{detector.Name}", ex.Message, 0.1, "orchestrator");
        }
    }
}

/// <summary>
///     Options for the detector orchestrator.
/// </summary>
public sealed class DetectorOrchestratorOptions
{
    /// <summary>
    ///     Whether to run detectors in parallel within a wave.
    /// </summary>
    public bool ParallelWaveExecution { get; init; } = true;

    /// <summary>
    ///     Whether to enable quorum-based early exit.
    /// </summary>
    public bool EnableQuorumExit { get; init; } = true;

    /// <summary>
    ///     Confidence threshold for quorum exit.
    /// </summary>
    public double QuorumConfidenceThreshold { get; init; } = 0.9;

    /// <summary>
    ///     Maximum waves to execute.
    /// </summary>
    public int MaxWaves { get; init; } = 10;

    /// <summary>
    ///     Overall timeout for detection.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
}

/// <summary>
///     Adapter to wrap legacy IContributingDetector implementations.
/// </summary>
/// <typeparam name="TDetector">The legacy detector type.</typeparam>
public abstract class LegacyDetectorAdapter<TDetector> : DetectorAtomBase
    where TDetector : class
{
    protected readonly TDetector _detector;

    protected LegacyDetectorAdapter(TDetector detector, string name, string category)
        : base(name, category)
    {
        _detector = detector;
    }
}