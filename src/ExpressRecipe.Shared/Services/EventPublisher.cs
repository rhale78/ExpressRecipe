using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.Shared.Services
{
    /// <summary>
    /// RabbitMQ event publisher for inter-service communication
    /// </summary>
    public class EventPublisher : IDisposable
    {
        private readonly IConnection? _connection;
        private readonly IChannel? _channel;
        private readonly ILogger<EventPublisher> _logger;
        private const string ExchangeName = "expressrecipe.events";

        public EventPublisher(IConnectionFactory connectionFactory, ILogger<EventPublisher> logger)
        {
            _logger = logger;

            try
            {
                _connection = connectionFactory.CreateConnectionAsync().GetAwaiter().GetResult();
                _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

                // Declare topic exchange
                _channel.ExchangeDeclareAsync(exchange: ExchangeName, type: ExchangeType.Topic, durable: true).GetAwaiter().GetResult();

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

                BasicProperties properties = new BasicProperties
                {
                    Persistent = true,
                    ContentType = "application/json",
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                };

                await _channel.BasicPublishAsync(
                    exchange: ExchangeName,
                    routingKey: routingKey,
                    mandatory: false,
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
            _channel?.CloseAsync().GetAwaiter().GetResult();
            _channel?.Dispose();
            _connection?.CloseAsync().GetAwaiter().GetResult();
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
}
