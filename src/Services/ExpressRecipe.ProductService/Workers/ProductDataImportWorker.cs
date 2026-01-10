using ExpressRecipe.ProductService.Data;
using ExpressRecipe.ProductService.Services;

namespace ExpressRecipe.ProductService.Workers;

/// <summary>
/// Background service that automatically imports product data from OpenFoodFacts
/// - On startup: Checks if database is empty and imports initial dataset
/// - Periodic: Downloads delta updates (products modified in last 14 days)
/// </summary>
public class ProductDataImportWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProductDataImportWorker> _logger;
    private readonly IConfiguration _configuration;
    private const int INITIAL_IMPORT_LIMIT = 5000000; // Import up to 5M products initially (all available)
    private const int DELTA_IMPORT_LIMIT = 50000; // Import up to 50k updates per delta run
    private readonly TimeSpan DELTA_UPDATE_INTERVAL = TimeSpan.FromDays(7); // Check weekly

    public ProductDataImportWorker(
        IServiceProvider serviceProvider,
        ILogger<ProductDataImportWorker> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ProductDataImportWorker starting...");

        // Check if auto-import is enabled
        var autoImportEnabled = _configuration.GetValue<bool>("ProductImport:AutoImport", true);
        if (!autoImportEnabled)
        {
            _logger.LogInformation("Auto-import is disabled in configuration. Worker will not run.");
            return;
        }

        // Wait a bit for the application to fully start
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        try
        {
            // Check if initial import is needed
            await PerformInitialImportIfNeededAsync(stoppingToken);

            // Schedule periodic delta updates
            await RunPeriodicDeltaUpdatesAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ProductDataImportWorker is stopping.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in ProductDataImportWorker");
        }
    }

    private async Task PerformInitialImportIfNeededAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var productRepository = scope.ServiceProvider.GetRequiredService<IProductRepository>();
        var importService = scope.ServiceProvider.GetRequiredService<OpenFoodFactsImportService>();

        try
        {
            // Check if products table is empty
            var productCount = await productRepository.GetProductCountAsync();

            _logger.LogInformation("Current product count: {Count}", productCount);

            // If we have fewer than 1000 products, perform initial import
            if (productCount < 1000)
            {
                // Get data source preference from configuration
                var dataSource = _configuration.GetValue<string>("ProductImport:DataSource", "Both");
                var enableCsv = _configuration.GetValue<bool>("ProductImport:EnableCsvImport", true);
                var enableJson = _configuration.GetValue<bool>("ProductImport:EnableJsonImport", false);
                var jsonDelayRecords = _configuration.GetValue<int>("ProductImport:JsonImportDelayRecords", 100000);

                _logger.LogInformation("Product database is nearly empty. Starting import (DataSource={DataSource}, CSV={EnableCsv}, JSON={EnableJson}, JSONDelay={JSONDelay})...",
                    dataSource, enableCsv, enableJson, jsonDelayRecords);

                var progress = new Progress<ImportProgress>(p =>
                {
                    if (p.PercentComplete % 5 == 0 || p.PercentComplete == 100)
                    {
                        _logger.LogInformation("Import progress: {Message} ({Percent}%)",
                            p.Message, p.PercentComplete);
                    }
                });

                // Determine which sources to use
                var useCsv = dataSource.Equals("Both", StringComparison.OrdinalIgnoreCase) || 
                             dataSource.Equals("CSV", StringComparison.OrdinalIgnoreCase) ||
                             enableCsv;

                var useJson = dataSource.Equals("Both", StringComparison.OrdinalIgnoreCase) || 
                              dataSource.Equals("JSON", StringComparison.OrdinalIgnoreCase) ||
                              enableJson;

                // Start CSV import task if enabled
                Task<BatchImportResult>? csvTask = null;
                if (useCsv)
                {
                    _logger.LogInformation("Starting CSV import task...");
                    csvTask = Task.Run(async () =>
                    {
                        return await importService.ImportFromCsvDataAsync(
                            dataFileUrl: null,
                            maxProducts: INITIAL_IMPORT_LIMIT,
                            cancellationToken: stoppingToken,
                            progress: new Progress<ImportProgress>(p =>
                            {
                                if (p.PercentComplete % 5 == 0)
                                {
                                    _logger.LogInformation("[CSV] {Message} ({Percent}%)", p.Message, p.PercentComplete);
                                }
                            }));
                    }, stoppingToken);
                }

                // Start JSON import task if enabled (with optional delay)
                Task<BatchImportResult>? jsonTask = null;
                if (useJson)
                {
                    _logger.LogInformation("Scheduling JSON import task (will start after {Delay} CSV records)...", jsonDelayRecords);
                    jsonTask = Task.Run(async () =>
                    {
                        // Wait for CSV to process some records first if both are running
                        if (useCsv && jsonDelayRecords > 0)
                        {
                            _logger.LogInformation("[JSON] Waiting for CSV to process {Delay} records before starting...", jsonDelayRecords);

                            // Poll staging table count until threshold met
                            while (!stoppingToken.IsCancellationRequested)
                            {
                                var stagedCount = await productRepository.GetProductCountAsync();
                                if (stagedCount >= jsonDelayRecords)
                                {
                                    _logger.LogInformation("[JSON] CSV has processed {Count} records, starting JSON import now", stagedCount);
                                    break;
                                }
                                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                            }
                        }

                        return await importService.ImportFromBulkDataAsync(
                            dataFileUrl: null,
                            maxProducts: INITIAL_IMPORT_LIMIT,
                            cancellationToken: stoppingToken,
                            progress: new Progress<ImportProgress>(p =>
                            {
                                if (p.PercentComplete % 5 == 0)
                                {
                                    _logger.LogInformation("[JSON] {Message} ({Percent}%)", p.Message, p.PercentComplete);
                                }
                            }));
                    }, stoppingToken);
                }

                // Wait for all tasks to complete
                var tasks = new List<Task<BatchImportResult>>();
                if (csvTask != null) tasks.Add(csvTask);
                if (jsonTask != null) tasks.Add(jsonTask);

                var results = await Task.WhenAll(tasks);

                // Combine results
                var totalSuccess = results.Sum(r => r.SuccessCount);
                var totalFailed = results.Sum(r => r.FailureCount);
                var totalProcessed = results.Sum(r => r.TotalProcessed);

                _logger.LogInformation(
                    "Initial import completed: {Success} successful, {Failed} failed. Total: {Total}",
                    totalSuccess, totalFailed, totalProcessed);

                // Log errors from all results
                foreach (var result in results)
                {
                    if (result.Errors.Any())
                    {
                        _logger.LogWarning("Import had {ErrorCount} errors. First few: {Errors}",
                            result.Errors.Count,
                            string.Join(", ", result.Errors.Take(5)));
                    }
                }
            }
            else
            {
                _logger.LogInformation("Product database already contains {Count} products. Skipping initial import.",
                    productCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform initial product import");
        }
    }

    private async Task RunPeriodicDeltaUpdatesAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting periodic delta update checks (every {Interval})", DELTA_UPDATE_INTERVAL);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wait for the next update cycle
                await Task.Delay(DELTA_UPDATE_INTERVAL, stoppingToken);

                _logger.LogInformation("Performing delta update check...");

                using var scope = _serviceProvider.CreateScope();
                var importService = scope.ServiceProvider.GetRequiredService<OpenFoodFactsImportService>();

                // Import products modified in the last 14 days
                var result = await importService.ImportDeltaUpdatesAsync(
                    days: 14,
                    maxProducts: DELTA_IMPORT_LIMIT,
                    cancellationToken: stoppingToken);

                _logger.LogInformation(
                    "Delta update completed: {Success} successful, {Failed} failed. Total: {Total}",
                    result.SuccessCount, result.FailureCount, result.TotalProcessed);

                if (result.Errors.Any())
                {
                    _logger.LogWarning("Delta update had {ErrorCount} errors. First few: {Errors}",
                        result.Errors.Count,
                        string.Join(", ", result.Errors.Take(5)));
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during delta update. Will retry on next cycle.");
            }
        }
    }
}
