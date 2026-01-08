namespace Mostlylucid.Ephemeral;

/// <summary>
///     Scoped signal emitter providing three convenience APIs:
///     - Signal() → atom-level (most specific)
///     - CoordinatorSignal() → coordinator-level (all atoms)
///     - SinkSignal() → sink-level (all coordinators + atoms)
/// </summary>
/// <remarks>
///     All three methods normalize to fully-qualified SignalKey before emission.
///     This ensures:
///     - Call sites stay terse and intention-level
///     - Backend always deals with canonical, fully-scoped keys
///     - No ambiguity about signal scope
/// </remarks>
/// <example>
///     <code>
/// var ctx = new SignalContext("request", "gateway", "ResizeImageJob");
/// var emitter = new ScopedSignalEmitter(ctx, sink);
/// 
/// await emitter.Signal("started");
/// // → "request.gateway.ResizeImageJob.started"
/// 
/// await emitter.CoordinatorSignal("batch.completed");
/// // → "request.gateway.*.batch.completed"
/// 
/// await emitter.SinkSignal("health.failed");
/// // → "request.*.*.health.failed"
/// </code>
/// </example>
public sealed class ScopedSignalEmitter : ISignalEmitter
{
    private readonly SignalConstraints? _constraints;
    private readonly SignalContext _context;
    private readonly Action<SignalEvent>? _onSignal;
    private readonly Action<SignalRetractedEvent>? _onSignalRetracted;
    private readonly SignalSink? _sink;

    /// <summary>
    ///     Create a scoped signal emitter.
    /// </summary>
    /// <param name="context">The three-level scope (sink/coordinator/atom).</param>
    /// <param name="operationId">Operation ID for correlation.</param>
    /// <param name="sink">Optional global signal sink.</param>
    /// <param name="onSignal">Optional callback when signal is raised.</param>
    /// <param name="onSignalRetracted">Optional callback when signal is retracted.</param>
    /// <param name="constraints">Optional signal propagation constraints.</param>
    public ScopedSignalEmitter(
        SignalContext context,
        long operationId,
        SignalSink? sink = null,
        Action<SignalEvent>? onSignal = null,
        Action<SignalRetractedEvent>? onSignalRetracted = null,
        SignalConstraints? constraints = null)
    {
        _context = context;
        OperationId = operationId;
        _sink = sink;
        _onSignal = onSignal;
        _onSignalRetracted = onSignalRetracted;
        _constraints = constraints;
    }

    /// <summary>
    ///     The operation ID (for correlation).
    /// </summary>
    public long OperationId { get; }

    /// <summary>
    ///     The operation key (coordinator name).
    /// </summary>
    public string? Key => _context.Coordinator;

    /// <summary>
    ///     Emit an atom-scoped signal (most specific).
    ///     Normalizes to: sink.coordinator.atom.name
    /// </summary>
    /// <param name="name">Signal name (e.g., "started", "completed", "metrics.queued").</param>
    public void Emit(string name)
    {
        var key = ScopedSignalKey.ForAtom(_context, name);
        EmitCore(key.ToString());
    }

    /// <summary>
    ///     Emit a coordinator-scoped signal (all atoms within this coordinator).
    ///     Normalizes to: sink.coordinator.*.name
    /// </summary>
    /// <param name="name">Signal name (e.g., "batch.completed", "throttled").</param>
    public void EmitCoordinatorSignal(string name)
    {
        var key = ScopedSignalKey.ForCoordinator(_context, name);
        EmitCore(key.ToString());
    }

    /// <summary>
    ///     Emit a sink-scoped signal (all coordinators and atoms).
    ///     Normalizes to: sink.*.*.name
    /// </summary>
    /// <param name="name">Signal name (e.g., "health.failed", "shutdown").</param>
    public void EmitSinkSignal(string name)
    {
        var key = ScopedSignalKey.ForSink(_context, name);
        EmitCore(key.ToString());
    }

    /// <summary>
    ///     Retract a previously emitted signal.
    /// </summary>
    /// <param name="name">Signal name to retract.</param>
    public bool Retract(string name)
    {
        var key = ScopedSignalKey.ForAtom(_context, name);
        return RetractCore(key.ToString());
    }

    /// <summary>
    ///     Emit a signal caused by another signal (for propagation tracking).
    ///     Tracks signal chains to enable cycle detection and depth limiting.
    /// </summary>
    /// <param name="signal">Signal name to emit.</param>
    /// <param name="cause">The propagation chain from the causing signal (null for root signals).</param>
    /// <returns>True if signal was emitted, false if blocked by constraints.</returns>
    public bool EmitCaused(string signal, SignalPropagation? cause)
    {
        var key = ScopedSignalKey.ForAtom(_context, signal);
        var normalizedSignal = key.ToString();

        // Check constraints if configured (pass propagation for cycle/depth checking)
        if (_constraints != null)
        {
            var blockReason = _constraints.ShouldBlock(normalizedSignal, cause);
            if (blockReason.HasValue)
            {
                var blockedEvent = new SignalEvent(
                    normalizedSignal,
                    OperationId,
                    _context.Coordinator,
                    DateTimeOffset.UtcNow,
                    cause);
                _constraints.OnBlocked?.Invoke(blockedEvent, blockReason.Value);
                return false; // Signal blocked
            }
        }

        // Build propagation chain: extend from cause, or start new root if null
        // Skip propagation for leaf signals (they don't cause downstream effects)
        SignalPropagation? propagation = null;
        if (_constraints?.IsLeaf(normalizedSignal) != true)
        {
            propagation = cause?.Extend(normalizedSignal) ?? SignalPropagation.Root(normalizedSignal);
        }

        var signalEvent = new SignalEvent(
            normalizedSignal,
            OperationId,
            _context.Coordinator,
            DateTimeOffset.UtcNow,
            propagation);

        // Invoke callbacks
        _onSignal?.Invoke(signalEvent);

        // Send to global sink
        _sink?.Raise(signalEvent);

        return true;
    }

    /// <summary>
    ///     Remove all signals matching a pattern.
    /// </summary>
    public int RetractMatching(string pattern)
    {
        // Scoped emitter doesn't track signals internally
        // This would need to query the sink
        return 0;
    }

    /// <summary>
    ///     Check if a signal is currently active.
    /// </summary>
    public bool HasSignal(string signal)
    {
        // Scoped emitter doesn't track signals internally
        // This would need to query the sink
        return false;
    }

    /// <summary>
    ///     Core emission logic - sends normalized signal to sink.
    /// </summary>
    private void EmitCore(string normalizedSignal)
    {
        var signalEvent = new SignalEvent(
            normalizedSignal,
            OperationId,
            _context.Coordinator, // Use coordinator as correlation key
            DateTimeOffset.UtcNow,
            null
        );

        // Check constraints if configured
        if (_constraints != null)
        {
            var blockReason = _constraints.ShouldBlock(normalizedSignal, null);
            if (blockReason.HasValue)
            {
                _constraints.OnBlocked?.Invoke(signalEvent, blockReason.Value);
                return; // Signal blocked
            }
        }

        // Invoke callbacks
        _onSignal?.Invoke(signalEvent);

        // Send to global sink
        _sink?.Raise(signalEvent);
    }

    /// <summary>
    ///     Retract a coordinator-scoped signal.
    /// </summary>
    /// <param name="name">Signal name to retract.</param>
    public bool RetractCoordinatorSignal(string name)
    {
        var key = ScopedSignalKey.ForCoordinator(_context, name);
        return RetractCore(key.ToString());
    }

    /// <summary>
    ///     Retract a sink-scoped signal.
    /// </summary>
    /// <param name="name">Signal name to retract.</param>
    public bool RetractSinkSignal(string name)
    {
        var key = ScopedSignalKey.ForSink(_context, name);
        return RetractCore(key.ToString());
    }

    private bool RetractCore(string normalizedSignal)
    {
        var retractedEvent = new SignalRetractedEvent(
            normalizedSignal,
            OperationId,
            _context.Coordinator,
            DateTimeOffset.UtcNow,
            false,
            null
        );

        _onSignalRetracted?.Invoke(retractedEvent);
        // Note: SignalSink doesn't track retractions in the window
        return true; // Always succeeds (fire-and-forget)
    }
}