using Mostlylucid.Notify.Email;

namespace Mostlylucid.Notify;

/// <summary>
///     A notification recipient. M0 ships <see cref="EmailRecipient"/> only; future channels add
///     their own sealed records (SlackChannel, SmsRecipient, etc.) that inherit Recipient.
/// </summary>
public abstract record Recipient;

/// <summary>
///     A single notification to be sent. Immutable; constructed via the static factories
///     (<see cref="Email"/> etc.) rather than directly so we keep channel/recipient pairs valid.
/// </summary>
public sealed record NotificationMessage
{
    public required string Channel { get; init; }
    public required Recipient Recipient { get; init; }
    public required string Template { get; init; }
    public required object Model { get; init; }
    public string? IdempotencyKey { get; init; }

    /// <summary>Construct an email notification.</summary>
    public static NotificationMessage Email(
        EmailRecipient to,
        string template,
        object model,
        string? idempotencyKey = null) =>
        new()
        {
            Channel = "email",
            Recipient = to,
            Template = template,
            Model = model,
            IdempotencyKey = idempotencyKey
        };
}
