using ExpressRecipe.Messaging.Saga.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.Messaging.Saga.Engine;

/// <summary>
/// Hosted service that calls <see cref="SagaOrchestrator{TState}.InitializeAsync"/> on application startup,
/// ensuring all result-message subscriptions are active before the application starts accepting work.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
internal sealed class SagaInitializerHostedService<TState> : IHostedService
    where TState : class, ISagaState
{
    private readonly SagaOrchestrator<TState> _orchestrator;
    private readonly ILogger<SagaInitializerHostedService<TState>> _logger;

    public SagaInitializerHostedService(
        SagaOrchestrator<TState> orchestrator,
        ILogger<SagaInitializerHostedService<TState>> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing saga orchestrator for {StateType}", typeof(TState).Name);
        await _orchestrator.InitializeAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
