using ExpressRecipe.Messaging.RabbitMQ.Internal;
using ExpressRecipe.Messaging.RabbitMQ.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace ExpressRecipe.Messaging.RabbitMQ;

/// <summary>
/// Starts consumers for every subscription registered in <see cref="SubscriptionRegistry"/>,
/// including subscriptions that arrive after the host has started (solving the IHostedService
/// startup-ordering timing race where subscriber services call SubscribeAsync in their own
/// StartAsync, which runs after this service's ExecuteAsync snapshot).
/// </summary>
public sealed class RabbitMqConsumerHostedService : BackgroundService
{
    private readonly RabbitMqMessageBus _bus;
    private readonly SubscriptionRegistry _registry;
    private readonly IConnection _connection;
    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMqMessagingOptions _options;
    private readonly ILogger<RabbitMqConsumerHostedService> _logger;

    private readonly List<IChannel> _consumerChannels = new();

    public RabbitMqConsumerHostedService(
        RabbitMqMessageBus bus,
        SubscriptionRegistry registry,
        IConnection connection,
        IServiceProvider serviceProvider,
        IOptions<RabbitMqMessagingOptions> options,
        ILogger<RabbitMqConsumerHostedService> logger)
    {
        _bus = bus;
        _registry = registry;
        _connection = connection;
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _bus.SetServiceProvider(_serviceProvider);

        _logger.LogInformation("Consumer hosted service started; awaiting subscription registrations.");

        // ReadAllAsync yields each registration as it arrives, whether registered before
        // or after this method starts executing.
        await foreach (var registration in _registry.Pending.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                var concurrency = (ushort)(registration.Options.ConsumerConcurrency ?? _options.ConsumerConcurrency);
                var channelOptions = new CreateChannelOptions(
                    publisherConfirmationsEnabled: false,
                    publisherConfirmationTrackingEnabled: false,
                    consumerDispatchConcurrency: concurrency);
                var channel = await _connection.CreateChannelAsync(channelOptions, stoppingToken).ConfigureAwait(false);
                _consumerChannels.Add(channel);
                await _bus.StartConsumerAsync(channel, registration, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start consumer for message type '{MessageType}'", registration.MessageType.Name);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _registry.Complete();

        _logger.LogInformation("Stopping {Count} consumer channel(s)...", _consumerChannels.Count);

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

