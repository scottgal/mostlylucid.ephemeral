using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Mostlylucid.Ephemeral.Atoms.Taxonomy.Ledger;

/// <summary>
/// A view over LIVE signals from Atoms running in Coordinators.
/// Views are lightweight query interfaces with NO separate lifetime - they don't own signals.
/// </summary>
/// <remarks>
/// **IMPORTANT DISTINCTION:**
/// - SignalView operates on LIVE signals from Atoms in Coordinators
/// - EntityLedger is a SEPARATE persisted entity (RDBMS + Vector)
/// - Signals flow: Atoms → SignalView → (escalation) → LedgerAtom → EntityLedger
///
/// Key principles:
/// - Views are QUERIES over live atom signals, not containers
/// - Views have no separate lifetime (signals die with atoms)
/// - Views can span multiple coordinators (for cross-coordinator visibility)
/// - Views apply filters (pattern, salience, source, time)
/// - High-salience signals can be ESCALATED to EntityLedger via LedgerAtom
/// </remarks>
public sealed class SignalView
{
    private readonly List<IEntityLedger> _ledgers = new();
    private readonly Regex? _patternRegex;

    /// <summary>
    /// Creates a view over a single ledger.
    /// </summary>
    public SignalView(IEntityLedger ledger, SignalViewOptions? options = null)
        : this(new[] { ledger }, options)
    {
    }

    /// <summary>
    /// Creates a view over multiple ledgers.
    /// </summary>
    public SignalView(IEnumerable<IEntityLedger> ledgers, SignalViewOptions? options = null)
    {
        if (ledgers is null)
            throw new ArgumentNullException(nameof(ledgers));

        _ledgers.AddRange(ledgers);
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
    /// View name for identification.
    /// </summary>
    public string Name => Options.Name ?? "unnamed";

    /// <summary>
    /// View configuration.
    /// </summary>
    public SignalViewOptions Options { get; }

    /// <summary>
    /// Number of ledgers this view covers.
    /// </summary>
    public int LedgerCount => _ledgers.Count;

    /// <summary>
    /// Adds a ledger to this view.
    /// </summary>
    public void AddLedger(IEntityLedger ledger)
    {
        if (ledger is null)
            throw new ArgumentNullException(nameof(ledger));

        lock (_ledgers)
        {
            if (!_ledgers.Contains(ledger))
                _ledgers.Add(ledger);
        }
    }

    /// <summary>
    /// Removes a ledger from this view.
    /// </summary>
    public bool RemoveLedger(IEntityLedger ledger)
    {
        lock (_ledgers)
        {
            return _ledgers.Remove(ledger);
        }
    }

    /// <summary>
    /// Gets all signals matching the view filter.
    /// </summary>
    public IReadOnlyList<LedgerSignal> GetSignals()
    {
        List<IEntityLedger> snapshot;
        lock (_ledgers)
        {
            snapshot = new List<IEntityLedger>(_ledgers);
        }

        var allSignals = snapshot
            .SelectMany(l => l.GetAllSignals())
            .AsEnumerable();

        return ApplyFilters(allSignals).ToList();
    }

    /// <summary>
    /// Gets signals matching a specific pattern (overrides view pattern).
    /// </summary>
    public IReadOnlyList<LedgerSignal> GetSignals(string pattern)
    {
        List<IEntityLedger> snapshot;
        lock (_ledgers)
        {
            snapshot = new List<IEntityLedger>(_ledgers);
        }

        var allSignals = snapshot
            .SelectMany(l => l.GetSignals(pattern))
            .AsEnumerable();

        // Apply other filters (not pattern - already applied)
        return ApplyFiltersExceptPattern(allSignals).ToList();
    }

    /// <summary>
    /// Checks if a signal exists in this view.
    /// </summary>
    public bool HasSignal(string key)
    {
        return GetSignal(key) is not null;
    }

    /// <summary>
    /// Gets a signal by key (if it passes the view filter).
    /// </summary>
    public LedgerSignal? GetSignal(string key)
    {
        List<IEntityLedger> snapshot;
        lock (_ledgers)
        {
            snapshot = new List<IEntityLedger>(_ledgers);
        }

        foreach (var ledger in snapshot)
        {
            var signal = ledger.GetSignal(key);
            if (signal is not null && PassesFilter(signal))
                return signal;
        }

        return null;
    }

    /// <summary>
    /// Gets the value of a signal.
    /// </summary>
    public T? GetValue<T>(string key)
    {
        var signal = GetSignal(key);
        return signal is not null ? signal.GetValue<T>() : default;
    }

    /// <summary>
    /// Gets high-salience signals from this view.
    /// </summary>
    public IReadOnlyList<LedgerSignal> GetHighSalienceSignals(double threshold)
    {
        var effectiveThreshold = Options.SalienceThreshold.HasValue
            ? Math.Max(threshold, Options.SalienceThreshold.Value)
            : threshold;

        List<IEntityLedger> snapshot;
        lock (_ledgers)
        {
            snapshot = new List<IEntityLedger>(_ledgers);
        }

        var allSignals = snapshot
            .SelectMany(l => l.GetHighSalienceSignals(effectiveThreshold))
            .AsEnumerable();

        return ApplyFiltersExceptSalience(allSignals).ToList();
    }

    /// <summary>
    /// Number of signals visible in this view.
    /// </summary>
    public int Count => GetSignals().Count;

    /// <summary>
    /// Creates a derived view with additional filters.
    /// </summary>
    public SignalView Derive(SignalViewOptions additionalOptions)
    {
        var merged = new SignalViewOptions
        {
            Name = additionalOptions.Name ?? Options.Name,
            Pattern = additionalOptions.Pattern ?? Options.Pattern,
            SalienceThreshold = additionalOptions.SalienceThreshold ?? Options.SalienceThreshold,
            SourceAtoms = MergeLists(Options.SourceAtoms, additionalOptions.SourceAtoms),
            SourceKinds = MergeLists(Options.SourceKinds, additionalOptions.SourceKinds),
            MaxSignals = additionalOptions.MaxSignals ?? Options.MaxSignals,
            Since = additionalOptions.Since ?? Options.Since
        };

        List<IEntityLedger> snapshot;
        lock (_ledgers)
        {
            snapshot = new List<IEntityLedger>(_ledgers);
        }

        return new SignalView(snapshot, merged);
    }

    private IEnumerable<LedgerSignal> ApplyFilters(IEnumerable<LedgerSignal> signals)
    {
        // Apply pattern filter
        if (_patternRegex is not null)
            signals = signals.Where(s => _patternRegex.IsMatch(s.Key));

        return ApplyFiltersExceptPattern(signals);
    }

    private IEnumerable<LedgerSignal> ApplyFiltersExceptPattern(IEnumerable<LedgerSignal> signals)
    {
        // Apply salience threshold
        if (Options.SalienceThreshold.HasValue)
            signals = signals.Where(s => s.Salience >= Options.SalienceThreshold.Value);

        return ApplyFiltersExceptSalience(signals);
    }

    private IEnumerable<LedgerSignal> ApplyFiltersExceptSalience(IEnumerable<LedgerSignal> signals)
    {
        // Apply source atom filter
        if (Options.SourceAtoms is { Count: > 0 })
        {
            var atomSet = new HashSet<string>(Options.SourceAtoms, StringComparer.OrdinalIgnoreCase);
            signals = signals.Where(s => atomSet.Contains(s.SourceAtom));
        }

        // Apply source kind filter
        if (Options.SourceKinds is { Count: > 0 })
        {
            var kindSet = new HashSet<string>(Options.SourceKinds, StringComparer.OrdinalIgnoreCase);
            signals = signals.Where(s => s.SourceKind is not null && kindSet.Contains(s.SourceKind));
        }

        // Apply time filter
        if (Options.Since.HasValue)
            signals = signals.Where(s => s.Timestamp >= Options.Since.Value);

        // Order by salience then timestamp
        signals = signals
            .OrderByDescending(s => s.Salience)
            .ThenByDescending(s => s.Timestamp);

        // Apply max signals limit
        if (Options.MaxSignals.HasValue)
            signals = signals.Take(Options.MaxSignals.Value);

        return signals;
    }

    private bool PassesFilter(LedgerSignal signal)
    {
        if (_patternRegex is not null && !_patternRegex.IsMatch(signal.Key))
            return false;

        if (Options.SalienceThreshold.HasValue && signal.Salience < Options.SalienceThreshold.Value)
            return false;

        if (Options.SourceAtoms is { Count: > 0 } &&
            !Options.SourceAtoms.Contains(signal.SourceAtom, StringComparer.OrdinalIgnoreCase))
            return false;

        if (Options.SourceKinds is { Count: > 0 } &&
            (signal.SourceKind is null || !Options.SourceKinds.Contains(signal.SourceKind, StringComparer.OrdinalIgnoreCase)))
            return false;

        if (Options.Since.HasValue && signal.Timestamp < Options.Since.Value)
            return false;

        return true;
    }

    private static IReadOnlyList<string>? MergeLists(IReadOnlyList<string>? a, IReadOnlyList<string>? b)
    {
        if (a is null || a.Count == 0)
            return b;
        if (b is null || b.Count == 0)
            return a;

        var merged = new HashSet<string>(a, StringComparer.OrdinalIgnoreCase);
        foreach (var item in b)
            merged.Add(item);

        return merged.ToList();
    }
}

/// <summary>
/// Options for configuring a signal view.
/// </summary>
public sealed class SignalViewOptions
{
    /// <summary>
    /// View name for identification.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Pattern filter for signal keys (glob-style: *, ?).
    /// </summary>
    public string? Pattern { get; init; }

    /// <summary>
    /// Minimum salience threshold for signals in this view.
    /// </summary>
    public double? SalienceThreshold { get; init; }

    /// <summary>
    /// Only include signals from these atoms.
    /// </summary>
    public IReadOnlyList<string>? SourceAtoms { get; init; }

    /// <summary>
    /// Only include signals of these kinds.
    /// </summary>
    public IReadOnlyList<string>? SourceKinds { get; init; }

    /// <summary>
    /// Maximum number of signals in the view.
    /// </summary>
    public int? MaxSignals { get; init; }

    /// <summary>
    /// Only include signals newer than this.
    /// </summary>
    public DateTimeOffset? Since { get; init; }

    /// <summary>
    /// Creates a view for fast-path signals (high priority, sensors only).
    /// </summary>
    public static SignalViewOptions FastPath => new()
    {
        Name = "fast-path",
        SourceKinds = new[] { "sensor" },
        SalienceThreshold = 0.0
    };

    /// <summary>
    /// Creates a view for learning signals (high salience only).
    /// </summary>
    public static SignalViewOptions Learning => new()
    {
        Name = "learning",
        SalienceThreshold = 0.7
    };

    /// <summary>
    /// Creates a view for escalation candidates (very high salience).
    /// </summary>
    public static SignalViewOptions Escalation => new()
    {
        Name = "escalation",
        SalienceThreshold = 0.85
    };

    /// <summary>
    /// Creates a view for all signals (no filtering).
    /// </summary>
    public static SignalViewOptions All => new()
    {
        Name = "all"
    };
}
