# Mostlylucid.Ephemeral.Atoms.Escalator

Promotes typed, ephemeral signals into durable sinks. EscalatorAtoms are the preferred way to persist outputs from
short-lived coordinators. They sit at the boundary between in-memory runs and long-term storage.

> WARNING - This is still in the 1.x lab phase; APIs may change.

## Installation

```bash
dotnet add package Mostlylucid.Ephemeral.Atoms.Escalator
```

## Why EscalatorAtoms

- Deterministic promotion of ephemeral signals into durable stores
- Fan-out to multiple sinks (database, object storage, audit log, queue)
- Centralized policy for what is allowed to persist
- Separate compute from persistence while keeping signals observable

## Usage

```csharp
using Mostlylucid.Ephemeral;
using Mostlylucid.Ephemeral.Atoms.Data.File;
using Mostlylucid.Ephemeral.Atoms.Escalator;

public sealed record EscalationPayload(string Kind, string? EvidenceId, double Confidence);

var sink = new SignalSink();
var typed = new TypedSignalSink<EscalationPayload>(sink);

await using var storage = new FileDataStorageAtom<string, EscalationPayload>(
    sink,
    new FileDataStorageConfig { DatabaseName = "signals" });

Func<string, CancellationToken, Task> audit = (line, ct) =>
{
    Console.WriteLine(line);
    return Task.CompletedTask;
};

var targets = new[]
{
    new EscalationTarget<EscalationPayload>(
        "signals",
        (evt, ct) => storage.SaveAsync(evt.Key ?? evt.OperationId.ToString(), evt.Payload, ct)),
    new EscalationTarget<EscalationPayload>(
        "audit",
        (evt, ct) => audit($"{evt.Timestamp:o} {evt.Signal} {evt.Key}", ct))
};

await using var escalator = new EscalatorAtom<EscalationPayload>(
    sink,
    typed,
    targets,
    new EscalatorAtomOptions<EscalationPayload>
    {
        EscalateSignalPattern = "escalate.*",
        EmitOnSuccess = "escalation.persisted",
        EmitOnFailure = "escalation.failed"
    });

// Emit a typed signal from a coordinator or molecule.
typed.Raise("escalate.signal", new EscalationPayload("risk", "file-42", 0.81), key: "order-123");
```

## Options

- `EscalateSignalPattern`: glob pattern to match typed signals (default: `escalate.*`).
- `ShouldEscalate`: predicate to override escalation decisions.
- `CoordinatorOptions`: control concurrency, retention, and window size for the internal coordinator.
- `EmitOnSuccess`: untyped signal name raised after all targets persist.
- `EmitOnFailure`: untyped signal name raised when a target fails (suffix includes exception type).

## Notes

- EscalatorAtoms are deterministic by default. Use them to persist signals, decisions, and evidence references.
- If a signal should not be durable, do not mark it for escalation.
- Pair EscalatorAtoms with guard checks for safety gates before persistence.
