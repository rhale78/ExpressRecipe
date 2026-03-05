using ExpressRecipe.PriceService.Data;

namespace ExpressRecipe.PriceService.Services;

/// <summary>
/// Background service that drains the <see cref="IPriceIngestionChannel"/> and
/// persists each price observation to the database, then fires a
/// <see cref="IPriceEventPublisher.PublishPriceRecordedAsync"/> event.
/// This is the async path – the sync REST path (<c>PriceController.RecordPrice</c>)
/// writes directly to the repository and also fires the same event.
/// </summary>
public sealed class PriceIngestionChannelWorker : BackgroundService
{
    private readonly IPriceIngestionChannel _channel;
    private readonly IServiceScopeFactory   _scopeFactory;
    private readonly IPriceEventPublisher   _events;
    private readonly ILogger<PriceIngestionChannelWorker> _logger;

    public PriceIngestionChannelWorker(
        IPriceIngestionChannel channel,
        IServiceScopeFactory   scopeFactory,
        IPriceEventPublisher   events,
        ILogger<PriceIngestionChannelWorker> logger)
    {
        _channel      = channel;
        _scopeFactory = scopeFactory;
        _events       = events;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[PriceIngestionChannelWorker] Started – waiting for price requests");

        await foreach (var request in _channel.ReadAllAsync(stoppingToken))
        {
            await ProcessAsync(request, stoppingToken);
        }

        _logger.LogInformation("[PriceIngestionChannelWorker] Stopped");
    }

    private async Task ProcessAsync(PriceIngestionRequest request, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository  = scope.ServiceProvider.GetRequiredService<IPriceRepository>();

            var priceId = await repository.RecordPriceAsync(
                request.ProductId, request.StoreId, request.Price,
                request.SubmittedBy, request.ObservedAt);

            _logger.LogInformation(
                "[PriceIngestionChannelWorker] Recorded price {PriceId} for product {ProductId} at store {StoreId} = ${Price}",
                priceId, request.ProductId, request.StoreId, request.Price);

            // Fire event – both sync and async paths converge here
            await _events.PublishPriceRecordedAsync(
                priceId, request.ProductId, request.StoreId,
                request.Price, request.SubmittedBy, ct);
        }
        catch (OperationCanceledException) { /* shutting down */ }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[PriceIngestionChannelWorker] Failed to process price for product {ProductId}: {Error}",
                request.ProductId, ex.Message);
        }
    }
}
