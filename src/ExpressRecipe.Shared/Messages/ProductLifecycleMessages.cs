using ExpressRecipe.Messaging.Core.Abstractions;

namespace ExpressRecipe.Shared.Messages;

/// <summary>
/// Routing key constants for product lifecycle events.
/// Use these in <see cref="ExpressRecipe.Messaging.Core.Options.SubscribeOptions.RoutingKey"/>
/// when subscribing, and in <see cref="ExpressRecipe.Messaging.Core.Options.PublishOptions.RoutingKey"/>
/// when publishing.
/// </summary>
public static class ProductEventKeys
{
    public const string Created            = "product.lifecycle.created";
    public const string Updated            = "product.lifecycle.updated";
    public const string Deleted            = "product.lifecycle.deleted";
    public const string Approved           = "product.lifecycle.approved";
    public const string Rejected           = "product.lifecycle.rejected";
    public const string Renamed            = "product.lifecycle.renamed";
    public const string BarcodeChanged     = "product.lifecycle.barcode-changed";
    public const string IngredientsChanged = "product.lifecycle.ingredients-changed";

    /// <summary>Wildcard that matches <em>all</em> product lifecycle events.</summary>
    public const string All = "product.lifecycle.#";
}

// ---------------------------------------------------------------------------
// Domain event records
// ---------------------------------------------------------------------------

/// <summary>
/// Broadcast when a new product is added to the catalogue (API or bulk import).
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
