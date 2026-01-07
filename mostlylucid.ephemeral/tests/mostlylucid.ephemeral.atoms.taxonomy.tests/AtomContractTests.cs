using Mostlylucid.Ephemeral.Atoms.Taxonomy;
using Xunit;

namespace Mostlylucid.Ephemeral.Atoms.Taxonomy.Tests;

public class AtomContractTests
{
    [Fact]
    public void CreateDefaultsNameToKindAtom()
    {
        var contract = AtomContract.Create(
            AtomKind.Ranker,
            AtomDeterminism.Deterministic,
            AtomPersistence.EphemeralOnly);

        Assert.Equal("RankerAtom", contract.Name);
    }

    [Fact]
    public void ComposeBuildsContractFromShards()
    {
        var shards = new[]
        {
            TaxonomyShard.Create<RetrieverShard>(),
            TaxonomyShard.Create<ProposerShard>(),
            TaxonomyShard.Create<EscalatorShard>()
        };

        var contract = AtomContract.Compose(shards);

        Assert.Equal(AtomKind.Retriever, contract.Kind);
        Assert.Equal(AtomDeterminism.Probabilistic, contract.Determinism);
        Assert.Equal(AtomPersistence.DirectWriteAllowed, contract.Persistence);
        Assert.Contains(AtomKind.Proposer, contract.Kinds);
        Assert.Contains(AtomKind.Escalator, contract.Kinds);
    }

    [Fact]
    public void RegisterCreatesCustomKind()
    {
        var kind = AtomKind.Register("custom.detector");
        var contract = AtomContract.Create(
            kind,
            AtomDeterminism.Deterministic,
            AtomPersistence.EphemeralOnly);

        Assert.Equal(kind, contract.Kind);
        Assert.Contains(kind, AtomKind.Registered);
    }
}
