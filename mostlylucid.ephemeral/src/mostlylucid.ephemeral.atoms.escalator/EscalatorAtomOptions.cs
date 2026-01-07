using System;
using Mostlylucid.Ephemeral;

namespace Mostlylucid.Ephemeral.Atoms.Escalator;

public sealed class EscalatorAtomOptions<TPayload>
{
    public string EscalateSignalPattern { get; init; } = "escalate.*";
    public Func<SignalEvent<TPayload>, bool>? ShouldEscalate { get; init; }
    public EphemeralOptions? CoordinatorOptions { get; init; }
    public string? EmitOnSuccess { get; init; } = "escalation.persisted";
    public string? EmitOnFailure { get; init; } = "escalation.failed";
    public Action<SignalEvent<TPayload>>? OnEscalated { get; init; }
    public Action<SignalEvent<TPayload>, Exception>? OnFailed { get; init; }
}
