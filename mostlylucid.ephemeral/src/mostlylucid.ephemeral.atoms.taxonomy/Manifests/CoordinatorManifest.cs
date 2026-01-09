namespace Mostlylucid.Ephemeral.Atoms.Taxonomy.Manifests;

/// <summary>
///     Deserialization model for coordinator YAML manifests.
///     A coordinator orchestrates processing with keying, queueing, and scheduling.
/// </summary>
public sealed class CoordinatorManifest
{
    /// <summary>
    ///     Unique identifier for this coordinator.
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
    ///     Taxonomy classification.
    /// </summary>
    public CoordinatorTaxonomySection Taxonomy { get; set; } = new();

    /// <summary>
    ///     Signal scope hierarchy.
    /// </summary>
    public CoordinatorScopeSection Scope { get; set; } = new();

    /// <summary>
    ///     Keying configuration for per-key processing.
    /// </summary>
    public CoordinatorKeyingSection? Keying { get; set; }

    /// <summary>
    ///     Execution configuration.
    /// </summary>
    public CoordinatorExecutionSection Execution { get; set; } = new();

    /// <summary>
    ///     Queue configuration.
    /// </summary>
    public CoordinatorQueueSection? Queue { get; set; }

    /// <summary>
    ///     Triggers that queue work to this coordinator.
    /// </summary>
    public Dictionary<string, CoordinatorTrigger> Triggers { get; set; } = new();

    /// <summary>
    ///     Escalation to downstream processors (e.g., LLM).
    /// </summary>
    public Dictionary<string, CoordinatorEscalation> Escalation { get; set; } = new();

    /// <summary>
    ///     Signals this coordinator emits.
    /// </summary>
    public CoordinatorEmitsSection Emits { get; set; } = new();

    /// <summary>
    ///     Budget constraints.
    /// </summary>
    public CoordinatorBudgetSection? Budget { get; set; }

    /// <summary>
    ///     Metrics to expose.
    /// </summary>
    public List<string> Metrics { get; set; } = new();

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
///     Taxonomy for coordinators.
/// </summary>
public sealed class CoordinatorTaxonomySection
{
    /// <summary>
    ///     Kind: "coordinator", "learning", "feedback".
    /// </summary>
    public string Kind { get; set; } = "coordinator";

    /// <summary>
    ///     Determinism.
    /// </summary>
    public string Determinism { get; set; } = "deterministic";

    /// <summary>
    ///     Persistence.
    /// </summary>
    public string Persistence { get; set; } = "ephemeral";
}

/// <summary>
///     Scope for coordinators.
/// </summary>
public sealed class CoordinatorScopeSection
{
    /// <summary>
    ///     Top-level sink.
    /// </summary>
    public string Sink { get; set; } = string.Empty;

    /// <summary>
    ///     Coordinator name.
    /// </summary>
    public string Coordinator { get; set; } = string.Empty;
}

/// <summary>
///     Keying configuration for per-key sequential processing.
/// </summary>
public sealed class CoordinatorKeyingSection
{
    /// <summary>
    ///     Expression to extract key from signals/context.
    ///     Examples: "signals['detection.signature.hash']", "$.userId"
    /// </summary>
    public string KeyExpression { get; set; } = string.Empty;

    /// <summary>
    ///     Maximum concurrent keys (bounds memory).
    /// </summary>
    public int MaxConcurrentKeys { get; set; } = 10000;

    /// <summary>
    ///     Eviction policy: "lru", "fifo", "priority".
    /// </summary>
    public string EvictionPolicy { get; set; } = "lru";

    /// <summary>
    ///     Per-key queue size.
    /// </summary>
    public int PerKeyQueueSize { get; set; } = 100;

    /// <summary>
    ///     Overflow policy: "drop_oldest", "drop_newest", "reject".
    /// </summary>
    public string OverflowPolicy { get; set; } = "drop_oldest";

    /// <summary>
    ///     Idle key timeout before cleanup (TimeSpan string).
    /// </summary>
    public string IdleKeyTimeout { get; set; } = "00:05:00";
}

/// <summary>
///     Execution configuration for coordinators.
/// </summary>
public sealed class CoordinatorExecutionSection
{
    /// <summary>
    ///     Mode: "sequential_per_key", "parallel", "sequential".
    ///     - sequential_per_key: Sequential within key, parallel across keys
    ///     - parallel: Full parallelism
    ///     - sequential: Global sequential
    /// </summary>
    public string Mode { get; set; } = "sequential_per_key";

    /// <summary>
    ///     Maximum parallel keys processing.
    /// </summary>
    public int MaxParallelKeys { get; set; } = 100;

    /// <summary>
    ///     Timeout per key item.
    /// </summary>
    public string KeyTimeout { get; set; } = "00:00:30";

    /// <summary>
    ///     Worker count for processing.
    /// </summary>
    public int WorkerCount { get; set; } = 4;

    /// <summary>
    ///     Scheduling mode: "round_robin", "priority", "fifo".
    /// </summary>
    public string Scheduling { get; set; } = "round_robin";
}

/// <summary>
///     Queue configuration for coordinators.
/// </summary>
public sealed class CoordinatorQueueSection
{
    /// <summary>
    ///     Maximum total items across all keys.
    /// </summary>
    public int MaxTotalItems { get; set; } = 100000;

    /// <summary>
    ///     Whether to enable backpressure signaling.
    /// </summary>
    public bool EnableBackpressure { get; set; } = true;

    /// <summary>
    ///     Backpressure threshold (percentage of max).
    /// </summary>
    public double BackpressureThreshold { get; set; } = 0.8;

    /// <summary>
    ///     Persistence: "memory", "durable".
    /// </summary>
    public string Persistence { get; set; } = "memory";
}

/// <summary>
///     Trigger that queues work to this coordinator.
/// </summary>
public sealed class CoordinatorTrigger
{
    /// <summary>
    ///     Conditions that trigger queueing.
    /// </summary>
    public List<SignalCondition> When { get; set; } = new();

    /// <summary>
    ///     Priority for this trigger.
    /// </summary>
    public string Priority { get; set; } = "normal";

    /// <summary>
    ///     Sample rate (0.0-1.0) for probabilistic triggers.
    /// </summary>
    public double? SampleRate { get; set; }

    /// <summary>
    ///     Signals to include in work item.
    /// </summary>
    public List<string> IncludeSignals { get; set; } = new();

    /// <summary>
    ///     Description of this trigger.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
///     Escalation from coordinator to downstream processor.
/// </summary>
public sealed class CoordinatorEscalation
{
    /// <summary>
    ///     Conditions that trigger escalation.
    /// </summary>
    public List<SignalCondition> When { get; set; } = new();

    /// <summary>
    ///     Target atom/molecule/wave.
    /// </summary>
    public string To { get; set; } = string.Empty;

    /// <summary>
    ///     Rate limiting.
    /// </summary>
    public EscalationRateLimitSection? RateLimit { get; set; }

    /// <summary>
    ///     Budget constraints.
    /// </summary>
    public EscalationBudgetSection? Budget { get; set; }

    /// <summary>
    ///     Callback signals on completion.
    /// </summary>
    public List<string>? OnComplete { get; set; }
}

/// <summary>
///     Signals emitted by the coordinator.
/// </summary>
public sealed class CoordinatorEmitsSection
{
    /// <summary>
    ///     Signals emitted when work is queued.
    /// </summary>
    public List<string> OnQueued { get; set; } = new();

    /// <summary>
    ///     Signals emitted when processing starts.
    /// </summary>
    public List<string> OnProcessingStart { get; set; } = new();

    /// <summary>
    ///     Signals emitted when item completes.
    /// </summary>
    public List<SignalDefinition> OnComplete { get; set; } = new();

    /// <summary>
    ///     Signals emitted on validation result.
    /// </summary>
    public List<SignalDefinition> OnValidation { get; set; } = new();

    /// <summary>
    ///     Signals emitted on failure.
    /// </summary>
    public List<SignalDefinition> OnFailure { get; set; } = new();

    /// <summary>
    ///     Signals emitted on backpressure.
    /// </summary>
    public List<string> OnBackpressure { get; set; } = new();
}

/// <summary>
///     Budget constraints for coordinators.
/// </summary>
public sealed class CoordinatorBudgetSection
{
    /// <summary>
    ///     Daily LLM budget in dollars.
    /// </summary>
    public decimal? DailyLlmDollars { get; set; }

    /// <summary>
    ///     Maximum concurrent LLM requests.
    /// </summary>
    public int? MaxConcurrentLlmRequests { get; set; }

    /// <summary>
    ///     Tokens per minute limit.
    /// </summary>
    public int? TokensPerMinute { get; set; }

    /// <summary>
    ///     Requests per minute limit.
    /// </summary>
    public int? RequestsPerMinute { get; set; }
}
