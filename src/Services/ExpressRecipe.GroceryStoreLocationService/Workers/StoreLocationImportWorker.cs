using ExpressRecipe.GroceryStoreLocationService.Data;
using ExpressRecipe.GroceryStoreLocationService.Services;

namespace ExpressRecipe.GroceryStoreLocationService.Workers;

/// <summary>
/// Background service that imports and refreshes grocery store location data.
/// - On startup (after 2 min delay): runs initial USDA SNAP import if fewer than 100 stores exist.
/// - Daily at 2:00 AM: refreshes from USDA SNAP and OSM.
/// </summary>
public class StoreLocationImportWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StoreLocationImportWorker> _logger;
    private readonly IConfiguration _configuration;

    public StoreLocationImportWorker(
        IServiceProvider serviceProvider,
        ILogger<StoreLocationImportWorker> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("StoreLocationImportWorker starting...");

        var autoImport = _configuration.GetValue<bool>("StoreLocationImport:AutoImport", true);
        if (!autoImport)
        {
            _logger.LogInformation("Auto-import is disabled. Worker will not run.");
            return;
        }

        // Wait for application to fully start
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        try
        {
            await PerformInitialImportIfNeededAsync(stoppingToken);
            await RunDailyScheduleAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("StoreLocationImportWorker is stopping.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in StoreLocationImportWorker");
        }
    }

    private async Task PerformInitialImportIfNeededAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IGroceryStoreRepository>();

            var count = await repo.GetStoreCountAsync();
            _logger.LogInformation("Current store count: {Count}", count);

            if (count >= 100)
            {
                _logger.LogInformation("Store database already has {Count} records. Skipping initial import.", count);
                return;
            }

            _logger.LogInformation("Store count is low ({Count}). Running initial USDA SNAP import...", count);
            await RunSnapImportAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during initial store import check");
        }
    }

    public async Task RunImportAsync(string source, CancellationToken cancellationToken = default)
    {
        var snapEnabled = source.Equals("snap", StringComparison.OrdinalIgnoreCase) ||
                          source.Equals("all", StringComparison.OrdinalIgnoreCase);
        var osmEnabled = source.Equals("osm", StringComparison.OrdinalIgnoreCase) ||
                         source.Equals("all", StringComparison.OrdinalIgnoreCase);

        if (snapEnabled)
            await RunSnapImportAsync(cancellationToken);

        if (osmEnabled)
            await RunOsmImportAsync(cancellationToken);
    }

    private async Task RunDailyScheduleAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var nextRun = now.Date.AddDays(1).AddHours(2); // 2:00 AM tomorrow
            var delay = nextRun - now;

            _logger.LogInformation("Next store import scheduled at {NextRun}", nextRun);

            await Task.Delay(delay, stoppingToken);

            _logger.LogInformation("Running daily store location import...");

            var snapEnabled = _configuration.GetValue<bool>("StoreLocationImport:UsSnapEnabled", true);
            var osmEnabled = _configuration.GetValue<bool>("StoreLocationImport:OpenStreetMapEnabled", true);

            if (snapEnabled)
            {
                await RunSnapImportAsync(stoppingToken);
            }

            if (osmEnabled)
            {
                await RunOsmImportAsync(stoppingToken);
            }
        }
    }

    private async Task RunSnapImportAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var importService = scope.ServiceProvider.GetRequiredService<UsdaSnapImportService>();
            var repo = scope.ServiceProvider.GetRequiredService<IGroceryStoreRepository>();

            _logger.LogInformation("Starting USDA SNAP import...");
            var (stores, error) = await importService.FetchStoresAsync(stoppingToken);

            if (stores.Count == 0)
            {
                _logger.LogWarning("USDA SNAP import returned no stores. Error: {Error}", error ?? "none");
                await repo.LogImportAsync(new StoreImportLogDto
                {
                    DataSource = "USDA_SNAP",
                    RecordsProcessed = 0,
                    ErrorMessage = error ?? "No stores returned",
                    Success = false
                });
                return;
            }

            var imported = await repo.BulkUpsertAsync(stores);

            await repo.LogImportAsync(new StoreImportLogDto
            {
                DataSource = "USDA_SNAP",
                RecordsProcessed = stores.Count,
                RecordsImported = imported,
                ErrorMessage = error,
                Success = error == null
            });

            _logger.LogInformation("USDA SNAP import complete: {Imported}/{Total} stores", imported, stores.Count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "USDA SNAP import failed");
        }
    }

    private async Task RunOsmImportAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var importService = scope.ServiceProvider.GetRequiredService<OpenStreetMapImportService>();
            var repo = scope.ServiceProvider.GetRequiredService<IGroceryStoreRepository>();

            _logger.LogInformation("Starting OpenStreetMap import for NC...");
            var (stores, error) = await importService.FetchStoresForStateAsync("NC", stoppingToken);

            var imported = stores.Count > 0 ? await repo.BulkUpsertAsync(stores) : 0;

            await repo.LogImportAsync(new StoreImportLogDto
            {
                DataSource = "OSM",
                RecordsProcessed = stores.Count,
                RecordsImported = imported,
                ErrorMessage = error,
                Success = error == null
            });

            _logger.LogInformation("OSM import complete: {Imported}/{Total} stores", imported, stores.Count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenStreetMap import failed");
        }
    }
}
