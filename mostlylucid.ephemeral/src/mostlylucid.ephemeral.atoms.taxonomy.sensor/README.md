# Mostlylucid.Ephemeral.Atoms.Taxonomy.Sensor

[![NuGet](https://img.shields.io/nuget/v/mostlylucid.ephemeral.atoms.taxonomy.sensor.svg)](https://www.nuget.org/packages/mostlylucid.ephemeral.atoms.taxonomy.sensor)

Deterministic sensor atom that extracts signals or evidence pointers from sources.

`ash
dotnet add package mostlylucid.ephemeral.atoms.taxonomy.sensor
`

## Quick Start

`csharp
using Mostlylucid.Ephemeral;
using Mostlylucid.Ephemeral.Atoms.Taxonomy;

await using var atom = new SensorAtom<string, int>(
    new SignalSink(),
    async (input, ct) => input.Length,
    outputSignal: "sensor.output");

await atom.RunAsync("probe");
`

## Contract Defaults

- Kind: Sensor
- Determinism: Deterministic
- Persistence: PersistableViaEscalation
- Output signal: tom.sensor.output (unless overridden)

## Related Packages

| Package                                                                                         | Description    |
|-------------------------------------------------------------------------------------------------|----------------|
| [mostlylucid.ephemeral](https://www.nuget.org/packages/mostlylucid.ephemeral)                   | Core library   |
| [mostlylucid.ephemeral.complete](https://www.nuget.org/packages/mostlylucid.ephemeral.complete) | All in one DLL |

## License

Unlicense (public domain)
