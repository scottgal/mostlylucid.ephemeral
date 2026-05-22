using System.Collections.Concurrent;
using System.Text.Json;

namespace Mostlylucid.Notify.Outbox;

/// <summary>
///     In-memory outbox backed by a concurrent dictionary. Survives the process lifetime only --
///     intended for the AOT CLI and tests, NOT for anything that needs durability. Use
///     <c>SqliteNotificationOutbox</c> for that.
///
///     Note: despite the "Channels" reference in the original spec sketch, we use a
///     ConcurrentDictionary because the outbox semantics need random access for
///     MarkSent/MarkFailed/MoveToDeadLetter by id, plus retry-time ordering. A bounded
///     channel doesn't fit those access patterns.
/// </summary>
public sealed class InMemoryNotificationOutbox : INotificationOutbox
{
    private readonly ConcurrentDictionary<Guid, OutboxEntry> _entries = new();
    private readonly ConcurrentDictionary<string, Guid> _byIdempotencyKey = new();
    private readonly object _claimLock = new();
    private readonly int _capacity;

    public InMemoryNotificationOutbox(int capacity = 10_000)
    {
        _capacity = capacity;
    }

    public Task<Guid> EnqueueAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        if (message.IdempotencyKey is { } key && _byIdempotencyKey.TryGetValue(key, out var existing))
            return Task.FromResult(existing);

        if (_entries.Count >= _capacity)
            throw new InvalidOperationException($"InMemoryNotificationOutbox at capacity ({_capacity})");

        var id = Guid.NewGuid();
        var entry = new OutboxEntry
        {
            Id = id,
            IdempotencyKey = message.IdempotencyKey,
            Channel = message.Channel,
            Template = message.Template,
            RecipientJson = JsonSerializer.Serialize<object>(message.Recipient),
            ModelJson = JsonSerializer.Serialize(message.Model, message.Model.GetType()),
            ModelType = message.Model.GetType().AssemblyQualifiedName ?? message.Model.GetType().FullName!,
            QueuedAt = DateTimeOffset.UtcNow,
            State = OutboxState.Queued
        };
        _entries[id] = entry;
        if (message.IdempotencyKey is { } k) _byIdempotencyKey[k] = id;
        return Task.FromResult(id);
    }

    public Task<IReadOnlyList<OutboxEntry>> ClaimAsync(int maxItems, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        lock (_claimLock)
        {
            var ready = _entries.Values
                .Where(e => e.State == OutboxState.Queued && (e.NextRetryAt is null || e.NextRetryAt <= now))
                .OrderBy(e => e.QueuedAt)
                .Take(maxItems)
                .ToList();

            foreach (var e in ready)
            {
                _entries[e.Id] = e with { State = OutboxState.Sending, ClaimedAt = now };
            }
            return Task.FromResult<IReadOnlyList<OutboxEntry>>(ready.Select(e => _entries[e.Id]).ToList());
        }
    }

    public Task MarkSentAsync(Guid id, string? providerMessageId, CancellationToken cancellationToken = default)
    {
        _entries.TryRemove(id, out var removed);
        if (removed?.IdempotencyKey is { } k) _byIdempotencyKey.TryRemove(k, out _);
        return Task.CompletedTask;
    }

    public Task MarkFailedAsync(Guid id, string error, DateTimeOffset nextRetryAt, CancellationToken cancellationToken = default)
    {
        if (_entries.TryGetValue(id, out var entry))
        {
            _entries[id] = entry with
            {
                State = OutboxState.Queued,
                NextRetryAt = nextRetryAt,
                Attempts = entry.Attempts + 1,
                LastError = error,
                ClaimedAt = null
            };
        }
        return Task.CompletedTask;
    }

    public Task MoveToDeadLetterAsync(Guid id, string finalError, CancellationToken cancellationToken = default)
    {
        _entries.TryRemove(id, out var removed);
        if (removed?.IdempotencyKey is { } k) _byIdempotencyKey.TryRemove(k, out _);
        // In-memory variant just drops; SQLite/Postgres impls write to notify_dead_letter.
        return Task.CompletedTask;
    }
}
