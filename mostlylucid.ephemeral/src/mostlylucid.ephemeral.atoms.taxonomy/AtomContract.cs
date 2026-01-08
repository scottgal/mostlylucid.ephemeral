namespace Mostlylucid.Ephemeral.Atoms.Taxonomy;

/// <summary>
///     Captures the execution and persistence contract for an atom.
/// </summary>
public sealed class AtomContract
{
    /// <summary>
    ///     Initializes a contract with explicit metadata.
    /// </summary>
    /// <param name="name">Human-friendly name for the atom.</param>
    /// <param name="kind">Taxonomy kind for the atom.</param>
    /// <param name="determinism">Determinism classification for outputs.</param>
    /// <param name="persistence">Persistence authority for outputs.</param>
    /// <param name="reads">Optional list of read domains.</param>
    /// <param name="writes">Optional list of write domains.</param>
    /// <param name="configs">Optional list of config signal domains (for dynamic configuration).</param>
    /// <param name="budget">Optional time, token, or cost limits.</param>
    /// <param name="evidenceRequirements">Optional evidence requirements for outputs.</param>
    /// <param name="kinds">Optional explicit kinds (defaults to the primary kind).</param>
    public AtomContract(
        string name,
        AtomKind kind,
        AtomDeterminism determinism,
        AtomPersistence persistence,
        IReadOnlyCollection<string>? reads = null,
        IReadOnlyCollection<string>? writes = null,
        IReadOnlyCollection<string>? configs = null,
        AtomBudget? budget = null,
        string? evidenceRequirements = null,
        IReadOnlyCollection<AtomKind>? kinds = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Atom name cannot be empty.", nameof(name));

        Name = name;
        Kind = kind;
        Determinism = determinism;
        Persistence = persistence;
        Reads = reads ?? Array.Empty<string>();
        Writes = writes ?? Array.Empty<string>();
        Configs = configs ?? Array.Empty<string>();
        Budget = budget;
        EvidenceRequirements = evidenceRequirements;
        Kinds = NormalizeKinds(kind, kinds);
    }

    /// <summary>
    ///     The human-friendly name for this atom.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     The taxonomy kind for this atom.
    /// </summary>
    public AtomKind Kind { get; }

    /// <summary>
    ///     The taxonomy kinds represented by this atom, including the primary kind.
    /// </summary>
    public IReadOnlyCollection<AtomKind> Kinds { get; }

    /// <summary>
    ///     Determinism classification for outputs.
    /// </summary>
    public AtomDeterminism Determinism { get; }

    /// <summary>
    ///     Persistence authority for outputs.
    /// </summary>
    public AtomPersistence Persistence { get; }

    /// <summary>
    ///     Optional list of read domains.
    /// </summary>
    public IReadOnlyCollection<string> Reads { get; }

    /// <summary>
    ///     Optional list of write domains.
    /// </summary>
    public IReadOnlyCollection<string> Writes { get; }

    /// <summary>
    ///     Optional list of config signal domains for dynamic configuration.
    ///     Config signals allow real-time configuration changes to be routed like data signals.
    /// </summary>
    public IReadOnlyCollection<string> Configs { get; }

    /// <summary>
    ///     Optional time, token, or cost limits.
    /// </summary>
    public AtomBudget? Budget { get; }

    /// <summary>
    ///     Optional evidence requirements for outputs.
    /// </summary>
    public string? EvidenceRequirements { get; }

    /// <summary>
    ///     Creates a contract with defaults and a computed name.
    /// </summary>
    /// <param name="kind">Taxonomy kind for the atom.</param>
    /// <param name="determinism">Determinism classification for outputs.</param>
    /// <param name="persistence">Persistence authority for outputs.</param>
    /// <param name="name">Optional name override.</param>
    /// <param name="reads">Optional list of read domains.</param>
    /// <param name="writes">Optional list of write domains.</param>
    /// <param name="configs">Optional list of config signal domains for runtime configuration.</param>
    /// <param name="budget">Optional time, token, or cost limits.</param>
    /// <param name="evidenceRequirements">Optional evidence requirements for outputs.</param>
    /// <param name="kinds">Optional explicit kinds (defaults to the primary kind).</param>
    public static AtomContract Create(
        AtomKind kind,
        AtomDeterminism determinism,
        AtomPersistence persistence,
        string? name = null,
        IReadOnlyCollection<string>? reads = null,
        IReadOnlyCollection<string>? writes = null,
        IReadOnlyCollection<string>? configs = null,
        AtomBudget? budget = null,
        string? evidenceRequirements = null,
        IReadOnlyCollection<AtomKind>? kinds = null)
    {
        var resolvedName = string.IsNullOrWhiteSpace(name) ? $"{kind}Atom" : name;
        return new AtomContract(
            resolvedName,
            kind,
            determinism,
            persistence,
            reads,
            writes,
            configs,
            budget,
            evidenceRequirements,
            kinds);
    }

    /// <summary>
    ///     Creates a composite contract from multiple taxonomy shards.
    /// </summary>
    /// <param name="shards">The shard descriptors to compose.</param>
    /// <param name="name">Optional name override.</param>
    /// <param name="reads">Optional read domains to append.</param>
    /// <param name="writes">Optional write domains to append.</param>
    /// <param name="configs">Optional config signal domains for runtime configuration.</param>
    /// <param name="budget">Optional budget override.</param>
    /// <param name="evidenceRequirements">Optional evidence requirement override.</param>
    public static AtomContract Compose(
        IReadOnlyCollection<TaxonomyShard> shards,
        string? name = null,
        IReadOnlyCollection<string>? reads = null,
        IReadOnlyCollection<string>? writes = null,
        IReadOnlyCollection<string>? configs = null,
        AtomBudget? budget = null,
        string? evidenceRequirements = null)
    {
        if (shards is null)
            throw new ArgumentNullException(nameof(shards));
        if (shards.Count == 0)
            throw new ArgumentException("At least one shard is required.", nameof(shards));

        var shardList = new List<TaxonomyShard>(shards.Count);
        foreach (var shard in shards)
            shardList.Add(shard);

        var primary = shardList[0];
        var determinism = CombineDeterminism(shardList);
        var persistence = CombinePersistence(shardList);
        var mergedReads = MergeStrings(shardList, reads, shard => shard.Reads);
        var mergedWrites = MergeStrings(shardList, writes, shard => shard.Writes);
        var mergedConfigs = MergeStrings(shardList, configs, shard => shard.Configs);
        var mergedBudget = budget ?? MergeBudgets(shardList);
        var mergedEvidence = evidenceRequirements ?? MergeEvidenceRequirements(shardList);

        var kinds = new List<AtomKind>(shardList.Count);
        foreach (var shard in shardList)
            kinds.Add(shard.Kind);

        var resolvedName = string.IsNullOrWhiteSpace(name) ? $"{primary.Kind}Atom" : name;
        return new AtomContract(
            resolvedName,
            primary.Kind,
            determinism,
            persistence,
            mergedReads,
            mergedWrites,
            mergedConfigs,
            mergedBudget,
            mergedEvidence,
            kinds);
    }

    private static IReadOnlyCollection<AtomKind> NormalizeKinds(
        AtomKind primary,
        IReadOnlyCollection<AtomKind>? kinds)
    {
        if (kinds is null || kinds.Count == 0)
            return new[] { primary };

        var normalized = new List<AtomKind>(kinds.Count + 1);
        foreach (var kind in kinds)
            if (!normalized.Contains(kind))
                normalized.Add(kind);

        if (!normalized.Contains(primary))
        {
            normalized.Insert(0, primary);
        }
        else if (normalized[0] != primary)
        {
            normalized.Remove(primary);
            normalized.Insert(0, primary);
        }

        return normalized;
    }

    private static AtomDeterminism CombineDeterminism(IEnumerable<TaxonomyShard> shards)
    {
        foreach (var shard in shards)
            if (shard.Determinism == AtomDeterminism.Probabilistic)
                return AtomDeterminism.Probabilistic;

        return AtomDeterminism.Deterministic;
    }

    private static AtomPersistence CombinePersistence(IEnumerable<TaxonomyShard> shards)
    {
        var resolved = AtomPersistence.EphemeralOnly;

        foreach (var shard in shards)
            if (shard.Persistence > resolved)
                resolved = shard.Persistence;

        return resolved;
    }

    private static IReadOnlyCollection<string> MergeStrings(
        IEnumerable<TaxonomyShard> shards,
        IReadOnlyCollection<string>? extra,
        Func<TaxonomyShard, IReadOnlyCollection<string>> selector)
    {
        var merged = new HashSet<string>(StringComparer.Ordinal);

        foreach (var shard in shards)
        foreach (var value in selector(shard))
            merged.Add(value);

        if (extra is not null)
            foreach (var value in extra)
                merged.Add(value);

        return merged.Count == 0 ? Array.Empty<string>() : new List<string>(merged);
    }

    private static AtomBudget? MergeBudgets(IEnumerable<TaxonomyShard> shards)
    {
        AtomBudget? merged = null;

        foreach (var shard in shards)
        {
            if (shard.Budget is null)
                continue;

            merged = merged is null
                ? shard.Budget
                : new AtomBudget(
                    MinNullable(merged.MaxDuration, shard.Budget.MaxDuration),
                    MinNullable(merged.MaxTokens, shard.Budget.MaxTokens),
                    MinNullable(merged.MaxCost, shard.Budget.MaxCost));
        }

        return merged;
    }

    private static string? MergeEvidenceRequirements(IEnumerable<TaxonomyShard> shards)
    {
        var merged = new HashSet<string>(StringComparer.Ordinal);

        foreach (var shard in shards)
        {
            if (string.IsNullOrWhiteSpace(shard.EvidenceRequirements))
                continue;

            merged.Add(shard.EvidenceRequirements);
        }

        if (merged.Count == 0)
            return null;

        if (merged.Count == 1)
            foreach (var value in merged)
                return value;

        return string.Join(" | ", merged);
    }

    private static TimeSpan? MinNullable(TimeSpan? left, TimeSpan? right)
    {
        if (left is null)
            return right;
        if (right is null)
            return left;

        return left <= right ? left : right;
    }

    private static int? MinNullable(int? left, int? right)
    {
        if (left is null)
            return right;
        if (right is null)
            return left;

        return left <= right ? left : right;
    }

    private static decimal? MinNullable(decimal? left, decimal? right)
    {
        if (left is null)
            return right;
        if (right is null)
            return left;

        return left <= right ? left : right;
    }
}