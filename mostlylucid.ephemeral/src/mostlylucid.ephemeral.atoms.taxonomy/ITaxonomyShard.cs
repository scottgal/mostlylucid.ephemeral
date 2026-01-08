namespace Mostlylucid.Ephemeral.Atoms.Taxonomy;

/// <summary>
///     Defines static metadata for a taxonomy shard used in multi-kind composition.
/// </summary>
public interface ITaxonomyShard
{
    /// <summary>
    ///     The taxonomy kind represented by this shard.
    /// </summary>
    static abstract AtomKind Kind { get; }

    /// <summary>
    ///     The determinism classification for this shard.
    /// </summary>
    static abstract AtomDeterminism Determinism { get; }

    /// <summary>
    ///     The persistence authority implied by this shard.
    /// </summary>
    static abstract AtomPersistence Persistence { get; }

    /// <summary>
    ///     The default output signal for this shard.
    /// </summary>
    static abstract string DefaultOutputSignal { get; }

    /// <summary>
    ///     Optional read domains implied by this shard.
    /// </summary>
    static virtual IReadOnlyCollection<string> Reads => Array.Empty<string>();

    /// <summary>
    ///     Optional write domains implied by this shard.
    /// </summary>
    static virtual IReadOnlyCollection<string> Writes => Array.Empty<string>();

    /// <summary>
    ///     Optional config signal domains for runtime configuration.
    /// </summary>
    static virtual IReadOnlyCollection<string> Configs => Array.Empty<string>();

    /// <summary>
    ///     Optional budget constraints implied by this shard.
    /// </summary>
    static virtual AtomBudget? Budget => null;

    /// <summary>
    ///     Optional evidence requirements implied by this shard.
    /// </summary>
    static virtual string? EvidenceRequirements => null;
}