using ExpressRecipe.RecallService.Data;
using ExpressRecipe.RecallService.Services;

namespace ExpressRecipe.RecallService.Services
{
    public class RecallMonitorWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RecallMonitorWorker> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromHours(1); // Check every hour

        public RecallMonitorWorker(IServiceProvider serviceProvider, ILogger<RecallMonitorWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Recall Monitor Worker started");

            // Wait 1 minute before first run to allow services to initialize
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckForNewRecallsAsync(stoppingToken);
                    await Task.Delay(_interval, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Error in Recall Monitor Worker");
                    await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
                }
            }

            _logger.LogInformation("Recall Monitor Worker stopped");
        }

        private async Task CheckForNewRecallsAsync(CancellationToken cancellationToken)
        {
            using IServiceScope scope = _serviceProvider.CreateScope();
            IRecallRepository repository = scope.ServiceProvider.GetRequiredService<IRecallRepository>();
            FDARecallImportService? importService = scope.ServiceProvider.GetService<FDARecallImportService>();

            _logger.LogInformation("Checking for new recalls...");

            if (importService != null)
            {
                try
                {
                    // Import recent recalls from FDA
                    RecallImportResult result = await importService.ImportRecentRecallsAsync(limit: 50);
                    _logger.LogInformation("Imported {Count} new FDA recalls, {Errors} errors",
                        result.SuccessCount, result.FailureCount);

                    // Import meat/poultry recalls from FDA (includes USDA-regulated products)
                    RecallImportResult meatPoultryResult = await importService.ImportMeatPoultryRecallsFromFDAAsync(limit: 50);
                    _logger.LogInformation("Imported {Count} meat/poultry recalls, {Errors} errors",
                        meatPoultryResult.SuccessCount, meatPoultryResult.FailureCount);

                    // Note: ImportUSDARecallsAsync() is deprecated - no public USDA API exists
                    // Meat/poultry recalls are now imported from FDA API above

                    // TODO: Check user subscriptions and send alerts
                    // This would require cross-service communication with NotificationService
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error importing recalls");
                }
            }

            _logger.LogInformation("Recall check completed");
        }
    }
}
