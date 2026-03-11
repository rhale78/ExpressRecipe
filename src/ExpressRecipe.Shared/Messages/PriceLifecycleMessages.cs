using ExpressRecipe.Messaging.Core.Abstractions;

namespace ExpressRecipe.Shared.Messages;

/// <summary>
/// Routing key constants for price lifecycle events.
/// </summary>
public static class PriceEventKeys
{
    public const string Recorded       = "price.recorded";
    public const string BatchSubmitted = "price.batch-submitted";
    public const string DealCreated    = "price.deal-created";
    public const string StoreAdded     = "price.store-added";

    public static readonly IReadOnlyList<string> All = new[]
        { Recorded, BatchSubmitted, DealCreated, StoreAdded };
}

/// <summary>
/// Emitted synchronously after a single price observation is recorded via the REST API or
/// the price ingestion channel. Both the sync and async paths publish this event.
/// </summary>
public record PriceRecordedEvent(
    Guid PriceObservationId,
    Guid ProductId,
    Guid StoreId,
    decimal Price,
    Guid RecordedBy,
    DateTimeOffset OccurredAt) : IMessage;

/// <summary>
/// Emitted once when a batch of price records has been queued for async processing via the
/// price ingestion channel. Use this to track submission – not completion.
/// </summary>
public record PriceBatchSubmittedEvent(
    string SessionId,
    int ItemCount,
    Guid SubmittedBy,
    DateTimeOffset OccurredAt) : IMessage;

/// <summary>
/// Emitted when a new promotional deal is created (sync path from REST API or deal channel).
/// </summary>
public record DealCreatedEvent(
    Guid DealId,
    Guid ProductId,
    Guid StoreId,
    string DealType,
    decimal OriginalPrice,
    decimal SalePrice,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    DateTimeOffset OccurredAt) : IMessage;

/// <summary>
/// Emitted when a new store location is added to the system.
/// </summary>
public record StoreAddedEvent(
    Guid StoreId,
    string Name,
    string? City,
    string? State,
    string? Chain,
    DateTimeOffset OccurredAt) : IMessage;

/// <summary>
/// Emitted when a significant price drop is detected for a product that households are tracking.
/// </summary>
public record PriceDropEvent(
    Guid ProductId,
    string ProductName,
    Guid StoreId,
    string StoreName,
    decimal OldPrice,
    decimal NewPrice,
    Guid[] HouseholdIds,
    DateTimeOffset OccurredAt) : IMessage;
