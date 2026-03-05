using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.Messaging.Core.Options;
using ExpressRecipe.PriceService.Data;
using ExpressRecipe.PriceService.Logging;
using ExpressRecipe.Shared.Messages;

namespace ExpressRecipe.PriceService.Services;

/// <summary>
/// Hosted service that subscribes to all <c>product.lifecycle.*</c> events published by
/// ProductService and keeps PriceService data consistent:
/// <list type="bullet">
///   <item><description>Created / Updated / Approved – warm the barcode lookup cache.</description></item>
///   <item><description>Deleted – deactivate all price rows for the product.</description></item>
///   <item><description>Renamed – update denormalised ProductName on ProductPrice rows.</description></item>
///   <item><description>BarcodeChanged – update denormalised Upc on ProductPrice rows and invalidate cache.</description></item>
/// </list>
/// </summary>
public sealed class ProductEventSubscriber : IHostedService
{
    private readonly IMessageBus _bus;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBatchProductLookupService _productCache;
    private readonly ILogger<ProductEventSubscriber> _logger;

    public ProductEventSubscriber(
        IMessageBus bus,
        IServiceScopeFactory scopeFactory,
        IBatchProductLookupService productCache,
        ILogger<ProductEventSubscriber> logger)
    {
        _bus        = bus;
        _scopeFactory = scopeFactory;
        _productCache = productCache;
        _logger     = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // ProductService publishes lifecycle events with RoutingMode.Broadcast (the default
        // for PublishOptions). Subscriptions must also use Broadcast so they bind to the
        // fanout exchange rather than the work queue exchange.
        var broadcastOpts = new SubscribeOptions { RoutingMode = RoutingMode.Broadcast };

        await _bus.SubscribeAsync<ProductCreatedEvent>(HandleCreatedAsync, broadcastOpts, cancellationToken);
        await _bus.SubscribeAsync<ProductUpdatedEvent>(HandleUpdatedAsync, broadcastOpts, cancellationToken);
        await _bus.SubscribeAsync<ProductDeletedEvent>(HandleDeletedAsync, broadcastOpts, cancellationToken);
        await _bus.SubscribeAsync<ProductApprovedEvent>(HandleApprovedAsync, broadcastOpts, cancellationToken);
        await _bus.SubscribeAsync<ProductRejectedEvent>(HandleRejectedAsync, broadcastOpts, cancellationToken);
        await _bus.SubscribeAsync<ProductRenamedEvent>(HandleRenamedAsync, broadcastOpts, cancellationToken);
        await _bus.SubscribeAsync<ProductBarcodeChangedEvent>(HandleBarcodeChangedAsync, broadcastOpts, cancellationToken);
        await _bus.SubscribeAsync<ProductIngredientsChangedEvent>(HandleIngredientsChangedAsync, broadcastOpts, cancellationToken);

        _logger.LogSubscriberStarted(ProductEventKeys.All);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // -----------------------------------------------------------------------
    // Handlers
    // -----------------------------------------------------------------------

    private async Task HandleCreatedAsync(
        ProductCreatedEvent evt, MessageContext ctx, CancellationToken ct)
    {
        _logger.LogProductEventReceived(nameof(ProductCreatedEvent), evt.ProductId);
        try
        {
            // Warm the lookup cache so newly-created products are immediately resolvable
            // without waiting for the next HTTP polling cycle.
            if (!string.IsNullOrWhiteSpace(evt.Barcode))
            {
                await _productCache.GetProductByBarcodeAsync(evt.Barcode, ct);
                _logger.LogProductCacheRefreshed(evt.ProductId, evt.Barcode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogProductEventHandlerFailed(nameof(ProductCreatedEvent), evt.ProductId, ex.Message);
        }
    }

    private async Task HandleUpdatedAsync(
        ProductUpdatedEvent evt, MessageContext ctx, CancellationToken ct)
    {
        _logger.LogProductEventReceived(nameof(ProductUpdatedEvent), evt.ProductId);
        try
        {
            if (!string.IsNullOrWhiteSpace(evt.Barcode))
            {
                await _productCache.GetProductByBarcodeAsync(evt.Barcode, ct);
                _logger.LogProductCacheRefreshed(evt.ProductId, evt.Barcode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogProductEventHandlerFailed(nameof(ProductUpdatedEvent), evt.ProductId, ex.Message);
        }
    }

    private async Task HandleDeletedAsync(
        ProductDeletedEvent evt, MessageContext ctx, CancellationToken ct)
    {
        _logger.LogProductEventReceived(nameof(ProductDeletedEvent), evt.ProductId);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IPriceRepository>();
            var count = await repo.DeactivatePricesByProductIdAsync(evt.ProductId, ct);
            _logger.LogPricesDeactivated(evt.ProductId, count);
        }
        catch (Exception ex)
        {
            _logger.LogProductEventHandlerFailed(nameof(ProductDeletedEvent), evt.ProductId, ex.Message);
        }
    }

    private async Task HandleApprovedAsync(
        ProductApprovedEvent evt, MessageContext ctx, CancellationToken ct)
    {
        _logger.LogProductEventReceived(nameof(ProductApprovedEvent), evt.ProductId);
        try
        {
            // Refresh cache so price lookups use the approved product immediately
            if (!string.IsNullOrWhiteSpace(evt.Barcode))
            {
                await _productCache.GetProductByBarcodeAsync(evt.Barcode, ct);
                _logger.LogProductApprovedCacheReady(evt.ProductId, evt.Barcode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogProductEventHandlerFailed(nameof(ProductApprovedEvent), evt.ProductId, ex.Message);
        }
    }

    private Task HandleRejectedAsync(
        ProductRejectedEvent evt, MessageContext ctx, CancellationToken ct)
    {
        // Rejected products are still stored; no price action needed beyond logging.
        _logger.LogProductEventReceived(nameof(ProductRejectedEvent), evt.ProductId);
        return Task.CompletedTask;
    }

    private async Task HandleRenamedAsync(
        ProductRenamedEvent evt, MessageContext ctx, CancellationToken ct)
    {
        _logger.LogProductEventReceived(nameof(ProductRenamedEvent), evt.ProductId);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IPriceRepository>();
            var count = await repo.UpdateProductNameOnPricesAsync(evt.ProductId, evt.NewName, ct);
            _logger.LogPriceProductNameUpdated(evt.ProductId, count, evt.NewName);
        }
        catch (Exception ex)
        {
            _logger.LogProductEventHandlerFailed(nameof(ProductRenamedEvent), evt.ProductId, ex.Message);
        }
    }

    private async Task HandleBarcodeChangedAsync(
        ProductBarcodeChangedEvent evt, MessageContext ctx, CancellationToken ct)
    {
        _logger.LogProductEventReceived(nameof(ProductBarcodeChangedEvent), evt.ProductId);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IPriceRepository>();
            var count = await repo.UpdateProductUpcOnPricesAsync(evt.ProductId, evt.NewBarcode, ct);
            _logger.LogPriceProductUpcUpdated(evt.ProductId, count, evt.NewBarcode);

            // Refresh cache for new barcode
            if (!string.IsNullOrWhiteSpace(evt.NewBarcode))
            {
                await _productCache.GetProductByBarcodeAsync(evt.NewBarcode, ct);
                _logger.LogProductCacheRefreshed(evt.ProductId, evt.NewBarcode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogProductEventHandlerFailed(nameof(ProductBarcodeChangedEvent), evt.ProductId, ex.Message);
        }
    }

    private Task HandleIngredientsChangedAsync(
        ProductIngredientsChangedEvent evt, MessageContext ctx, CancellationToken ct)
    {
        // Price data is not directly affected by ingredient changes; log only.
        _logger.LogProductEventReceived(nameof(ProductIngredientsChangedEvent), evt.ProductId);
        return Task.CompletedTask;
    }
}
