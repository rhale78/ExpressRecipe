using ExpressRecipe.GroceryStoreLocationService.Data;
using ExpressRecipe.GroceryStoreLocationService.Services;

namespace ExpressRecipe.GroceryStoreLocationService.Workers;

/// <summary>
/// Background service that imports and refreshes grocery store location data.
/// - On startup (after 2 min delay): runs initial Overture/SNAP import if fewer than 100 stores exist.
/// - Daily at 2:00 AM: refreshes from configured sources (OpenPrices, SNAP, OSM, Overture, HIFLD).
/// Each source can be individually enabled/disabled via StoreLocationImport config.
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
            await WarmChainNormalizerAsync(stoppingToken);
            await PerformInitialImportIfNeededAsync(stoppingToken);
            await RunDailyScheduleAsync(stoppingToken);
        }
        catch (OperationCanceledException) { _logger.LogInformation("StoreLocationImportWorker is stopping."); }
        catch (Exception ex) { _logger.LogError(ex, "Fatal error in StoreLocationImportWorker"); }
    }

    private async Task WarmChainNormalizerAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var normalizer = scope.ServiceProvider.GetService<IStoreChainNormalizer>();
            if (normalizer != null)
            {
                await normalizer.EnsureLoadedAsync(stoppingToken);
                _logger.LogInformation("StoreChainNormalizer warmed successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "StoreChainNormalizer warm-up failed (non-fatal)");
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

            _logger.LogInformation("Store count is low ({Count}). Running initial imports...", count);

            // Overture is the primary source
            var overtureEnabled = _configuration.GetValue<bool>("StoreLocationImport:Overture:Enabled", false);
            if (overtureEnabled)
            {
                await RunOvertureImportAsync(stoppingToken);
            }
            else
            {
                await RunOpenPricesImportAsync(stoppingToken);
            }

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
        if (all || source.Equals("overture", StringComparison.OrdinalIgnoreCase))
            await RunOvertureImportAsync(cancellationToken);
        if (all || source.Equals("hifld", StringComparison.OrdinalIgnoreCase))
            await RunHifldImportAsync(cancellationToken);
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
            var overtureEnabled = _configuration.GetValue<bool>("StoreLocationImport:Overture:Enabled", false);
            var hifldEnabled = _configuration.GetValue<bool>("StoreLocationImport:Hifld:Enabled", false);

            if (overtureEnabled) await RunOvertureImportAsync(stoppingToken);
            if (openPricesEnabled) await RunOpenPricesImportAsync(stoppingToken);
            if (snapEnabled) await RunSnapImportAsync(stoppingToken);
            if (osmEnabled) await RunOsmImportAsync(stoppingToken);
            if (hifldEnabled) await RunHifldImportAsync(stoppingToken);

            // Inter-item delay to prevent overwhelming CPU/disk between sources (configured via StoreLocationImport:BatchDelayMs)
            var batchDelayMs = _configuration.GetValue<int>("StoreLocationImport:BatchDelayMs", 0);
            if (batchDelayMs > 0)
                await Task.Delay(batchDelayMs, stoppingToken);
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

            await LogResultAsync(repo, "USDA_SNAP", stores, error);
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

            var stateCodes = _configuration.GetSection("StoreLocationImport:OsmStateCodes").Get<string[]>()
                ?? new[] { "NC" };

            _logger.LogInformation("[OSM] Starting import for states: {States}", string.Join(", ", stateCodes));

            var (stores, error) = await importService.FetchStoresForStatesAsync(stateCodes, stoppingToken);
            var imported = stores.Count > 0 ? await repo.BulkUpsertAsync(stores) : 0;

            await repo.LogImportAsync(new StoreImportLogDto
            {
                DataSource = "OPENSTREETMAP",
                RecordsProcessed = stores.Count,
                RecordsImported = imported,
                Success = error == null,
                ErrorMessage = error
            });

            _logger.LogInformation("[OSM] Import complete: {Imported}/{Total} stores", imported, stores.Count);
        }
        catch (Exception ex) { _logger.LogError(ex, "[OSM] Import failed"); }
    }

    private async Task RunOvertureImportAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var importService = scope.ServiceProvider.GetRequiredService<OvertureImportService>();
            var repo = scope.ServiceProvider.GetRequiredService<IGroceryStoreRepository>();

            _logger.LogInformation("[OVERTURE] Starting import...");
            var (stores, error) = await importService.FetchStoresAsync(stoppingToken);

            if (stores.Count == 0)
            {
                _logger.LogWarning("[OVERTURE] No stores found. Error: {Error}", error ?? "Unknown");
                await repo.LogImportAsync(new StoreImportLogDto { DataSource = "OVERTURE_MAPS", Success = false, ErrorMessage = error ?? "No stores" });
                return;
            }

            await LogResultAsync(repo, "OVERTURE_MAPS", stores, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OVERTURE] Import failed with exception");
        }
    }

    private async Task RunHifldImportAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var importService = scope.ServiceProvider.GetRequiredService<HifldImportService>();
            var repo = scope.ServiceProvider.GetRequiredService<IGroceryStoreRepository>();

            _logger.LogInformation("[HIFLD] Starting verification import...");
            var (records, error) = await importService.FetchVerificationRecordsAsync(stoppingToken);

            if (records.Count == 0)
            {
                _logger.LogWarning("[HIFLD] No records found. Error: {Error}", error ?? "Unknown");
                await repo.LogImportAsync(new StoreImportLogDto { DataSource = "HIFLD", Success = false, ErrorMessage = error ?? "No records" });
                return;
            }

            // HIFLD is verification only: cross-match by HifldId or Address+ZipCode and mark IsVerified=1.
            // Never insert new stores from HIFLD data.
            var verifiedCount = 0;
            var processedCount = 0;

            foreach (var record in records)
            {
                stoppingToken.ThrowIfCancellationRequested();
                processedCount++;

                try
                {
                    GroceryStoreDto? existing = null;

                    // 1. Try to find by HifldId (previously stored from a prior HIFLD run)
                    if (!string.IsNullOrWhiteSpace(record.HifldId))
                    {
                        existing = await repo.GetByExternalIdAsync($"HIFLD_{record.HifldId}", "HIFLD");
                    }

                    // 2. Fall back to Address+ZipCode lookup across all data sources
                    if (existing == null
                        && !string.IsNullOrWhiteSpace(record.Address)
                        && !string.IsNullOrWhiteSpace(record.ZipCode))
                    {
                        existing = await repo.GetByAddressAndZipAsync(record.Address, record.ZipCode);
                    }

                    // Only mark as verified if a matching store already exists in the DB
                    if (existing != null)
                    {
                        await repo.MarkVerifiedAsync(existing.Id, "HIFLD");
                        verifiedCount++;
                    }

                    if (processedCount % 1000 == 0)
                    {
                        _logger.LogInformation("[HIFLD] Progress: {Processed}/{Total} records processed, {Verified} verified",
                            processedCount, records.Count, verifiedCount);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[HIFLD] Error processing record {Name}", record.Name);
                }
            }

            await repo.LogImportAsync(new StoreImportLogDto
            {
                DataSource = "HIFLD",
                RecordsProcessed = processedCount,
                RecordsImported = verifiedCount,
                Success = error == null,
                ErrorMessage = error
            });

            _logger.LogInformation("[HIFLD] Verification complete: {Verified}/{Total} stores verified", verifiedCount, processedCount);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { _logger.LogError(ex, "[HIFLD] Import failed"); }
    }
}
