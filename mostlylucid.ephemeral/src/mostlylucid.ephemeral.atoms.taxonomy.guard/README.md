# Mostlylucid.Ephemeral.Atoms.Taxonomy.Guard

[![NuGet](https://img.shields.io/nuget/v/mostlylucid.ephemeral.atoms.taxonomy.guard.svg)](https://www.nuget.org/packages/mostlylucid.ephemeral.atoms.taxonomy.guard)

Deterministic guard atom that enforces safety or compliance rules.

```bash
dotnet add package mostlylucid.ephemeral.atoms.taxonomy.guard
```

## Quick Start

```csharp
using Mostlylucid.Ephemeral;
using Mostlylucid.Ephemeral.Atoms.Taxonomy;

await using var atom = new GuardAtom<string, int>(
    new SignalSink(),
    async (input, ct) => input.Length,
    outputSignal: "guard.output");

await atom.RunAsync("probe");
```

## Contract Defaults

- Kind: Guard
- Determinism: Deterministic
- Persistence: EphemeralOnly
- Output signal: atom.guard.output (unless overridden)

## Related Packages

| Package | Description |
|---------|-------------|
| [mostlylucid.ephemeral](https://www.nuget.org/packages/mostlylucid.ephemeral) | Core library |
| [mostlylucid.ephemeral.complete](https://www.nuget.org/packages/mostlylucid.ephemeral.complete) | All in one DLL |

## License

Unlicense (public domain)
