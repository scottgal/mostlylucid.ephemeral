using Mostlylucid.Notify.Outbox;

namespace Mostlylucid.Notify;

/// <summary>
///     Persistent (or in-memory) outbox. The drain pipeline calls <see cref="ClaimAsync"/>
///     to pick up <paramref name="maxItems"/> ready rows, sends them, and reports via
///     <see cref="MarkSentAsync"/> / <see cref="MarkFailedAsync"/> /
///     <see cref="MoveToDeadLetterAsync"/>.
/// </summary>
public interface INotificationOutbox
{
    /// <summary>
    ///     Enqueue a message. Returns the outbox row id. If <paramref name="idempotencyKey"/>
    ///     duplicates an existing row, the existing row's id is returned and no new row is created.
    /// </summary>
    Task<Guid> EnqueueAsync(NotificationMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Claim up to <paramref name="maxItems"/> rows whose state is Queued and
    ///     NextRetryAt is null or in the past. Sets state=Sending and ClaimedAt=now.
    /// </summary>
    Task<IReadOnlyList<OutboxEntry>> ClaimAsync(int maxItems, CancellationToken cancellationToken = default);

    Task MarkSentAsync(Guid id, string? providerMessageId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Mark a transient failure; schedule next retry per the supplied <paramref name="nextRetryAt"/>.
    /// </summary>
    Task MarkFailedAsync(Guid id, string error, DateTimeOffset nextRetryAt, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Final state: max attempts reached or permanent error. Moves the row out of the outbox.
    /// </summary>
    Task MoveToDeadLetterAsync(Guid id, string finalError, CancellationToken cancellationToken = default);
}
