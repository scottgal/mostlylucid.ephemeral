namespace Mostlylucid.Ephemeral.Atoms.Taxonomy.Manifests;

/// <summary>
///     Deserialization model for pipeline YAML manifests.
///     A pipeline orchestrates waves in sequence with hot path and background phases.
/// </summary>
public sealed class PipelineManifest
{
    /// <summary>
    ///     Unique identifier for this pipeline.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Semantic version.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    ///     Human-readable description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Signal scope hierarchy.
    /// </summary>
    public PipelineScopeSection Scope { get; set; } = new();

    /// <summary>
    ///     Hot path configuration (in request thread, latency-critical).
    /// </summary>
    public HotPathSection HotPath { get; set; } = new();

    /// <summary>
    ///     Background processing configuration (post-response, async).
    /// </summary>
    public BackgroundSection? Background { get; set; }

    /// <summary>
    ///     Lane configurations (concurrency pools).
    /// </summary>
    public Dictionary<string, LaneSection> Lanes { get; set; } = new();

    /// <summary>
    ///     Decision routing based on signals.
    /// </summary>
    public Dictionary<string, DecisionRouteSection> Routing { get; set; } = new();

    /// <summary>
    ///     Global escalation rules.
    /// </summary>
    public Dictionary<string, PipelineEscalationRule> Escalation { get; set; } = new();

    /// <summary>
    ///     Pipeline-level defaults.
    /// </summary>
    public PipelineDefaultsSection Defaults { get; set; } = new();

    /// <summary>
    ///     Tags for filtering/grouping.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    ///     Additional metadata.
    /// </summary>
    public MetaSection? Meta { get; set; }
}

/// <summary>
///     Scope section for pipelines.
/// </summary>
public sealed class PipelineScopeSection
{
    /// <summary>
    ///     Top-level boundary (project/domain).
    /// </summary>
    public string Sink { get; set; } = string.Empty;

    /// <summary>
    ///     Coordinator context.
    /// </summary>
    public string Coordinator { get; set; } = string.Empty;
}

/// <summary>
///     Hot path section - latency-critical waves in request thread.
/// </summary>
public sealed class HotPathSection
{
    /// <summary>
    ///     Waves to execute in hot path (inline or referenced).
    /// </summary>
    public List<WaveReference> Waves { get; set; } = new();

    /// <summary>
    ///     Total hot path timeout (TimeSpan string).
    /// </summary>
    public string TotalTimeout { get; set; } = "00:00:00.200";

    /// <summary>
    ///     Target latency for performance monitoring.
    /// </summary>
    public string? TargetLatency { get; set; }

    /// <summary>
    ///     Signals emitted when hot path completes.
    /// </summary>
    public List<SignalDefinition> OnComplete { get; set; } = new();
}

/// <summary>
///     Reference to a wave (can be inline or external manifest).
/// </summary>
public sealed class WaveReference
{
    /// <summary>
    ///     Wave name (for inline definition).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Path to wave manifest file (if external).
    /// </summary>
    public string? Manifest { get; set; }

    /// <summary>
    ///     Wave number override (optional, from manifest if not specified).
    /// </summary>
    public int? WaveNumber { get; set; }

    /// <summary>
    ///     Items for inline wave definition.
    /// </summary>
    public List<WaveItemReference>? Items { get; set; }

    /// <summary>
    ///     Execution config for inline wave.
    /// </summary>
    public WaveExecutionSection? Execution { get; set; }

    /// <summary>
    ///     Early exit config for inline wave.
    /// </summary>
    public WaveEarlyExitSection? EarlyExit { get; set; }

    /// <summary>
    ///     Conditional execution for inline wave.
    /// </summary>
    public WaveConditionSection? Conditional { get; set; }
}

/// <summary>
///     Background processing section - async post-response processing.
/// </summary>
public sealed class BackgroundSection
{
    /// <summary>
    ///     Coordinator manifest for background processing.
    /// </summary>
    public CoordinatorReference? Coordinator { get; set; }

    /// <summary>
    ///     Background waves (e.g., LLM validation).
    /// </summary>
    public List<WaveReference> Waves { get; set; } = new();

    /// <summary>
    ///     Queue configuration for background work.
    /// </summary>
    public BackgroundQueueSection? Queue { get; set; }
}

/// <summary>
///     Reference to a coordinator.
/// </summary>
public sealed class CoordinatorReference
{
    /// <summary>
    ///     Coordinator name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Path to coordinator manifest.
    /// </summary>
    public string? Manifest { get; set; }
}

/// <summary>
///     Background queue configuration.
/// </summary>
public sealed class BackgroundQueueSection
{
    /// <summary>
    ///     Maximum items in queue.
    /// </summary>
    public int MaxSize { get; set; } = 10000;

    /// <summary>
    ///     Overflow policy: "drop_oldest", "drop_newest", "reject".
    /// </summary>
    public string OverflowPolicy { get; set; } = "drop_oldest";

    /// <summary>
    ///     Processing concurrency.
    /// </summary>
    public int Concurrency { get; set; } = 4;
}

/// <summary>
///     Decision routing based on signal values.
/// </summary>
public sealed class DecisionRouteSection
{
    /// <summary>
    ///     Condition expression for this route.
    /// </summary>
    public string When { get; set; } = string.Empty;

    /// <summary>
    ///     Action to take: "allow", "block", "challenge", "throttle".
    /// </summary>
    public string Action { get; set; } = "allow";

    /// <summary>
    ///     Background processing mode: "none", "verification_sample", "full_learning".
    /// </summary>
    public string Background { get; set; } = "none";

    /// <summary>
    ///     Sample rate for verification (0.0-1.0).
    /// </summary>
    public double? SampleRate { get; set; }

    /// <summary>
    ///     Priority for background processing.
    /// </summary>
    public string? Priority { get; set; }

    /// <summary>
    ///     Additional signals to emit on this route.
    /// </summary>
    public List<SignalDefinition> Emit { get; set; } = new();
}

/// <summary>
///     Pipeline-level escalation rule.
/// </summary>
public sealed class PipelineEscalationRule
{
    /// <summary>
    ///     Signals that trigger this escalation.
    /// </summary>
    public List<string> Signals { get; set; } = new();

    /// <summary>
    ///     Conditions that skip this escalation.
    /// </summary>
    public List<string> SkipWhen { get; set; } = new();

    /// <summary>
    ///     Target for escalation.
    /// </summary>
    public string? To { get; set; }

    /// <summary>
    ///     Rate limiting for this escalation.
    /// </summary>
    public EscalationRateLimitSection? RateLimit { get; set; }

    /// <summary>
    ///     Budget constraints.
    /// </summary>
    public EscalationBudgetSection? Budget { get; set; }
}

/// <summary>
///     Rate limiting for escalation.
/// </summary>
public sealed class EscalationRateLimitSection
{
    /// <summary>
    ///     Maximum requests per minute.
    /// </summary>
    public int RequestsPerMinute { get; set; } = 60;

    /// <summary>
    ///     Maximum tokens per minute (for LLM).
    /// </summary>
    public int? TokensPerMinute { get; set; }
}

/// <summary>
///     Budget constraints for escalation.
/// </summary>
public sealed class EscalationBudgetSection
{
    /// <summary>
    ///     Daily budget in dollars.
    /// </summary>
    public decimal DailyDollars { get; set; } = 10.00m;

    /// <summary>
    ///     Per-request max cost in cents.
    /// </summary>
    public decimal? PerRequestMaxCents { get; set; }
}

/// <summary>
///     Pipeline-level defaults.
/// </summary>
public sealed class PipelineDefaultsSection
{
    /// <summary>
    ///     Timing defaults.
    /// </summary>
    public PipelineTimingDefaults Timing { get; set; } = new();

    /// <summary>
    ///     Scoring defaults.
    /// </summary>
    public PipelineScoringDefaults Scoring { get; set; } = new();

    /// <summary>
    ///     Circuit breaker defaults.
    /// </summary>
    public WaveCircuitBreakerSection? CircuitBreaker { get; set; }

    /// <summary>
    ///     Learning defaults.
    /// </summary>
    public PipelineLearningDefaults? Learning { get; set; }

    /// <summary>
    ///     LLM defaults.
    /// </summary>
    public PipelineLlmDefaults? Llm { get; set; }

    /// <summary>
    ///     Feature flags.
    /// </summary>
    public Dictionary<string, bool> Features { get; set; } = new();
}

/// <summary>
///     Timing defaults for pipeline.
/// </summary>
public sealed class PipelineTimingDefaults
{
    /// <summary>
    ///     Target hot path latency in ms.
    /// </summary>
    public int TargetLatencyMs { get; set; } = 150;

    /// <summary>
    ///     Maximum hot path latency in ms.
    /// </summary>
    public int MaxLatencyMs { get; set; } = 200;

    /// <summary>
    ///     Total timeout in ms.
    /// </summary>
    public int TotalTimeoutMs { get; set; } = 6000;
}

/// <summary>
///     Scoring defaults for pipeline.
/// </summary>
public sealed class PipelineScoringDefaults
{
    /// <summary>
    ///     Score threshold for bot classification.
    /// </summary>
    public double BotThreshold { get; set; } = 0.7;

    /// <summary>
    ///     Score threshold for human classification.
    /// </summary>
    public double HumanThreshold { get; set; } = 0.3;

    /// <summary>
    ///     Lower bound for escalation consideration.
    /// </summary>
    public double EscalationLow { get; set; } = 0.4;

    /// <summary>
    ///     Upper bound for escalation consideration.
    /// </summary>
    public double EscalationHigh { get; set; } = 0.6;
}

/// <summary>
///     Learning defaults.
/// </summary>
public sealed class PipelineLearningDefaults
{
    /// <summary>
    ///     Verification sample rate (0.0-1.0).
    /// </summary>
    public double VerificationSampleRate { get; set; } = 0.01;

    /// <summary>
    ///     Maximum keys in learning coordinator.
    /// </summary>
    public int MaxKeys { get; set; } = 10000;

    /// <summary>
    ///     Per-key queue size.
    /// </summary>
    public int PerKeyQueueSize { get; set; } = 100;
}

/// <summary>
///     LLM defaults.
/// </summary>
public sealed class PipelineLlmDefaults
{
    /// <summary>
    ///     Daily budget in dollars.
    /// </summary>
    public decimal DailyBudgetDollars { get; set; } = 10.00m;

    /// <summary>
    ///     Requests per minute limit.
    /// </summary>
    public int RequestsPerMinute { get; set; } = 60;

    /// <summary>
    ///     Tokens per minute limit.
    /// </summary>
    public int? TokensPerMinute { get; set; }
}
