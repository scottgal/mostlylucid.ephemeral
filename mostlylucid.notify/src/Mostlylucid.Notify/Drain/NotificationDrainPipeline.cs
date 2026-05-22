using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Mostlylucid.Notify.Outbox;

namespace Mostlylucid.Notify.Drain;

/// <summary>
///     Composes the claim -> send -> finalize flow. Owns no thread; the host calls
///     <see cref="RunAsync"/> from an Ephemeral coordinator task. Cancellation tears the
///     pipeline down cleanly.
/// </summary>
public sealed class NotificationDrainPipeline
{
    private readonly OutboxClaimAtom _claim;
    private readonly INotificationSender _sender;
    private readonly OutboxFinalizeAtom _finalize;
    private readonly ILogger<NotificationDrainPipeline> _logger;

    public NotificationDrainPipeline(
        OutboxClaimAtom claim,
        INotificationSender sender,
        OutboxFinalizeAtom finalize,
        ILogger<NotificationDrainPipeline> logger)
    {
        _claim = claim;
        _sender = sender;
        _finalize = finalize;
        _logger = logger;
    }

    /// <summary>Long-running drain loop. Returns when <paramref name="cancellationToken"/> fires.</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await foreach (var entry in _claim.StreamAsync(cancellationToken).ConfigureAwait(false))
        {
            await DispatchAsync(entry, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Test seam: drains one entry then returns.</summary>
    public async Task RunOneCycleAsync(CancellationToken cancellationToken)
    {
        await foreach (var entry in _claim.StreamAsync(cancellationToken).ConfigureAwait(false))
        {
            await DispatchAsync(entry, cancellationToken).ConfigureAwait(false);
            return;
        }
    }

    private async Task DispatchAsync(OutboxEntry entry, CancellationToken cancellationToken)
    {
        var message = RehydrateMessage(entry);
        var result = await _sender.SendAsync(message, cancellationToken).ConfigureAwait(false);
        await _finalize.FinalizeAsync(entry, result, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Rebuild a NotificationMessage from outbox JSON. AOT note: uses Type.GetType + runtime
    ///     JsonSerializer.Deserialize with the dynamic type. Trim warnings are unavoidable here;
    ///     the AOT smoke test (Phase H) ensures we only reference model types the consumer
    ///     registered, so the trimmer keeps them via the explicit template registrations in DI.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Model types are registered explicitly via AddEmailTemplate<TModel, _>; trimmer keeps them.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "Same as above; model deserialization paths are anchored by explicit registrations.")]
    private static NotificationMessage RehydrateMessage(OutboxEntry entry)
    {
        var modelType = Type.GetType(entry.ModelType)
            ?? throw new InvalidOperationException($"Cannot resolve model type '{entry.ModelType}'");
        var model = JsonSerializer.Deserialize(entry.ModelJson, modelType)
            ?? throw new InvalidOperationException("Model deserialization returned null");
        var recipient = JsonSerializer.Deserialize<Email.EmailRecipient>(entry.RecipientJson)
            ?? throw new InvalidOperationException("Recipient deserialization returned null");
        return new NotificationMessage
        {
            Channel = entry.Channel,
            Recipient = recipient,
            Template = entry.Template,
            Model = model,
            IdempotencyKey = entry.IdempotencyKey
        };
    }
}
