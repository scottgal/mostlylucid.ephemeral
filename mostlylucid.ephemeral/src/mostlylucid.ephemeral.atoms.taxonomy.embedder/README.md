# Mostlylucid.Ephemeral.Atoms.Taxonomy.Embedder

[![NuGet](https://img.shields.io/nuget/v/mostlylucid.ephemeral.atoms.taxonomy.embedder.svg)](https://www.nuget.org/packages/mostlylucid.ephemeral.atoms.taxonomy.embedder)

Deterministic embedder atom that produces embeddings for extracted units.

```bash
dotnet add package mostlylucid.ephemeral.atoms.taxonomy.embedder
```

## Quick Start

```csharp
using Mostlylucid.Ephemeral;
using Mostlylucid.Ephemeral.Atoms.Taxonomy;

await using var atom = new EmbedderAtom<string, int>(
    new SignalSink(),
    async (input, ct) => input.Length,
    outputSignal: "embedder.output");

await atom.RunAsync("probe");
```

## Contract Defaults

- Kind: Embedder
- Determinism: Deterministic
- Persistence: PersistableViaEscalation
- Output signal: atom.embedder.output (unless overridden)

## Related Packages

| Package                                                                                         | Description    |
|-------------------------------------------------------------------------------------------------|----------------|
| [mostlylucid.ephemeral](https://www.nuget.org/packages/mostlylucid.ephemeral)                   | Core library   |
| [mostlylucid.ephemeral.complete](https://www.nuget.org/packages/mostlylucid.ephemeral.complete) | All in one DLL |

## License

Unlicense (public domain)
