namespace ExpressRecipe.Messaging.Saga.Abstractions;

/// <summary>
/// Provides high-throughput, batched persistence for saga state objects.
/// Implementors use raw ADO.NET for maximum performance.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
public interface ISagaBatchRepository<TState> where TState : class, ISagaState
{
    /// <summary>
    /// Loads a single saga state by correlation ID. Returns null if not found.
    /// </summary>
    Task<TState?> LoadAsync(string correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads multiple saga states by correlation ID in a single round-trip.
    /// </summary>
    Task<IReadOnlyList<TState>> BatchLoadAsync(IEnumerable<string> correlationIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new saga state record (INSERT).
    /// </summary>
    Task SaveAsync(TState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically OR-sets bits into CurrentMask for multiple saga instances.
    /// Returns the updated states after the operation.
    /// </summary>
    Task<IReadOnlyList<TState>> BatchUpdateMaskAsync(
        IEnumerable<(string CorrelationId, long MaskToAdd)> updates,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks one or more sagas as completed or failed in a single batch.
    /// </summary>
    Task BatchUpdateStatusAsync(
        IEnumerable<(string CorrelationId, SagaStatus Status, DateTimeOffset? CompletedAt)> updates,
        CancellationToken cancellationToken = default);
}
