using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Saga.Abstractions;

namespace ExpressRecipe.Messaging.Saga.Builder;

/// <summary>
/// Fluent builder for declaring a bit-flag DAG saga workflow.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
public sealed class SagaWorkflowBuilder<TState> where TState : class, ISagaState
{
    private readonly string _workflowName;
    private readonly List<SagaStepDefinition<TState>> _steps = new();
    private Func<TState, IMessageBus, CancellationToken, Task>? _onCompleted;
    private Func<TState, Exception, IMessageBus, CancellationToken, Task>? _onFailed;

    /// <summary>Creates a new builder for the named workflow.</summary>
    public SagaWorkflowBuilder(string workflowName)
    {
        _workflowName = workflowName;
    }

    /// <summary>
    /// Adds a step to the workflow. Returns a step builder for further configuration.
    /// Steps are automatically assigned bit positions in the order they are added.
    /// </summary>
    public SagaStepBuilder<TState> AddStep(string stepName)
    {
        var bit = 1L << _steps.Count;
        var def = new SagaStepDefinition<TState>(stepName, bit);
        _steps.Add(def);
        return new SagaStepBuilder<TState>(def, this);
    }

    /// <summary>
    /// Registers an async callback invoked when ALL steps complete.
    /// </summary>
    public SagaWorkflowBuilder<TState> OnWorkflowCompleted(Func<TState, IMessageBus, CancellationToken, Task> handler)
    {
        _onCompleted = handler;
        return this;
    }

    /// <summary>
    /// Registers an async callback invoked when the workflow fails.
    /// </summary>
    public SagaWorkflowBuilder<TState> OnWorkflowFailed(Func<TState, Exception, IMessageBus, CancellationToken, Task> handler)
    {
        _onFailed = handler;
        return this;
    }

    /// <summary>
    /// Builds the immutable workflow definition. Must be called after all steps have been configured.
    /// </summary>
    public SagaWorkflowDefinition<TState> Build()
    {
        // Resolve dependency masks now that all steps are registered
        foreach (var step in _steps)
            step.ResolveDependencyMask(_steps);

        var completionMask = _steps.Aggregate(0L, (acc, s) => acc | s.Bit);
        return new SagaWorkflowDefinition<TState>(_workflowName, _steps.AsReadOnly(), completionMask, _onCompleted, _onFailed);
    }
}
