using ExpressRecipe.MealPlanningService.Data;
using ExpressRecipe.MealPlanningService.Logging;
using ExpressRecipe.Shared.Messages;
using ExpressRecipe.Messaging.Core.Abstractions;

namespace ExpressRecipe.MealPlanningService.Workers;

/// <summary>
/// Polls CookingHistory for rows where InventoryDeductionSent=0 and publishes
/// a <see cref="RecipeCookedEvent"/> to RabbitMQ routing key 'recipe.cooked'.
/// Runs every 5 minutes.
/// </summary>
public class RecipeCookedEventPublisherWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RecipeCookedEventPublisherWorker> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);

    public RecipeCookedEventPublisherWorker(
        IServiceProvider serviceProvider,
        ILogger<RecipeCookedEventPublisherWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RecipeCookedEventPublisherWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingDeductionsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in RecipeCookedEventPublisherWorker");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("RecipeCookedEventPublisherWorker stopped");
    }

    /// <summary>
    /// Processes all pending inventory deductions in one cycle.
    /// Exposed as <c>internal</c> so tests can call it directly without timing-based delays.
    /// </summary>
    internal async Task ProcessPendingDeductionsAsync(CancellationToken ct)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        IMealPlanningRepository repository = scope.ServiceProvider.GetRequiredService<IMealPlanningRepository>();
        IMessageBus? messageBus = scope.ServiceProvider.GetService<IMessageBus>();

        if (messageBus == null)
        {
            _logger.LogWarning("IMessageBus not registered — skipping inventory deduction event publishing");
            return;
        }

        List<CookingHistoryDto> pending = await repository.GetPendingInventoryDeductionsAsync(ct);

        if (pending.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Publishing {Count} pending inventory deduction events", pending.Count);

        foreach (CookingHistoryDto row in pending)
        {
            try
            {
                _logger.LogPublishingRecipeCookedEvent(row.UserId, row.HouseholdId, row.Id);
                RecipeCookedEvent evt = new(
                    RecipeId:         row.RecipeId,
                    UserId:           row.UserId,
                    HouseholdId:      row.HouseholdId,
                    Servings:         row.Servings,
                    CookedAt:         new DateTimeOffset(row.CookedAt, TimeSpan.Zero),
                    CookingHistoryId: row.Id);

                await messageBus.PublishAsync(evt, cancellationToken: ct);
                await repository.MarkInventoryDeductionSentAsync(row.Id, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to publish RecipeCookedEvent for history {HistoryId} — will retry next run",
                    row.Id);
                // Leave InventoryDeductionSent=0 so it retries next cycle
            }
        }
    }
}
