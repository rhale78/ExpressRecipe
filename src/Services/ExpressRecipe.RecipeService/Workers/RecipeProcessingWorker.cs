using ExpressRecipe.RecipeService.Data;
using ExpressRecipe.RecipeService.Services;
using ExpressRecipe.Client.Shared.Services;

namespace ExpressRecipe.RecipeService.Workers;

/// <summary>
/// Background service that processes staged recipes using BatchRecipeProcessor.
/// </summary>
public class RecipeProcessingWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RecipeProcessingWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan PROCESSING_INTERVAL = TimeSpan.FromSeconds(10);

    public RecipeProcessingWorker(
        IServiceProvider serviceProvider,
        ILogger<RecipeProcessingWorker> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RecipeProcessingWorker starting...");

        // Check if processing is enabled
        var processingEnabled = _configuration.GetValue<bool>("RecipeImport:AutoProcessing", true);
        _logger.LogInformation("Recipe auto-processing is: {Status}", processingEnabled ? "ENABLED" : "DISABLED");
        
        if (!processingEnabled)
        {
            _logger.LogInformation("Auto-processing is disabled in configuration. Worker will not run.");
            return;
        }

        // Wait for migrations and import worker to get ahead
        _logger.LogInformation("Waiting 30 seconds for migrations and initial imports before starting processing...");
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        var processingResetMins = _configuration.GetValue<int>("RecipeImport:ProcessingResetMinutes", 30);
        var failedResetMins = _configuration.GetValue<int>("RecipeImport:FailedResetMinutes", 30);

        // INITIAL RECOVERY: Reset STALE recipes from a previous crash
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var stagingRepo = scope.ServiceProvider.GetRequiredService<IRecipeStagingRepository>();
            
            _logger.LogInformation("Performing initial recovery: Resetting stale 'Processing' recipes to 'Pending' (>{P}m)...", processingResetMins);
            await stagingRepo.ResetProcessingStatusAsync(olderThanMinutes: processingResetMins);
            
            _logger.LogInformation("Performing initial recovery: Resetting stale 'Failed' recipes to 'Pending' (>{F}m)...", failedResetMins);
            await stagingRepo.ResetFailedStatusAsync(olderThanMinutes: failedResetMins);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform initial recipe recovery.");
        }

        _logger.LogInformation("RecipeProcessingWorker entering main loop. Monitoring for stale (>{P}m) and failed (>{F}m) records.", 
            processingResetMins, failedResetMins);
            
        DateTime lastWatchdogCheck = DateTime.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var stagingRepo = scope.ServiceProvider.GetRequiredService<IRecipeStagingRepository>();
                var recipeRepo = scope.ServiceProvider.GetRequiredService<IRecipeRepository>();
                var processorLogger = scope.ServiceProvider.GetRequiredService<ILogger<BatchRecipeProcessor>>();

                // PERIODIC WATCHDOG: Every 15 mins, check for stale or failed records
                if (DateTime.UtcNow - lastWatchdogCheck > TimeSpan.FromMinutes(15))
                {
                    _logger.LogInformation("Watchdog: Checking for stale 'Processing' (>{P}m) or 'Failed' (>{F}m) recipes...", 
                        processingResetMins, failedResetMins);
                    
                    await stagingRepo.ResetProcessingStatusAsync(olderThanMinutes: processingResetMins);
                    await stagingRepo.ResetFailedStatusAsync(olderThanMinutes: failedResetMins);
                    
                    lastWatchdogCheck = DateTime.UtcNow;
                }

                _logger.LogDebug("Checking for pending recipes in staging table...");
                var pendingCount = await stagingRepo.GetPendingCountAsync();

                if (pendingCount > 0)
                {
                    _logger.LogInformation("Found {Count} pending recipes to process", pendingCount);

                    var ingredientClient = scope.ServiceProvider.GetRequiredService<IngredientServiceClient>();

                    var maxParallelism = _configuration.GetValue<int>("RecipeImport:MaxParallelism", 4);
                    var batchSize = _configuration.GetValue<int>("RecipeImport:BatchSize", 5000);
                    var bufferSize = _configuration.GetValue<int>("RecipeImport:BufferSize", 50000);

                    var batchProcessor = new BatchRecipeProcessor(
                        processorLogger,
                        _configuration,
                        ingredientClient,
                        maxParallelism,
                        batchSize,
                        bufferSize);

                    var result = await batchProcessor.ProcessStagedRecipesAsync(
                        stagingRepo,
                        recipeRepo,
                        stoppingToken);

                    _logger.LogInformation(
                        "Recipe processing cycle complete: {Success} succeeded, {Failed} failed",
                        result.SuccessCount,
                        result.FailureCount);
                }
                else
                {
                    // Minimal logging for idle state
                    if (Random.Shared.Next(0, 60) == 0) // Roughly once every 10 mins (10s interval * 60)
                    {
                        _logger.LogInformation("RecipeProcessingWorker is idle. No pending recipes found.");
                    }
                }

                await Task.Delay(PROCESSING_INTERVAL, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during recipe processing cycle. Will retry.");
                await Task.Delay(PROCESSING_INTERVAL, stoppingToken);
            }
        }
    }
}
