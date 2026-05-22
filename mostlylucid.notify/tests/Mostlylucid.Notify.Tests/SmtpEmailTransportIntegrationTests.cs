using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Mostlylucid.Notify.Email;
using Xunit;

namespace Mostlylucid.Notify.Tests;

[Trait("Category", "Integration")]
public class SmtpEmailTransportIntegrationTests
{
    private const string Smtp4devUrl = "http://localhost:5266";

    [Fact]
    public async Task SendAsync_through_smtp4dev_delivers_message()
    {
        if (!await CanReachSmtp4dev()) return;

        var opts = Options.Create(new EmailNotifyOptions
        {
            From = "test@stylobot.local",
            FromName = "stylobot test",
            Smtp = new SmtpOptions
            {
                Host = "localhost",
                Port = 2525,
                UseTls = false
            }
        });

        await using var transport = new SmtpEmailTransport(opts, NullLogger<SmtpEmailTransport>.Instance);

        var subject = $"notify integration test {Guid.NewGuid():N}";
        var result = await transport.SendAsync(new EmailPayload
        {
            From = "test@stylobot.local",
            FromName = "stylobot test",
            To = "recipient@stylobot.local",
            Subject = subject,
            HtmlBody = "<p>hello from notify integration test</p>",
            TextBody = "hello from notify integration test"
        });

        Assert.True(result.Success, $"SmtpEmailTransport returned failure: {result.Error}");

        // Verify smtp4dev actually received it.
        using var http = new HttpClient { BaseAddress = new Uri(Smtp4devUrl) };
        var json = await http.GetStringAsync("/api/Messages");
        Assert.Contains(subject, json);
    }

    private static async Task<bool> CanReachSmtp4dev()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var resp = await http.GetAsync($"{Smtp4devUrl}/api/Messages");
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
