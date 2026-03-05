using ExpressRecipe.ProductService.Data;
using ExpressRecipe.Shared.DTOs.Product;

namespace ExpressRecipe.ProductService.Services;

/// <summary>
/// Background service that drains the <see cref="IProductBatchChannel"/> and
/// creates each product in the database, then fires lifecycle events.
/// This is the async path for bulk product ingestion.
/// Single product creates go directly through the REST controller (sync + event).
/// </summary>
public sealed class ProductBatchChannelWorker : BackgroundService
{
    private readonly IProductBatchChannel  _channel;
    private readonly IServiceScopeFactory  _scopeFactory;
    private readonly IProductEventPublisher _events;
    private readonly ILogger<ProductBatchChannelWorker> _logger;

    public ProductBatchChannelWorker(
        IProductBatchChannel   channel,
        IServiceScopeFactory   scopeFactory,
        IProductEventPublisher events,
        ILogger<ProductBatchChannelWorker> logger)
    {
        _channel      = channel;
        _scopeFactory = scopeFactory;
        _events       = events;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[ProductBatchChannelWorker] Started – waiting for batch product submissions");

        await foreach (var item in _channel.ReadAllAsync(stoppingToken))
        {
            await ProcessAsync(item, stoppingToken);
        }

        _logger.LogInformation("[ProductBatchChannelWorker] Stopped");
    }

    private async Task ProcessAsync(ProductBatchItem item, CancellationToken ct)
    {
        try
        {
            using var scope      = _scopeFactory.CreateScope();
            var productRepo      = scope.ServiceProvider.GetRequiredService<IProductRepository>();
            var ingredientRepo   = scope.ServiceProvider.GetRequiredService<IIngredientRepository>();

            var productId = await productRepo.CreateAsync(item.Request, item.SubmittedBy);

            // Attach any pre-specified ingredient IDs
            if (item.Request.IngredientIds.Count > 0)
            {
                for (int i = 0; i < item.Request.IngredientIds.Count; i++)
                {
                    await ingredientRepo.AddProductIngredientAsync(
                        productId,
                        new AddProductIngredientRequest { IngredientId = item.Request.IngredientIds[i], OrderIndex = i },
                        item.SubmittedBy);
                }
            }

            _logger.LogInformation(
                "[ProductBatchChannelWorker] Created product {ProductId} '{Name}' (session={Session}) by user {UserId}",
                productId, item.Request.Name, item.SessionId, item.SubmittedBy);

            await _events.PublishCreatedAsync(
                productId,
                item.Request.Name ?? string.Empty,
                item.Request.Brand,
                item.Request.Barcode,
                item.Request.Category,
                approvalStatus: "Pending",
                submittedBy: item.SubmittedBy,
                ct: ct);
        }
        catch (OperationCanceledException) { /* shutting down */ }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[ProductBatchChannelWorker] Failed to create product '{Name}': {Error}",
                item.Request.Name, ex.Message);
        }
    }
}
