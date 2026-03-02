using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Saga.Abstractions;

namespace ExpressRecipe.Messaging.Saga.Builder;

/// <summary>
/// Immutable, compiled representation of a saga workflow.
/// Produced by <see cref="SagaWorkflowBuilder{TState}.Build"/>.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
public sealed class SagaWorkflowDefinition<TState> where TState : class, ISagaState
{
    public string WorkflowName { get; }
    public IReadOnlyList<SagaStepDefinition<TState>> Steps { get; }
    public long CompletionMask { get; }

    internal Func<TState, IMessageBus, CancellationToken, Task>? OnCompleted { get; }
    internal Func<TState, Exception, IMessageBus, CancellationToken, Task>? OnFailed { get; }

    internal SagaWorkflowDefinition(
        string workflowName,
        IReadOnlyList<SagaStepDefinition<TState>> steps,
        long completionMask,
        Func<TState, IMessageBus, CancellationToken, Task>? onCompleted,
        Func<TState, Exception, IMessageBus, CancellationToken, Task>? onFailed)
    {
        WorkflowName = workflowName;
        Steps = steps;
        CompletionMask = completionMask;
        OnCompleted = onCompleted;
        OnFailed = onFailed;
    }

    /// <summary>
    /// Returns the steps that are eligible to start given the current completed mask.
    /// A step is eligible when:
    ///   1. Its bit is NOT yet set in currentMask (not already completed/in-flight)
    ///   2. ALL its dependency bits ARE set in currentMask
    /// </summary>
    public IEnumerable<SagaStepDefinition<TState>> GetEligibleSteps(long currentMask)
    {
        return Steps.Where(s =>
            (currentMask & s.Bit) == 0 &&                           // not already done
            (currentMask & s.DependencyMask) == s.DependencyMask);  // all deps done
    }

    /// <summary>Returns true if all steps have completed.</summary>
    public bool IsComplete(long currentMask) => (currentMask & CompletionMask) == CompletionMask;
}
