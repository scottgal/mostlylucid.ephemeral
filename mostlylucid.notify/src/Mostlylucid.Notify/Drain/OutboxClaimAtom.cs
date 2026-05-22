using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Mostlylucid.Ephemeral;
using Mostlylucid.Notify.Outbox;

namespace Mostlylucid.Notify.Drain;

/// <summary>
///     Source atom -- polls the outbox on a cadence and yields claimed <see cref="OutboxEntry"/>
///     rows downstream. Signal sink emits <c>notify.outbox.claimed</c> per row.
/// </summary>
public sealed class OutboxClaimAtom
{
    private readonly INotificationOutbox _outbox;
    private readonly SignalSink? _signals;
    private readonly ILogger<OutboxClaimAtom> _logger;
    private readonly int _maxItemsPerPoll;
    private readonly TimeSpan _idleSleep;

    public OutboxClaimAtom(
        INotificationOutbox outbox,
        ILogger<OutboxClaimAtom> logger,
        SignalSink? signals = null,
        int maxItemsPerPoll = 50,
        TimeSpan? idleSleep = null)
    {
        _outbox = outbox;
        _logger = logger;
        _signals = signals;
        _maxItemsPerPoll = maxItemsPerPoll;
        _idleSleep = idleSleep ?? TimeSpan.FromSeconds(5);
    }

    public async IAsyncEnumerable<OutboxEntry> StreamAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            IReadOnlyList<OutboxEntry> claimed;
            try
            {
                claimed = await _outbox.ClaimAsync(_maxItemsPerPoll, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Outbox claim failed; backing off");
                await Task.Delay(_idleSleep, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (claimed.Count == 0)
            {
                await Task.Delay(_idleSleep, cancellationToken).ConfigureAwait(false);
                continue;
            }

            foreach (var entry in claimed)
            {
                _signals?.Raise($"notify.outbox.claimed:id={entry.Id}:channel={entry.Channel}");
                yield return entry;
            }
        }
    }
}
