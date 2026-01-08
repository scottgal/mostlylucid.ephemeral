namespace Mostlylucid.Ephemeral.Atoms.Taxonomy;

/// <summary>
///     Value descriptor for composing multiple taxonomy kinds into a single contract.
/// </summary>
public readonly record struct TaxonomyShard
{
    /// <summary>
    ///     Initializes a taxonomy shard descriptor.
    /// </summary>
    /// <param name="kind">The taxonomy kind represented by the shard.</param>
    /// <param name="determinism">The determinism classification.</param>
    /// <param name="persistence">The persistence authority.</param>
    /// <param name="defaultOutputSignal">Default output signal for this shard.</param>
    /// <param name="reads">Optional read domains implied by this shard.</param>
    /// <param name="writes">Optional write domains implied by this shard.</param>
    /// <param name="configs">Optional config signal domains for runtime configuration.</param>
    /// <param name="budget">Optional budget constraints implied by this shard.</param>
    /// <param name="evidenceRequirements">Optional evidence requirements implied by this shard.</param>
    public TaxonomyShard(
        AtomKind kind,
        AtomDeterminism determinism,
        AtomPersistence persistence,
        string defaultOutputSignal,
        IReadOnlyCollection<string>? reads = null,
        IReadOnlyCollection<string>? writes = null,
        IReadOnlyCollection<string>? configs = null,
        AtomBudget? budget = null,
        string? evidenceRequirements = null)
    {
        if (string.IsNullOrWhiteSpace(defaultOutputSignal))
            throw new ArgumentException("Default output signal cannot be empty.", nameof(defaultOutputSignal));

        Kind = kind;
        Determinism = determinism;
        Persistence = persistence;
        DefaultOutputSignal = defaultOutputSignal;
        Reads = reads ?? Array.Empty<string>();
        Writes = writes ?? Array.Empty<string>();
        Configs = configs ?? Array.Empty<string>();
        Budget = budget;
        EvidenceRequirements = evidenceRequirements;
    }

    /// <summary>
    ///     The taxonomy kind represented by the shard.
    /// </summary>
    public AtomKind Kind { get; }

    /// <summary>
    ///     The determinism classification for the shard.
    /// </summary>
    public AtomDeterminism Determinism { get; }

    /// <summary>
    ///     The persistence authority implied by the shard.
    /// </summary>
    public AtomPersistence Persistence { get; }

    /// <summary>
    ///     Default output signal for this shard.
    /// </summary>
    public string DefaultOutputSignal { get; }

    /// <summary>
    ///     Optional read domains implied by this shard.
    /// </summary>
    public IReadOnlyCollection<string> Reads { get; }

    /// <summary>
    ///     Optional write domains implied by this shard.
    /// </summary>
    public IReadOnlyCollection<string> Writes { get; }

    /// <summary>
    ///     Optional config signal domains for runtime configuration.
    /// </summary>
    public IReadOnlyCollection<string> Configs { get; }

    /// <summary>
    ///     Optional budget constraints implied by this shard.
    /// </summary>
    public AtomBudget? Budget { get; }

    /// <summary>
    ///     Optional evidence requirements implied by this shard.
    /// </summary>
    public string? EvidenceRequirements { get; }

    /// <summary>
    ///     Creates a taxonomy shard descriptor from a static shard definition.
    /// </summary>
    /// <typeparam name="TShard">The shard type implementing <see cref="ITaxonomyShard" />.</typeparam>
    public static TaxonomyShard Create<TShard>() where TShard : ITaxonomyShard
    {
        return new TaxonomyShard(
            TShard.Kind,
            TShard.Determinism,
            TShard.Persistence,
            TShard.DefaultOutputSignal,
            TShard.Reads,
            TShard.Writes,
            TShard.Configs,
            TShard.Budget,
            TShard.EvidenceRequirements);
    }
}