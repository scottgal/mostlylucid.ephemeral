namespace Mostlylucid.Ephemeral.Atoms.Taxonomy;

/// <summary>
///     Shard metadata for sensor atoms.
/// </summary>
public sealed class SensorShard : ITaxonomyShard
{
    public static AtomKind Kind => AtomKind.Sensor;
    public static AtomDeterminism Determinism => AtomDeterminism.Deterministic;
    public static AtomPersistence Persistence => AtomPersistence.PersistableViaEscalation;
    public static string DefaultOutputSignal => "atom.sensor.output";
}

/// <summary>
///     Shard metadata for extractor atoms.
/// </summary>
public sealed class ExtractorShard : ITaxonomyShard
{
    public static AtomKind Kind => AtomKind.Extractor;
    public static AtomDeterminism Determinism => AtomDeterminism.Deterministic;
    public static AtomPersistence Persistence => AtomPersistence.PersistableViaEscalation;
    public static string DefaultOutputSignal => "atom.extractor.output";
}

/// <summary>
///     Shard metadata for embedder atoms.
/// </summary>
public sealed class EmbedderShard : ITaxonomyShard
{
    public static AtomKind Kind => AtomKind.Embedder;
    public static AtomDeterminism Determinism => AtomDeterminism.Deterministic;
    public static AtomPersistence Persistence => AtomPersistence.PersistableViaEscalation;
    public static string DefaultOutputSignal => "atom.embedder.output";
}

/// <summary>
///     Shard metadata for retriever atoms.
/// </summary>
public sealed class RetrieverShard : ITaxonomyShard
{
    public static AtomKind Kind => AtomKind.Retriever;
    public static AtomDeterminism Determinism => AtomDeterminism.Deterministic;
    public static AtomPersistence Persistence => AtomPersistence.EphemeralOnly;
    public static string DefaultOutputSignal => "atom.retriever.output";
}

/// <summary>
///     Shard metadata for proposer atoms.
/// </summary>
public sealed class ProposerShard : ITaxonomyShard
{
    public static AtomKind Kind => AtomKind.Proposer;
    public static AtomDeterminism Determinism => AtomDeterminism.Probabilistic;
    public static AtomPersistence Persistence => AtomPersistence.PersistableViaEscalation;
    public static string DefaultOutputSignal => "atom.proposer.output";
}

/// <summary>
///     Shard metadata for constrainer atoms.
/// </summary>
public sealed class ConstrainerShard : ITaxonomyShard
{
    public static AtomKind Kind => AtomKind.Constrainer;
    public static AtomDeterminism Determinism => AtomDeterminism.Deterministic;
    public static AtomPersistence Persistence => AtomPersistence.PersistableViaEscalation;
    public static string DefaultOutputSignal => "atom.constrainer.output";
}

/// <summary>
///     Shard metadata for ranker atoms.
/// </summary>
public sealed class RankerShard : ITaxonomyShard
{
    public static AtomKind Kind => AtomKind.Ranker;
    public static AtomDeterminism Determinism => AtomDeterminism.Deterministic;
    public static AtomPersistence Persistence => AtomPersistence.EphemeralOnly;
    public static string DefaultOutputSignal => "atom.ranker.output";
}

/// <summary>
///     Shard metadata for renderer atoms.
/// </summary>
public sealed class RendererShard : ITaxonomyShard
{
    public static AtomKind Kind => AtomKind.Renderer;
    public static AtomDeterminism Determinism => AtomDeterminism.Deterministic;
    public static AtomPersistence Persistence => AtomPersistence.EphemeralOnly;
    public static string DefaultOutputSignal => "atom.renderer.output";
}

/// <summary>
///     Shard metadata for coordinator atoms.
/// </summary>
public sealed class CoordinatorShard : ITaxonomyShard
{
    public static AtomKind Kind => AtomKind.Coordinator;
    public static AtomDeterminism Determinism => AtomDeterminism.Deterministic;
    public static AtomPersistence Persistence => AtomPersistence.EphemeralOnly;
    public static string DefaultOutputSignal => "atom.coordinator.output";
}

/// <summary>
///     Shard metadata for feedback atoms.
/// </summary>
public sealed class FeedbackShard : ITaxonomyShard
{
    public static AtomKind Kind => AtomKind.Feedback;
    public static AtomDeterminism Determinism => AtomDeterminism.Deterministic;
    public static AtomPersistence Persistence => AtomPersistence.PersistableViaEscalation;
    public static string DefaultOutputSignal => "atom.feedback.output";
}

/// <summary>
///     Shard metadata for escalator atoms.
/// </summary>
public sealed class EscalatorShard : ITaxonomyShard
{
    public static AtomKind Kind => AtomKind.Escalator;
    public static AtomDeterminism Determinism => AtomDeterminism.Deterministic;
    public static AtomPersistence Persistence => AtomPersistence.DirectWriteAllowed;
    public static string DefaultOutputSignal => "atom.escalator.output";
}

/// <summary>
///     Shard metadata for guard atoms.
/// </summary>
public sealed class GuardShard : ITaxonomyShard
{
    public static AtomKind Kind => AtomKind.Guard;
    public static AtomDeterminism Determinism => AtomDeterminism.Deterministic;
    public static AtomPersistence Persistence => AtomPersistence.EphemeralOnly;
    public static string DefaultOutputSignal => "atom.guard.output";
}