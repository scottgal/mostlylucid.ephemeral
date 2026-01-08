using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Mostlylucid.Ephemeral.Atoms.Taxonomy.Ledger;

/// <summary>
///     Default implementation of entity ledger for signal accumulation.
///     Thread-safe for concurrent atom execution.
/// </summary>
public sealed class EntityLedger : IEntityLedger
{
    private readonly ConcurrentDictionary<string, LedgerSignal> _signals = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public EntityLedger(string entityId, string entityType)
    {
        EntityId = entityId ?? throw new ArgumentNullException(nameof(entityId));
        EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public string EntityId { get; }
    public string EntityType { get; }
    public DateTimeOffset CreatedAt { get; }
    public int Count => _signals.Count;

    public void Record(LedgerSignal signal)
    {
        if (signal is null)
            throw new ArgumentNullException(nameof(signal));

        _signals[signal.Key] = signal;
    }

    public void Record(
        string key,
        object? value,
        double salience,
        string sourceAtom,
        string? sourceKind = null,
        IDictionary<string, object>? metadata = null)
    {
        var signal = new LedgerSignal
        {
            Key = key,
            Value = value,
            Salience = Math.Clamp(salience, 0.0, 1.0),
            SourceAtom = sourceAtom,
            SourceKind = sourceKind,
            Timestamp = DateTimeOffset.UtcNow,
            Metadata = metadata
        };

        Record(signal);
    }

    public bool HasSignal(string key)
    {
        return _signals.ContainsKey(key);
    }

    public bool HasSignal(string key, Func<LedgerSignal, bool> predicate)
    {
        if (_signals.TryGetValue(key, out var signal))
            return predicate(signal);

        return false;
    }

    public LedgerSignal? GetSignal(string key)
    {
        return _signals.TryGetValue(key, out var signal) ? signal : null;
    }

    public T? GetValue<T>(string key)
    {
        var signal = GetSignal(key);
        return signal is not null ? signal.GetValue<T>() : default;
    }

    public IReadOnlyList<LedgerSignal> GetSignals(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return Array.Empty<LedgerSignal>();

        var regex = GlobToRegex(pattern);
        return _signals.Values
            .Where(s => regex.IsMatch(s.Key))
            .OrderByDescending(s => s.Salience)
            .ThenByDescending(s => s.Timestamp)
            .ToList();
    }

    public IReadOnlyList<LedgerSignal> GetSignalsFromAtom(string atomName)
    {
        return _signals.Values
            .Where(s => s.SourceAtom.Equals(atomName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(s => s.Salience)
            .ThenByDescending(s => s.Timestamp)
            .ToList();
    }

    public IReadOnlyList<LedgerSignal> GetHighSalienceSignals(double threshold)
    {
        return _signals.Values
            .Where(s => s.Salience >= threshold)
            .OrderByDescending(s => s.Salience)
            .ThenByDescending(s => s.Timestamp)
            .ToList();
    }

    public IReadOnlyList<LedgerSignal> GetAllSignals()
    {
        return _signals.Values
            .OrderByDescending(s => s.Salience)
            .ThenByDescending(s => s.Timestamp)
            .ToList();
    }

    public ILedgerView CreateView(LedgerViewOptions? options = null)
    {
        return new LedgerView(this, options ?? new LedgerViewOptions());
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _signals.Clear();
    }

    private static Regex GlobToRegex(string pattern)
    {
        var escaped = Regex.Escape(pattern);
        var regexPattern = escaped
            .Replace(@"\*", ".*")
            .Replace(@"\?", ".");

        return new Regex($"^{regexPattern}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }
}

/// <summary>
///     A filtered view over an entity ledger.
/// </summary>
internal sealed class LedgerView : ILedgerView
{
    private readonly Regex? _patternRegex;

    public LedgerView(IEntityLedger ledger, LedgerViewOptions options)
    {
        Ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        Options = options ?? throw new ArgumentNullException(nameof(options));

        if (!string.IsNullOrWhiteSpace(options.Pattern))
        {
            var escaped = Regex.Escape(options.Pattern);
            var regexPattern = escaped
                .Replace(@"\*", ".*")
                .Replace(@"\?", ".");

            _patternRegex = new Regex($"^{regexPattern}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }
    }

    public IEntityLedger Ledger { get; }
    public LedgerViewOptions Options { get; }

    public int Count => GetSignals().Count;

    public IReadOnlyList<LedgerSignal> GetSignals()
    {
        var signals = Ledger.GetAllSignals().AsEnumerable();

        // Apply pattern filter
        if (_patternRegex is not null)
            signals = signals.Where(s => _patternRegex.IsMatch(s.Key));

        // Apply salience threshold
        if (Options.SalienceThreshold.HasValue)
            signals = signals.Where(s => s.Salience >= Options.SalienceThreshold.Value);

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

        // Apply max signals limit
        if (Options.MaxSignals.HasValue)
            signals = signals.Take(Options.MaxSignals.Value);

        return signals.ToList();
    }

    public bool HasSignal(string key)
    {
        var signal = GetSignal(key);
        return signal is not null;
    }

    public LedgerSignal? GetSignal(string key)
    {
        var signal = Ledger.GetSignal(key);
        if (signal is null)
            return null;

        // Check if signal passes view filter
        if (_patternRegex is not null && !_patternRegex.IsMatch(signal.Key))
            return null;

        if (Options.SalienceThreshold.HasValue && signal.Salience < Options.SalienceThreshold.Value)
            return null;

        if (Options.SourceAtoms is { Count: > 0 } &&
            !Options.SourceAtoms.Contains(signal.SourceAtom, StringComparer.OrdinalIgnoreCase))
            return null;

        if (Options.SourceKinds is { Count: > 0 } &&
            (signal.SourceKind is null ||
             !Options.SourceKinds.Contains(signal.SourceKind, StringComparer.OrdinalIgnoreCase)))
            return null;

        if (Options.Since.HasValue && signal.Timestamp < Options.Since.Value)
            return null;

        return signal;
    }
}

/// <summary>
///     Default factory for entity ledgers.
/// </summary>
public sealed class EntityLedgerFactory : IEntityLedgerFactory
{
    public IEntityLedger Create(string entityId, string entityType)
    {
        return new EntityLedger(entityId, entityType);
    }

    public IEntityLedger Create(string entityType)
    {
        var entityId = $"{entityType}_{Guid.NewGuid():N}";
        return new EntityLedger(entityId, entityType);
    }
}