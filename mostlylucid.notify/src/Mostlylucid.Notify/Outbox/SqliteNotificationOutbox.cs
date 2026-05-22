using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Mostlylucid.Notify.Outbox;

/// <summary>
///     SQLite-backed outbox. Suitable for single-node FOSS dashboard installs that already
///     use a SQLite store. Schema is applied idempotently from the embedded SQL resource.
/// </summary>
public sealed class SqliteNotificationOutbox : INotificationOutbox, IAsyncDisposable
{
    private readonly string _connectionString;

    public SqliteNotificationOutbox(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var sql = LoadEmbeddedSchema();
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await conn.ExecuteAsync(sql).ConfigureAwait(false);
    }

    private static string LoadEmbeddedSchema()
    {
        var asm = typeof(SqliteNotificationOutbox).Assembly;
        using var stream = asm.GetManifestResourceStream("Mostlylucid.Notify.Outbox.Schema.notify_outbox.sqlite.sql")
            ?? throw new InvalidOperationException("Embedded notify_outbox.sqlite.sql missing");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public async Task<Guid> EnqueueAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        if (message.IdempotencyKey is { } key)
        {
            var existing = await conn.QueryFirstOrDefaultAsync<string?>(
                "SELECT id FROM notify_outbox WHERE idempotency_key = @key",
                new { key }).ConfigureAwait(false);
            if (existing is not null) return Guid.Parse(existing);
        }

        var id = Guid.NewGuid();
        await conn.ExecuteAsync(@"
INSERT INTO notify_outbox (id, idempotency_key, channel, template, recipient_json, model_json, model_type, queued_at, attempts, state)
VALUES (@id, @key, @channel, @template, @recipient, @model, @modelType, @queuedAt, 0, 'queued')",
            new
            {
                id = id.ToString(),
                key = message.IdempotencyKey,
                channel = message.Channel,
                template = message.Template,
                recipient = JsonSerializer.Serialize<object>(message.Recipient),
                model = JsonSerializer.Serialize(message.Model, message.Model.GetType()),
                modelType = message.Model.GetType().AssemblyQualifiedName,
                queuedAt = DateTimeOffset.UtcNow.ToString("O")
            }).ConfigureAwait(false);
        return id;
    }

    public async Task<IReadOnlyList<OutboxEntry>> ClaimAsync(int maxItems, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow.ToString("O");
        var rows = (await conn.QueryAsync<OutboxRow>(@"
SELECT id, idempotency_key AS IdempotencyKey, channel, template, recipient_json AS RecipientJson,
       model_json AS ModelJson, model_type AS ModelType, queued_at AS QueuedAt, next_retry_at AS NextRetryAt,
       claimed_at AS ClaimedAt, attempts, last_error AS LastError, state
FROM notify_outbox
WHERE state = 'queued' AND (next_retry_at IS NULL OR next_retry_at <= @now)
ORDER BY queued_at
LIMIT @max",
            new { now, max = maxItems }, tx).ConfigureAwait(false)).ToList();

        foreach (var r in rows)
        {
            await conn.ExecuteAsync(
                "UPDATE notify_outbox SET state = 'sending', claimed_at = @now WHERE id = @id",
                new { now, id = r.Id }, tx).ConfigureAwait(false);
        }
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

        return rows.Select(MapToEntry).ToList();
    }

    public async Task MarkSentAsync(Guid id, string? providerMessageId, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await conn.ExecuteAsync("DELETE FROM notify_outbox WHERE id = @id", new { id = id.ToString() }).ConfigureAwait(false);
    }

    public async Task MarkFailedAsync(Guid id, string error, DateTimeOffset nextRetryAt, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await conn.ExecuteAsync(@"
UPDATE notify_outbox
SET state = 'queued',
    attempts = attempts + 1,
    last_error = @err,
    next_retry_at = @next,
    claimed_at = NULL
WHERE id = @id",
            new { id = id.ToString(), err = error, next = nextRetryAt.ToString("O") }).ConfigureAwait(false);
    }

    public async Task MoveToDeadLetterAsync(Guid id, string finalError, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var payload = await conn.QueryFirstOrDefaultAsync<string?>(
            "SELECT json_object('channel', channel, 'template', template, 'recipient_json', recipient_json, 'model_json', model_json, 'model_type', model_type) FROM notify_outbox WHERE id = @id",
            new { id = id.ToString() }, tx).ConfigureAwait(false);
        if (payload is null) { await tx.CommitAsync(cancellationToken); return; }

        await conn.ExecuteAsync(@"
INSERT INTO notify_dead_letter (id, original_id, payload, final_error, dead_lettered_at)
VALUES (@dlId, @origId, @payload, @err, @at)",
            new
            {
                dlId = Guid.NewGuid().ToString(),
                origId = id.ToString(),
                payload,
                err = finalError,
                at = DateTimeOffset.UtcNow.ToString("O")
            }, tx).ConfigureAwait(false);

        await conn.ExecuteAsync("DELETE FROM notify_outbox WHERE id = @id", new { id = id.ToString() }, tx).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static OutboxEntry MapToEntry(OutboxRow r) => new()
    {
        Id = Guid.Parse(r.Id),
        IdempotencyKey = r.IdempotencyKey,
        Channel = r.Channel,
        Template = r.Template,
        RecipientJson = r.RecipientJson,
        ModelJson = r.ModelJson,
        ModelType = r.ModelType,
        QueuedAt = DateTimeOffset.Parse(r.QueuedAt),
        NextRetryAt = r.NextRetryAt is null ? null : DateTimeOffset.Parse(r.NextRetryAt),
        ClaimedAt = r.ClaimedAt is null ? null : DateTimeOffset.Parse(r.ClaimedAt),
        Attempts = r.Attempts,
        LastError = r.LastError,
        State = r.State switch
        {
            "queued" => OutboxState.Queued,
            "sending" => OutboxState.Sending,
            "sent" => OutboxState.Sent,
            _ => OutboxState.Failed
        }
    };

    private sealed class OutboxRow
    {
        public string Id { get; set; } = "";
        public string? IdempotencyKey { get; set; }
        public string Channel { get; set; } = "";
        public string Template { get; set; } = "";
        public string RecipientJson { get; set; } = "";
        public string ModelJson { get; set; } = "";
        public string ModelType { get; set; } = "";
        public string QueuedAt { get; set; } = "";
        public string? NextRetryAt { get; set; }
        public string? ClaimedAt { get; set; }
        public int Attempts { get; set; }
        public string? LastError { get; set; }
        public string State { get; set; } = "";
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
