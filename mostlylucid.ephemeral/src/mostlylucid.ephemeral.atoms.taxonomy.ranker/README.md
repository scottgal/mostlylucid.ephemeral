# Mostlylucid.Ephemeral.Atoms.Taxonomy.Ranker

[![NuGet](https://img.shields.io/nuget/v/mostlylucid.ephemeral.atoms.taxonomy.ranker.svg)](https://www.nuget.org/packages/mostlylucid.ephemeral.atoms.taxonomy.ranker)

Deterministic ranker atom that re-scores and re-orders candidates.

```bash
dotnet add package mostlylucid.ephemeral.atoms.taxonomy.ranker
```

## Quick Start

```csharp
using Mostlylucid.Ephemeral;
using Mostlylucid.Ephemeral.Atoms.Taxonomy;

await using var atom = new RankerAtom<string, int>(
    new SignalSink(),
    async (input, ct) => input.Length,
    outputSignal: "ranker.output");

await atom.RunAsync("probe");
```

## Contract Defaults

- Kind: Ranker
- Determinism: Deterministic
- Persistence: EphemeralOnly
- Output signal: atom.ranker.output (unless overridden)

## Related Packages

| Package | Description |
|---------|-------------|
| [mostlylucid.ephemeral](https://www.nuget.org/packages/mostlylucid.ephemeral) | Core library |
| [mostlylucid.ephemeral.complete](https://www.nuget.org/packages/mostlylucid.ephemeral.complete) | All in one DLL |

## License

Unlicense (public domain)
