using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace ExpressRecipe.Shared.Services;

/// <summary>
/// Base class for RabbitMQ event subscribers
/// </summary>
public abstract class EventSubscriber : BackgroundService
{
    private readonly IConnectionFactory _connectionFactory;
    protected readonly ILogger Logger;
    private IConnection? _connection;
    private IModel? _channel;
    private const string ExchangeName = "expressrecipe.events";

    protected EventSubscriber(IConnectionFactory connectionFactory, ILogger logger)
    {
        _connectionFactory = connectionFactory;
        Logger = logger;
    }

    protected abstract string QueueName { get; }
    protected abstract List<string> RoutingKeys { get; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _connection = _connectionFactory.CreateConnection();
            _channel = _connection.CreateModel();

            // Declare exchange
            _channel.ExchangeDeclare(exchange: ExchangeName, type: ExchangeType.Topic, durable: true);

            // Declare queue
            _channel.QueueDeclare(queue: QueueName, durable: true, exclusive: false, autoDelete: false);

            // Bind queue to routing keys
            foreach (var routingKey in RoutingKeys)
            {
                _channel.QueueBind(queue: QueueName, exchange: ExchangeName, routingKey: routingKey);
                Logger.LogInformation("Bound {Queue} to routing key {RoutingKey}", QueueName, routingKey);
            }

            // Set up consumer
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    Logger.LogInformation("Received event with routing key {RoutingKey}", ea.RoutingKey);

                    await ProcessEventAsync(ea.RoutingKey, message, stoppingToken);

                    _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error processing event");
                    _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                }
            };

            _channel.BasicConsume(queue: QueueName, autoAck: false, consumer: consumer);

            Logger.LogInformation("{QueueName} subscriber started", QueueName);

            // Keep the service running
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to start event subscriber");
        }
    }

    protected abstract Task ProcessEventAsync(string routingKey, string message, CancellationToken cancellationToken);

    protected T? DeserializeEvent<T>(string message)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to deserialize event");
            return default;
        }
    }

    public override void Dispose()
    {
        _channel?.Close();
        _channel?.Dispose();
        _connection?.Close();
        _connection?.Dispose();
        base.Dispose();
    }
}
