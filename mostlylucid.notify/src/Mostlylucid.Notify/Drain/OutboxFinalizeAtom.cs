using Microsoft.Extensions.Logging;
using Mostlylucid.Ephemeral;
using Mostlylucid.Notify.Outbox;

namespace Mostlylucid.Notify.Drain;

/// <summary>
///     Sink atom -- takes (<see cref="OutboxEntry"/>, <see cref="NotificationResult"/>) pairs
///     from the channel atoms and reports back to the outbox. Decides retry vs dead-letter
///     based on attempts + transience.
/// </summary>
public sealed class OutboxFinalizeAtom
{
    private static readonly TimeSpan[] BackoffSchedule =
    {
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(6)
    };
    private const int MaxAttempts = 5;

    private readonly INotificationOutbox _outbox;
    private readonly SignalSink? _signals;
    private readonly ILogger<OutboxFinalizeAtom> _logger;

    public OutboxFinalizeAtom(INotificationOutbox outbox, ILogger<OutboxFinalizeAtom> logger, SignalSink? signals = null)
    {
        _outbox = outbox;
        _signals = signals;
        _logger = logger;
    }

    public async Task FinalizeAsync(OutboxEntry entry, NotificationResult result, CancellationToken cancellationToken = default)
    {
        if (result.Success)
        {
            await _outbox.MarkSentAsync(entry.Id, result.ProviderMessageId, cancellationToken).ConfigureAwait(false);
            _signals?.Raise($"notify.send.completed:id={entry.Id}:channel={entry.Channel}");
            return;
        }

        var nextAttempt = entry.Attempts + 1;
        if (!result.IsTransient || nextAttempt >= MaxAttempts)
        {
            await _outbox.MoveToDeadLetterAsync(entry.Id, result.Error ?? "unknown", cancellationToken).ConfigureAwait(false);
            _signals?.Raise($"notify.send.dead_lettered:id={entry.Id}:channel={entry.Channel}:reason={result.Error}");
            _logger.LogWarning("Dead-lettered notify row {Id} after {Attempts} attempts: {Error}",
                entry.Id, nextAttempt, result.Error);
            return;
        }

        var delay = BackoffSchedule[Math.Min(entry.Attempts, BackoffSchedule.Length - 1)];
        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, (int)(delay.TotalMilliseconds * 0.2)));
        var next = DateTimeOffset.UtcNow + delay + jitter;

        await _outbox.MarkFailedAsync(entry.Id, result.Error ?? "transient", next, cancellationToken).ConfigureAwait(false);
        _signals?.Raise($"notify.send.failed:id={entry.Id}:retry_in={delay.TotalSeconds}s");
    }
}
