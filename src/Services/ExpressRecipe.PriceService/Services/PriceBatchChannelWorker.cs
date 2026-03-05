using ExpressRecipe.PriceService.Data;

namespace ExpressRecipe.PriceService.Services;

/// <summary>
/// Background service that drains the <see cref="IPriceBatchChannel"/> and
/// persists each price observation to the database, then fires a
/// <see cref="IPriceEventPublisher.PublishPriceRecordedAsync"/> event.
/// This is the async path – the sync REST path (<c>PriceController.RecordPrice</c>)
/// writes directly to the repository and also fires the same event.
/// Consistent with ProductBatchChannelWorker, RecipeBatchChannelWorker, IngredientBatchChannelWorker.
/// </summary>
public sealed class PriceBatchChannelWorker : BackgroundService
{
    private readonly IPriceBatchChannel   _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPriceEventPublisher _events;
    private readonly ILogger<PriceBatchChannelWorker> _logger;

    public PriceBatchChannelWorker(
        IPriceBatchChannel   channel,
        IServiceScopeFactory scopeFactory,
        IPriceEventPublisher events,
        ILogger<PriceBatchChannelWorker> logger)
    {
        _channel      = channel;
        _scopeFactory = scopeFactory;
        _events       = events;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[PriceBatchChannelWorker] Started – waiting for price batch items");

        await foreach (var item in _channel.ReadAllAsync(stoppingToken))
        {
            await ProcessAsync(item, stoppingToken);
        }

        _logger.LogInformation("[PriceBatchChannelWorker] Stopped");
    }

    private async Task ProcessAsync(PriceBatchItem item, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository  = scope.ServiceProvider.GetRequiredService<IPriceRepository>();

            var priceId = await repository.RecordPriceAsync(
                item.ProductId, item.StoreId, item.Price,
                item.SubmittedBy, item.ObservedAt);

            _logger.LogInformation(
                "[PriceBatchChannelWorker] Recorded price {PriceId} for product {ProductId} at store {StoreId} = ${Price}",
                priceId, item.ProductId, item.StoreId, item.Price);

            // Fire event – both sync and async paths converge here
            await _events.PublishPriceRecordedAsync(
                priceId, item.ProductId, item.StoreId,
                item.Price, item.SubmittedBy, ct);
        }
        catch (OperationCanceledException) { /* shutting down */ }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[PriceBatchChannelWorker] Failed to process price for product {ProductId}: {Error}",
                item.ProductId, ex.Message);
        }
    }
}
