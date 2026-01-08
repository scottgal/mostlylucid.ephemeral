using System;
using System.Collections.Generic;

namespace Mostlylucid.Ephemeral.Atoms.Taxonomy.Manifests;

/// <summary>
/// Deserialization model for atom YAML manifests.
/// Atoms own their signals - signals die when the atom dies unless preserved.
/// </summary>
public sealed class AtomManifest
{
    /// <summary>
    /// Unique identifier (matches class name).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Semantic version.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Human-readable description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Implementation reference (NuGet package containing the atom implementation).
    /// Manifests reference packages - the inverted package model.
    /// </summary>
    public ImplementationSection? Implementation { get; set; }

    /// <summary>
    /// Taxonomy classification.
    /// </summary>
    public TaxonomySection Taxonomy { get; set; } = new();

    /// <summary>
    /// Signal scope hierarchy.
    /// </summary>
    public ScopeSection Scope { get; set; } = new();

    /// <summary>
    /// Trigger conditions for execution.
    /// </summary>
    public TriggerSection? Triggers { get; set; }

    /// <summary>
    /// Signal emissions (owned by this atom).
    /// </summary>
    public EmitsSection Emits { get; set; } = new();

    /// <summary>
    /// Signal preservation rules (echo, escalate, propagate).
    /// </summary>
    public PreserveSection? Preserve { get; set; }

    /// <summary>
    /// Signal dependencies (what this atom listens for).
    /// </summary>
    public ListensSection? Listens { get; set; }

    /// <summary>
    /// Escalation rules for downstream processing.
    /// </summary>
    public Dictionary<string, EscalationRule>? Escalation { get; set; }

    /// <summary>
    /// Budget constraints.
    /// </summary>
    public BudgetSection? Budget { get; set; }

    /// <summary>
    /// Concurrency lane configuration.
    /// </summary>
    public LaneSection? Lane { get; set; }

    /// <summary>
    /// Evidence requirements expression.
    /// </summary>
    public EvidenceSection? Evidence { get; set; }

    /// <summary>
    /// Configuration bindings.
    /// </summary>
    public ConfigSection? Config { get; set; }

    /// <summary>
    /// Tags for filtering/grouping.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Additional metadata.
    /// </summary>
    public MetaSection? Meta { get; set; }
}

/// <summary>
/// Taxonomy classification for an atom.
/// </summary>
public sealed class TaxonomySection
{
    /// <summary>
    /// Primary kind: sensor, extractor, embedder, retriever, proposer, constrainer, ranker, renderer, coordinator, feedback, escalator, guard.
    /// </summary>
    public string Kind { get; set; } = "sensor";

    /// <summary>
    /// Determinism: deterministic or probabilistic.
    /// </summary>
    public string Determinism { get; set; } = "deterministic";

    /// <summary>
    /// Persistence authority: ephemeral, escalatable, or direct_write.
    /// </summary>
    public string Persistence { get; set; } = "ephemeral";
}

/// <summary>
/// Signal scope hierarchy (sink > coordinator > atom).
/// </summary>
public sealed class ScopeSection
{
    /// <summary>
    /// Top-level boundary (project/domain).
    /// </summary>
    public string Sink { get; set; } = string.Empty;

    /// <summary>
    /// Processing unit context.
    /// </summary>
    public string Coordinator { get; set; } = string.Empty;

    /// <summary>
    /// This atom's unique name within coordinator.
    /// </summary>
    public string Atom { get; set; } = string.Empty;
}

/// <summary>
/// Trigger conditions for atom execution.
/// </summary>
public sealed class TriggerSection
{
    /// <summary>
    /// ALL must be satisfied to run.
    /// </summary>
    public List<SignalCondition> Requires { get; set; } = new();

    /// <summary>
    /// Run when ANY of these signals exist.
    /// </summary>
    public List<string> Signals { get; set; } = new();

    /// <summary>
    /// Skip if ANY of these exist.
    /// </summary>
    public List<SignalCondition> SkipWhen { get; set; } = new();
}

/// <summary>
/// A signal condition with optional value/expression check.
/// </summary>
public sealed class SignalCondition
{
    /// <summary>
    /// Signal key to check.
    /// </summary>
    public string Signal { get; set; } = string.Empty;

    /// <summary>
    /// Condition expression: HasValue, IsNullOrWhiteSpace, >, <, >=, <=, ==, !=.
    /// </summary>
    public string? Condition { get; set; }

    /// <summary>
    /// Exact value match.
    /// </summary>
    public object? Value { get; set; }
}

/// <summary>
/// Signal emissions owned by this atom.
/// </summary>
public sealed class EmitsSection
{
    /// <summary>
    /// Signals emitted when atom starts.
    /// </summary>
    public List<string> OnStart { get; set; } = new();

    /// <summary>
    /// Signals emitted when atom completes successfully.
    /// </summary>
    public List<SignalDefinition> OnComplete { get; set; } = new();

    /// <summary>
    /// Signals emitted when atom fails.
    /// </summary>
    public List<SignalDefinition> OnFailure { get; set; } = new();

    /// <summary>
    /// Context-dependent signal emissions.
    /// </summary>
    public List<ConditionalSignal> Conditional { get; set; } = new();
}

/// <summary>
/// A signal definition with type and metadata.
/// </summary>
public class SignalDefinition
{
    /// <summary>
    /// Signal key (may be simple string for lifecycle signals).
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// C# type: int, bool, double, string, List&lt;T&gt;, float[], etc.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Human-readable description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Expected confidence range [min, max].
    /// </summary>
    public double[]? ConfidenceRange { get; set; }
}

/// <summary>
/// A conditional signal emitted based on runtime state.
/// </summary>
public sealed class ConditionalSignal : SignalDefinition
{
    /// <summary>
    /// Condition expression for emission.
    /// </summary>
    public string? When { get; set; }
}

/// <summary>
/// Signal preservation rules - how signals survive atom death.
/// </summary>
public sealed class PreserveSection
{
    /// <summary>
    /// Echo: Copy signals to another atom.
    /// </summary>
    public List<EchoRule> Echo { get; set; } = new();

    /// <summary>
    /// Escalate: Persist signals to durable storage.
    /// </summary>
    public List<EscalateRule> Escalate { get; set; } = new();

    /// <summary>
    /// Propagate: Forward to molecule's aggregate output.
    /// </summary>
    public List<PropagateRule> Propagate { get; set; } = new();
}

/// <summary>
/// Rule for echoing a signal to another atom.
/// </summary>
public sealed class EchoRule
{
    /// <summary>
    /// Signal key to echo.
    /// </summary>
    public string Signal { get; set; } = string.Empty;

    /// <summary>
    /// Target atom that will own the copy.
    /// </summary>
    public string To { get; set; } = string.Empty;

    /// <summary>
    /// When to echo: always, on_complete, or conditional expression.
    /// </summary>
    public string When { get; set; } = "always";
}

/// <summary>
/// Rule for escalating a signal to a higher processing level.
/// Escalation increases salience - each level filters/promotes signals for further processing.
/// Low-salience signals are dropped, high-salience signals progress through the pipeline.
/// </summary>
public sealed class EscalateRule
{
    /// <summary>
    /// Signal key to escalate.
    /// </summary>
    public string Signal { get; set; } = string.Empty;

    /// <summary>
    /// Escalation target - can be:
    /// - Another atom/molecule for further processing (e.g., "llm_analyzer")
    /// - A coordinator for learning (e.g., "learning_coordinator")
    /// - A storage layer for persistence (e.g., "signal_database")
    /// </summary>
    public string To { get; set; } = string.Empty;

    /// <summary>
    /// Salience threshold - minimum salience score to escalate (0.0-1.0).
    /// Signals below this threshold are dropped.
    /// </summary>
    public double? SalienceThreshold { get; set; }

    /// <summary>
    /// Condition expression for escalation (evaluated at runtime).
    /// </summary>
    public string? When { get; set; }

    /// <summary>
    /// Priority boost applied to escalated signals (0-100).
    /// Higher-priority signals are processed first at the target.
    /// </summary>
    public int PriorityBoost { get; set; }

    /// <summary>
    /// Whether to batch escalated signals (for efficiency).
    /// </summary>
    public bool Batch { get; set; }

    /// <summary>
    /// Maximum batch size before flush.
    /// </summary>
    public int? BatchSize { get; set; }

    /// <summary>
    /// Maximum time to wait before flushing batch.
    /// </summary>
    public string? BatchTimeout { get; set; }
}

/// <summary>
/// Rule for propagating a signal to molecule output.
/// </summary>
public sealed class PropagateRule
{
    /// <summary>
    /// Signal key to propagate.
    /// </summary>
    public string Signal { get; set; } = string.Empty;

    /// <summary>
    /// Renamed key in molecule context.
    /// </summary>
    public string? As { get; set; }
}

/// <summary>
/// Signal dependencies for this atom.
/// </summary>
public sealed class ListensSection
{
    /// <summary>
    /// Required signals (must exist before atom runs).
    /// </summary>
    public List<string> Required { get; set; } = new();

    /// <summary>
    /// Optional signals (may use if available).
    /// </summary>
    public List<string> Optional { get; set; } = new();
}

/// <summary>
/// Escalation rule for downstream processing.
/// </summary>
public sealed class EscalationRule
{
    /// <summary>
    /// Conditions that trigger escalation.
    /// </summary>
    public List<SignalCondition> When { get; set; } = new();

    /// <summary>
    /// Conditions that prevent escalation.
    /// </summary>
    public List<SignalCondition> SkipWhen { get; set; } = new();

    /// <summary>
    /// Description of escalation purpose.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Budget constraints for atom execution.
/// </summary>
public sealed class BudgetSection
{
    /// <summary>
    /// Maximum execution duration (TimeSpan string).
    /// </summary>
    public string? MaxDuration { get; set; }

    /// <summary>
    /// Maximum token count (for LLM atoms).
    /// </summary>
    public int? MaxTokens { get; set; }

    /// <summary>
    /// Maximum cost in decimal units.
    /// </summary>
    public decimal? MaxCost { get; set; }
}

/// <summary>
/// Concurrency lane configuration.
/// </summary>
public sealed class LaneSection
{
    /// <summary>
    /// Lane name for semaphore grouping.
    /// </summary>
    public string Name { get; set; } = "default";

    /// <summary>
    /// Maximum parallel executions in this lane.
    /// </summary>
    public int MaxConcurrency { get; set; } = 1;

    /// <summary>
    /// Priority within lane (higher = earlier).
    /// </summary>
    public int Priority { get; set; } = 50;
}

/// <summary>
/// Evidence requirements for atom outputs.
/// </summary>
public sealed class EvidenceSection
{
    /// <summary>
    /// Requirements expression.
    /// </summary>
    public string? Requirements { get; set; }
}

/// <summary>
/// Configuration bindings.
/// </summary>
public sealed class ConfigSection
{
    /// <summary>
    /// List of config key bindings.
    /// </summary>
    public List<ConfigBinding> Bindings { get; set; } = new();
}

/// <summary>
/// A configuration key binding.
/// </summary>
public sealed class ConfigBinding
{
    /// <summary>
    /// Configuration key name.
    /// </summary>
    public string ConfigKey { get; set; } = string.Empty;

    /// <summary>
    /// Skip atom if config value is false.
    /// </summary>
    public bool SkipIfFalse { get; set; }

    /// <summary>
    /// Maps config value to local property name.
    /// </summary>
    public string? MapsTo { get; set; }
}

/// <summary>
/// Additional metadata.
/// </summary>
public sealed class MetaSection
{
    /// <summary>
    /// Author email/name.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Creation date.
    /// </summary>
    public string? Created { get; set; }

    /// <summary>
    /// Last update date.
    /// </summary>
    public string? Updated { get; set; }
}

/// <summary>
/// Implementation reference - manifests reference NuGet packages (inverted model).
/// </summary>
public sealed class ImplementationSection
{
    /// <summary>
    /// NuGet package reference.
    /// </summary>
    public NuGetReference? NuGet { get; set; }

    /// <summary>
    /// Implementation mode when no NuGet package is specified.
    /// </summary>
    public string? Mode { get; set; }

    /// <summary>
    /// Note for contract-only manifests.
    /// </summary>
    public string? Note { get; set; }
}

/// <summary>
/// NuGet package reference for atom implementation.
/// </summary>
public sealed class NuGetReference
{
    /// <summary>
    /// NuGet package ID.
    /// </summary>
    public string Package { get; set; } = string.Empty;

    /// <summary>
    /// SemVer version constraint (e.g., "^2.0.0", ">=1.0.0 &lt;2.0.0", "1.2.3").
    /// </summary>
    public string Version { get; set; } = "*";

    /// <summary>
    /// Fully qualified type name implementing ISignalSource.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Interface the type implements (for discovery).
    /// </summary>
    public string? Implements { get; set; }

    /// <summary>
    /// Name for interface-based discovery.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Factory type for factory pattern.
    /// </summary>
    public string? Factory { get; set; }

    /// <summary>
    /// Factory method name.
    /// </summary>
    public string? Method { get; set; }

    /// <summary>
    /// Generic type arguments.
    /// </summary>
    public Dictionary<string, string>? TypeArgs { get; set; }

    /// <summary>
    /// Custom NuGet source URL (for private registries).
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Whether package requires a license.
    /// </summary>
    public bool RequiresLicense { get; set; }
}
