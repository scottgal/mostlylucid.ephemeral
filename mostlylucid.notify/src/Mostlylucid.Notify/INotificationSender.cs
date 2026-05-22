namespace Mostlylucid.Notify;

/// <summary>
///     Hands a single notification to the configured channel and returns the terminal outcome.
///     Inline send -- callers wanting outbox semantics use <c>INotificationOutbox.EnqueueAsync</c>
///     and let the drain pipeline call this.
/// </summary>
public interface INotificationSender
{
    Task<NotificationResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken = default);
}
