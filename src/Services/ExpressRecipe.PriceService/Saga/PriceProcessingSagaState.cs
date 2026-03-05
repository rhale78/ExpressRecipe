using ExpressRecipe.Messaging.Saga.Abstractions;

namespace ExpressRecipe.PriceService.Saga;

/// <summary>
/// Tracks the state of a single price observation through the processing pipeline.
/// Steps: Staged → ProductLinked → Verified → Published
/// A price is NOT complete until it is linked to a product.
/// </summary>
public class PriceProcessingSagaState : ISagaState
{
    // ISagaState required fields
    public string CorrelationId { get; set; } = string.Empty;
    public long CurrentMask { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public SagaStatus Status { get; set; }

    // Domain-specific fields
    public Guid? PriceObservationId { get; set; }
    public Guid? PriceStagingId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? StoreId { get; set; }
    public string? Barcode { get; set; }
    public string? ExternalProductId { get; set; }
    public string? ProductName { get; set; }
    public decimal? Price { get; set; }
    public bool IsProductLinked { get; set; }
    public bool WasExactMatch { get; set; }
    public string? ImportSessionId { get; set; }
    public string? LastError { get; set; }
}
