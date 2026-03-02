using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Saga.Abstractions;

namespace ExpressRecipe.Messaging.Saga.Builder;

/// <summary>
/// Configures a single step within a saga workflow.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
public sealed class SagaStepBuilder<TState> where TState : class, ISagaState
{
    private readonly SagaStepDefinition<TState> _definition;
    private readonly SagaWorkflowBuilder<TState> _parent;

    internal SagaStepBuilder(SagaStepDefinition<TState> definition, SagaWorkflowBuilder<TState> parent)
    {
        _definition = definition;
        _parent = parent;
    }

    /// <summary>
    /// Specifies one or more step names that must complete before this step can start.
    /// </summary>
    public SagaStepBuilder<TState> DependsOn(params string[] stepNames)
    {
        _definition.DependencyNames.AddRange(stepNames);
        return this;
    }

    /// <summary>
    /// Specifies the command message factory for this step.
    /// The factory receives the current saga state and produces the command to send.
    /// The command is routed to a queue named after the command type by default.
    /// </summary>
    public SagaStepBuilder<TState> Sends<TCommand>(Func<TState, TCommand> commandFactory)
        where TCommand : IMessage
    {
        _definition.CommandFactory = state =>
        {
            var cmd = commandFactory(state);
            return (cmd!, typeof(TCommand));
        };
        // Default destination: lowercase type name (can be overridden with SendsTo)
        _definition.CommandDestination ??= typeof(TCommand).Name.ToLowerInvariant();
        return this;
    }

    /// <summary>
    /// Overrides the routing destination queue for the command sent by this step.
    /// Use this to decouple the command type name from the destination queue name.
    /// Must be called after <see cref="Sends{TCommand}"/>.
    /// </summary>
    public SagaStepBuilder<TState> SendsTo(string destination)
    {
        if (string.IsNullOrWhiteSpace(destination))
            throw new ArgumentException("Destination must not be empty.", nameof(destination));
        _definition.CommandDestination = destination;
        return this;
    }

    /// <summary>
    /// Specifies the result message type that marks this step as completed.
    /// When a message of type <typeparamref name="TResult"/> with a matching CorrelationId arrives, the step's bit is set.
    /// </summary>
    public SagaStepBuilder<TState> OnResult<TResult>() where TResult : IMessage
    {
        _definition.ResultType = typeof(TResult);
        return this;
    }

    /// <summary>
    /// Specifies the result message type AND a custom handler to run when the result arrives.
    /// The handler can update the saga state based on the result payload.
    /// </summary>
    public SagaStepBuilder<TState> OnResult<TResult>(Func<TState, TResult, CancellationToken, Task<TState>> handler)
        where TResult : IMessage
    {
        _definition.ResultType = typeof(TResult);
        _definition.ResultHandler = async (state, msg, ct) => await handler(state, (TResult)msg, ct);
        return this;
    }

    /// <summary>
    /// Specifies a compensation action to run if this step fails.
    /// </summary>
    public SagaStepBuilder<TState> OnFailure(Func<TState, Exception, CancellationToken, Task> compensationHandler)
    {
        _definition.CompensationHandler = compensationHandler;
        return this;
    }

    /// <summary>Goes back to the parent workflow builder to chain another <see cref="SagaWorkflowBuilder{TState}.AddStep"/> call.</summary>
    public SagaWorkflowBuilder<TState> And() => _parent;
}
