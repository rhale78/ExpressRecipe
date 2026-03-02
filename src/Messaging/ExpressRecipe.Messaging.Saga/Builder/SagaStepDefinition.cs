using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Saga.Abstractions;

namespace ExpressRecipe.Messaging.Saga.Builder;

/// <summary>
/// Internal representation of a configured saga step.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
public sealed class SagaStepDefinition<TState> where TState : class, ISagaState
{
    /// <summary>The unique name of this step within the workflow.</summary>
    public string Name { get; }

    /// <summary>The unique bit assigned to this step (1 &lt;&lt; stepIndex).</summary>
    public long Bit { get; }

    /// <summary>Names of steps that must complete before this step can start.</summary>
    public List<string> DependencyNames { get; } = new();

    /// <summary>Bitmask of all dependency steps (computed at Build() time).</summary>
    public long DependencyMask { get; private set; }

    /// <summary>Factory: takes state, returns (commandObject, commandType).</summary>
    public Func<TState, (object Command, Type CommandType)>? CommandFactory { get; set; }

    /// <summary>
    /// Explicit routing destination for the command. Defaults to the command type name in lowercase.
    /// Set via <c>.SendsTo("my-queue")</c> to decouple from type naming.
    /// </summary>
    public string? CommandDestination { get; set; }

    /// <summary>The CLR type of the result message that completes this step.</summary>
    public Type? ResultType { get; set; }

    /// <summary>Optional handler that runs when the result message arrives (can mutate state).</summary>
    public Func<TState, object, CancellationToken, Task<TState>>? ResultHandler { get; set; }

    /// <summary>Optional compensation handler if the step fails.</summary>
    public Func<TState, Exception, CancellationToken, Task>? CompensationHandler { get; set; }

    internal SagaStepDefinition(string name, long bit)
    {
        Name = name;
        Bit = bit;
    }

    /// <summary>Resolves DependencyMask from dependency names. Called at Build() time.</summary>
    internal void ResolveDependencyMask(IReadOnlyList<SagaStepDefinition<TState>> allSteps)
    {
        DependencyMask = 0;
        foreach (var depName in DependencyNames)
        {
            var dep = allSteps.FirstOrDefault(s => s.Name == depName)
                ?? throw new InvalidOperationException($"Saga step '{Name}' depends on unknown step '{depName}'.");
            DependencyMask |= dep.Bit;
        }
    }
}
