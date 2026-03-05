using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Options;
using ExpressRecipe.ProductService.Logging;
using ExpressRecipe.ProductService.Messages;

namespace ExpressRecipe.ProductService.Services;

/// <summary>
/// Publishes product domain events to the message bus.
/// All methods are fire-and-forget on a best-effort basis: if the bus is
/// unavailable the warning is logged and the caller is not disrupted.
/// </summary>
public interface IProductEventPublisher
{
    Task PublishCreatedAsync(Guid productId, string name, string? brand,
        string? barcode, string? category, string approvalStatus,
        Guid? submittedBy, CancellationToken ct = default);

    Task PublishUpdatedAsync(Guid productId, string name, string? brand,
        string? barcode, string? category, string approvalStatus,
        Guid? updatedBy, IReadOnlyList<string> changedFields,
        CancellationToken ct = default);

    Task PublishDeletedAsync(Guid productId, string? barcode,
        Guid? deletedBy, CancellationToken ct = default);

    Task PublishApprovedAsync(Guid productId, string name, string? barcode,
        Guid approvedBy, CancellationToken ct = default);

    Task PublishRejectedAsync(Guid productId, string name, Guid rejectedBy,
        string? rejectionReason, CancellationToken ct = default);

    Task PublishRenamedAsync(Guid productId, string oldName, string newName,
        Guid? updatedBy, CancellationToken ct = default);

    Task PublishBarcodeChangedAsync(Guid productId, string? oldBarcode,
        string? newBarcode, Guid? updatedBy, CancellationToken ct = default);

    Task PublishIngredientsChangedAsync(Guid productId, string? productName,
        IReadOnlyList<Guid> added, IReadOnlyList<Guid> removed,
        Guid? changedBy, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class ProductEventPublisher : IProductEventPublisher
{
    private readonly IMessageBus _bus;
    private readonly ILogger<ProductEventPublisher> _logger;

    // Shared publish options that set the routing key per-call
    private static PublishOptions Opts(string routingKey) =>
        new() { RoutingKey = routingKey };

    public ProductEventPublisher(IMessageBus bus, ILogger<ProductEventPublisher> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    public Task PublishCreatedAsync(Guid productId, string name, string? brand,
        string? barcode, string? category, string approvalStatus, Guid? submittedBy,
        CancellationToken ct = default) =>
        SafePublishAsync(
            new ProductCreatedEvent(productId, name, brand, barcode, category,
                approvalStatus, submittedBy, DateTimeOffset.UtcNow),
            ProductEventKeys.Created, ct);

    public Task PublishUpdatedAsync(Guid productId, string name, string? brand,
        string? barcode, string? category, string approvalStatus, Guid? updatedBy,
        IReadOnlyList<string> changedFields, CancellationToken ct = default) =>
        SafePublishAsync(
            new ProductUpdatedEvent(productId, name, brand, barcode, category,
                approvalStatus, updatedBy, changedFields, DateTimeOffset.UtcNow),
            ProductEventKeys.Updated, ct);

    public Task PublishDeletedAsync(Guid productId, string? barcode, Guid? deletedBy,
        CancellationToken ct = default) =>
        SafePublishAsync(
            new ProductDeletedEvent(productId, barcode, deletedBy, DateTimeOffset.UtcNow),
            ProductEventKeys.Deleted, ct);

    public Task PublishApprovedAsync(Guid productId, string name, string? barcode,
        Guid approvedBy, CancellationToken ct = default) =>
        SafePublishAsync(
            new ProductApprovedEvent(productId, name, barcode, approvedBy, DateTimeOffset.UtcNow),
            ProductEventKeys.Approved, ct);

    public Task PublishRejectedAsync(Guid productId, string name, Guid rejectedBy,
        string? rejectionReason, CancellationToken ct = default) =>
        SafePublishAsync(
            new ProductRejectedEvent(productId, name, rejectedBy, rejectionReason,
                DateTimeOffset.UtcNow),
            ProductEventKeys.Rejected, ct);

    public Task PublishRenamedAsync(Guid productId, string oldName, string newName,
        Guid? updatedBy, CancellationToken ct = default) =>
        SafePublishAsync(
            new ProductRenamedEvent(productId, oldName, newName, updatedBy,
                DateTimeOffset.UtcNow),
            ProductEventKeys.Renamed, ct);

    public Task PublishBarcodeChangedAsync(Guid productId, string? oldBarcode,
        string? newBarcode, Guid? updatedBy, CancellationToken ct = default) =>
        SafePublishAsync(
            new ProductBarcodeChangedEvent(productId, oldBarcode, newBarcode, updatedBy,
                DateTimeOffset.UtcNow),
            ProductEventKeys.BarcodeChanged, ct);

    public Task PublishIngredientsChangedAsync(Guid productId, string? productName,
        IReadOnlyList<Guid> added, IReadOnlyList<Guid> removed, Guid? changedBy,
        CancellationToken ct = default) =>
        SafePublishAsync(
            new ProductIngredientsChangedEvent(productId, productName, added, removed,
                changedBy, DateTimeOffset.UtcNow),
            ProductEventKeys.IngredientsChanged, ct);

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private async Task SafePublishAsync<TMsg>(TMsg msg, string routingKey,
        CancellationToken ct) where TMsg : IMessage
    {
        try
        {
            await _bus.PublishAsync(msg, Opts(routingKey), ct).ConfigureAwait(false);
            _logger.LogProductEventPublished(routingKey, typeof(TMsg).Name);
        }
        catch (Exception ex)
        {
            // Non-fatal: log and continue so the HTTP request is not disrupted
            _logger.LogProductEventPublishFailed(routingKey, typeof(TMsg).Name, ex.Message);
        }
    }
}

/// <summary>
/// No-op publisher used when messaging is disabled (RabbitMQ not configured).
/// Keeps service registrations identical regardless of environment.
/// </summary>
public sealed class NullProductEventPublisher : IProductEventPublisher
{
    public Task PublishCreatedAsync(Guid productId, string name, string? brand,
        string? barcode, string? category, string approvalStatus, Guid? submittedBy,
        CancellationToken ct = default) => Task.CompletedTask;

    public Task PublishUpdatedAsync(Guid productId, string name, string? brand,
        string? barcode, string? category, string approvalStatus, Guid? updatedBy,
        IReadOnlyList<string> changedFields, CancellationToken ct = default) => Task.CompletedTask;

    public Task PublishDeletedAsync(Guid productId, string? barcode, Guid? deletedBy,
        CancellationToken ct = default) => Task.CompletedTask;

    public Task PublishApprovedAsync(Guid productId, string name, string? barcode,
        Guid approvedBy, CancellationToken ct = default) => Task.CompletedTask;

    public Task PublishRejectedAsync(Guid productId, string name, Guid rejectedBy,
        string? rejectionReason, CancellationToken ct = default) => Task.CompletedTask;

    public Task PublishRenamedAsync(Guid productId, string oldName, string newName,
        Guid? updatedBy, CancellationToken ct = default) => Task.CompletedTask;

    public Task PublishBarcodeChangedAsync(Guid productId, string? oldBarcode,
        string? newBarcode, Guid? updatedBy, CancellationToken ct = default) => Task.CompletedTask;

    public Task PublishIngredientsChangedAsync(Guid productId, string? productName,
        IReadOnlyList<Guid> added, IReadOnlyList<Guid> removed, Guid? changedBy,
        CancellationToken ct = default) => Task.CompletedTask;
}
