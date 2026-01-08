# Breaking Changes in Ephemeral 2.0

This document describes breaking changes in version 2.0 and provides migration guidance.

## Removed: `SignalSink.SignalRaised` Event

### What Changed

The `SignalRaised` event has been removed from `SignalSink`. This event was marked `[Obsolete]` in version 1.x with the recommendation to use `Subscribe()` instead.

### Why

The event-based pattern had several issues:
1. **Memory leaks**: Event handlers that weren't properly unsubscribed could keep objects alive
2. **Performance**: Event invocation has overhead compared to direct delegate calls
3. **No unsubscribe guarantee**: The `+=` operator doesn't return a handle for cleanup
4. **Thread safety**: Event subscription/unsubscription has subtle race conditions

The `Subscribe()` method returns an `IDisposable` that guarantees clean unsubscription.

### Before (1.x)

```csharp
// Old pattern - event subscription
sink.SignalRaised += (signal) =>
{
    Console.WriteLine($"Signal: {signal.Name}");
};

// Problem: No clean way to unsubscribe
// Problem: Potential memory leak if object holds reference
```

### After (2.0)

```csharp
// New pattern - returns IDisposable for clean unsubscription
var subscription = sink.Subscribe((signal) =>
{
    Console.WriteLine($"Signal: {signal.Name}");
});

// Clean unsubscription when done
subscription.Dispose();

// Or use with 'using' for automatic cleanup
using var sub = sink.Subscribe(signal => ProcessSignal(signal));
```

### Migration Steps

1. **Find all usages** of `SignalRaised`:
   ```bash
   grep -r "SignalRaised" --include="*.cs"
   ```

2. **Replace event subscription** with `Subscribe()`:
   ```csharp
   // Before
   sink.SignalRaised += MyHandler;

   // After
   _subscription = sink.Subscribe(MyHandler);
   ```

3. **Store the subscription** if you need to unsubscribe later:
   ```csharp
   private IDisposable? _subscription;

   public void Start()
   {
       _subscription = sink.Subscribe(OnSignal);
   }

   public void Stop()
   {
       _subscription?.Dispose();
   }
   ```

4. **Use `using` for scoped subscriptions**:
   ```csharp
   public async Task ProcessAsync()
   {
       using var sub = sink.Subscribe(signal =>
       {
           // Handle signals during this scope
       });

       await DoWorkAsync();
       // Subscription automatically disposed
   }
   ```

### Handling Multiple Subscriptions

If you had multiple event handlers:

```csharp
// Before
sink.SignalRaised += Handler1;
sink.SignalRaised += Handler2;

// After
var subscriptions = new List<IDisposable>
{
    sink.Subscribe(Handler1),
    sink.Subscribe(Handler2)
};

// Cleanup
foreach (var sub in subscriptions)
    sub.Dispose();

// Or use CompositeDisposable pattern
```

### Test Migration

Tests using `SignalRaised` need updates:

```csharp
// Before
var received = new List<SignalEvent>();
sink.SignalRaised += received.Add;
sink.Raise("test");
Assert.Single(received);

// After
var received = new List<SignalEvent>();
using var sub = sink.Subscribe(received.Add);
sink.Raise("test");
Assert.Single(received);
```

## Other Changes

### Dependency Updates

All dependencies updated to latest stable versions:
- `System.Text.Json`: Framework-appropriate versions (8.0.5+ / 9.0.1+ / 10.0.0+)
- `Npgsql`: 8.0.5+ / 9.0.2+ / 10.0.0+
- `Microsoft.Data.Sqlite`: Framework-appropriate versions

### Minimum Framework Support

- .NET 8.0 (LTS)
- .NET 9.0
- .NET 10.0

.NET 6.0 and .NET 7.0 support has been removed as they are out of support.

## Version Compatibility Matrix

| Ephemeral Version | .NET 6 | .NET 7 | .NET 8 | .NET 9 | .NET 10 |
|-------------------|--------|--------|--------|--------|---------|
| 1.x               | ✅     | ✅     | ✅     | ✅     | ✅      |
| 2.0               | ❌     | ❌     | ✅     | ✅     | ✅      |

## Need Help?

If you encounter issues migrating to 2.0:
1. Check the [GitHub Issues](https://github.com/scottgal/mostlylucid.atoms/issues)
2. Review the [CHANGELOG.md](CHANGELOG.md) for detailed changes
3. See [SIGNALS_PATTERN.md](SIGNALS_PATTERN.md) for signal pattern best practices
