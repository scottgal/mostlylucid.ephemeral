# Mostlylucid.Ephemeral.Atoms.Taxonomy.Feedback

[![NuGet](https://img.shields.io/nuget/v/mostlylucid.ephemeral.atoms.taxonomy.feedback.svg)](https://www.nuget.org/packages/mostlylucid.ephemeral.atoms.taxonomy.feedback)

Deterministic feedback atom that consumes outcomes and updates weights.

`ash
dotnet add package mostlylucid.ephemeral.atoms.taxonomy.feedback
`
I thi
## Quick Start

`csharp
using Mostlylucid.Ephemeral;
using Mostlylucid.Ephemeral.Atoms.Taxonomy;

await using var atom = new FeedbackAtom<string, int>(
    new SignalSink(),
    async (input, ct) => input.Length,
    outputSignal: "feedback.output");

await atom.RunAsync("probe");
`

## Contract Defaults

- Kind: Feedback
- Determinism: Deterministic
- Persistence: PersistableViaEscalation
- Output signal: tom.feedback.output (unless overridden)

## Related Packages

| Package                                                                                         | Description    |
|-------------------------------------------------------------------------------------------------|----------------|
| [mostlylucid.ephemeral](https://www.nuget.org/packages/mostlylucid.ephemeral)                   | Core library   |
| [mostlylucid.ephemeral.complete](https://www.nuget.org/packages/mostlylucid.ephemeral.complete) | All in one DLL |

## License

Unlicense (public domain)
