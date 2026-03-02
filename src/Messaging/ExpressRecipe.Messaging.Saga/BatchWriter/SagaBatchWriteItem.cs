using ExpressRecipe.Messaging.Saga.Abstractions;

namespace ExpressRecipe.Messaging.Saga.BatchWriter;

/// <summary>
/// Represents a single pending state update enqueued in the write-behind channel.
/// </summary>
internal sealed record SagaBatchWriteItem(
    string CorrelationId,
    long MaskToAdd,
    SagaStatus? NewStatus,
    DateTimeOffset? CompletedAt);
