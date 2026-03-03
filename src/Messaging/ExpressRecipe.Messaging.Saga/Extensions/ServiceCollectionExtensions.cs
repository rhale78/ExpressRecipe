using ExpressRecipe.Messaging.Saga.Abstractions;
using ExpressRecipe.Messaging.Saga.BatchWriter;
using ExpressRecipe.Messaging.Saga.Builder;
using ExpressRecipe.Messaging.Saga.Engine;
using ExpressRecipe.Messaging.Saga.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.Messaging.Saga.Extensions;

/// <summary>
/// Extension methods for registering saga infrastructure in a DI container.
/// </summary>
public static class SagaServiceCollectionExtensions
{
    /// <summary>
    /// Registers a saga workflow with the DI container.
    /// Call this for each workflow your service participates in.
    /// </summary>
    /// <typeparam name="TState">The saga state type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="workflowDefinition">The compiled workflow definition (from <see cref="SagaWorkflowBuilder{TState}.Build"/>).</param>
    /// <param name="configureBatchWriter">Optional batch writer configuration.</param>
    public static IServiceCollection AddSagaWorkflow<TState>(
        this IServiceCollection services,
        SagaWorkflowDefinition<TState> workflowDefinition,
        Action<SagaBatchWriterOptions>? configureBatchWriter = null)
        where TState : class, ISagaState
    {
        var batchWriterOptions = new SagaBatchWriterOptions();
        configureBatchWriter?.Invoke(batchWriterOptions);

        services.AddSingleton(workflowDefinition);
        services.AddSingleton(batchWriterOptions);

        services.AddSingleton<SagaBatchWriter<TState>>(sp =>
        {
            var repo = sp.GetRequiredService<ISagaBatchRepository<TState>>();
            var logger = sp.GetRequiredService<ILogger<SagaBatchWriter<TState>>>();
            return new SagaBatchWriter<TState>(repo, batchWriterOptions, logger);
        });

        services.AddSingleton<SagaOrchestrator<TState>>(sp =>
        {
            var workflow = sp.GetRequiredService<SagaWorkflowDefinition<TState>>();
            var repo = sp.GetRequiredService<ISagaBatchRepository<TState>>();
            var bus = sp.GetRequiredService<Core.Abstractions.IMessageBus>();
            var writer = sp.GetRequiredService<SagaBatchWriter<TState>>();
            var logger = sp.GetRequiredService<ILogger<SagaOrchestrator<TState>>>();
            return new SagaOrchestrator<TState>(workflow, repo, bus, writer, logger);
        });

        services.AddSingleton<ISagaOrchestrator<TState>>(sp => sp.GetRequiredService<SagaOrchestrator<TState>>());

        // Hosted service that calls InitializeAsync on startup
        services.AddHostedService<SagaInitializerHostedService<TState>>();

        return services;
    }

    /// <summary>
    /// Registers the in-memory repository implementation. Suitable for tests and demos.
    /// </summary>
    public static IServiceCollection AddInMemorySagaRepository<TState>(this IServiceCollection services)
        where TState : class, ISagaState
    {
        services.AddSingleton<ISagaBatchRepository<TState>, InMemorySagaRepository<TState>>();
        return services;
    }

    /// <summary>
    /// Registers the SQL Server ADO.NET repository implementation.
    /// </summary>
    public static IServiceCollection AddSqlSagaRepository<TState>(
        this IServiceCollection services, string connectionString, string? tableName = null)
        where TState : class, ISagaState
    {
        services.AddSingleton<ISagaBatchRepository<TState>>(
            _ => new SqlSagaRepository<TState>(connectionString, tableName));
        return services;
    }
}
