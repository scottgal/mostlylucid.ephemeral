namespace Mostlylucid.Ephemeral.Atoms.Taxonomy.Manifests;

/// <summary>
///     Deserialization model for wave YAML manifests.
///     A wave is a sequential execution stage containing parallel work items.
///     Waves execute in order; items within a wave execute according to wave mode.
/// </summary>
public sealed class WaveManifest
{
    /// <summary>
    ///     Unique identifier for this wave.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Execution order (0 = first). Waves with the same number execute in parallel.
    /// </summary>
    public int WaveNumber { get; set; }

    /// <summary>
    ///     Human-readable description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Signal scope hierarchy.
    /// </summary>
    public WaveScopeSection Scope { get; set; } = new();

    /// <summary>
    ///     Items to execute in this wave (atoms or molecules).
    /// </summary>
    public List<WaveItemReference> Items { get; set; } = new();

    /// <summary>
    ///     Execution configuration for this wave.
    /// </summary>
    public WaveExecutionSection Execution { get; set; } = new();

    /// <summary>
    ///     Early exit configuration.
    /// </summary>
    public WaveEarlyExitSection? EarlyExit { get; set; }

    /// <summary>
    ///     Conditional execution - only run wave if conditions met.
    /// </summary>
    public WaveConditionSection? Conditional { get; set; }

    /// <summary>
    ///     Concurrency lane for this wave.
    /// </summary>
    public string Lane { get; set; } = "default";

    /// <summary>
    ///     Circuit breaker configuration.
    /// </summary>
    public WaveCircuitBreakerSection? CircuitBreaker { get; set; }

    /// <summary>
    ///     Signals emitted by the wave itself.
    /// </summary>
    public WaveEmitsSection Emits { get; set; } = new();

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
///     Scope section for waves.
/// </summary>
public sealed class WaveScopeSection
{
    /// <summary>
    ///     Top-level boundary (project/domain).
    /// </summary>
    public string Sink { get; set; } = string.Empty;

    /// <summary>
    ///     Pipeline this wave belongs to.
    /// </summary>
    public string Pipeline { get; set; } = string.Empty;

    /// <summary>
    ///     This wave's unique name.
    /// </summary>
    public string Wave { get; set; } = string.Empty;
}

/// <summary>
///     Reference to an item (atom or molecule) within a wave.
/// </summary>
public sealed class WaveItemReference
{
    /// <summary>
    ///     Type of item: "atom" or "molecule".
    /// </summary>
    public string Type { get; set; } = "atom";

    /// <summary>
    ///     Item name (for reference).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Path to manifest file (relative or absolute).
    /// </summary>
    public string? Manifest { get; set; }

    /// <summary>
    ///     Whether wave fails if this item fails.
    /// </summary>
    public bool Required { get; set; } = true;

    /// <summary>
    ///     Minimum salience for this item's outputs to be considered.
    /// </summary>
    public double SalienceThreshold { get; set; }

    /// <summary>
    ///     Priority within the wave (higher = earlier in parallel execution).
    /// </summary>
    public int Priority { get; set; } = 50;

    /// <summary>
    ///     Configuration overrides for this item instance.
    /// </summary>
    public Dictionary<string, object>? ConfigOverrides { get; set; }
}

/// <summary>
///     Execution configuration for a wave.
/// </summary>
public sealed class WaveExecutionSection
{
    /// <summary>
    ///     Execution mode: "parallel" or "sequential".
    /// </summary>
    public string Mode { get; set; } = "parallel";

    /// <summary>
    ///     Maximum concurrent items (for parallel mode).
    /// </summary>
    public int MaxConcurrency { get; set; } = 4;

    /// <summary>
    ///     Wave timeout (TimeSpan string).
    /// </summary>
    public string Timeout { get; set; } = "00:00:01";

    /// <summary>
    ///     Stop on first failure of required item.
    /// </summary>
    public bool FailFast { get; set; }

    /// <summary>
    ///     Delay between wave completion and next wave start.
    /// </summary>
    public string? InterWaveDelay { get; set; }
}

/// <summary>
///     Early exit configuration for a wave.
/// </summary>
public sealed class WaveEarlyExitSection
{
    /// <summary>
    ///     Whether early exit is enabled for this wave.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     Signals that allow early exit (positive decision made).
    /// </summary>
    public List<string> AllowSignals { get; set; } = new();

    /// <summary>
    ///     Signals that block/reject early (negative decision made).
    /// </summary>
    public List<string> BlockSignals { get; set; } = new();

    /// <summary>
    ///     Custom condition expression for early exit.
    /// </summary>
    public string? Condition { get; set; }
}

/// <summary>
///     Conditional execution section for waves.
/// </summary>
public sealed class WaveConditionSection
{
    /// <summary>
    ///     Conditions that must be met to run this wave.
    /// </summary>
    public List<SignalCondition> When { get; set; } = new();

    /// <summary>
    ///     Conditions that skip this wave.
    /// </summary>
    public List<SignalCondition> SkipWhen { get; set; } = new();
}

/// <summary>
///     Circuit breaker configuration for wave items.
/// </summary>
public sealed class WaveCircuitBreakerSection
{
    /// <summary>
    ///     Enable circuit breaker per item.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Failures before circuit opens.
    /// </summary>
    public int FailureThreshold { get; set; } = 3;

    /// <summary>
    ///     Successes needed to close circuit after half-open.
    /// </summary>
    public int SuccessThreshold { get; set; } = 2;

    /// <summary>
    ///     Time before half-open attempt (TimeSpan string).
    /// </summary>
    public string ResetTimeout { get; set; } = "00:00:30";
}

/// <summary>
///     Signals emitted by the wave.
/// </summary>
public sealed class WaveEmitsSection
{
    /// <summary>
    ///     Signals emitted when wave starts.
    /// </summary>
    public List<string> OnStart { get; set; } = new();

    /// <summary>
    ///     Signals emitted when wave completes successfully.
    /// </summary>
    public List<SignalDefinition> OnComplete { get; set; } = new();

    /// <summary>
    ///     Signals emitted on early exit.
    /// </summary>
    public List<SignalDefinition> OnEarlyExit { get; set; } = new();

    /// <summary>
    ///     Signals emitted on wave failure.
    /// </summary>
    public List<SignalDefinition> OnFailure { get; set; } = new();

    /// <summary>
    ///     Signals emitted on timeout.
    /// </summary>
    public List<SignalDefinition> OnTimeout { get; set; } = new();
}
