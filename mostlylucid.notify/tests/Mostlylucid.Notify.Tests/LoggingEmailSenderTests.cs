using Microsoft.Extensions.Logging.Abstractions;
using Mostlylucid.Notify.Email;
using Xunit;

namespace Mostlylucid.Notify.Tests;

public class LoggingEmailSenderTests
{
    [Fact]
    public async Task SendAsync_returns_sent_with_synthetic_id()
    {
        var sender = new LoggingEmailSender(NullLogger<LoggingEmailSender>.Instance);

        var result = await sender.SendAsync(NotificationMessage.Email(
            new EmailRecipient("u@x"), "any.template", new { }));

        Assert.True(result.Success);
        Assert.NotNull(result.ProviderMessageId);
        Assert.StartsWith("logged:", result.ProviderMessageId);
    }
}
