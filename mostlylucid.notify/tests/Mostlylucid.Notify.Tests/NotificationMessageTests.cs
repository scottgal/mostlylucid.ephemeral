using Mostlylucid.Notify.Email;
using Xunit;

namespace Mostlylucid.Notify.Tests;

public class NotificationMessageTests
{
    [Fact]
    public void Email_factory_sets_channel_and_template()
    {
        var msg = NotificationMessage.Email(
            new EmailRecipient("user@example.com"),
            "registration.verify",
            new { Name = "Jane", VerifyUrl = "https://x/v?t=1" });

        Assert.Equal("email", msg.Channel);
        Assert.Equal("registration.verify", msg.Template);
        Assert.IsType<EmailRecipient>(msg.Recipient);
        Assert.Null(msg.IdempotencyKey);
    }

    [Fact]
    public void Email_with_idempotency_key_preserves_it()
    {
        var msg = NotificationMessage.Email(
            new EmailRecipient("u@x"),
            "t",
            new { },
            idempotencyKey: "verify:42:abc");

        Assert.Equal("verify:42:abc", msg.IdempotencyKey);
    }

    [Fact]
    public void NotificationResult_sent_factory_marks_success()
    {
        var r = NotificationResult.Sent("provider-id-7");

        Assert.True(r.Success);
        Assert.Equal("provider-id-7", r.ProviderMessageId);
        Assert.Null(r.Error);
    }

    [Fact]
    public void NotificationResult_failed_factory_marks_failure()
    {
        var r = NotificationResult.Failed("network down", isTransient: true);

        Assert.False(r.Success);
        Assert.Equal("network down", r.Error);
        Assert.True(r.IsTransient);
    }
}
