using ExpressRecipe.Messaging.RabbitMQ.Internal;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace ExpressRecipe.Messaging.RabbitMQ;

/// <summary>
/// An <see cref="IHostedService"/> that starts all registered consumers when the application starts
/// and gracefully shuts them down when the application stops.
/// </summary>
public sealed class RabbitMqConsumerHostedService : BackgroundService
{
    private readonly RabbitMqMessageBus _bus;
    private readonly SubscriptionRegistry _registry;
    private readonly IConnection _connection;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RabbitMqConsumerHostedService> _logger;

    private readonly List<IChannel> _consumerChannels = new();

    /// <summary>
    /// Initializes a new instance of <see cref="RabbitMqConsumerHostedService"/>.
    /// </summary>
    public RabbitMqConsumerHostedService(
        RabbitMqMessageBus bus,
        SubscriptionRegistry registry,
        IConnection connection,
        IServiceProvider serviceProvider,
        ILogger<RabbitMqConsumerHostedService> logger)
    {
        _bus = bus;
        _registry = registry;
        _connection = connection;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _bus.SetServiceProvider(_serviceProvider);

        var registrations = _registry.GetAll();
        if (registrations.Count == 0)
        {
            _logger.LogDebug("No subscriptions registered. Consumer hosted service is idle.");
            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
            return;
        }

        _logger.LogInformation("Starting {Count} consumer(s)...", registrations.Count);

        foreach (var registration in registrations)
        {
            try
            {
                var channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken).ConfigureAwait(false);
                _consumerChannels.Add(channel);
                await _bus.StartConsumerAsync(channel, registration, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start consumer for message type '{MessageType}'", registration.MessageType.Name);
            }
        }

        _logger.LogInformation("All consumers started.");

        // Keep running until cancelled
        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping consumers...");

        foreach (var channel in _consumerChannels)
        {
            try
            {
                await channel.CloseAsync(cancellationToken).ConfigureAwait(false);
                await channel.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing consumer channel");
            }
        }

        _consumerChannels.Clear();
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }
}
