using ExpressRecipe.MealPlanningService.Data;

namespace ExpressRecipe.MealPlanningService.Workers;

/// <summary>
/// Runs hourly to auto-expire WorkQueueItems whose ExpiresAt has passed.
/// </summary>
public sealed class WorkQueueCleanupWorker : BackgroundService
{
    private readonly IWorkQueueRepository _queue;
    private readonly ILogger<WorkQueueCleanupWorker> _logger;

    public WorkQueueCleanupWorker(IWorkQueueRepository queue, ILogger<WorkQueueCleanupWorker> logger)
    {
        _queue  = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WorkQueueCleanupWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _queue.ExpireStaleItemsAsync(stoppingToken);
                _logger.LogInformation("WorkQueueCleanupWorker: stale items expired");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "WorkQueueCleanupWorker: error expiring stale items");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }

        _logger.LogInformation("WorkQueueCleanupWorker stopped");
    }
}
