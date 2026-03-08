using ExpressRecipe.Messaging.Core.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ExpressRecipe.Messaging.RabbitMQ;

/// <summary>
/// Tracks RabbitMQ connection health and exposes <see cref="IMessagingStatus.IsAvailable"/>
/// so publishers can choose between messaging and REST fallback.
///
/// The Aspire <c>AddRabbitMQClient</c> registers an <see cref="AutorecoveringConnection"/>
/// which automatically reconnects after network interruptions. This service listens to the
/// connection events to keep <see cref="IsAvailable"/> accurate:
/// <list type="bullet">
///   <item><c>ConnectionShutdownAsync</c> → marks unavailable</item>
///   <item><c>RecoverySucceededAsync</c>  → marks available again</item>
/// </list>
/// </summary>
public sealed class MessagingStatusService : IHostedService, IMessagingStatus
{
    private readonly IConnection _connection;
    private readonly ILogger<MessagingStatusService> _logger;

    private volatile bool _isAvailable;

    public bool IsAvailable => _isAvailable;

    public MessagingStatusService(IConnection connection, ILogger<MessagingStatusService> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Assume connected at startup — Aspire waits for the container to be healthy.
        _isAvailable = _connection.IsOpen;

        _connection.ConnectionShutdownAsync += OnConnectionShutdownAsync;
        _connection.RecoverySucceededAsync  += OnRecoverySucceededAsync;

        _logger.LogInformation("Messaging status service started. IsAvailable={IsAvailable}", _isAvailable);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _connection.ConnectionShutdownAsync -= OnConnectionShutdownAsync;
        _connection.RecoverySucceededAsync  -= OnRecoverySucceededAsync;

        _isAvailable = false;
        return Task.CompletedTask;
    }

    private Task OnConnectionShutdownAsync(object sender, ShutdownEventArgs e)
    {
        _isAvailable = false;
        _logger.LogWarning("RabbitMQ connection lost: {Reason}. Messaging falling back to REST.", e.ReplyText);
        return Task.CompletedTask;
    }

    private Task OnRecoverySucceededAsync(object sender, AsyncEventArgs e)
    {
        _isAvailable = true;
        _logger.LogInformation("RabbitMQ connection recovered. Messaging restored.");
        return Task.CompletedTask;
    }
}
