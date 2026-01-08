# Mostlylucid.Ephemeral.Atoms.Taxonomy.Renderer

[![NuGet](https://img.shields.io/nuget/v/mostlylucid.ephemeral.atoms.taxonomy.renderer.svg)](https://www.nuget.org/packages/mostlylucid.ephemeral.atoms.taxonomy.renderer)

Deterministic renderer atom that turns decisions into output artifacts.

```bash
dotnet add package mostlylucid.ephemeral.atoms.taxonomy.renderer
```

## Quick Start

```csharp
using Mostlylucid.Ephemeral;
using Mostlylucid.Ephemeral.Atoms.Taxonomy;

await using var atom = new RendererAtom<string, int>(
    new SignalSink(),
    async (input, ct) => input.Length,
    outputSignal: "renderer.output");

await atom.RunAsync("probe");
```

## Contract Defaults

- Kind: Renderer
- Determinism: Deterministic
- Persistence: EphemeralOnly
- Output signal: atom.renderer.output (unless overridden)

## Related Packages

| Package                                                                                         | Description    |
|-------------------------------------------------------------------------------------------------|----------------|
| [mostlylucid.ephemeral](https://www.nuget.org/packages/mostlylucid.ephemeral)                   | Core library   |
| [mostlylucid.ephemeral.complete](https://www.nuget.org/packages/mostlylucid.ephemeral.complete) | All in one DLL |

## License

Unlicense (public domain)
