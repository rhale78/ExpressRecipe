using ExpressRecipe.Messaging.Core.Abstractions;

namespace ExpressRecipe.Shared.Messages;

/// <summary>
/// Published by any saga workflow when it completes or fails.
/// NotificationService can subscribe to these to send in-app alerts about long-running operations.
/// NOTE: NotificationService does not currently process these – subscribe when ready.
/// </summary>
public record SagaCompletedNotification(
    /// <summary>Name of the workflow that completed (e.g. "PriceProcessing", "ProductProcessing").</summary>
    string WorkflowName,

    /// <summary>Correlation ID that ties the saga instance back to the triggering request.</summary>
    string CorrelationId,

    /// <summary>True if the saga finished without errors; false if it was aborted or failed.</summary>
    bool Succeeded,

    /// <summary>Human-readable one-line description of the outcome, suitable for a notification body.</summary>
    string? Summary,

    /// <summary>
    /// The primary entity the saga processed (e.g. ProductId for product sagas, PriceObservationId
    /// for price sagas). Null when no entity could be identified.
    /// </summary>
    Guid? AffectedEntityId,

    DateTimeOffset CompletedAt) : IMessage;
