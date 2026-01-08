# Mostlylucid.Ephemeral.Atoms.Taxonomy.Constrainer

[![NuGet](https://img.shields.io/nuget/v/mostlylucid.ephemeral.atoms.taxonomy.constrainer.svg)](https://www.nuget.org/packages/mostlylucid.ephemeral.atoms.taxonomy.constrainer)

Deterministic constrainer atom that validates or selects proposals.

```bash
dotnet add package mostlylucid.ephemeral.atoms.taxonomy.constrainer
```

## Quick Start

```csharp
using Mostlylucid.Ephemeral;
using Mostlylucid.Ephemeral.Atoms.Taxonomy;

await using var atom = new ConstrainerAtom<string, int>(
    new SignalSink(),
    async (input, ct) => input.Length,
    outputSignal: "constrainer.output");

await atom.RunAsync("probe");
```

## Contract Defaults

- Kind: Constrainer
- Determinism: Deterministic
- Persistence: PersistableViaEscalation
- Output signal: atom.constrainer.output (unless overridden)

## Related Packages

| Package | Description |
|---------|-------------|
| [mostlylucid.ephemeral](https://www.nuget.org/packages/mostlylucid.ephemeral) | Core library |
| [mostlylucid.ephemeral.complete](https://www.nuget.org/packages/mostlylucid.ephemeral.complete) | All in one DLL |

## License

Unlicense (public domain)
