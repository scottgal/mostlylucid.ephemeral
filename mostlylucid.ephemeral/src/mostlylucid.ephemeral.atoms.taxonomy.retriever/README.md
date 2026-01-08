# Mostlylucid.Ephemeral.Atoms.Taxonomy.Retriever

[![NuGet](https://img.shields.io/nuget/v/mostlylucid.ephemeral.atoms.taxonomy.retriever.svg)](https://www.nuget.org/packages/mostlylucid.ephemeral.atoms.taxonomy.retriever)

Deterministic retriever atom that selects candidates under a lens.

```bash
dotnet add package mostlylucid.ephemeral.atoms.taxonomy.retriever
```

## Quick Start

```csharp
using Mostlylucid.Ephemeral;
using Mostlylucid.Ephemeral.Atoms.Taxonomy;

await using var atom = new RetrieverAtom<string, int>(
    new SignalSink(),
    async (input, ct) => input.Length,
    outputSignal: "retriever.output");

await atom.RunAsync("probe");
```

## Contract Defaults

- Kind: Retriever
- Determinism: Deterministic
- Persistence: EphemeralOnly
- Output signal: atom.retriever.output (unless overridden)

## Related Packages

| Package                                                                                         | Description    |
|-------------------------------------------------------------------------------------------------|----------------|
| [mostlylucid.ephemeral](https://www.nuget.org/packages/mostlylucid.ephemeral)                   | Core library   |
| [mostlylucid.ephemeral.complete](https://www.nuget.org/packages/mostlylucid.ephemeral.complete) | All in one DLL |

## License

Unlicense (public domain)
