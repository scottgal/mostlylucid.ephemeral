using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mostlylucid.Notify.DependencyInjection;
using Mostlylucid.Notify.Email;
using Xunit;

namespace Mostlylucid.Notify.Tests;

public class DiRegistrationTests
{
    [Fact]
    public void AddNotifyEmail_with_config_resolves_INotificationSender()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Notify:Email:From"] = "noreply@x",
            ["Notify:Email:Smtp:Host"] = "smtp.example.com",
            ["Notify:Email:Smtp:Port"] = "587"
        }).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNotify(cfg).AddNotifyEmail();
        var sp = services.BuildServiceProvider();

        var sender = sp.GetRequiredService<INotificationSender>();
        Assert.IsType<EmailSender>(sender);
        Assert.IsType<SmtpEmailTransport>(sp.GetRequiredService<IEmailTransport>());
    }

    [Fact]
    public void AddNotifyEmailLogging_replaces_sender_with_logger()
    {
        var cfg = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNotify(cfg).AddNotifyEmailLogging();
        var sp = services.BuildServiceProvider();

        var sender = sp.GetRequiredService<INotificationSender>();
        Assert.IsType<LoggingEmailSender>(sender);
    }

    [Fact]
    public void AddEmailTemplate_registers_into_registry()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Notify:Email:From"] = "noreply@x",
            ["Notify:Email:Smtp:Host"] = "smtp.example.com"
        }).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNotify(cfg)
            .AddNotifyEmail()
            .AddEmailTemplate<DummyModel, DummyTemplate>("dummy");

        var sp = services.BuildServiceProvider();
        sp.ActivateNotifyTemplates();   // eager realization

        var registry = sp.GetRequiredService<EmailTemplateRegistry>();
        Assert.True(registry.TryGet("dummy", out _));
    }

    private sealed record DummyModel(string V);
    private sealed class DummyTemplate : INotificationTemplate<DummyModel>
    {
        public string Subject(DummyModel m) => "subj";
        public Task<string> RenderHtmlAsync(DummyModel m, CancellationToken ct = default) => Task.FromResult("h");
        public Task<string> RenderTextAsync(DummyModel m, CancellationToken ct = default) => Task.FromResult("t");
    }
}
