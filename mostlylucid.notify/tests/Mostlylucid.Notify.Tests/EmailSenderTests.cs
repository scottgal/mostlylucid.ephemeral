using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Mostlylucid.Notify.Email;
using Xunit;

namespace Mostlylucid.Notify.Tests;

public class EmailSenderTests
{
    [Fact]
    public async Task SendAsync_unknown_template_returns_failure_non_transient()
    {
        var registry = new EmailTemplateRegistry();
        var opts = Options.Create(new EmailNotifyOptions
        {
            Smtp = new() { Host = "localhost", Port = 25 },
            From = "noreply@x"
        });
        var stubTransport = new StubEmailTransport();
        var sender = new EmailSender(registry, opts, stubTransport, NullLogger<EmailSender>.Instance);

        var msg = NotificationMessage.Email(new EmailRecipient("u@x"), "missing.template", new { });
        var result = await sender.SendAsync(msg);

        Assert.False(result.Success);
        Assert.False(result.IsTransient);
        Assert.Contains("missing.template", result.Error);
    }

    [Fact]
    public async Task SendAsync_known_template_renders_and_calls_transport()
    {
        var registry = new EmailTemplateRegistry();
        registry.Register<FakeModel>("test", new FakeTemplate());
        var opts = Options.Create(new EmailNotifyOptions
        {
            Smtp = new() { Host = "localhost", Port = 25 },
            From = "noreply@x"
        });
        var stubTransport = new StubEmailTransport();
        var sender = new EmailSender(registry, opts, stubTransport, NullLogger<EmailSender>.Instance);

        var msg = NotificationMessage.Email(new EmailRecipient("user@x"), "test", new FakeModel("hi"));
        var result = await sender.SendAsync(msg);

        Assert.True(result.Success);
        Assert.NotNull(stubTransport.Last);
        Assert.Equal("Subj: hi", stubTransport.Last!.Subject);
        Assert.Contains("hi", stubTransport.Last!.HtmlBody);
        Assert.Contains("hi", stubTransport.Last!.TextBody);
        Assert.Equal("user@x", stubTransport.Last!.To);
    }

    [Fact]
    public async Task SendAsync_wrong_channel_returns_failure()
    {
        var registry = new EmailTemplateRegistry();
        var opts = Options.Create(new EmailNotifyOptions
        {
            Smtp = new() { Host = "x" }, From = "f@x"
        });
        var sender = new EmailSender(registry, opts, new StubEmailTransport(), NullLogger<EmailSender>.Instance);

        var msg = new NotificationMessage
        {
            Channel = "sms",   // wrong channel
            Recipient = new EmailRecipient("u@x"),
            Template = "t",
            Model = new { }
        };
        var result = await sender.SendAsync(msg);

        Assert.False(result.Success);
        Assert.False(result.IsTransient);
        Assert.Contains("channel=email", result.Error);
    }

    private sealed record FakeModel(string Word);

    private sealed class FakeTemplate : INotificationTemplate<FakeModel>
    {
        public string Subject(FakeModel m) => $"Subj: {m.Word}";
        public Task<string> RenderHtmlAsync(FakeModel m, CancellationToken ct = default) => Task.FromResult($"<p>{m.Word}</p>");
        public Task<string> RenderTextAsync(FakeModel m, CancellationToken ct = default) => Task.FromResult(m.Word);
    }

    private sealed class StubEmailTransport : IEmailTransport
    {
        public EmailPayload? Last;
        public Task<NotificationResult> SendAsync(EmailPayload p, CancellationToken ct = default)
        {
            Last = p;
            return Task.FromResult(NotificationResult.Sent("stub-1"));
        }
    }
}
