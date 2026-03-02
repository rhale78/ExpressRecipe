namespace ExpressRecipe.Messaging.Saga.Abstractions;

/// <summary>
/// Represents the persisted state of a running saga instance.
/// Implementors add their domain-specific properties alongside these base fields.
/// </summary>
public interface ISagaState
{
    /// <summary>Unique identifier that correlates all messages in this saga instance.</summary>
    string CorrelationId { get; set; }

    /// <summary>
    /// Bit-flag mask tracking completed steps.
    /// Bit N is set when step with bit (1 &lt;&lt; N) completes.
    /// </summary>
    long CurrentMask { get; set; }

    /// <summary>Optional ISO-8601 timestamp when this saga instance started.</summary>
    DateTimeOffset StartedAt { get; set; }

    /// <summary>Optional ISO-8601 timestamp when this saga instance completed (all steps done).</summary>
    DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Current status of the saga instance.</summary>
    SagaStatus Status { get; set; }
}
