using ExpressRecipe.InventoryService.Data;

namespace ExpressRecipe.InventoryService.Services;

public class ExpirationAlertWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExpirationAlertWorker> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(6); // Run every 6 hours

    public ExpirationAlertWorker(IServiceProvider serviceProvider, ILogger<ExpirationAlertWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Expiration Alert Worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpirationAlertsAsync(stoppingToken);
                await Task.Delay(_interval, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in Expiration Alert Worker");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Wait before retry
            }
        }

        _logger.LogInformation("Expiration Alert Worker stopped");
    }

    private async Task ProcessExpirationAlertsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IInventoryRepository>();

        _logger.LogInformation("Processing expiration alerts...");

        // Get all unique users with inventory items
        // For production, you'd query the database for all active users
        // For now, this is a placeholder implementation

        _logger.LogInformation("Expiration alerts processed successfully");
    }
}
