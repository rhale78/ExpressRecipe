using ExpressRecipe.Messaging.Saga.Abstractions;

namespace ExpressRecipe.ProductService.Saga;

/// <summary>
/// Tracks the state of a single product through the processing pipeline.
/// Steps: Staged → AIVerified → Enriched → Published
/// </summary>
public class ProductProcessingSagaState : ISagaState
{
    // ISagaState required fields
    public string CorrelationId { get; set; } = string.Empty;
    public long CurrentMask { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public SagaStatus Status { get; set; }

    // Domain-specific fields
    public Guid StagingId { get; set; }
    public Guid? ProductId { get; set; }
    public string? ExternalId { get; set; }
    public string? Barcode { get; set; }
    public string? ProductName { get; set; }
    public bool AIVerificationPassed { get; set; }
    public string? AIVerificationNotes { get; set; }
    public string? ImportSessionId { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
}
