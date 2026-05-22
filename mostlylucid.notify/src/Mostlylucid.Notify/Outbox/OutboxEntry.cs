namespace Mostlylucid.Notify.Outbox;

/// <summary>
///     A row in the notify outbox table (or in-memory channel). Carries enough state for the
///     drain pipeline to claim, send, finalize, and retry.
/// </summary>
public sealed record OutboxEntry
{
    public required Guid Id { get; init; }
    public string? IdempotencyKey { get; init; }
    public required string Channel { get; init; }
    public required string Template { get; init; }
    public required string RecipientJson { get; init; }
    public required string ModelJson { get; init; }
    public required string ModelType { get; init; }       // assembly-qualified, for deserialization
    public required DateTimeOffset QueuedAt { get; init; }
    public DateTimeOffset? NextRetryAt { get; init; }
    public DateTimeOffset? ClaimedAt { get; init; }
    public int Attempts { get; init; }
    public string? LastError { get; init; }
    public OutboxState State { get; init; }
}

public enum OutboxState
{
    Queued,
    Sending,
    Sent,
    Failed
}
