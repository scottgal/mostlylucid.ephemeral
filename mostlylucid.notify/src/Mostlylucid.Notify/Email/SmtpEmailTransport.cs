using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.Ephemeral.Atoms.Smtp;

namespace Mostlylucid.Notify.Email;

/// <summary>
///     Production email transport. Wraps the ephemeral SmtpAtom which owns MailKit connection
///     management, retry, and signal-tracking. Maps our EmailPayload to SmtpAtom's EmailMessage
///     at the boundary.
/// </summary>
public sealed class SmtpEmailTransport : IEmailTransport, IAsyncDisposable
{
    private readonly SmtpAtom _atom;
    private readonly ILogger<SmtpEmailTransport> _logger;

    public SmtpEmailTransport(IOptions<EmailNotifyOptions> options, ILogger<SmtpEmailTransport> logger)
    {
        var opts = options.Value;
        var smtp = opts.Smtp;

        // Map our SmtpOptions (Notify.Email.SmtpOptions) -> SmtpAtom's SmtpOptions.
        // Our SmtpOptions.User -> SmtpAtomOptions.Username
        // Our SmtpOptions.UseTls -> SecureSocketOptions.StartTls (true) or None (false)
        // DefaultFromAddress / DefaultFromName come from the parent EmailNotifyOptions.
        _atom = new SmtpAtom(new Mostlylucid.Ephemeral.Atoms.Smtp.SmtpOptions
        {
            Host = smtp.Host,
            Port = smtp.Port,
            RequiresAuthentication = smtp.User is not null,
            Username = smtp.User,
            Password = smtp.Password,
            SecureSocketOptions = smtp.UseTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
            DefaultFromAddress = opts.From,
            DefaultFromName = opts.FromName
        });
        _logger = logger;
    }

    public async Task<NotificationResult> SendAsync(EmailPayload payload, CancellationToken cancellationToken = default)
    {
        // EmailMessage uses a single Body field + IsHtml flag.
        // We send the HTML body and set IsHtml=true; the plain-text fallback is dropped on this transport in v0.1.
        var msg = new EmailMessage
        {
            From = payload.From,
            FromName = payload.FromName,
            To = payload.To,
            Subject = payload.Subject,
            Body = payload.HtmlBody,
            IsHtml = true
        };

        try
        {
            var result = await _atom.SendAsync(msg, cancellationToken).ConfigureAwait(false);
            // EmailResult uses Description (not Error) for the failure reason.
            return result.Success
                ? NotificationResult.Sent(result.MessageId)
                : NotificationResult.Failed(result.Description, isTransient: true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SMTP send failed for {To}", payload.To);
            return NotificationResult.Failed(ex.Message, isTransient: IsTransient(ex));
        }
    }

    private static bool IsTransient(Exception ex) =>
        ex is IOException
            or TimeoutException
            or MailKit.Net.Smtp.SmtpProtocolException
            or MailKit.ServiceNotConnectedException
            or MailKit.ServiceNotAuthenticatedException;

    public ValueTask DisposeAsync() => _atom.DisposeAsync();
}
