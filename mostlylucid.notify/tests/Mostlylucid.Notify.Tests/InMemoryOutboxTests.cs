using Mostlylucid.Notify.Email;
using Mostlylucid.Notify.Outbox;
using Xunit;

namespace Mostlylucid.Notify.Tests;

public class InMemoryOutboxTests
{
    [Fact]
    public async Task EnqueueAsync_then_ClaimAsync_returns_entry()
    {
        var outbox = new InMemoryNotificationOutbox(capacity: 100);
        var msg = NotificationMessage.Email(new EmailRecipient("u@x"), "t", new { N = 1 });

        var id = await outbox.EnqueueAsync(msg);
        var claimed = await outbox.ClaimAsync(10);

        Assert.Single(claimed);
        Assert.Equal(id, claimed[0].Id);
        Assert.Equal(OutboxState.Sending, claimed[0].State);
        Assert.Equal("email", claimed[0].Channel);
        Assert.Equal("t", claimed[0].Template);
    }

    [Fact]
    public async Task EnqueueAsync_with_same_idempotency_key_returns_same_id()
    {
        var outbox = new InMemoryNotificationOutbox(capacity: 100);
        var msg = NotificationMessage.Email(new EmailRecipient("u@x"), "t", new { }, idempotencyKey: "k1");

        var id1 = await outbox.EnqueueAsync(msg);
        var id2 = await outbox.EnqueueAsync(msg);

        Assert.Equal(id1, id2);
    }

    [Fact]
    public async Task MarkSentAsync_removes_from_pending()
    {
        var outbox = new InMemoryNotificationOutbox(capacity: 100);
        var id = await outbox.EnqueueAsync(NotificationMessage.Email(new EmailRecipient("u@x"), "t", new { }));
        _ = await outbox.ClaimAsync(10);

        await outbox.MarkSentAsync(id, providerMessageId: "p");
        var nothingLeft = await outbox.ClaimAsync(10);

        Assert.Empty(nothingLeft);
    }

    [Fact]
    public async Task MarkFailedAsync_schedules_retry_and_makes_claimable_later()
    {
        var outbox = new InMemoryNotificationOutbox(capacity: 100);
        var id = await outbox.EnqueueAsync(NotificationMessage.Email(new EmailRecipient("u@x"), "t", new { }));
        _ = await outbox.ClaimAsync(10);

        await outbox.MarkFailedAsync(id, "fail", nextRetryAt: DateTimeOffset.UtcNow.AddMilliseconds(-1));
        var reclaimed = await outbox.ClaimAsync(10);

        Assert.Single(reclaimed);
        Assert.Equal(1, reclaimed[0].Attempts);
        Assert.Equal("fail", reclaimed[0].LastError);
    }

    [Fact]
    public async Task MoveToDeadLetterAsync_drops_in_memory()
    {
        var outbox = new InMemoryNotificationOutbox(capacity: 100);
        var id = await outbox.EnqueueAsync(NotificationMessage.Email(new EmailRecipient("u@x"), "t", new { }));
        _ = await outbox.ClaimAsync(10);

        await outbox.MoveToDeadLetterAsync(id, "final");
        var nothingLeft = await outbox.ClaimAsync(10);

        Assert.Empty(nothingLeft);
    }
}
