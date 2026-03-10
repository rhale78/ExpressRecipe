using System.Net.Http.Json;
using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.Messaging.Core.Options;
using ExpressRecipe.Shared.Messages;

namespace ExpressRecipe.PriceService.Services;

/// <summary>
/// Subscribes to <see cref="PriceDropEvent"/> and forwards a WorkQueue item to
/// MealPlanningService for each household that tracks the affected product.
/// ExpiresAt = 48 hours (price deals are time-sensitive).
/// </summary>
public sealed class PriceDropQueueSubscriber : IHostedService
{
    private readonly IMessageBus _bus;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<PriceDropQueueSubscriber> _logger;
    private readonly string _mealPlanningServiceUrl;

    public PriceDropQueueSubscriber(
        IMessageBus bus,
        IHttpClientFactory httpFactory,
        IConfiguration configuration,
        ILogger<PriceDropQueueSubscriber> logger)
    {
        _bus                    = bus;
        _httpFactory            = httpFactory;
        _logger                 = logger;
        _mealPlanningServiceUrl = configuration["Services:MealPlanningService"] ?? "http://localhost:5106";
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        SubscribeOptions opts = new() { RoutingMode = RoutingMode.Broadcast };
        await _bus.SubscribeAsync<PriceDropEvent>(HandleAsync, opts, cancellationToken);
        _logger.LogInformation("[PriceDropQueueSubscriber] Subscribed to PriceDropEvent");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    internal async Task HandleAsync(
        PriceDropEvent evt,
        MessageContext context,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "PriceDropEvent received: product {ProductId} dropped from {OldPrice} to {NewPrice} at store {StoreId}",
            evt.ProductId, evt.OldPrice, evt.NewPrice, evt.StoreId);

        using HttpClient client = _httpFactory.CreateClient("MealPlanningService");

        foreach (Guid householdId in evt.HouseholdIds)
        {
            try
            {
                string payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    productId   = evt.ProductId,
                    storeId     = evt.StoreId,
                    oldPrice    = evt.OldPrice,
                    newPrice    = evt.NewPrice,
                    productName = evt.ProductName,
                    storeName   = evt.StoreName
                });

                object upsertRequest = new
                {
                    HouseholdId    = householdId,
                    ItemType       = "PriceDrop",
                    Priority       = 5,
                    Title          = $"{evt.ProductName} is on sale at {evt.StoreName}",
                    Body           = $"Price dropped from ${evt.OldPrice:F2} to ${evt.NewPrice:F2}",
                    ActionPayload  = payload,
                    SourceEntityId = evt.ProductId,
                    SourceService  = "Price",
                    ExpiresAt      = DateTime.UtcNow.AddHours(48)  // price deals are time-sensitive
                };

                HttpResponseMessage response = await client.PostAsJsonAsync(
                    $"{_mealPlanningServiceUrl}/api/work-queue/internal/upsert", upsertRequest, ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "PriceDropQueueSubscriber: upsert returned {Status} for household {HouseholdId}",
                        response.StatusCode, householdId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "PriceDropQueueSubscriber: error upserting for household {HouseholdId}", householdId);
            }
        }
    }
}
