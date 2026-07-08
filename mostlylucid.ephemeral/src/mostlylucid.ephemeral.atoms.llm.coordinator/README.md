# Mostlylucid.Ephemeral.Atoms.Llm

An **ephemeral LLM coordinator** atom: a small `prompt → pick → invoke → writeback`
pipeline for running scheduling-coordinated LLM calls off ephemeral signals.

The coordinator (`EphemeralLlmCoordinator`) ticks on an `IScheduleCoordinator`
(from `Mostlylucid.Common.Scheduling`) and walks a set of pluggable collaborators:

| Interface | Role |
|-----------|------|
| `IEphemeralPrompter` | builds the prompt to send |
| `IEphemeralPicker` | selects which candidate(s) to act on |
| `IEphemeralLlmInvoker` | performs the LLM call |
| `IEphemeralWriteback` | persists / applies the result |

Register with `AddEphemeralLlmCoordinator(...)` and supply the four collaborators
via DI. The only external dependencies are the `Microsoft.Extensions.*`
abstractions and `Mostlylucid.Common` scheduling — no dependency on the
`mostlylucid.ephemeral` core.

License: Unlicense.