using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mostlylucid.Notify.Email;

/// <summary>
///     Default email-channel implementation of <see cref="INotificationSender"/>. Looks the
///     template up by key, renders Subject/Html/Text, hands the payload to <see cref="IEmailTransport"/>.
/// </summary>
public sealed class EmailSender : INotificationSender
{
    private readonly EmailTemplateRegistry _registry;
    private readonly EmailNotifyOptions _options;
    private readonly IEmailTransport _transport;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(
        EmailTemplateRegistry registry,
        IOptions<EmailNotifyOptions> options,
        IEmailTransport transport,
        ILogger<EmailSender> logger)
    {
        _registry = registry;
        _options = options.Value;
        _transport = transport;
        _logger = logger;
    }

    public async Task<NotificationResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        if (message.Channel != "email")
            return NotificationResult.Failed($"EmailSender only handles channel=email, got {message.Channel}", isTransient: false);

        if (message.Recipient is not EmailRecipient to)
            return NotificationResult.Failed($"EmailSender requires EmailRecipient, got {message.Recipient.GetType().Name}", isTransient: false);

        if (!_registry.TryGet(message.Template, out var template))
            return NotificationResult.Failed($"No email template registered for key '{message.Template}'", isTransient: false);

        try
        {
            var subject = template.Subject(message.Model);
            var html = await template.RenderHtmlAsync(message.Model, cancellationToken).ConfigureAwait(false);
            var text = await template.RenderTextAsync(message.Model, cancellationToken).ConfigureAwait(false);

            var payload = new EmailPayload
            {
                From = _options.From,
                FromName = _options.FromName,
                To = to.Address,
                Subject = subject,
                HtmlBody = html,
                TextBody = text
            };

            return await _transport.SendAsync(payload, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EmailSender threw rendering template {Template}", message.Template);
            return NotificationResult.Failed(ex.Message, isTransient: false);
        }
    }
}
