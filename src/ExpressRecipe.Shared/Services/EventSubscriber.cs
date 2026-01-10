using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.Shared.Services;

/// <summary>
/// Base class for RabbitMQ event subscribers
/// </summary>
public abstract class EventSubscriber : BackgroundService
{
    private readonly IConnectionFactory _connectionFactory;
    protected readonly ILogger Logger;
    private IConnection? _connection;
    private IChannel? _channel;
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
            // retry policy for broker connectivity
            var delayMs = 1000;
            for (var attempt = 1; attempt <= 5; attempt++)
            {
                try
                {
                    _connection = await _connectionFactory.CreateConnectionAsync(stoppingToken);
                    _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
                    break;
                }
                catch (Exception ex) when (attempt < 5 && !stoppingToken.IsCancellationRequested)
                {
                    Logger.LogWarning(ex, "RabbitMQ connection attempt {Attempt} failed. Retrying in {Delay}ms...", attempt, delayMs);
                    await Task.Delay(delayMs, stoppingToken);
                    delayMs = Math.Min(delayMs * 2, 15000);
                }
            }

            if (_connection is null || _channel is null)
            {
                Logger.LogError("RabbitMQ broker unreachable. Subscriber will not start.");
                return; // exit background loop
            }

            // Declare exchange
            await _channel.ExchangeDeclareAsync(exchange: ExchangeName, type: ExchangeType.Topic, durable: true, cancellationToken: stoppingToken);

            // Declare queue
            await _channel.QueueDeclareAsync(queue: QueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);

            // Bind queue to routing keys
            foreach (var routingKey in RoutingKeys)
            {
                await _channel.QueueBindAsync(queue: QueueName, exchange: ExchangeName, routingKey: routingKey, cancellationToken: stoppingToken);
                Logger.LogInformation("Bound {Queue} to routing key {RoutingKey}", QueueName, routingKey);
            }

            // Set up consumer
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    Logger.LogInformation("Received event with routing key {RoutingKey}", ea.RoutingKey);

                    await ProcessEventAsync(ea.RoutingKey, message, stoppingToken);

                    await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error processing event");
                    await _channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                }
            };

            await _channel.BasicConsumeAsync(queue: QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

            Logger.LogInformation("{QueueName} subscriber started", QueueName);

            // Keep the service running
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
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
        _channel?.CloseAsync().GetAwaiter().GetResult();
        _channel?.Dispose();
        _connection?.CloseAsync().GetAwaiter().GetResult();
        _connection?.Dispose();
        base.Dispose();
    }
}
