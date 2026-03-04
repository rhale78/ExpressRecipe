using ExpressRecipe.GroceryStoreLocationService.Data;
using ExpressRecipe.GroceryStoreLocationService.Services;

namespace ExpressRecipe.GroceryStoreLocationService.Workers;

/// <summary>
/// Background service that imports and refreshes grocery store location data.
/// - On startup (after 2 min delay): runs initial USDA SNAP import if fewer than 100 stores exist.
/// - Daily at 2:00 AM: refreshes from USDA SNAP, OSM, and OpenPrices.
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

        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        try
        {
            await PerformInitialImportIfNeededAsync(stoppingToken);
            await RunDailyScheduleAsync(stoppingToken);
        }
        catch (OperationCanceledException) { _logger.LogInformation("StoreLocationImportWorker is stopping."); }
        catch (Exception ex) { _logger.LogError(ex, "Fatal error in StoreLocationImportWorker"); }
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

            _logger.LogInformation("Store count is low ({Count}). Running initial imports...", count);
            await RunOpenPricesImportAsync(stoppingToken);
            
            if (await repo.GetStoreCountAsync() < 100)
            {
                await RunSnapImportAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { _logger.LogError(ex, "Error during initial store import check"); }
    }

    public async Task RunImportAsync(string source, CancellationToken cancellationToken = default)
    {
        var all = source.Equals("all", StringComparison.OrdinalIgnoreCase);
        if (all || source.Equals("openprices", StringComparison.OrdinalIgnoreCase))
            await RunOpenPricesImportAsync(cancellationToken);
        if (all || source.Equals("snap", StringComparison.OrdinalIgnoreCase))
            await RunSnapImportAsync(cancellationToken);
        if (all || source.Equals("osm", StringComparison.OrdinalIgnoreCase))
            await RunOsmImportAsync(cancellationToken);
    }

    private async Task RunDailyScheduleAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var nextRun = now.Date.AddDays(1).AddHours(2);
            await Task.Delay(nextRun - now, stoppingToken);

            _logger.LogInformation("Running daily store location import...");
            var openPricesEnabled = _configuration.GetValue<bool>("StoreLocationImport:OpenPricesEnabled", true);
            var snapEnabled = _configuration.GetValue<bool>("StoreLocationImport:UsSnapEnabled", true);
            var osmEnabled = _configuration.GetValue<bool>("StoreLocationImport:OpenStreetMapEnabled", true);

            if (openPricesEnabled) await RunOpenPricesImportAsync(stoppingToken);
            if (snapEnabled) await RunSnapImportAsync(stoppingToken);
            if (osmEnabled) await RunOsmImportAsync(stoppingToken);
        }
    }

    private async Task RunOpenPricesImportAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var importService = scope.ServiceProvider.GetRequiredService<IOpenPricesLocationImportService>();
            var repo = scope.ServiceProvider.GetRequiredService<IGroceryStoreRepository>();

            var dataFile = _configuration["StoreLocationImport:OpenPricesFilePath"];
            var url = _configuration["StoreLocationImport:OpenPricesUrl"];

            // 1. PRIORITY: Local File
            if (!string.IsNullOrWhiteSpace(dataFile) && importService.FileExists(dataFile))
            {
                _logger.LogInformation("STRATEGY: Local locations file found at {Path}. Skipping web call.", dataFile);
                var (stores, error) = await importService.FetchStoresFromFileAsync(dataFile, stoppingToken);
                await LogResultAsync(repo, "OpenPrices-File", stores, error);
                return;
            }

            // 2. ATTEMPT WEB
            if (!string.IsNullOrWhiteSpace(url))
            {
                _logger.LogInformation("STRATEGY: Local file not found. Attempting web import from {Url}", url);
                try
                {
                    var (stores, error) = await importService.FetchStoresFromUrlAsync(url, stoppingToken);
                    if (error == null && stores.Count > 0)
                    {
                        await LogResultAsync(repo, "OpenPrices-Web", stores, null);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "STRATEGY: Web locations import failed. Initiating fallback to file search.");
                }
            }

            // 3. FALLBACK: Search for file again
            if (!string.IsNullOrWhiteSpace(dataFile) && importService.FileExists(dataFile))
            {
                _logger.LogInformation("FALLBACK: Found local locations file {Path} after web failure. Importing...", dataFile);
                var (stores, error) = await importService.FetchStoresFromFileAsync(dataFile, stoppingToken);
                await LogResultAsync(repo, "OpenPrices-Fallback", stores, error);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { _logger.LogError(ex, "Open Prices location import failed completely"); }
    }

    private async Task LogResultAsync(IGroceryStoreRepository repo, string source, List<UpsertGroceryStoreRequest> stores, string? error)
    {
        if (stores.Count > 0)
        {
            var firstStore = stores.First();
            var lastStore = stores.Last();

            _logger.LogInformation(
                "[{Source}] Starting bulk upsert of {Count} stores. First: {FirstName} @ {FirstCity}, {FirstState}. Last: {LastName} @ {LastCity}, {LastState}",
                source, stores.Count,
                firstStore.Name, firstStore.City, firstStore.State,
                lastStore.Name, lastStore.City, lastStore.State);

            var imported = await repo.BulkUpsertAsync(stores);

            await repo.LogImportAsync(new StoreImportLogDto
            {
                DataSource = source,
                RecordsProcessed = stores.Count,
                RecordsImported = imported,
                ErrorMessage = error,
                Success = error == null
            });

            _logger.LogInformation("[{Source}] Import complete: {Imported}/{Total} stores saved to database", 
                source, imported, stores.Count);
        }
        else
        {
            _logger.LogWarning("[{Source}] No stores to import. Error: {Error}", source, error ?? "No data");
        }
    }

    private async Task RunSnapImportAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var importService = scope.ServiceProvider.GetRequiredService<UsdaSnapImportService>();
            var repo = scope.ServiceProvider.GetRequiredService<IGroceryStoreRepository>();

            _logger.LogInformation("[USDA-SNAP] Starting import...");
            var (stores, error) = await importService.FetchStoresAsync(stoppingToken);

            if (stores.Count == 0)
            {
                _logger.LogWarning("[USDA-SNAP] No stores found. Error: {Error}", error ?? "Unknown");
                await repo.LogImportAsync(new StoreImportLogDto { DataSource = "USDA_SNAP", Success = false, ErrorMessage = error ?? "No stores" });
                return;
            }

            await LogResultAsync(repo, "USDA-SNAP", stores, error);
        }
        catch (Exception ex) 
        { 
            _logger.LogError(ex, "[USDA-SNAP] Import failed with exception"); 
        }
    }

    private async Task RunOsmImportAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var importService = scope.ServiceProvider.GetRequiredService<OpenStreetMapImportService>();
            var repo = scope.ServiceProvider.GetRequiredService<IGroceryStoreRepository>();

            _logger.LogInformation("Starting OSM import for NC...");
            var (stores, error) = await importService.FetchStoresForStateAsync("NC", stoppingToken);
            var imported = stores.Count > 0 ? await repo.BulkUpsertAsync(stores) : 0;
            await repo.LogImportAsync(new StoreImportLogDto { DataSource = "OSM", RecordsProcessed = stores.Count, RecordsImported = imported, Success = error == null });
            _logger.LogInformation("OSM import complete: {Imported}/{Total} stores", imported, stores.Count);
        }
        catch (Exception ex) { _logger.LogError(ex, "OSM import failed"); }
    }
}
