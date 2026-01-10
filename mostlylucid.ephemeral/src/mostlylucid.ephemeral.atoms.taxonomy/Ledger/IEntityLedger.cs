namespace Mostlylucid.Ephemeral.Atoms.Taxonomy.Ledger;

/// <summary>
///     A ledger that accumulates signals for an entity (image, document, request, data row).
///     This is the universal signal accumulator that enables cross-project atom portability.
/// </summary>
/// <remarks>
///     Key principles:
///     - One ledger per entity being processed
///     - Atoms write signals to the ledger
///     - Atoms read signals from the ledger (to check dependencies)
///     - Ledger lifetime = entity processing lifetime
///     - High-salience signals can be escalated to RAG storage
/// </remarks>
public interface IEntityLedger : IDisposable
{
    /// <summary>
    ///     Unique identifier for the entity this ledger tracks.
    /// </summary>
    string EntityId { get; }

    /// <summary>
    ///     Entity type (e.g., "image", "document", "request", "row").
    /// </summary>
    string EntityType { get; }

    /// <summary>
    ///     When this ledger was created.
    /// </summary>
    DateTimeOffset CreatedAt { get; }

    /// <summary>
    ///     Total number of signals in the ledger.
    /// </summary>
    int Count { get; }

    /// <summary>
    ///     Records a signal from an atom.
    /// </summary>
    /// <param name="signal">The signal to record.</param>
    void Record(LedgerSignal signal);

    /// <summary>
    ///     Records a signal with explicit parameters.
    /// </summary>
    void Record(
        string key,
        object? value,
        double salience,
        string sourceAtom,
        string? sourceKind = null,
        IDictionary<string, object>? metadata = null);

    /// <summary>
    ///     Records a signal with a reference to externally stored data (avoids boxing).
    ///     Preferred for large data - store externally and pass the storage key/location.
    /// </summary>
    void RecordRef(
        string key,
        string? valueRef,
        double salience,
        string sourceAtom,
        string? sourceKind = null,
        IDictionary<string, string>? metadata = null);

    /// <summary>
    ///     Checks if a signal exists in the ledger.
    /// </summary>
    bool HasSignal(string key);

    /// <summary>
    ///     Checks if a signal exists and matches a condition.
    /// </summary>
    bool HasSignal(string key, Func<LedgerSignal, bool> predicate);

    /// <summary>
    ///     Gets a signal by key.
    /// </summary>
    LedgerSignal? GetSignal(string key);

    /// <summary>
    ///     Gets the value of a signal, cast to the expected type.
    /// </summary>
    T? GetValue<T>(string key);

    /// <summary>
    ///     Gets all signals matching a pattern (glob-style: *, ?).
    /// </summary>
    IReadOnlyList<LedgerSignal> GetSignals(string pattern);

    /// <summary>
    ///     Gets all signals from a specific atom.
    /// </summary>
    IReadOnlyList<LedgerSignal> GetSignalsFromAtom(string atomName);

    /// <summary>
    ///     Gets all signals with salience above a threshold.
    /// </summary>
    IReadOnlyList<LedgerSignal> GetHighSalienceSignals(double threshold);

    /// <summary>
    ///     Gets all signals in the ledger.
    /// </summary>
    IReadOnlyList<LedgerSignal> GetAllSignals();

    /// <summary>
    ///     Creates a view over this ledger with optional filtering.
    /// </summary>
    ILedgerView CreateView(LedgerViewOptions? options = null);
}

/// <summary>
///     A signal stored in an entity ledger.
/// </summary>
/// <remarks>
///     Best practice: Store large data externally (cache, persistence, shared storage).
///     Use ValueRef to point to storage location. Ledger signals are coordination, not transport.
///     Example: After processing image → RecordRef("image.processed", valueRef: "cache://processed/abc123")
/// </remarks>
public sealed class LedgerSignal
{
    /// <summary>
    ///     Signal key (e.g., "identity.format", "detection.confidence").
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    ///     Signal value (can be any type). May cause boxing for value types.
    ///     Consider using ValueRef for large data to avoid boxing and keep signals lightweight.
    /// </summary>
    public object? Value { get; init; }

    /// <summary>
    ///     Reference to stored value (e.g., storage key, URI, identifier).
    ///     Preferred over Value for large data - avoids boxing, keeps signals lightweight.
    ///     Example: "cache://processed/abc123" or "blob://container/image.jpg"
    /// </summary>
    public string? ValueRef { get; init; }

    /// <summary>
    ///     Salience score (0.0 to 1.0). Higher = more important.
    /// </summary>
    public double Salience { get; init; } = 0.5;

    /// <summary>
    ///     Name of the atom that produced this signal.
    /// </summary>
    public required string SourceAtom { get; init; }

    /// <summary>
    ///     Kind of the source atom (sensor, extractor, proposer, etc.).
    /// </summary>
    public string? SourceKind { get; init; }

    /// <summary>
    ///     When this signal was recorded.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    ///     Additional metadata about the signal.
    /// </summary>
    public IDictionary<string, object>? Metadata { get; init; }

    /// <summary>
    ///     Gets the value cast to the expected type.
    /// </summary>
    public T? GetValue<T>()
    {
        if (Value is null)
            return default;

        if (Value is T typed)
            return typed;

        try
        {
            return (T)Convert.ChangeType(Value, typeof(T));
        }
        catch
        {
            return default;
        }
    }
}

/// <summary>
///     Options for creating a ledger view.
/// </summary>
public sealed class LedgerViewOptions
{
    /// <summary>
    ///     Pattern filter for signal keys (glob-style).
    /// </summary>
    public string? Pattern { get; init; }

    /// <summary>
    ///     Minimum salience threshold for signals in this view.
    /// </summary>
    public double? SalienceThreshold { get; init; }

    /// <summary>
    ///     Only include signals from these atoms.
    /// </summary>
    public IReadOnlyList<string>? SourceAtoms { get; init; }

    /// <summary>
    ///     Only include signals of these kinds.
    /// </summary>
    public IReadOnlyList<string>? SourceKinds { get; init; }

    /// <summary>
    ///     Maximum number of signals in the view.
    /// </summary>
    public int? MaxSignals { get; init; }

    /// <summary>
    ///     Only include signals newer than this.
    /// </summary>
    public DateTimeOffset? Since { get; init; }
}

/// <summary>
///     A filtered view over an entity ledger.
/// </summary>
public interface ILedgerView
{
    /// <summary>
    ///     The underlying ledger.
    /// </summary>
    IEntityLedger Ledger { get; }

    /// <summary>
    ///     View options.
    /// </summary>
    LedgerViewOptions Options { get; }

    /// <summary>
    ///     Number of signals visible in this view.
    /// </summary>
    int Count { get; }

    /// <summary>
    ///     Gets all signals matching the view filter.
    /// </summary>
    IReadOnlyList<LedgerSignal> GetSignals();

    /// <summary>
    ///     Checks if a signal exists in this view.
    /// </summary>
    bool HasSignal(string key);

    /// <summary>
    ///     Gets a signal by key (if it passes the view filter).
    /// </summary>
    LedgerSignal? GetSignal(string key);
}

/// <summary>
///     Factory for creating entity ledgers.
/// </summary>
public interface IEntityLedgerFactory
{
    /// <summary>
    ///     Creates a new ledger for an entity.
    /// </summary>
    IEntityLedger Create(string entityId, string entityType);

    /// <summary>
    ///     Creates a ledger with a generated ID.
    /// </summary>
    IEntityLedger Create(string entityType);
}

/// <summary>
///     Extension point for escalating ledger signals to persistent storage.
/// </summary>
public interface ILedgerEscalator
{
    /// <summary>
    ///     Escalates high-salience signals to persistent storage.
    /// </summary>
    /// <param name="ledger">The ledger to escalate from.</param>
    /// <param name="salienceThreshold">Minimum salience for escalation.</param>
    /// <param name="target">Escalation target (e.g., "rag", "learning", "audit").</param>
    /// <param name="ct">Cancellation token.</param>
    Task EscalateAsync(
        IEntityLedger ledger,
        double salienceThreshold,
        string target,
        CancellationToken ct = default);

    /// <summary>
    ///     Escalates specific signals to persistent storage.
    /// </summary>
    Task EscalateAsync(
        IEntityLedger ledger,
        IEnumerable<LedgerSignal> signals,
        string target,
        CancellationToken ct = default);
}