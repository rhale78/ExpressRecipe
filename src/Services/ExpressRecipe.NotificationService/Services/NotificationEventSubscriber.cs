using ExpressRecipe.NotificationService.Data;
using ExpressRecipe.Shared.Services;
using RabbitMQ.Client;

namespace ExpressRecipe.NotificationService.Services;

/// <summary>
/// Subscribes to events and creates notifications for users
/// </summary>
public class NotificationEventSubscriber : EventSubscriber
{
    private readonly IServiceProvider _serviceProvider;

    public NotificationEventSubscriber(
        IConnectionFactory connectionFactory,
        ILogger<NotificationEventSubscriber> logger,
        IServiceProvider serviceProvider)
        : base(connectionFactory, logger)
    {
        _serviceProvider = serviceProvider;
    }

    protected override string QueueName => "notification-service-queue";

    protected override List<string> RoutingKeys => new()
    {
        "inventory.item.expiring",
        "recall.published",
        "price.changed",
        "product.created",
        "recipe.created"
    };

    protected override async Task ProcessEventAsync(string routingKey, string message, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<INotificationRepository>();

        switch (routingKey)
        {
            case "inventory.item.expiring":
                await HandleInventoryExpiringAsync(message, repository);
                break;

            case "recall.published":
                await HandleRecallPublishedAsync(message, repository);
                break;

            case "price.changed":
                await HandlePriceChangedAsync(message, repository);
                break;

            case "product.created":
                await HandleProductCreatedAsync(message, repository);
                break;

            case "recipe.created":
                await HandleRecipeCreatedAsync(message, repository);
                break;

            default:
                Logger.LogWarning("Unknown routing key: {RoutingKey}", routingKey);
                break;
        }
    }

    private async Task HandleInventoryExpiringAsync(string message, INotificationRepository repository)
    {
        var @event = DeserializeEvent<InventoryExpiringEvent>(message);
        if (@event == null) return;

        await repository.CreateNotificationAsync(
            @event.UserId,
            "InventoryExpiring",
            "Items Expiring Soon",
            $"{@event.ItemCount} items in your inventory are expiring in the next {@event.DaysUntilExpiration} days.",
            "/inventory/expiring",
            new Dictionary<string, string> { ["itemCount"] = @event.ItemCount.ToString() }
        );

        Logger.LogInformation("Created expiring notification for user {UserId}", @event.UserId);
    }

    private async Task HandleRecallPublishedAsync(string message, INotificationRepository repository)
    {
        var @event = DeserializeEvent<RecallPublishedEvent>(message);
        if (@event == null) return;

        // TODO: Query users who have affected products in their inventory
        // For now, this is a placeholder
        Logger.LogInformation("Recall published: {RecallId} - {Severity}", @event.RecallId, @event.Severity);
    }

    private async Task HandlePriceChangedAsync(string message, INotificationRepository repository)
    {
        var @event = DeserializeEvent<PriceChangedEvent>(message);
        if (@event == null) return;

        if (@event.PercentChange <= -10) // 10% price drop
        {
            // TODO: Notify users who have this product in their shopping lists
            Logger.LogInformation("Price dropped {Percent}% for product {ProductId}", @event.PercentChange, @event.ProductId);
        }
    }

    private async Task HandleProductCreatedAsync(string message, INotificationRepository repository)
    {
        var @event = DeserializeEvent<ProductCreatedEvent>(message);
        if (@event == null) return;

        Logger.LogInformation("Product created: {ProductId}", @event.ProductId);
    }

    private async Task HandleRecipeCreatedAsync(string message, INotificationRepository repository)
    {
        var @event = DeserializeEvent<RecipeCreatedEvent>(message);
        if (@event == null) return;

        Logger.LogInformation("Recipe created: {RecipeId}", @event.RecipeId);
    }
}

// Event DTOs
public class InventoryExpiringEvent
{
    public Guid UserId { get; set; }
    public int ItemCount { get; set; }
    public int DaysUntilExpiration { get; set; }
}

public class RecallPublishedEvent
{
    public Guid RecallId { get; set; }
    public string Severity { get; set; } = string.Empty;
    public List<Guid> ProductIds { get; set; } = new();
}

public class PriceChangedEvent
{
    public Guid ProductId { get; set; }
    public decimal OldPrice { get; set; }
    public decimal NewPrice { get; set; }
    public decimal PercentChange { get; set; }
}

public class ProductCreatedEvent
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class RecipeCreatedEvent
{
    public Guid RecipeId { get; set; }
    public string Name { get; set; } = string.Empty;
}
