using System;
using System.Threading;
using System.Threading.Tasks;
using Mostlylucid.Ephemeral;

namespace Mostlylucid.Ephemeral.Atoms.Escalator;

public sealed class EscalationTarget<TPayload>
{
    public EscalationTarget(string name, Func<SignalEvent<TPayload>, CancellationToken, Task> persistAsync)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Target name cannot be empty.", nameof(name));

        PersistAsync = persistAsync ?? throw new ArgumentNullException(nameof(persistAsync));
        Name = name;
    }

    public string Name { get; }

    public Func<SignalEvent<TPayload>, CancellationToken, Task> PersistAsync { get; }
}
