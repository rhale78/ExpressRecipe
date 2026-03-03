using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.Messaging.Core.Options;
using ExpressRecipe.Messaging.Demo.Messages;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.Messaging.Demo.Workers;

/// <summary>
/// A background worker that publishes various demo messages to showcase all routing patterns.
/// </summary>
public sealed class PublisherWorker : BackgroundService
{
    private readonly IMessageBus _bus;
    private readonly ILogger<PublisherWorker> _logger;

    public PublisherWorker(IMessageBus bus, ILogger<PublisherWorker> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Give consumers time to start
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        _logger.LogInformation("=== Publisher starting demo scenarios ===");

        // 1. Broadcast: ProductCreatedEvent to all subscribers
        _logger.LogInformation("--- Scenario 1: Broadcast ProductCreatedEvent ---");
        await _bus.PublishAsync(new ProductCreatedEvent(
            Guid.NewGuid(), "Organic Oat Milk", "NatureFarm", "1234567890", 3.99m),
            new PublishOptions { RoutingMode = RoutingMode.Broadcast },
            stoppingToken);

        // 2. Competing consumer: ProcessInventoryCommand
        _logger.LogInformation("--- Scenario 2: Work queue ProcessInventoryCommand (x3) ---");
        for (int i = 0; i < 3; i++)
        {
            await _bus.PublishAsync(new ProcessInventoryCommand(
                Guid.NewGuid(), i % 2 == 0 ? 10 : -5, $"WH-{i + 1:D3}", "stock-adjustment"),
                new PublishOptions { RoutingMode = RoutingMode.CompetingConsumer },
                stoppingToken);
        }

        // 3. Request/response: GetProductQuery
        _logger.LogInformation("--- Scenario 3: Request/response GetProductQuery ---");
        try
        {
            var productId = Guid.NewGuid();
            var response = await _bus.RequestAsync<GetProductQuery, ProductQueryResponse>(
                new GetProductQuery(productId),
                new RequestOptions { Timeout = TimeSpan.FromSeconds(10) },
                stoppingToken);

            _logger.LogInformation("Request/response result: Found={Found} Name={Name}", response.Found, response.Name);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Request timed out (no handler registered or RabbitMQ not available)");
        }

        // 4. Direct send to specific service
        _logger.LogInformation("--- Scenario 4: Direct send AlertNotification to notification-service ---");
        await _bus.SendToServiceAsync(
            new AlertNotification("HIGH", "Low Stock Alert", "Product XYZ is below minimum stock level.", DateTimeOffset.UtcNow),
            "notification-service",
            cancellationToken: stoppingToken);

        // 5. Broadcast RecipePublishedEvent with 30-second TTL
        _logger.LogInformation("--- Scenario 5: Broadcast RecipePublishedEvent with TTL=30s ---");
        await _bus.PublishAsync(
            new RecipePublishedEvent(Guid.NewGuid(), "Vegan Banana Pancakes", "Chef Maria", ["vegan", "breakfast", "quick"]),
            new PublishOptions
            {
                RoutingMode = RoutingMode.Broadcast,
                Ttl = TimeSpan.FromSeconds(30)
            },
            stoppingToken);

        // 6. Direct send to named queue
        _logger.LogInformation("--- Scenario 6: Direct send to named queue ---");
        await _bus.SendAsync(
            new AlertNotification("LOW", "Daily Report Ready", "Your daily summary is ready to review.", DateTimeOffset.UtcNow),
            "expressrecipe.direct.alertnotification.queue",
            cancellationToken: stoppingToken);

        _logger.LogInformation("=== All publisher scenarios complete ===");
    }
}
