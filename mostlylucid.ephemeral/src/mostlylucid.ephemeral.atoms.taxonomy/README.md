# Mostlylucid.Ephemeral.Atoms.Taxonomy

Shared contracts and base types for the taxonomy atom kinds. This package provides the metadata vocabulary
(AtomKind, AtomDeterminism, AtomPersistence, AtomBudget, AtomContract) and the SignalDrivenAtom base class that the
individual atom packages build on.

> WARNING - This is still in the 1.x lab phase; APIs may change.

## Installation

```bash
dotnet add package mostlylucid.ephemeral.atoms.taxonomy
```

## Included types

- AtomKind
- AtomDeterminism
- AtomPersistence
- AtomBudget
- AtomContract
- ITaxonomyShard
- TaxonomyShard
- Shard definitions (SensorShard, ExtractorShard, EmbedderShard, RetrieverShard, ProposerShard, ConstrainerShard,
  RankerShard, RendererShard, CoordinatorShard, FeedbackShard, EscalatorShard, GuardShard)
- SignalDrivenAtom
- MultiTaxonomyAtom

## Atom packages

- mostlylucid.ephemeral.atoms.taxonomy.sensor
- mostlylucid.ephemeral.atoms.taxonomy.extractor
- mostlylucid.ephemeral.atoms.taxonomy.embedder
- mostlylucid.ephemeral.atoms.taxonomy.retriever
- mostlylucid.ephemeral.atoms.taxonomy.proposer
- mostlylucid.ephemeral.atoms.taxonomy.constrainer
- mostlylucid.ephemeral.atoms.taxonomy.ranker
- mostlylucid.ephemeral.atoms.taxonomy.renderer
- mostlylucid.ephemeral.atoms.taxonomy.coordinator
- mostlylucid.ephemeral.atoms.taxonomy.feedback
- mostlylucid.ephemeral.atoms.taxonomy.guard

## Usage

```csharp
using Mostlylucid.Ephemeral.Atoms.Taxonomy;

var contract = AtomContract.Create(
    AtomKind.Ranker,
    AtomDeterminism.Deterministic,
    AtomPersistence.EphemeralOnly,
    name: "ranker.fusion",
    reads: new[] { "signals", "feedback" },
    writes: new[] { "candidates" });
```

Atom kinds are extensible:

```csharp
var customKind = AtomKind.Register("deterministic-escalator");
var customContract = AtomContract.Create(
    customKind,
    AtomDeterminism.Deterministic,
    AtomPersistence.DirectWriteAllowed);
```

Multi-kind composition uses taxonomy shards:

```csharp
var shards = new[]
{
    TaxonomyShard.Create<CoordinatorShard>(),
    TaxonomyShard.Create<EscalatorShard>()
};

await using var multi = new MultiTaxonomyAtom<string, int>(
    new SignalSink(),
    async (input, ct) => input.Length,
    shards);
```
