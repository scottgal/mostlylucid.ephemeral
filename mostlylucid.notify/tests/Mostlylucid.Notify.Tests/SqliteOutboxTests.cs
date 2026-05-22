using Microsoft.Data.Sqlite;
using Mostlylucid.Notify.Email;
using Mostlylucid.Notify.Outbox;
using Xunit;

namespace Mostlylucid.Notify.Tests;

public class SqliteOutboxTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly string _connectionString;

    public SqliteOutboxTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"notify-test-{Guid.NewGuid():N}.db");
        _connectionString = $"Data Source={_dbPath}";
    }

    [Fact]
    public async Task EnqueueAsync_persists_across_instances()
    {
        var msg = NotificationMessage.Email(new EmailRecipient("u@x"), "t", new TestModel("hello"));

        Guid id;
        await using (var outbox1 = new SqliteNotificationOutbox(_connectionString))
        {
            await outbox1.InitializeAsync();
            id = await outbox1.EnqueueAsync(msg);
        }

        await using var outbox2 = new SqliteNotificationOutbox(_connectionString);
        await outbox2.InitializeAsync();
        var claimed = await outbox2.ClaimAsync(10);
        Assert.Single(claimed);
        Assert.Equal(id, claimed[0].Id);
    }

    [Fact]
    public async Task Idempotency_key_dedupes_at_insert()
    {
        await using var outbox = new SqliteNotificationOutbox(_connectionString);
        await outbox.InitializeAsync();
        var msg = NotificationMessage.Email(new EmailRecipient("u@x"), "t", new TestModel("h"), idempotencyKey: "dedupe-1");

        var id1 = await outbox.EnqueueAsync(msg);
        var id2 = await outbox.EnqueueAsync(msg);
        Assert.Equal(id1, id2);

        var claimed = await outbox.ClaimAsync(10);
        Assert.Single(claimed);
    }

    [Fact]
    public async Task MarkFailedAsync_schedules_retry()
    {
        await using var outbox = new SqliteNotificationOutbox(_connectionString);
        await outbox.InitializeAsync();
        var id = await outbox.EnqueueAsync(NotificationMessage.Email(new EmailRecipient("u@x"), "t", new TestModel("h")));
        _ = await outbox.ClaimAsync(10);

        await outbox.MarkFailedAsync(id, "transient blip", DateTimeOffset.UtcNow.AddMilliseconds(-1));
        var reclaimed = await outbox.ClaimAsync(10);
        Assert.Single(reclaimed);
        Assert.Equal(1, reclaimed[0].Attempts);
        Assert.Equal("transient blip", reclaimed[0].LastError);
    }

    [Fact]
    public async Task MoveToDeadLetterAsync_writes_dead_letter_row()
    {
        await using var outbox = new SqliteNotificationOutbox(_connectionString);
        await outbox.InitializeAsync();
        var id = await outbox.EnqueueAsync(NotificationMessage.Email(new EmailRecipient("u@x"), "t", new TestModel("h")));
        _ = await outbox.ClaimAsync(10);

        await outbox.MoveToDeadLetterAsync(id, "final error");

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM notify_dead_letter WHERE original_id = @id";
        cmd.Parameters.AddWithValue("@id", id.ToString());
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        Assert.Equal(1, count);

        // and the row is gone from notify_outbox
        var nothingLeft = await outbox.ClaimAsync(10);
        Assert.Empty(nothingLeft);
    }

    private sealed record TestModel(string Word);

    public ValueTask DisposeAsync()
    {
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
        return ValueTask.CompletedTask;
    }
}
