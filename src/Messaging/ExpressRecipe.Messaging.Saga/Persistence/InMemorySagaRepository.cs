using System.Collections.Concurrent;
using ExpressRecipe.Messaging.Saga.Abstractions;

namespace ExpressRecipe.Messaging.Saga.Persistence;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="ISagaBatchRepository{TState}"/>.
/// Suitable for testing and development. Not for production use.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
public sealed class InMemorySagaRepository<TState> : ISagaBatchRepository<TState>
    where TState : class, ISagaState
{
    private readonly ConcurrentDictionary<string, TState> _store = new();

    /// <inheritdoc />
    public Task<TState?> LoadAsync(string correlationId, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(correlationId, out var state) ? state : null);

    /// <inheritdoc />
    public Task<IReadOnlyList<TState>> BatchLoadAsync(IEnumerable<string> correlationIds, CancellationToken cancellationToken = default)
    {
        var result = correlationIds
            .Select(id => _store.TryGetValue(id, out var s) ? s : null)
            .Where(s => s is not null)
            .Cast<TState>()
            .ToList();
        return Task.FromResult<IReadOnlyList<TState>>(result);
    }

    /// <inheritdoc />
    public Task SaveAsync(TState state, CancellationToken cancellationToken = default)
    {
        _store[state.CorrelationId] = state;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TState>> BatchUpdateMaskAsync(
        IEnumerable<(string CorrelationId, long MaskToAdd)> updates,
        CancellationToken cancellationToken = default)
    {
        var results = new List<TState>();
        foreach (var (correlationId, maskToAdd) in updates)
        {
            if (_store.TryGetValue(correlationId, out var state))
            {
                state.CurrentMask |= maskToAdd;
                results.Add(state);
            }
        }
        return Task.FromResult<IReadOnlyList<TState>>(results);
    }

    /// <inheritdoc />
    public Task BatchUpdateStatusAsync(
        IEnumerable<(string CorrelationId, SagaStatus Status, DateTimeOffset? CompletedAt)> updates,
        CancellationToken cancellationToken = default)
    {
        foreach (var (correlationId, status, completedAt) in updates)
        {
            if (_store.TryGetValue(correlationId, out var state))
            {
                state.Status = status;
                state.CompletedAt = completedAt;
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>Returns a snapshot of all stored states (for testing).</summary>
    public IReadOnlyDictionary<string, TState> GetAll() =>
        new Dictionary<string, TState>(_store);
}
