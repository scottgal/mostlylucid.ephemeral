using Microsoft.Extensions.Logging;
using Mostlylucid.Ephemeral;

namespace Mostlylucid.Notify.Drain;

public interface INotifyDrainStarter
{
    void Start(CancellationToken applicationStopping);
}

/// <summary>
///     Schedules <see cref="NotificationDrainPipeline.RunAsync"/> as a long-running task on
///     the host's <see cref="IEphemeralCoordinator"/>. The pipeline cancels cleanly when the
///     host shuts down — no IHostedService glue.
/// </summary>
/// <remarks>
///     <see cref="IEphemeralCoordinator"/> exposes only lifecycle methods (DrainAsync, Complete,
///     IsCompleted, IsDrained) — there is no Schedule/Submit/Enqueue API for fire-and-forget work.
///     The drain pipeline is a long-running loop that owns its own thread budget; Task.Run is the
///     correct primitive here. The task is rooted by the field so GC cannot collect it early, and
///     cancellation flows through applicationStopping so the pipeline stops at host shutdown.
/// </remarks>
internal sealed class EphemeralDrainStarter : INotifyDrainStarter
{
    private readonly IEphemeralCoordinator _coordinator;
    private readonly NotificationDrainPipeline _pipeline;
    private readonly ILogger<EphemeralDrainStarter> _logger;

    // Rooted reference prevents GC collection before the host disposes the container.
#pragma warning disable IDE0052
    private Task? _drainTask;
#pragma warning restore IDE0052

    public EphemeralDrainStarter(
        IEphemeralCoordinator coordinator,
        NotificationDrainPipeline pipeline,
        ILogger<EphemeralDrainStarter> logger)
    {
        _coordinator = coordinator;
        _pipeline = pipeline;
        _logger = logger;
    }

    public void Start(CancellationToken applicationStopping)
    {
        // IEphemeralCoordinator has no fire-and-forget scheduling API (only DrainAsync /
        // Complete / IsCompleted / IsDrained). Task.Run is intentional: the drain pipeline
        // is a long-running I/O loop and must not block the caller. The task is stored in
        // _drainTask to prevent premature GC. Cancellation is wired through applicationStopping
        // so the loop exits cleanly when the host stops.
        _drainTask = Task.Run(() => RunDrainAsync(applicationStopping), applicationStopping);
    }

    private async Task RunDrainAsync(CancellationToken applicationStopping)
    {
        try
        {
            await _pipeline.RunAsync(applicationStopping).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (applicationStopping.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Notification drain pipeline terminated unexpectedly");
        }
    }
}
