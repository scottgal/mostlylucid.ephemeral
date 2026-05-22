using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mostlylucid.Notify.DependencyInjection;
using Mostlylucid.Notify.Drain;
using Xunit;

namespace Mostlylucid.Notify.Tests;

public class DrainStarterTests
{
    [Fact]
    public void AddNotifyOutboxInMemory_StartDrainOnCoordinator_resolves()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Notify:Email:From"] = "f@x",
            ["Notify:Email:Smtp:Host"] = "smtp.example.com"
        }).Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // IEphemeralCoordinator has no fire-and-forget scheduling API; EphemeralDrainStarter
        // uses Task.Run internally so FakeCoordinator only needs to satisfy the interface shape.
        services.AddSingleton<Mostlylucid.Ephemeral.IEphemeralCoordinator>(new FakeCoordinator());

        services.AddNotify(cfg)
            .AddNotifyEmailLogging()
            .AddNotifyOutboxInMemory()
            .StartDrainOnCoordinator();

        var sp = services.BuildServiceProvider();
        var starter = sp.GetService<INotifyDrainStarter>();

        Assert.NotNull(starter);

        // Start with a short token: pipeline starts then cancels cleanly.
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        starter!.Start(cts.Token);

        // Give the background task a moment to start + observe cancellation.
        Thread.Sleep(200);
    }

    /// <summary>
    ///     Stub for IEphemeralCoordinator. The starter never calls any coordinator method
    ///     (Task.Run is used because there is no scheduling API on the interface); all members
    ///     except DisposeAsync throw NotImplementedException to catch accidental calls.
    /// </summary>
    private sealed class FakeCoordinator : Mostlylucid.Ephemeral.IEphemeralCoordinator
    {
        public bool IsCompleted => throw new NotImplementedException();
        public bool IsDrained => throw new NotImplementedException();
        public Task DrainAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public void Complete() => throw new NotImplementedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
