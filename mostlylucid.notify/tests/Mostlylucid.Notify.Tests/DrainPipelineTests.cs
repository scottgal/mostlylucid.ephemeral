using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Mostlylucid.Notify.Drain;
using Mostlylucid.Notify.Email;
using Mostlylucid.Notify.Outbox;
using Xunit;

namespace Mostlylucid.Notify.Tests;

public class DrainPipelineTests
{
    [Fact]
    public async Task Enqueue_then_drain_marks_row_sent()
    {
        var outbox = new InMemoryNotificationOutbox();
        var registry = new EmailTemplateRegistry();
        registry.Register<TestModel>("t", new TestTemplate());

        var sender = new EmailSender(
            registry,
            Options.Create(new EmailNotifyOptions { Smtp = new() { Host = "x" }, From = "f@x" }),
            new SuccessTransport(),
            NullLogger<EmailSender>.Instance);

        var claim = new OutboxClaimAtom(outbox, NullLogger<OutboxClaimAtom>.Instance,
            idleSleep: TimeSpan.FromMilliseconds(50));
        var finalize = new OutboxFinalizeAtom(outbox, NullLogger<OutboxFinalizeAtom>.Instance);
        var pipeline = new NotificationDrainPipeline(claim, sender, finalize, NullLogger<NotificationDrainPipeline>.Instance);

        await outbox.EnqueueAsync(NotificationMessage.Email(new EmailRecipient("u@x"), "t", new TestModel("hi")));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await pipeline.RunOneCycleAsync(cts.Token);

        var leftover = await outbox.ClaimAsync(10);
        Assert.Empty(leftover);
    }

    [Fact]
    public async Task Permanent_failure_dead_letters_immediately()
    {
        var outbox = new InMemoryNotificationOutbox();
        var registry = new EmailTemplateRegistry();
        registry.Register<TestModel>("t", new TestTemplate());

        var sender = new EmailSender(
            registry,
            Options.Create(new EmailNotifyOptions { Smtp = new() { Host = "x" }, From = "f@x" }),
            new FailingTransport(isTransient: false),
            NullLogger<EmailSender>.Instance);

        var claim = new OutboxClaimAtom(outbox, NullLogger<OutboxClaimAtom>.Instance,
            idleSleep: TimeSpan.FromMilliseconds(50));
        var finalize = new OutboxFinalizeAtom(outbox, NullLogger<OutboxFinalizeAtom>.Instance);
        var pipeline = new NotificationDrainPipeline(claim, sender, finalize, NullLogger<NotificationDrainPipeline>.Instance);

        await outbox.EnqueueAsync(NotificationMessage.Email(new EmailRecipient("u@x"), "t", new TestModel("hi")));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await pipeline.RunOneCycleAsync(cts.Token);

        // dead-lettered (in-memory variant just drops, so the outbox is empty)
        var leftover = await outbox.ClaimAsync(10);
        Assert.Empty(leftover);
    }

    private sealed record TestModel(string Word);
    private sealed class TestTemplate : INotificationTemplate<TestModel>
    {
        public string Subject(TestModel m) => $"s:{m.Word}";
        public Task<string> RenderHtmlAsync(TestModel m, CancellationToken ct = default) => Task.FromResult($"<p>{m.Word}</p>");
        public Task<string> RenderTextAsync(TestModel m, CancellationToken ct = default) => Task.FromResult(m.Word);
    }
    private sealed class SuccessTransport : IEmailTransport
    {
        public Task<NotificationResult> SendAsync(EmailPayload p, CancellationToken ct = default) =>
            Task.FromResult(NotificationResult.Sent("ok"));
    }
    private sealed class FailingTransport : IEmailTransport
    {
        private readonly bool _transient;
        public FailingTransport(bool isTransient) => _transient = isTransient;
        public Task<NotificationResult> SendAsync(EmailPayload p, CancellationToken ct = default) =>
            Task.FromResult(NotificationResult.Failed("nope", isTransient: _transient));
    }
}
