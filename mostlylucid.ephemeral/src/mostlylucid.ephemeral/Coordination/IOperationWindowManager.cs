namespace Mostlylucid.Ephemeral;

/// <summary>
///     Interface for coordinators that support dynamic adjustment of operation window settings.
///     Allows runtime tuning of MaxTrackedOperations and MaxOperationLifetime without recreating the coordinator.
/// </summary>
public interface IOperationWindowManager
{
    /// <summary>
    ///     Current maximum number of tracked operations.
    /// </summary>
    int CurrentMaxTrackedOperations { get; }

    /// <summary>
    ///     Current maximum operation lifetime.
    /// </summary>
    TimeSpan? CurrentMaxOperationLifetime { get; }

    /// <summary>
    ///     Dynamically adjust the maximum number of tracked operations.
    ///     Changes take effect on the next cleanup cycle.
    /// </summary>
    /// <param name="maxTrackedOperations">New maximum (must be > 0).</param>
    void AdjustMaxTrackedOperations(int maxTrackedOperations);

    /// <summary>
    ///     Dynamically adjust the maximum operation lifetime.
    ///     Changes take effect on the next cleanup cycle.
    /// </summary>
    /// <param name="maxOperationLifetime">New maximum lifetime (null for unlimited).</param>
    void AdjustMaxOperationLifetime(TimeSpan? maxOperationLifetime);
}
