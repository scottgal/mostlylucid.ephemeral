namespace Mostlylucid.Notify.Email;

/// <summary>
///     Thin transport contract. The default impl <see cref="SmtpEmailTransport"/> wraps the
///     ephemeral <c>SmtpAtom</c>. Tests substitute with stubs so we don't open SMTP sockets.
/// </summary>
public interface IEmailTransport
{
    Task<NotificationResult> SendAsync(EmailPayload payload, CancellationToken cancellationToken = default);
}

public sealed record EmailPayload
{
    public required string From { get; init; }
    public string? FromName { get; init; }
    public required string To { get; init; }
    public required string Subject { get; init; }
    public required string HtmlBody { get; init; }
    public required string TextBody { get; init; }
}
