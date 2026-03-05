using ExpressRecipe.Messaging.Core.Abstractions;

namespace ExpressRecipe.ProductService.Messages;

// ---------------------------------------------------------------------------
// Product lifecycle domain events – broadcast to all interested subscribers.
// Every field is nullable-safe so consumers don't need to worry about missing
// data on older message schema versions.
// ---------------------------------------------------------------------------

/// <summary>
/// Routing key constants for product lifecycle events.
/// Use these in SubscribeOptions.RoutingKey when subscribing.
/// </summary>
public static class ProductEventKeys
{
    public const string Created  = "product.lifecycle.created";
    public const string Updated  = "product.lifecycle.updated";
    public const string Deleted  = "product.lifecycle.deleted";
    public const string Approved = "product.lifecycle.approved";
    public const string Rejected = "product.lifecycle.rejected";
    public const string Renamed  = "product.lifecycle.renamed";
    public const string BarcodeChanged     = "product.lifecycle.barcode-changed";
    public const string IngredientsChanged = "product.lifecycle.ingredients-changed";
    /// <summary>Wildcard that matches all lifecycle events.</summary>
    public const string All = "product.lifecycle.#";
}

/// <summary>
/// Broadcast when a new product is added to the catalogue (via API or bulk import).
/// </summary>
public record ProductCreatedEvent(
    Guid   ProductId,
    string Name,
    string? Brand,
    string? Barcode,
    string? Category,
    string  ApprovalStatus,
    Guid?   SubmittedBy,
    DateTimeOffset OccurredAt) : IMessage;

/// <summary>
/// Broadcast when any mutable field of an existing product changes.
/// <see cref="ChangedFields"/> lists the property names that were modified.
/// </summary>
public record ProductUpdatedEvent(
    Guid   ProductId,
    string Name,
    string? Brand,
    string? Barcode,
    string? Category,
    string  ApprovalStatus,
    Guid?   UpdatedBy,
    IReadOnlyList<string> ChangedFields,
    DateTimeOffset OccurredAt) : IMessage;

/// <summary>
/// Broadcast when a product is soft-deleted.
/// Interested subscribers (e.g. PriceService) should deactivate related data.
/// </summary>
public record ProductDeletedEvent(
    Guid   ProductId,
    string? Barcode,
    Guid?  DeletedBy,
    DateTimeOffset OccurredAt) : IMessage;

/// <summary>
/// Broadcast when a product's admin approval status changes to Approved.
/// </summary>
public record ProductApprovedEvent(
    Guid   ProductId,
    string Name,
    string? Barcode,
    Guid   ApprovedBy,
    DateTimeOffset OccurredAt) : IMessage;

/// <summary>
/// Broadcast when a product's admin approval status changes to Rejected.
/// </summary>
public record ProductRejectedEvent(
    Guid   ProductId,
    string Name,
    Guid   RejectedBy,
    string? RejectionReason,
    DateTimeOffset OccurredAt) : IMessage;

/// <summary>
/// Broadcast when a product's <c>Name</c> field changes.
/// Consumers with denormalised product-name copies (e.g. PriceService.ProductPrice)
/// should update their local copies.
/// </summary>
public record ProductRenamedEvent(
    Guid   ProductId,
    string OldName,
    string NewName,
    Guid?  UpdatedBy,
    DateTimeOffset OccurredAt) : IMessage;

/// <summary>
/// Broadcast when the barcode (UPC/EAN) of a product changes.
/// Price lookup caches keyed by barcode must be invalidated.
/// </summary>
public record ProductBarcodeChangedEvent(
    Guid   ProductId,
    string? OldBarcode,
    string? NewBarcode,
    Guid?  UpdatedBy,
    DateTimeOffset OccurredAt) : IMessage;

/// <summary>
/// Broadcast when ingredients are added to or removed from a product.
/// </summary>
public record ProductIngredientsChangedEvent(
    Guid   ProductId,
    string? ProductName,
    IReadOnlyList<Guid> AddedIngredientIds,
    IReadOnlyList<Guid> RemovedIngredientIds,
    Guid?  ChangedBy,
    DateTimeOffset OccurredAt) : IMessage;

// Emitted when a batch of raw products has been read and queued for staging
public record ProductBatchIngested(
    string ImportSessionId,
    int BatchNumber,
    int BatchSize,
    int TotalExpected,
    DateTimeOffset IngestedAt) : IMessage;

// Emitted when a staged product has been verified by local AI parser
public record ProductAIVerified(
    string CorrelationId,
    Guid StagingId,
    bool IsValid,
    string? ValidationNotes,
    DateTimeOffset VerifiedAt) : IMessage;

// Emitted when a staged product has been enriched (ingredients linked, allergens resolved, etc.)
public record ProductEnriched(
    string CorrelationId,
    Guid StagingId,
    Guid? ProductId, // null if failed
    DateTimeOffset EnrichedAt) : IMessage;

// Emitted when a product has been fully published to the Product table
public record ProductPublished(
    string CorrelationId,
    Guid ProductId,
    string ExternalId,
    string? Barcode,
    DateTimeOffset PublishedAt) : IMessage;

// Emitted when a product processing step fails
public record ProductFailed(
    string CorrelationId,
    Guid StagingId,
    string StepName,
    string ErrorMessage,
    DateTimeOffset FailedAt) : IMessage;

// Broadcast: overall import session progress
public record ImportProgressUpdated(
    string ImportSessionId,
    string ServiceName,
    int TotalRecords,
    int ProcessedRecords,
    int SuccessCount,
    int FailureCount,
    double RecordsPerSecond,
    TimeSpan? EstimatedTimeRemaining,
    string Status, // "Running", "Completed", "Failed", "Paused"
    DateTimeOffset UpdatedAt) : IMessage;

// Command: request AI verification for a product
public record RequestProductAIVerification(
    string CorrelationId,
    Guid StagingId,
    string? ProductName,
    string? IngredientsText,
    string? Allergens,
    string? Categories) : IMessage;

// Command: request product enrichment
public record RequestProductEnrichment(
    string CorrelationId,
    Guid StagingId) : IMessage;

// Command: request product publish (write enriched product to Product table)
public record RequestProductPublish(
    string CorrelationId,
    Guid StagingId) : IMessage;
