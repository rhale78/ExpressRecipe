using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.Messaging.Core.Options;
using ExpressRecipe.ScannerService.Data;
using ExpressRecipe.Shared.Messages;

namespace ExpressRecipe.ScannerService.Services;

/// <summary>
/// Subscribes to <see cref="GdprDeleteEvent"/> and hard-deletes all scanner data
/// owned by the user (scan history, alerts, OCR results, vision captures, unknown products).
/// </summary>
public sealed class GdprEventSubscriber : IHostedService
{
    private readonly IMessageBus _bus;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GdprEventSubscriber> _logger;

    public GdprEventSubscriber(
        IMessageBus bus,
        IServiceScopeFactory scopeFactory,
        ILogger<GdprEventSubscriber> logger)
    {
        _bus = bus;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        SubscribeOptions opts = new SubscribeOptions { RoutingMode = RoutingMode.Broadcast };
        await _bus.SubscribeAsync<GdprDeleteEvent>(HandleDeleteAsync, opts, cancellationToken);
        _logger.LogInformation("[ScannerService] Subscribed to GDPR delete events");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task HandleDeleteAsync(
        GdprDeleteEvent evt,
        MessageContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "[ScannerService] GDPR delete received for user {UserId} (requestId {RequestId})",
            evt.UserId, evt.RequestId);

        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            IScannerRepository repo = scope.ServiceProvider.GetRequiredService<IScannerRepository>();
            await repo.DeleteUserDataAsync(evt.UserId, cancellationToken);
            _logger.LogInformation(
                "[ScannerService] GDPR delete complete for user {UserId}", evt.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[ScannerService] Error processing GDPR delete for user {UserId}", evt.UserId);
        }
    }
}
