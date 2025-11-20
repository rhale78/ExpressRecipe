using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace ExpressRecipe.Shared.Services;

/// <summary>
/// RabbitMQ event publisher for inter-service communication
/// </summary>
public class EventPublisher : IDisposable
{
    private readonly IConnection? _connection;
    private readonly IModel? _channel;
    private readonly ILogger<EventPublisher> _logger;
    private const string ExchangeName = "expressrecipe.events";

    public EventPublisher(IConnectionFactory connectionFactory, ILogger<EventPublisher> logger)
    {
        _logger = logger;

        try
        {
            _connection = connectionFactory.CreateConnection();
            _channel = _connection.CreateModel();

            // Declare topic exchange
            _channel.ExchangeDeclare(exchange: ExchangeName, type: ExchangeType.Topic, durable: true);

            _logger.LogInformation("EventPublisher initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize EventPublisher");
        }
    }

    public async Task PublishAsync<T>(string routingKey, T @event)
    {
        if (_channel == null)
        {
            _logger.LogWarning("Cannot publish event - channel not initialized");
            return;
        }

        try
        {
            var message = JsonSerializer.Serialize(@event);
            var body = Encoding.UTF8.GetBytes(message);

            var properties = _channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            _channel.BasicPublish(
                exchange: ExchangeName,
                routingKey: routingKey,
                basicProperties: properties,
                body: body);

            _logger.LogInformation("Published event {EventType} with routing key {RoutingKey}",
                typeof(T).Name, routingKey);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing event {EventType}", typeof(T).Name);
        }
    }

    public void Dispose()
    {
        _channel?.Close();
        _channel?.Dispose();
        _connection?.Close();
        _connection?.Dispose();
    }
}

/// <summary>
/// Common event types for inter-service communication
/// </summary>
public static class EventKeys
{
    public const string ProductCreated = "product.created";
    public const string ProductUpdated = "product.updated";
    public const string RecipeCreated = "recipe.created";
    public const string RecipeUpdated = "recipe.updated";
    public const string InventoryItemAdded = "inventory.item.added";
    public const string InventoryItemExpiring = "inventory.item.expiring";
    public const string RecallPublished = "recall.published";
    public const string PriceChanged = "price.changed";
    public const string UserRegistered = "user.registered";
}
