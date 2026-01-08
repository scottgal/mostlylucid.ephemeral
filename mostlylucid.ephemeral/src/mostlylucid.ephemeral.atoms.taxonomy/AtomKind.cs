namespace Mostlylucid.Ephemeral.Atoms.Taxonomy;

/// <summary>
///     Extensible taxonomy kind registry for atoms.
/// </summary>
public sealed class AtomKind : IEquatable<AtomKind>
{
    private static readonly Dictionary<string, AtomKind> Registry = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Sync = new();

    private AtomKind(string id, string name, string? description)
    {
        Id = id;
        Name = name;
        Description = description;
    }

    public static AtomKind Sensor { get; } = Register("sensor", "Sensor");
    public static AtomKind Extractor { get; } = Register("extractor", "Extractor");
    public static AtomKind Embedder { get; } = Register("embedder", "Embedder");
    public static AtomKind Retriever { get; } = Register("retriever", "Retriever");
    public static AtomKind Proposer { get; } = Register("proposer", "Proposer");
    public static AtomKind Constrainer { get; } = Register("constrainer", "Constrainer");
    public static AtomKind Ranker { get; } = Register("ranker", "Ranker");
    public static AtomKind Renderer { get; } = Register("renderer", "Renderer");
    public static AtomKind Coordinator { get; } = Register("coordinator", "Coordinator");
    public static AtomKind Feedback { get; } = Register("feedback", "Feedback");
    public static AtomKind Escalator { get; } = Register("escalator", "Escalator");
    public static AtomKind Guard { get; } = Register("guard", "Guard");

    /// <summary>
    ///     Stable identifier for the kind (lowercase slug).
    /// </summary>
    public string Id { get; }

    /// <summary>
    ///     Display name for the kind.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     Optional description for the kind.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    ///     Slug used for output signals.
    /// </summary>
    public string Slug => Id;

    /// <summary>
    ///     Returns a snapshot of all registered kinds.
    /// </summary>
    public static IReadOnlyCollection<AtomKind> Registered
    {
        get
        {
            lock (Sync)
            {
                return new List<AtomKind>(Registry.Values);
            }
        }
    }

    public bool Equals(AtomKind? other)
    {
        if (ReferenceEquals(this, other))
            return true;
        if (other is null)
            return false;

        return string.Equals(Id, other.Id, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Registers (or returns) a kind by id.
    /// </summary>
    /// <param name="id">Stable identifier for the kind.</param>
    /// <param name="name">Optional display name override.</param>
    /// <param name="description">Optional description for the kind.</param>
    public static AtomKind Register(string id, string? name = null, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Kind id cannot be empty.", nameof(id));

        var normalized = NormalizeId(id);
        var resolvedName = string.IsNullOrWhiteSpace(name) ? ToDisplayName(id) : name;

        lock (Sync)
        {
            if (Registry.TryGetValue(normalized, out var existing))
                return existing;

            var kind = new AtomKind(normalized, resolvedName, description);
            Registry[normalized] = kind;
            return kind;
        }
    }

    /// <summary>
    ///     Returns the registered kind or registers a new one.
    /// </summary>
    public static AtomKind From(string id)
    {
        return Register(id);
    }

    /// <summary>
    ///     Attempts to fetch a registered kind.
    /// </summary>
    public static bool TryGet(string id, out AtomKind? kind)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            kind = null;
            return false;
        }

        var normalized = NormalizeId(id);
        lock (Sync)
        {
            return Registry.TryGetValue(normalized, out kind);
        }
    }

    public override bool Equals(object? obj)
    {
        return obj is AtomKind other && Equals(other);
    }

    public override int GetHashCode()
    {
        return StringComparer.OrdinalIgnoreCase.GetHashCode(Id);
    }

    public override string ToString()
    {
        return Name;
    }

    public static bool operator ==(AtomKind? left, AtomKind? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(AtomKind? left, AtomKind? right)
    {
        return !Equals(left, right);
    }

    private static string NormalizeId(string id)
    {
        return id.Trim().ToLowerInvariant();
    }

    private static string ToDisplayName(string id)
    {
        var parts = id.Trim().Split(new[] { '-', '_', '.', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return id.Trim();

        var buffer = new char[id.Length];
        var index = 0;

        foreach (var part in parts)
        {
            if (part.Length == 0)
                continue;

            buffer[index++] = char.ToUpperInvariant(part[0]);
            for (var i = 1; i < part.Length; i++)
                buffer[index++] = part[i];
        }

        return new string(buffer, 0, index);
    }
}