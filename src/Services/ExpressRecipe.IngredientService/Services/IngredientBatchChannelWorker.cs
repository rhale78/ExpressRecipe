using ExpressRecipe.IngredientService.Data;
using ExpressRecipe.Shared.DTOs.Product;

namespace ExpressRecipe.IngredientService.Services;

/// <summary>
/// Background service that drains the <see cref="IIngredientBatchChannel"/> and
/// creates each ingredient in the database, then fires lifecycle events.
/// Single ingredient creates go directly through the REST controller (sync + event).
/// The existing /api/ingredient/bulk/create endpoint goes through the direct DB path;
/// this channel worker handles submissions made via /api/ingredient/batch.
/// </summary>
public sealed class IngredientBatchChannelWorker : BackgroundService
{
    private readonly IIngredientBatchChannel   _channel;
    private readonly IServiceScopeFactory      _scopeFactory;
    private readonly IIngredientEventPublisher _events;
    private readonly ILogger<IngredientBatchChannelWorker> _logger;

    public IngredientBatchChannelWorker(
        IIngredientBatchChannel   channel,
        IServiceScopeFactory      scopeFactory,
        IIngredientEventPublisher events,
        ILogger<IngredientBatchChannelWorker> logger)
    {
        _channel      = channel;
        _scopeFactory = scopeFactory;
        _events       = events;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[IngredientBatchChannelWorker] Started – waiting for batch ingredient submissions");

        await foreach (var item in _channel.ReadAllAsync(stoppingToken))
        {
            await ProcessAsync(item, stoppingToken);
        }

        _logger.LogInformation("[IngredientBatchChannelWorker] Stopped");
    }

    private async Task ProcessAsync(IngredientBatchItem item, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo        = scope.ServiceProvider.GetRequiredService<IIngredientRepository>();

            var id = await repo.CreateIngredientAsync(
                new CreateIngredientRequest { Name = item.Name },
                item.SubmittedBy);

            _logger.LogInformation(
                "[IngredientBatchChannelWorker] Created ingredient {IngredientId} '{Name}' (session={Session}) by user {UserId}",
                id, item.Name, item.SessionId, item.SubmittedBy);

            await _events.PublishCreatedAsync(id, item.Name, ct);
        }
        catch (OperationCanceledException) { /* shutting down */ }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[IngredientBatchChannelWorker] Failed to create ingredient '{Name}': {Error}",
                item.Name, ex.Message);
        }
    }
}
