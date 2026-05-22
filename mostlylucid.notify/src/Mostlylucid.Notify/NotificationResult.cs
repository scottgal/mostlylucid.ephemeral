namespace Mostlylucid.Notify;

/// <summary>
///     Terminal outcome of a send attempt. <see cref="IsTransient"/> distinguishes
///     "retry later" failures (HTTP 5xx, timeouts, SMTP 4xx) from permanent ones
///     (bad address, HTTP 4xx other than 429).
/// </summary>
public sealed record NotificationResult
{
    public required bool Success { get; init; }
    public string? ProviderMessageId { get; init; }
    public string? Error { get; init; }
    public bool IsTransient { get; init; }

    public static NotificationResult Sent(string? providerMessageId = null) =>
        new() { Success = true, ProviderMessageId = providerMessageId };

    public static NotificationResult Failed(string error, bool isTransient) =>
        new() { Success = false, Error = error, IsTransient = isTransient };
}
