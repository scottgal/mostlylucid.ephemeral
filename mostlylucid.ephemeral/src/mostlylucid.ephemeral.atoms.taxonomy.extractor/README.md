# Mostlylucid.Ephemeral.Atoms.Taxonomy.Extractor

[![NuGet](https://img.shields.io/nuget/v/mostlylucid.ephemeral.atoms.taxonomy.extractor.svg)](https://www.nuget.org/packages/mostlylucid.ephemeral.atoms.taxonomy.extractor)

Deterministic extractor atom that turns raw content into stable semantic units.

```bash
dotnet add package mostlylucid.ephemeral.atoms.taxonomy.extractor
```

## Quick Start

```csharp
using Mostlylucid.Ephemeral;
using Mostlylucid.Ephemeral.Atoms.Taxonomy;

await using var atom = new ExtractorAtom<string, int>(
    new SignalSink(),
    async (input, ct) => input.Length,
    outputSignal: "extractor.output");

await atom.RunAsync("probe");
```

## Contract Defaults

- Kind: Extractor
- Determinism: Deterministic
- Persistence: PersistableViaEscalation
- Output signal: atom.extractor.output (unless overridden)

## Related Packages

| Package | Description |
|---------|-------------|
| [mostlylucid.ephemeral](https://www.nuget.org/packages/mostlylucid.ephemeral) | Core library |
| [mostlylucid.ephemeral.complete](https://www.nuget.org/packages/mostlylucid.ephemeral.complete) | All in one DLL |

## License

Unlicense (public domain)
