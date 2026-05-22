using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mostlylucid.Notify;
using Mostlylucid.Notify.DependencyInjection;
using Mostlylucid.Notify.Email;

var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Notify:Email:From"] = "noreply@example.com",
    ["Notify:Email:Smtp:Host"] = "localhost",
    ["Notify:Email:Smtp:Port"] = "25",
}).Build();

var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole());
services.AddNotify(cfg)
    .AddNotifyEmailLogging()
    .AddNotifyOutboxInMemory()
    .AddEmailTemplate<SmokeModel, SmokeTemplate>("smoke");

var sp = services.BuildServiceProvider();
sp.ActivateNotifyTemplates();

var sender = sp.GetRequiredService<INotificationSender>();
var result = await sender.SendAsync(NotificationMessage.Email(
    new EmailRecipient("recipient@example.com"),
    "smoke",
    new SmokeModel("aot-smoke")));

Console.WriteLine($"smoke success={result.Success} id={result.ProviderMessageId}");
return result.Success ? 0 : 1;

internal sealed record SmokeModel(string Word);

internal sealed class SmokeTemplate : INotificationTemplate<SmokeModel>
{
    public string Subject(SmokeModel m) => $"smoke {m.Word}";
    public Task<string> RenderHtmlAsync(SmokeModel m, CancellationToken ct = default) => Task.FromResult($"<p>{m.Word}</p>");
    public Task<string> RenderTextAsync(SmokeModel m, CancellationToken ct = default) => Task.FromResult(m.Word);
}
