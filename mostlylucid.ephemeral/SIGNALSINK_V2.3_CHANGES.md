# SignalSink Changes in v2.3+

## Summary

SignalSink is now a **readonly view** onto coordinator signals. All lifecycle management moved to coordinators.

## Breaking Changes (Mitigated)

**None!** All changes are backward-compatible with obsolete warnings.

### Obsolete APIs

```csharp
// ❌ OBSOLETE (still works, but no-op)
var sink = new SignalSink(maxCapacity: 1000, maxAge: TimeSpan.FromMinutes(1));
sink.UpdateWindowSize(2000, TimeSpan.FromMinutes(5));
var capacity = sink.MaxCapacity;  // Returns 0
var age = sink.MaxAge;             // Returns TimeSpan.Zero

// ✅ CORRECT (new approach)
var sink = new SignalSink();  // Parameterless constructor
```

## Migration Guide

### Old Pattern (v2.2 and earlier)

```csharp
// SignalSink managed signal lifetime
var sink = new SignalSink(
    maxCapacity: 1000,
    maxAge: TimeSpan.FromMinutes(5)
);

var coordinator = new EphemeralWorkCoordinator<T>(
    body,
    new EphemeralOptions { Signals = sink }
);
```

### New Pattern (v2.3+)

```csharp
// Coordinators manage signal lifetime
var sink = new SignalSink();  // No params

var coordinator = new EphemeralWorkCoordinator<T>(
    body,
    new EphemeralOptions
    {
        Signals = sink,
        MaxTrackedOperations = 1000,           // Replaces sink maxCapacity
        MaxOperationLifetime = TimeSpan.FromMinutes(5)  // Replaces sink maxAge
    }
);
```

## Key Concepts

### 1. SignalSink is a View

```csharp
// SignalSink NO LONGER:
// - Performs automatic cleanup
// - Enforces capacity limits
// - Evicts aged signals

// SignalSink NOW:
// - Stores all signals until manually cleared
// - Acts as event bus and query surface
// - Provides readonly view across coordinators
```

### 2. Coordinators Control Lifetime

When a coordinator evicts an operation (due to `MaxTrackedOperations` or `MaxOperationLifetime`):
- The operation can no longer emit new signals
- Signals ALREADY in the sink **remain** until manually cleared

This means SignalSink acts as **persistent signal history** across coordinator lifecycles.

### 3. Manual Cleanup

```csharp
var sink = new SignalSink();

// Clear all signals
sink.Clear();

// Clear by pattern
sink.ClearPattern("error.*");

// Clear by operation
sink.ClearOperation(operationId);

// Clear by key
sink.ClearKey("entity-123");
```

## ValueRef Pattern (NEW)

Avoid boxing and keep signals lightweight:

```csharp
// OLD (causes boxing for value types)
ledger.Record("score", 42, salience: 0.8, atom: "scorer");

// NEW (no boxing)
await cache.SetAsync("score-123", 42);
ledger.RecordRef("score", "cache://score-123", salience: 0.8, atom: "scorer");
```

### LedgerSignal Properties

```csharp
public sealed class LedgerSignal
{
    public string Key { get; }
    public object? Value { get; }      // Existing - may cause boxing
    public string? ValueRef { get; }   // NEW - reference to external storage
    public double Salience { get; }
    // ...
}
```

### Best Practices

1. **Store large data externally** (cache, blob storage, database)
2. **Signal the location** via `ValueRef` or `Key`
3. **Keep signals lightweight** - they're coordination, not transport
4. **Use URIs** for references: `"cache://key"`, `"blob://container/file"`, `"db://table/id"`

## Example: Image Processing

```csharp
// BAD: Carrying large data in signals (causes boxing, memory pressure)
var imageBytes = await ProcessImageAsync(input);
ledger.Record("image.processed", imageBytes, 0.9, "processor");

// GOOD: Store externally, signal the location
var imageBytes = await ProcessImageAsync(input);
var cacheKey = $"processed/{Guid.NewGuid()}";
await cache.SetAsync(cacheKey, imageBytes);
ledger.RecordRef("image.processed", $"cache://{cacheKey}", 0.9, "processor");

// Later: Retrieve when needed
if (ledger.GetSignal("image.processed") is { } signal && signal.ValueRef is { } ref)
{
    var bytes = await cache.GetAsync<byte[]>(ref.Replace("cache://", ""));
}
```

## Documentation Updates Needed

The following files reference obsolete SignalSink APIs and need updates:

- [x] `docs/SignalSink-Lifetime.md` - Partially updated, needs complete pass
- [ ] `docs/Correlation-Architecture.md` - Update SignalSink construction examples
- [ ] All README.md files with SignalSink examples

## See Also

- Commit: `26f97e6` - SignalSink refactoring
- Commit: `0ed8a15` - ValueRef and RecordRef added
