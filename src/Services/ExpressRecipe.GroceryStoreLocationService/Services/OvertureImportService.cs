using System.Diagnostics;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace ExpressRecipe.GroceryStoreLocationService.Services;

/// <summary>
/// Imports US grocery store locations from Overture Maps Foundation Places GeoParquet.
/// Uses DuckDB CLI as a subprocess to query the S3 dataset and export a local CSV.
/// The CSV is cached and re-queried monthly (configurable via OvertureRelease date).
/// Enabled: false by default in config.
/// </summary>
public class OvertureImportService
{
    private readonly ILogger<OvertureImportService> _logger;
    private readonly IConfiguration _configuration;

    private const string DataSource = "OVERTURE_MAPS";

    public OvertureImportService(
        ILogger<OvertureImportService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<(List<Data.UpsertGroceryStoreRequest> Stores, string? ErrorMessage)> FetchStoresAsync(
        CancellationToken cancellationToken = default)
    {
        var outputCsvPath = _configuration["StoreLocationImport:Overture:OutputCsvPath"]
            ?? Path.Combine(Path.GetTempPath(), "overture_stores.csv");

        var overtureRelease = _configuration["StoreLocationImport:Overture:OvertureRelease"]
            ?? "2026-02-18.0";

        var duckDbPath = _configuration["StoreLocationImport:Overture:DuckDbPath"];

        // Use existing CSV if fresh enough (within 30 days)
        if (File.Exists(outputCsvPath))
        {
            var fileAge = DateTime.UtcNow - File.GetLastWriteTimeUtc(outputCsvPath);
            if (fileAge < TimeSpan.FromDays(30))
            {
                _logger.LogInformation("[OVERTURE] Using cached CSV at {Path} (age: {Age:0.0} days)",
                    outputCsvPath, fileAge.TotalDays);
                return await ParseCsvAsync(outputCsvPath, cancellationToken);
            }
        }

        // Run DuckDB to export fresh CSV
        if (!string.IsNullOrWhiteSpace(duckDbPath) && File.Exists(duckDbPath))
        {
            var (success, error) = await RunDuckDbQueryAsync(duckDbPath, outputCsvPath, overtureRelease, cancellationToken);
            if (success)
            {
                return await ParseCsvAsync(outputCsvPath, cancellationToken);
            }
            _logger.LogWarning("[OVERTURE] DuckDB query failed: {Error}. Trying existing CSV if available.", error);
        }
        else
        {
            _logger.LogWarning("[OVERTURE] DuckDB binary not configured or not found at {Path}. " +
                "Set StoreLocationImport:Overture:DuckDbPath to enable live queries.", duckDbPath);
        }

        // Fall back to existing CSV even if stale
        if (File.Exists(outputCsvPath))
        {
            _logger.LogInformation("[OVERTURE] Falling back to existing (possibly stale) CSV at {Path}", outputCsvPath);
            return await ParseCsvAsync(outputCsvPath, cancellationToken);
        }

        return (new List<Data.UpsertGroceryStoreRequest>(),
            "No DuckDB binary and no cached CSV available. Configure StoreLocationImport:Overture:DuckDbPath.");
    }

    private async Task<(bool Success, string? Error)> RunDuckDbQueryAsync(
        string duckDbPath,
        string outputCsvPath,
        string overtureRelease,
        CancellationToken cancellationToken)
    {
        var query = $@"
LOAD spatial;
LOAD httpfs;
SET s3_region='us-west-2';

COPY (
    SELECT names.primary AS store_name,
           addresses[1].freeform AS street_address,
           addresses[1].locality AS city,
           addresses[1].region AS state,
           addresses[1].postcode AS zip_code,
           categories.primary AS category,
           ST_X(geometry) AS longitude,
           ST_Y(geometry) AS latitude,
           id AS gers_id
    FROM read_parquet('s3://overturemaps-us-west-2/release/{overtureRelease}/theme=places/type=place/*')
    WHERE addresses[1].country = 'US'
    AND categories.primary IN ('grocery_store','supermarket','warehouse_store')
) TO '{outputCsvPath.Replace("'", "''")}' (HEADER, DELIMITER ',');";

        // Write query to a temp file to avoid shell escaping issues
        var queryFile = Path.Combine(Path.GetTempPath(), $"overture_query_{Guid.NewGuid():N}.sql");
        try
        {
            await File.WriteAllTextAsync(queryFile, query, cancellationToken);

            _logger.LogInformation("[OVERTURE] Running DuckDB query for release {Release}", overtureRelease);

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = duckDbPath,
                    Arguments = $"-c \".read '{queryFile.Replace("'", "\\'")}'\""  ,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            // Wait with timeout (Overture query can take several minutes)
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(30));

            await process.WaitForExitAsync(timeoutCts.Token);

            var stderr = await stderrTask;
            var stdout = await stdoutTask;

            if (process.ExitCode != 0)
            {
                _logger.LogError("[OVERTURE] DuckDB exited with code {Code}. Stderr: {Stderr}", process.ExitCode, stderr);
                return (false, $"DuckDB exit code {process.ExitCode}: {stderr}");
            }

            _logger.LogInformation("[OVERTURE] DuckDB query completed successfully. Output: {Output}", stdout);
            return (true, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OVERTURE] Failed to run DuckDB query");
            return (false, ex.Message);
        }
        finally
        {
            if (File.Exists(queryFile))
            {
                File.Delete(queryFile);
            }
        }
    }

    private async Task<(List<Data.UpsertGroceryStoreRequest> Stores, string? ErrorMessage)> ParseCsvAsync(
        string csvPath,
        CancellationToken cancellationToken)
    {
        var stores = new List<Data.UpsertGroceryStoreRequest>();
        var rowCount = 0;

        try
        {
            using var reader = new StreamReader(csvPath);
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null
            };

            using var csv = new CsvReader(reader, config);
            await csv.ReadAsync();
            csv.ReadHeader();

            while (await csv.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                rowCount++;

                try
                {
                    var storeName = csv.GetField("store_name")?.Trim();
                    if (string.IsNullOrWhiteSpace(storeName)) continue;

                    var gersId = csv.GetField("gers_id")?.Trim();
                    if (string.IsNullOrWhiteSpace(gersId)) continue;

                    double? latitude = null;
                    double? longitude = null;

                    if (double.TryParse(csv.GetField("latitude"), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat))
                        latitude = lat;
                    if (double.TryParse(csv.GetField("longitude"), NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
                        longitude = lon;

                    var category = csv.GetField("category")?.Trim();

                    stores.Add(new Data.UpsertGroceryStoreRequest
                    {
                        Name = storeName,
                        StoreType = MapOvertureCategory(category),
                        Address = csv.GetField("street_address")?.Trim(),
                        City = csv.GetField("city")?.Trim(),
                        State = csv.GetField("state")?.Trim(),
                        ZipCode = csv.GetField("zip_code")?.Trim(),
                        Latitude = latitude,
                        Longitude = longitude,
                        ExternalId = gersId,
                        GersId = gersId,
                        DataSource = DataSource,
                        AcceptsSnap = false,
                        IsActive = true
                    });

                    if (stores.Count % 1000 == 0)
                    {
                        _logger.LogInformation("[OVERTURE] Progress: {Count:N0} stores loaded", stores.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[OVERTURE] Skipping malformed CSV row {Row}", rowCount);
                }
            }

            _logger.LogInformation("[OVERTURE] Parsed {Count:N0} stores from {Rows:N0} CSV rows", stores.Count, rowCount);
            return (stores, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OVERTURE] Failed to parse CSV {Path}", csvPath);
            return (stores, ex.Message);
        }
    }

    private static string MapOvertureCategory(string? category)
    {
        return category?.ToLowerInvariant() switch
        {
            "supermarket" => "Supermarket",
            "warehouse_store" => "Super Store",
            "grocery_store" => "Large Grocery Store",
            _ => "Supermarket"
        };
    }
}
