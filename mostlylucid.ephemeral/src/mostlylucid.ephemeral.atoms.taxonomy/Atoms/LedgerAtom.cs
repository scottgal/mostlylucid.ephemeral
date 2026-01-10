using Mostlylucid.Ephemeral.Atoms.Taxonomy.Ledger;
using Mostlylucid.Ephemeral.Atoms.Taxonomy.Signals;
using SignalView = Mostlylucid.Ephemeral.Atoms.Taxonomy.Signals.SignalView;

namespace Mostlylucid.Ephemeral.Atoms.Taxonomy.Atoms;

/// <summary>
///     An atom that bridges live signals to persistent EntityLedger.
///     Listens to SignalViews and escalates high-salience signals to storage.
/// </summary>
/// <remarks>
///     **Signal Flow:**
///     ```
///     Atoms → SignalView → LedgerAtom → EntityLedger → RDBMS + Vector
///     ↑
///     (applies salience threshold)
///     (batches for efficiency)
///     (generates embeddings)
///     ```
///     The system decides what's important:
///     - High salience → escalate to EntityLedger (persisted)
///     - Low salience → ephemeral (dies with source atom)
///     Signals don't care about their fate - the system chooses.
/// </remarks>
public sealed class LedgerAtom : Signals.ISignalSource, IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly SignalView _inputView;
    private readonly ILedgerStore _ledgerStore;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly LedgerAtomOptions _options;
    private readonly List<LiveSignal> _pendingEscalation = new();
    private readonly Dictionary<string, LiveSignal> _signals = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILedgerVectorStore? _vectorStore;
    private bool _disposed;
    private Task? _escalationTask;

    public LedgerAtom(
        SignalView inputView,
        ILedgerStore ledgerStore,
        ILedgerVectorStore? vectorStore = null,
        LedgerAtomOptions? options = null)
    {
        _inputView = inputView ?? throw new ArgumentNullException(nameof(inputView));
        _ledgerStore = ledgerStore ?? throw new ArgumentNullException(nameof(ledgerStore));
        _vectorStore = vectorStore;
        _options = options ?? new LedgerAtomOptions();

        SourceId = $"ledger-{Guid.NewGuid():N}";
        Name = _options.Name ?? "LedgerAtom";
        Kind = "escalator";
        IsActive = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        IsActive = false;

        _cts.Cancel();

        if (_escalationTask is not null)
            try
            {
                await _escalationTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

        _cts.Dispose();
        _lock.Dispose();
    }

    public string SourceId { get; }
    public string Name { get; }
    public string Kind { get; }
    public bool IsActive { get; private set; }

    public event Action<LiveSignal>? SignalEmitted;

    public IReadOnlyList<LiveSignal> GetSignals()
    {
        lock (_signals)
        {
            return _signals.Values.ToList();
        }
    }

    public IReadOnlyList<LiveSignal> GetSignals(string pattern)
    {
        // Simple implementation - could use regex
        lock (_signals)
        {
            return _signals.Values
                .Where(s => s.Key.Contains(pattern.Replace("*", "").Replace("?", ""),
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    public bool HasSignal(string key)
    {
        lock (_signals)
        {
            return _signals.ContainsKey(key);
        }
    }

    public LiveSignal? GetSignal(string key)
    {
        lock (_signals)
        {
            return _signals.TryGetValue(key, out var signal) ? signal : null;
        }
    }

    /// <summary>
    ///     Starts the escalation process - monitors the input view and escalates signals.
    /// </summary>
    public void Start()
    {
        if (_escalationTask is not null)
            return;

        _escalationTask = RunEscalationLoopAsync(_cts.Token);
    }

    /// <summary>
    ///     Manually escalates signals from the input view to the specified entity ledger.
    /// </summary>
    public async Task EscalateAsync(string entityId, string entityType, CancellationToken ct = default)
    {
        var candidates = _inputView.GetEscalationCandidates(_options.SalienceThreshold);

        if (candidates.Count == 0)
            return;

        var ledger = new EntityLedger(entityId, entityType);

        foreach (var signal in candidates)
            ledger.Record(
                signal.Key,
                signal.Value,
                signal.Salience,
                signal.Source.Name,
                signal.Source.Kind,
                signal.Metadata);

        var saveOptions = new LedgerSaveOptions
        {
            SalienceThreshold = _options.SalienceThreshold,
            Overwrite = false
        };

        await _ledgerStore.SaveAsync(ledger, saveOptions, ct);

        // Emit escalation signal
        EmitSignal($"escalation.complete.{entityId}", new
        {
            EntityId = entityId,
            EntityType = entityType,
            SignalCount = candidates.Count,
            Timestamp = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    ///     Escalates signals and generates vector embeddings.
    /// </summary>
    public async Task EscalateWithVectorsAsync(
        string entityId,
        string entityType,
        IEnumerable<EntityVector> vectors,
        CancellationToken ct = default)
    {
        var candidates = _inputView.GetEscalationCandidates(_options.SalienceThreshold);

        if (candidates.Count == 0 && !vectors.Any())
            return;

        var ledger = new EntityLedger(entityId, entityType);

        foreach (var signal in candidates)
            ledger.Record(
                signal.Key,
                signal.Value,
                signal.Salience,
                signal.Source.Name,
                signal.Source.Kind,
                signal.Metadata);

        var saveOptions = new LedgerSaveOptions
        {
            SalienceThreshold = _options.SalienceThreshold,
            Overwrite = false,
            Vectors = vectors.ToList()
        };

        await _ledgerStore.SaveAsync(ledger, saveOptions, ct);

        // Store vectors separately if vector store is available
        if (_vectorStore is not null) await _vectorStore.StoreVectorsAsync(entityId, vectors, ct);

        EmitSignal($"escalation.complete.{entityId}", new
        {
            EntityId = entityId,
            EntityType = entityType,
            SignalCount = candidates.Count,
            VectorCount = vectors.Count(),
            Timestamp = DateTimeOffset.UtcNow
        });
    }

    private async Task RunEscalationLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
            try
            {
                await Task.Delay(_options.EscalationInterval, ct);

                // Check for signals above threshold
                var candidates = _inputView.GetEscalationCandidates(_options.SalienceThreshold);

                if (candidates.Count > 0)
                    EmitSignal("escalation.candidates.found", new
                    {
                        candidates.Count,
                        MaxSalience = candidates.Max(c => c.Salience),
                        Timestamp = DateTimeOffset.UtcNow
                    });
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                EmitSignal("escalation.error", new
                {
                    Error = ex.Message,
                    Timestamp = DateTimeOffset.UtcNow
                });
            }
    }

    private void EmitSignal(string key, object? value)
    {
        var signal = new LiveSignal
        {
            Key = key,
            Value = value,
            Salience = 0.5,
            Source = this,
            Timestamp = DateTimeOffset.UtcNow
        };

        lock (_signals)
        {
            _signals[key] = signal;
        }

        SignalEmitted?.Invoke(signal);
    }
}

/// <summary>
///     Options for configuring a LedgerAtom.
/// </summary>
public sealed class LedgerAtomOptions
{
    /// <summary>
    ///     Atom name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    ///     Minimum salience for escalation (signals below this are ephemeral).
    /// </summary>
    public double SalienceThreshold { get; init; } = 0.8;

    /// <summary>
    ///     How often to check for escalation candidates.
    /// </summary>
    public TimeSpan EscalationInterval { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     Maximum signals to batch before forcing escalation.
    /// </summary>
    public int BatchSize { get; init; } = 100;

    /// <summary>
    ///     Whether to auto-generate signal embeddings.
    /// </summary>
    public bool GenerateSignalEmbeddings { get; init; } = false;
}