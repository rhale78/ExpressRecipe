using ExpressRecipe.Messaging.Core.Abstractions;

namespace ExpressRecipe.PriceService.Messages;

// Emitted when a batch of raw prices has been ingested
public record PriceBatchIngested(
    string ImportSessionId,
    int BatchNumber,
    int BatchSize,
    int TotalExpected,
    DateTimeOffset IngestedAt) : IMessage;

// Command: request product linking for a price observation
public record RequestPriceProductLink(
    string CorrelationId,
    Guid PriceStagingId,
    string? Barcode,
    string? ExternalProductId,
    string? ProductName) : IMessage;

// Result: price has been linked to a product
public record PriceLinkedToProduct(
    string CorrelationId,
    Guid PriceStagingId,
    Guid ProductId,
    bool WasExactMatch,
    DateTimeOffset LinkedAt) : IMessage;

// Result: price linking failed (no matching product found)
public record PriceLinkFailed(
    string CorrelationId,
    Guid PriceStagingId,
    string Reason,
    DateTimeOffset FailedAt) : IMessage;

// Emitted when a price observation is complete (linked + verified)
public record PriceObservationCompleted(
    string CorrelationId,
    Guid PriceObservationId,
    Guid ProductId,
    Guid StoreId,
    decimal Price,
    DateTimeOffset CompletedAt) : IMessage;
