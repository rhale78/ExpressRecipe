namespace ExpressRecipe.Messaging.Saga.Abstractions;

/// <summary>
/// Starts and manages saga instances for a specific workflow definition.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
public interface ISagaOrchestrator<TState> where TState : class, ISagaState
{
    /// <summary>
    /// Starts a new saga instance with the given initial state.
    /// The CorrelationId on <paramref name="initialState"/> must already be set.
    /// </summary>
    Task StartAsync(TState initialState, CancellationToken cancellationToken = default);
}
