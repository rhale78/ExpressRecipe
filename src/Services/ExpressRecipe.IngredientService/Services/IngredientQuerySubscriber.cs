using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Options;
using ExpressRecipe.Shared.Messages;

namespace ExpressRecipe.IngredientService.Services;

/// <summary>
/// Subscribes to ingredient query messages so other services can resolve ingredients
/// via the message bus instead of REST HTTP calls.
/// </summary>
public sealed class IngredientQuerySubscriber : IHostedService
{
    private readonly IMessageBus _bus;
    private readonly ILogger<IngredientQuerySubscriber> _logger;

    public IngredientQuerySubscriber(IMessageBus bus, ILogger<IngredientQuerySubscriber> logger)
    {
        _bus    = bus;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // DB reads/writes are safe to parallelise; use higher concurrency to keep the pipeline full
        var opts = new SubscribeOptions
        {
            RoutingMode          = RoutingMode.CompetingConsumer,
            PrefetchCount        = 50,
            ConsumerConcurrency  = 8
        };

        await _bus.SubscribeRequestAsync<
            RequestIngredientLookup,
            IngredientLookupResponse,
            IngredientQueryHandler>(opts, cancellationToken);

        await _bus.SubscribeRequestAsync<
            RequestIngredientBulkCreate,
            IngredientBulkCreateResponse,
            IngredientQueryHandler>(opts, cancellationToken);

        _logger.LogInformation("[IngredientQuerySubscriber] Subscribed to ingredient query messages");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
