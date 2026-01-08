# Mostlylucid.Ephemeral.Atoms.Taxonomy.Proposer

[![NuGet](https://img.shields.io/nuget/v/mostlylucid.ephemeral.atoms.taxonomy.proposer.svg)](https://www.nuget.org/packages/mostlylucid.ephemeral.atoms.taxonomy.proposer)

Probabilistic proposer atom that emits proposals with confidence.

```bash
dotnet add package mostlylucid.ephemeral.atoms.taxonomy.proposer
```

## Quick Start

```csharp
using Mostlylucid.Ephemeral;
using Mostlylucid.Ephemeral.Atoms.Taxonomy;

await using var atom = new ProposerAtom<string, int>(
    new SignalSink(),
    async (input, ct) => input.Length,
    outputSignal: "proposer.output");

await atom.RunAsync("probe");
```

## Contract Defaults

- Kind: Proposer
- Determinism: Probabilistic
- Persistence: PersistableViaEscalation
- Output signal: atom.proposer.output (unless overridden)

## Related Packages

| Package | Description |
|---------|-------------|
| [mostlylucid.ephemeral](https://www.nuget.org/packages/mostlylucid.ephemeral) | Core library |
| [mostlylucid.ephemeral.complete](https://www.nuget.org/packages/mostlylucid.ephemeral.complete) | All in one DLL |

## License

Unlicense (public domain)
