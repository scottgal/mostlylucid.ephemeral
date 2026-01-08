using System.Text.RegularExpressions;

namespace Mostlylucid.Ephemeral.Atoms.Taxonomy.Signals;

/// <summary>
///     A view over LIVE signals from Atoms running in Coordinators.
///     Views are lightweight query interfaces with NO separate lifetime.
/// </summary>
/// <remarks>
///     **Signal Flow:**
///     ```
///     Atoms (own signals) → SignalView (queries live signals)
///     ↓
///     (escalation via LedgerAtom)
///     ↓
///     EntityLedger (persisted to RDBMS + Vector)
///     ```
///     Key principles:
///     - Views query LIVE signals from atoms in coordinators
///     - Views have no separate lifetime (signals die with atoms)
///     - Views can span multiple coordinators
///     - High-salience signals can be ESCALATED to EntityLedger via LedgerAtom
/// </remarks>
public sealed class SignalView
{
    private readonly List<ISignalCoordinator> _coordinators = new();
    private readonly Regex? _patternRegex;

    /// <summary>
    ///     Creates a view over a single coordinator.
    /// </summary>
    public SignalView(ISignalCoordinator coordinator, SignalViewOptions? options = null)
        : this(new[] { coordinator }, options)
    {
    }

    /// <summary>
    ///     Creates a view over multiple coordinators.
    /// </summary>
    public SignalView(IEnumerable<ISignalCoordinator> coordinators, SignalViewOptions? options = null)
    {
        if (coordinators is null)
            throw new ArgumentNullException(nameof(coordinators));

        _coordinators.AddRange(coordinators);
        Options = options ?? new SignalViewOptions();

        if (!string.IsNullOrWhiteSpace(Options.Pattern))
        {
            var escaped = Regex.Escape(Options.Pattern);
            var regexPattern = escaped
                .Replace(@"\*", ".*")
                .Replace(@"\?", ".");

            _patternRegex = new Regex($"^{regexPattern}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }
    }

    /// <summary>
    ///     View name for identification.
    /// </summary>
    public string Name => Options.Name ?? "unnamed";

    /// <summary>
    ///     View configuration.
    /// </summary>
    public SignalViewOptions Options { get; }

    /// <summary>
    ///     Number of coordinators this view covers.
    /// </summary>
    public int CoordinatorCount => _coordinators.Count;

    /// <summary>
    ///     Number of live signals visible in this view.
    /// </summary>
    public int Count => GetSignals().Count;

    /// <summary>
    ///     Adds a coordinator to this view.
    /// </summary>
    public void AddCoordinator(ISignalCoordinator coordinator)
    {
        if (coordinator is null)
            throw new ArgumentNullException(nameof(coordinator));

        lock (_coordinators)
        {
            if (!_coordinators.Contains(coordinator))
                _coordinators.Add(coordinator);
        }
    }

    /// <summary>
    ///     Removes a coordinator from this view.
    /// </summary>
    public bool RemoveCoordinator(ISignalCoordinator coordinator)
    {
        lock (_coordinators)
        {
            return _coordinators.Remove(coordinator);
        }
    }

    /// <summary>
    ///     Gets all live signals matching the view filter.
    /// </summary>
    public IReadOnlyList<LiveSignal> GetSignals()
    {
        List<ISignalCoordinator> snapshot;
        lock (_coordinators)
        {
            snapshot = new List<ISignalCoordinator>(_coordinators);
        }

        var allSignals = snapshot
            .SelectMany(c => c.GetAllSignals())
            .Where(s => s.Source.IsActive) // Only live signals
            .AsEnumerable();

        return ApplyFilters(allSignals).ToList();
    }

    /// <summary>
    ///     Gets signals matching a specific pattern (overrides view pattern).
    /// </summary>
    public IReadOnlyList<LiveSignal> GetSignals(string pattern)
    {
        List<ISignalCoordinator> snapshot;
        lock (_coordinators)
        {
            snapshot = new List<ISignalCoordinator>(_coordinators);
        }

        var allSignals = snapshot
            .SelectMany(c => c.GetSignals(pattern))
            .Where(s => s.Source.IsActive)
            .AsEnumerable();

        return ApplyFiltersExceptPattern(allSignals).ToList();
    }

    /// <summary>
    ///     Checks if a signal exists in this view.
    /// </summary>
    public bool HasSignal(string key)
    {
        return GetSignal(key) is not null;
    }

    /// <summary>
    ///     Gets a signal by key (if it passes the view filter and is live).
    /// </summary>
    public LiveSignal? GetSignal(string key)
    {
        List<ISignalCoordinator> snapshot;
        lock (_coordinators)
        {
            snapshot = new List<ISignalCoordinator>(_coordinators);
        }

        foreach (var coordinator in snapshot)
        foreach (var source in coordinator.GetSources())
        {
            if (!source.IsActive)
                continue;

            var signal = source.GetSignal(key);
            if (signal is not null && PassesFilter(signal))
                return signal;
        }

        return null;
    }

    /// <summary>
    ///     Gets the value of a signal.
    /// </summary>
    public T? GetValue<T>(string key)
    {
        var signal = GetSignal(key);
        return signal is not null ? signal.GetValue<T>() : default;
    }

    /// <summary>
    ///     Gets high-salience signals from this view (candidates for escalation).
    /// </summary>
    public IReadOnlyList<LiveSignal> GetEscalationCandidates(double threshold = 0.8)
    {
        var effectiveThreshold = Options.SalienceThreshold.HasValue
            ? Math.Max(threshold, Options.SalienceThreshold.Value)
            : threshold;

        return GetSignals()
            .Where(s => s.Salience >= effectiveThreshold)
            .OrderByDescending(s => s.Salience)
            .ToList();
    }

    /// <summary>
    ///     Creates a derived view with additional filters.
    /// </summary>
    public SignalView Derive(SignalViewOptions additionalOptions)
    {
        var merged = new SignalViewOptions
        {
            Name = additionalOptions.Name ?? Options.Name,
            Pattern = additionalOptions.Pattern ?? Options.Pattern,
            SalienceThreshold = additionalOptions.SalienceThreshold ?? Options.SalienceThreshold,
            SourceNames = MergeLists(Options.SourceNames, additionalOptions.SourceNames),
            SourceKinds = MergeLists(Options.SourceKinds, additionalOptions.SourceKinds),
            MaxSignals = additionalOptions.MaxSignals ?? Options.MaxSignals,
            Since = additionalOptions.Since ?? Options.Since
        };

        List<ISignalCoordinator> snapshot;
        lock (_coordinators)
        {
            snapshot = new List<ISignalCoordinator>(_coordinators);
        }

        return new SignalView(snapshot, merged);
    }

    private IEnumerable<LiveSignal> ApplyFilters(IEnumerable<LiveSignal> signals)
    {
        if (_patternRegex is not null)
            signals = signals.Where(s => _patternRegex.IsMatch(s.Key));

        return ApplyFiltersExceptPattern(signals);
    }

    private IEnumerable<LiveSignal> ApplyFiltersExceptPattern(IEnumerable<LiveSignal> signals)
    {
        if (Options.SalienceThreshold.HasValue)
            signals = signals.Where(s => s.Salience >= Options.SalienceThreshold.Value);

        if (Options.SourceNames is { Count: > 0 })
        {
            var nameSet = new HashSet<string>(Options.SourceNames, StringComparer.OrdinalIgnoreCase);
            signals = signals.Where(s => nameSet.Contains(s.Source.Name));
        }

        if (Options.SourceKinds is { Count: > 0 })
        {
            var kindSet = new HashSet<string>(Options.SourceKinds, StringComparer.OrdinalIgnoreCase);
            signals = signals.Where(s => kindSet.Contains(s.Source.Kind));
        }

        if (Options.Since.HasValue)
            signals = signals.Where(s => s.Timestamp >= Options.Since.Value);

        signals = signals
            .OrderByDescending(s => s.Salience)
            .ThenByDescending(s => s.Timestamp);

        if (Options.MaxSignals.HasValue)
            signals = signals.Take(Options.MaxSignals.Value);

        return signals;
    }

    private bool PassesFilter(LiveSignal signal)
    {
        if (_patternRegex is not null && !_patternRegex.IsMatch(signal.Key))
            return false;

        if (Options.SalienceThreshold.HasValue && signal.Salience < Options.SalienceThreshold.Value)
            return false;

        if (Options.SourceNames is { Count: > 0 } &&
            !Options.SourceNames.Contains(signal.Source.Name, StringComparer.OrdinalIgnoreCase))
            return false;

        if (Options.SourceKinds is { Count: > 0 } &&
            !Options.SourceKinds.Contains(signal.Source.Kind, StringComparer.OrdinalIgnoreCase))
            return false;

        if (Options.Since.HasValue && signal.Timestamp < Options.Since.Value)
            return false;

        return true;
    }

    private static IReadOnlyList<string>? MergeLists(IReadOnlyList<string>? a, IReadOnlyList<string>? b)
    {
        if (a is null || a.Count == 0) return b;
        if (b is null || b.Count == 0) return a;

        var merged = new HashSet<string>(a, StringComparer.OrdinalIgnoreCase);
        foreach (var item in b) merged.Add(item);
        return merged.ToList();
    }
}

/// <summary>
///     Options for configuring a signal view over live signals.
/// </summary>
public sealed class SignalViewOptions
{
    /// <summary>
    ///     View name for identification.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    ///     Pattern filter for signal keys (glob-style: *, ?).
    /// </summary>
    public string? Pattern { get; init; }

    /// <summary>
    ///     Minimum salience threshold.
    /// </summary>
    public double? SalienceThreshold { get; init; }

    /// <summary>
    ///     Only include signals from sources with these names.
    /// </summary>
    public IReadOnlyList<string>? SourceNames { get; init; }

    /// <summary>
    ///     Only include signals from sources of these kinds.
    /// </summary>
    public IReadOnlyList<string>? SourceKinds { get; init; }

    /// <summary>
    ///     Maximum number of signals.
    /// </summary>
    public int? MaxSignals { get; init; }

    /// <summary>
    ///     Only include signals newer than this.
    /// </summary>
    public DateTimeOffset? Since { get; init; }

    /// <summary>
    ///     Fast-path view (sensors only).
    /// </summary>
    public static SignalViewOptions FastPath => new()
    {
        Name = "fast-path",
        SourceKinds = new[] { "sensor" }
    };

    /// <summary>
    ///     Learning view (high salience only).
    /// </summary>
    public static SignalViewOptions Learning => new()
    {
        Name = "learning",
        SalienceThreshold = 0.7
    };

    /// <summary>
    ///     Escalation view (very high salience).
    /// </summary>
    public static SignalViewOptions Escalation => new()
    {
        Name = "escalation",
        SalienceThreshold = 0.85
    };

    /// <summary>
    ///     All signals view.
    /// </summary>
    public static SignalViewOptions All => new()
    {
        Name = "all"
    };
}