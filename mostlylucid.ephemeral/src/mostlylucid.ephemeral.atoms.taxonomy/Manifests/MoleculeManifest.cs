using System;
using System.Collections.Generic;

namespace Mostlylucid.Ephemeral.Atoms.Taxonomy.Manifests;

/// <summary>
/// Deserialization model for molecule YAML manifests.
/// Molecules are assemblages of atoms with aggregate signal contracts.
/// </summary>
public sealed class MoleculeManifest
{
    /// <summary>
    /// Unique identifier for this molecule.
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
    /// Aggregated taxonomy classification.
    /// </summary>
    public MoleculeTaxonomySection Taxonomy { get; set; } = new();

    /// <summary>
    /// Signal scope hierarchy.
    /// </summary>
    public MoleculeScopeSection Scope { get; set; } = new();

    /// <summary>
    /// Constituent atoms in execution order.
    /// </summary>
    public List<AtomReference> Atoms { get; set; } = new();

    /// <summary>
    /// Aggregate signal emissions from constituent atoms.
    /// </summary>
    public MoleculeEmitsSection Emits { get; set; } = new();

    /// <summary>
    /// Aggregate signal dependencies.
    /// </summary>
    public ListensSection? Listens { get; set; }

    /// <summary>
    /// Molecule-level escalation rules.
    /// </summary>
    public Dictionary<string, MoleculeEscalationRule>? Escalation { get; set; }

    /// <summary>
    /// Concurrency lane configuration for the molecule as a unit.
    /// </summary>
    public LaneSection? Lane { get; set; }

    /// <summary>
    /// Execution semantics.
    /// </summary>
    public ExecutionSection Execution { get; set; } = new();

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
/// Aggregated taxonomy for a molecule.
/// </summary>
public sealed class MoleculeTaxonomySection
{
    /// <summary>
    /// Primary role of this molecule.
    /// </summary>
    public string PrimaryKind { get; set; } = "sensor";

    /// <summary>
    /// All roles from constituent atoms.
    /// </summary>
    public List<string> Kinds { get; set; } = new();

    /// <summary>
    /// Aggregated determinism (any probabilistic → probabilistic).
    /// </summary>
    public string Determinism { get; set; } = "deterministic";

    /// <summary>
    /// Aggregated persistence (highest level wins).
    /// </summary>
    public string Persistence { get; set; } = "ephemeral";
}

/// <summary>
/// Scope section for molecules.
/// </summary>
public sealed class MoleculeScopeSection
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
    /// This molecule's unique name within coordinator.
    /// </summary>
    public string Molecule { get; set; } = string.Empty;
}

/// <summary>
/// Reference to a constituent atom.
/// </summary>
public sealed class AtomReference
{
    /// <summary>
    /// Atom name (for inline definition or reference).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Path to atom manifest file (if external).
    /// </summary>
    public string? Manifest { get; set; }

    /// <summary>
    /// Whether molecule fails if this atom fails.
    /// </summary>
    public bool Required { get; set; } = true;

    /// <summary>
    /// Salience threshold for signals from this atom to be considered.
    /// </summary>
    public double? SalienceThreshold { get; set; }

    /// <summary>
    /// Override configuration for this atom instance.
    /// </summary>
    public Dictionary<string, object>? ConfigOverrides { get; set; }
}

/// <summary>
/// Aggregate signal emissions for molecules.
/// </summary>
public sealed class MoleculeEmitsSection
{
    /// <summary>
    /// Signals emitted on molecule completion (aggregated from atoms).
    /// </summary>
    public List<MoleculeSignalDefinition> OnComplete { get; set; } = new();

    /// <summary>
    /// Signals emitted on molecule failure.
    /// </summary>
    public List<SignalDefinition> OnFailure { get; set; } = new();
}

/// <summary>
/// A signal definition with source atom reference.
/// </summary>
public sealed class MoleculeSignalDefinition : SignalDefinition
{
    /// <summary>
    /// Source atom that produces this signal.
    /// </summary>
    public string? SourceAtom { get; set; }

    /// <summary>
    /// Minimum salience required to include in molecule output.
    /// </summary>
    public double? MinSalience { get; set; }
}

/// <summary>
/// Molecule-level escalation rule.
/// </summary>
public sealed class MoleculeEscalationRule
{
    /// <summary>
    /// Conditions that trigger escalation.
    /// </summary>
    public List<SignalCondition> When { get; set; } = new();

    /// <summary>
    /// Escalation target.
    /// </summary>
    public string? To { get; set; }

    /// <summary>
    /// Salience threshold for escalation.
    /// </summary>
    public double? SalienceThreshold { get; set; }

    /// <summary>
    /// Description of escalation purpose.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Execution semantics for molecule.
/// </summary>
public sealed class ExecutionSection
{
    /// <summary>
    /// Execution mode: sequential, parallel, or pipeline.
    /// </summary>
    public string Mode { get; set; } = "sequential";

    /// <summary>
    /// Stop on first required atom failure.
    /// </summary>
    public bool FailFast { get; set; } = true;

    /// <summary>
    /// Molecule-level timeout (TimeSpan string).
    /// </summary>
    public string? Timeout { get; set; }

    /// <summary>
    /// Maximum concurrent atom executions (for parallel mode).
    /// </summary>
    public int? MaxConcurrency { get; set; }
}
