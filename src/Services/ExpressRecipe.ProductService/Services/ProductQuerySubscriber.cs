using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Options;
using ExpressRecipe.Shared.Messages;

namespace ExpressRecipe.ProductService.Services;

/// <summary>
/// Hosted service that subscribes to product barcode query messages.
/// Other services (e.g. PriceService) can use request/response messaging
/// to look up products by barcode without making REST API calls.
/// </summary>
public sealed class ProductQuerySubscriber : IHostedService
{
    private readonly IMessageBus _bus;
    private readonly ILogger<ProductQuerySubscriber> _logger;

    public ProductQuerySubscriber(IMessageBus bus, ILogger<ProductQuerySubscriber> logger)
    {
        _bus    = bus;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Higher prefetch + concurrency: product lookups are fast DB reads, safe to parallelize
        var queueOpts = new SubscribeOptions
        {
            RoutingMode = RoutingMode.CompetingConsumer,
            PrefetchCount = 50,
            ConsumerConcurrency = 8
        };

        await _bus.SubscribeRequestAsync<
            RequestProductByBarcodeQuery,
            ProductByBarcodeResponse,
            ProductQueryHandler>(queueOpts, cancellationToken);

        await _bus.SubscribeRequestAsync<
            RequestProductsByBarcodesQuery,
            ProductsByBarcodesResponse,
            ProductQueryHandler>(queueOpts, cancellationToken);

        _logger.LogInformation("[ProductQuerySubscriber] Subscribed to barcode query messages");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
