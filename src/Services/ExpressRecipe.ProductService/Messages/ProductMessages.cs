// Global using aliases so existing ProductService code that imports
// ExpressRecipe.ProductService.Messages continues to resolve the lifecycle types
// without duplication – the canonical definitions live in ExpressRecipe.Shared.Messages.
global using ProductEventKeys               = ExpressRecipe.Shared.Messages.ProductEventKeys;
global using ProductCreatedEvent            = ExpressRecipe.Shared.Messages.ProductCreatedEvent;
global using ProductUpdatedEvent            = ExpressRecipe.Shared.Messages.ProductUpdatedEvent;
global using ProductDeletedEvent            = ExpressRecipe.Shared.Messages.ProductDeletedEvent;
global using ProductApprovedEvent           = ExpressRecipe.Shared.Messages.ProductApprovedEvent;
global using ProductRejectedEvent           = ExpressRecipe.Shared.Messages.ProductRejectedEvent;
global using ProductRenamedEvent            = ExpressRecipe.Shared.Messages.ProductRenamedEvent;
global using ProductBarcodeChangedEvent     = ExpressRecipe.Shared.Messages.ProductBarcodeChangedEvent;
global using ProductIngredientsChangedEvent = ExpressRecipe.Shared.Messages.ProductIngredientsChangedEvent;

using ExpressRecipe.Messaging.Core.Abstractions;

namespace ExpressRecipe.ProductService.Messages;

// -----------------------------------------------------------------------
// Product staging/pipeline internal messages (not shared externally).
// -----------------------------------------------------------------------

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
