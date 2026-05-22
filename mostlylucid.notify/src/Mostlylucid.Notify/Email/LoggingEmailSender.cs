using Microsoft.Extensions.Logging;

namespace Mostlylucid.Notify.Email;

/// <summary>
///     Dev / no-SMTP-configured sender. Logs the would-be send and returns success.
///     Replaces the old StyloBotDevEmailSender -- same observable behaviour
///     (no socket opens, log line per "send").
/// </summary>
public sealed class LoggingEmailSender : INotificationSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger) => _logger = logger;

    public Task<NotificationResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        var id = $"logged:{Guid.NewGuid():N}";
        _logger.LogInformation(
            "[LoggingEmailSender] channel={Channel} template={Template} recipient={Recipient} id={Id}",
            message.Channel, message.Template, message.Recipient, id);
        return Task.FromResult(NotificationResult.Sent(id));
    }
}
