namespace Mostlylucid.Ephemeral.Atoms.Taxonomy.Ledger;

/// <summary>
///     Persistent storage for entity ledgers.
///     Ledgers are stored in BOTH relational (signals) and vector (embeddings) stores, linked by entity ID.
/// </summary>
public interface ILedgerStore
{
    /// <summary>
    ///     Saves a ledger to persistent storage.
    /// </summary>
    Task SaveAsync(IEntityLedger ledger, CancellationToken ct = default);

    /// <summary>
    ///     Saves a ledger with specific escalation options.
    /// </summary>
    Task SaveAsync(IEntityLedger ledger, LedgerSaveOptions options, CancellationToken ct = default);

    /// <summary>
    ///     Loads a ledger by entity ID.
    /// </summary>
    Task<IEntityLedger?> LoadAsync(string entityId, CancellationToken ct = default);

    /// <summary>
    ///     Checks if a ledger exists for an entity.
    /// </summary>
    Task<bool> ExistsAsync(string entityId, CancellationToken ct = default);

    /// <summary>
    ///     Deletes a ledger by entity ID.
    /// </summary>
    Task DeleteAsync(string entityId, CancellationToken ct = default);

    /// <summary>
    ///     Queries ledgers by signal pattern.
    /// </summary>
    Task<IReadOnlyList<EntityLedgerSummary>> QueryBySignalAsync(
        string signalPattern,
        LedgerQueryOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    ///     Queries ledgers by entity type.
    /// </summary>
    Task<IReadOnlyList<EntityLedgerSummary>> QueryByTypeAsync(
        string entityType,
        LedgerQueryOptions? options = null,
        CancellationToken ct = default);
}

/// <summary>
///     Vector storage for entity embeddings.
///     Each entity can have multiple vectors for different lenses/perspectives.
/// </summary>
public interface ILedgerVectorStore
{
    /// <summary>
    ///     Stores a vector for an entity under a specific lens.
    /// </summary>
    /// <param name="entityId">Entity identifier.</param>
    /// <param name="lens">Lens/perspective name (e.g., "text", "image", "clip", "signal").</param>
    /// <param name="vector">Embedding vector.</param>
    /// <param name="metadata">Optional metadata about the vector.</param>
    /// <param name="ct">Cancellation token.</param>
    Task StoreVectorAsync(
        string entityId,
        string lens,
        float[] vector,
        IDictionary<string, object>? metadata = null,
        CancellationToken ct = default);

    /// <summary>
    ///     Stores multiple vectors for an entity.
    /// </summary>
    Task StoreVectorsAsync(
        string entityId,
        IEnumerable<EntityVector> vectors,
        CancellationToken ct = default);

    /// <summary>
    ///     Gets all vectors for an entity.
    /// </summary>
    Task<IReadOnlyList<EntityVector>> GetVectorsAsync(
        string entityId,
        CancellationToken ct = default);

    /// <summary>
    ///     Gets a specific vector for an entity.
    /// </summary>
    Task<EntityVector?> GetVectorAsync(
        string entityId,
        string lens,
        CancellationToken ct = default);

    /// <summary>
    ///     Finds similar entities by vector similarity.
    /// </summary>
    /// <param name="vector">Query vector.</param>
    /// <param name="lens">Lens to search in.</param>
    /// <param name="topK">Number of results to return.</param>
    /// <param name="threshold">Minimum similarity threshold (0.0-1.0).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        float[] vector,
        string lens,
        int topK = 10,
        double threshold = 0.0,
        CancellationToken ct = default);

    /// <summary>
    ///     Finds similar entities by entity ID (using its stored vector).
    /// </summary>
    Task<IReadOnlyList<VectorSearchResult>> SearchByEntityAsync(
        string entityId,
        string lens,
        int topK = 10,
        double threshold = 0.0,
        CancellationToken ct = default);

    /// <summary>
    ///     Deletes all vectors for an entity.
    /// </summary>
    Task DeleteAsync(string entityId, CancellationToken ct = default);

    /// <summary>
    ///     Deletes a specific vector for an entity.
    /// </summary>
    Task DeleteAsync(string entityId, string lens, CancellationToken ct = default);
}

/// <summary>
///     An embedding vector for an entity.
/// </summary>
public sealed class EntityVector
{
    /// <summary>
    ///     Entity identifier.
    /// </summary>
    public required string EntityId { get; init; }

    /// <summary>
    ///     Lens/perspective name (e.g., "text", "image", "clip", "signal").
    /// </summary>
    public required string Lens { get; init; }

    /// <summary>
    ///     Embedding vector.
    /// </summary>
    public required float[] Vector { get; init; }

    /// <summary>
    ///     When this vector was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    ///     Model/algorithm used to generate this vector.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    ///     Additional metadata about the vector.
    /// </summary>
    public IDictionary<string, object>? Metadata { get; init; }
}

/// <summary>
///     Result from vector similarity search.
/// </summary>
public sealed class VectorSearchResult
{
    /// <summary>
    ///     Entity identifier.
    /// </summary>
    public required string EntityId { get; init; }

    /// <summary>
    ///     Lens that was searched.
    /// </summary>
    public required string Lens { get; init; }

    /// <summary>
    ///     Similarity score (0.0-1.0, higher = more similar).
    /// </summary>
    public required double Similarity { get; init; }

    /// <summary>
    ///     Vector metadata.
    /// </summary>
    public IDictionary<string, object>? Metadata { get; init; }
}

/// <summary>
///     Options for saving a ledger.
/// </summary>
public sealed class LedgerSaveOptions
{
    /// <summary>
    ///     Minimum salience threshold for signals to persist.
    ///     Signals below this threshold are not saved.
    /// </summary>
    public double SalienceThreshold { get; init; } = 0.0;

    /// <summary>
    ///     Whether to overwrite existing ledger or merge.
    /// </summary>
    public bool Overwrite { get; init; } = false;

    /// <summary>
    ///     Vectors to store alongside the ledger.
    /// </summary>
    public IReadOnlyList<EntityVector>? Vectors { get; init; }

    /// <summary>
    ///     Additional metadata to store with the ledger.
    /// </summary>
    public IDictionary<string, object>? Metadata { get; init; }
}

/// <summary>
///     Options for querying ledgers.
/// </summary>
public sealed class LedgerQueryOptions
{
    /// <summary>
    ///     Maximum number of results.
    /// </summary>
    public int Limit { get; init; } = 100;

    /// <summary>
    ///     Skip this many results (for pagination).
    /// </summary>
    public int Offset { get; init; } = 0;

    /// <summary>
    ///     Only return ledgers created after this time.
    /// </summary>
    public DateTimeOffset? Since { get; init; }

    /// <summary>
    ///     Only return ledgers created before this time.
    /// </summary>
    public DateTimeOffset? Until { get; init; }

    /// <summary>
    ///     Minimum signal salience in the ledger.
    /// </summary>
    public double? MinSalience { get; init; }

    /// <summary>
    ///     Order by field.
    /// </summary>
    public string OrderBy { get; init; } = "created_at";

    /// <summary>
    ///     Order direction (asc/desc).
    /// </summary>
    public bool Descending { get; init; } = true;
}

/// <summary>
///     Summary of an entity ledger (without full signal data).
/// </summary>
public sealed class EntityLedgerSummary
{
    /// <summary>
    ///     Entity identifier.
    /// </summary>
    public required string EntityId { get; init; }

    /// <summary>
    ///     Entity type.
    /// </summary>
    public required string EntityType { get; init; }

    /// <summary>
    ///     When the ledger was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    ///     Number of signals in the ledger.
    /// </summary>
    public int SignalCount { get; init; }

    /// <summary>
    ///     Maximum salience of any signal.
    /// </summary>
    public double MaxSalience { get; init; }

    /// <summary>
    ///     Average salience of signals.
    /// </summary>
    public double AvgSalience { get; init; }

    /// <summary>
    ///     Available vector lenses.
    /// </summary>
    public IReadOnlyList<string> VectorLenses { get; init; } = Array.Empty<string>();

    /// <summary>
    ///     Additional metadata.
    /// </summary>
    public IDictionary<string, object>? Metadata { get; init; }
}

/// <summary>
///     Standard vector lens names.
/// </summary>
public static class VectorLens
{
    /// <summary>
    ///     Text content embedding (e.g., from document text, OCR).
    /// </summary>
    public const string Text = "text";

    /// <summary>
    ///     Image embedding (e.g., from CLIP, ResNet).
    /// </summary>
    public const string Image = "image";

    /// <summary>
    ///     CLIP embedding (multi-modal text+image).
    /// </summary>
    public const string Clip = "clip";

    /// <summary>
    ///     Signal pattern embedding (from accumulated signals).
    /// </summary>
    public const string Signal = "signal";

    /// <summary>
    ///     Behavior pattern embedding (for bot detection).
    /// </summary>
    public const string Behavior = "behavior";

    /// <summary>
    ///     Caption/description embedding.
    /// </summary>
    public const string Caption = "caption";

    /// <summary>
    ///     Semantic content embedding.
    /// </summary>
    public const string Semantic = "semantic";

    /// <summary>
    ///     Audio embedding (for audio/video content).
    /// </summary>
    public const string Audio = "audio";
}