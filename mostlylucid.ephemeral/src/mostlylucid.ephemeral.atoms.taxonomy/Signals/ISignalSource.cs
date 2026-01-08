using System;
using System.Collections.Generic;

namespace Mostlylucid.Ephemeral.Atoms.Taxonomy.Signals;

/// <summary>
/// A source of live signals - typically an Atom running in a Coordinator.
/// Signals are owned by the source and die when the source dies.
/// </summary>
public interface ISignalSource
{
    /// <summary>
    /// Unique identifier for this signal source.
    /// </summary>
    string SourceId { get; }

    /// <summary>
    /// Name of the atom/source.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Kind of the source (sensor, extractor, proposer, etc.).
    /// </summary>
    string Kind { get; }

    /// <summary>
    /// Whether this source is still active (signals are live).
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Gets all signals owned by this source.
    /// </summary>
    IReadOnlyList<LiveSignal> GetSignals();

    /// <summary>
    /// Gets signals matching a pattern.
    /// </summary>
    IReadOnlyList<LiveSignal> GetSignals(string pattern);

    /// <summary>
    /// Checks if a signal exists.
    /// </summary>
    bool HasSignal(string key);

    /// <summary>
    /// Gets a signal by key.
    /// </summary>
    LiveSignal? GetSignal(string key);

    /// <summary>
    /// Event raised when a new signal is emitted.
    /// </summary>
    event Action<LiveSignal>? SignalEmitted;
}

/// <summary>
/// A live signal owned by an active signal source (atom).
/// Dies when the source dies.
/// </summary>
public sealed class LiveSignal
{
    /// <summary>
    /// Signal key.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Signal value.
    /// </summary>
    public object? Value { get; init; }

    /// <summary>
    /// Salience score (0.0-1.0).
    /// </summary>
    public double Salience { get; init; } = 0.5;

    /// <summary>
    /// Source that owns this signal.
    /// </summary>
    public required ISignalSource Source { get; init; }

    /// <summary>
    /// When this signal was emitted.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Additional metadata.
    /// </summary>
    public IDictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Gets the value cast to the expected type.
    /// </summary>
    public T? GetValue<T>()
    {
        if (Value is null) return default;
        if (Value is T typed) return typed;
        try { return (T)Convert.ChangeType(Value, typeof(T)); }
        catch { return default; }
    }
}

/// <summary>
/// A coordinator that manages signal sources (atoms).
/// </summary>
public interface ISignalCoordinator
{
    /// <summary>
    /// Coordinator identifier.
    /// </summary>
    string CoordinatorId { get; }

    /// <summary>
    /// Gets all active signal sources in this coordinator.
    /// </summary>
    IReadOnlyList<ISignalSource> GetSources();

    /// <summary>
    /// Gets a source by name.
    /// </summary>
    ISignalSource? GetSource(string name);

    /// <summary>
    /// Gets all signals from all sources.
    /// </summary>
    IReadOnlyList<LiveSignal> GetAllSignals();

    /// <summary>
    /// Gets signals matching a pattern from all sources.
    /// </summary>
    IReadOnlyList<LiveSignal> GetSignals(string pattern);

    /// <summary>
    /// Checks if a signal exists in any source.
    /// </summary>
    bool HasSignal(string key);

    /// <summary>
    /// Event raised when any source emits a signal.
    /// </summary>
    event Action<LiveSignal>? SignalEmitted;
}
