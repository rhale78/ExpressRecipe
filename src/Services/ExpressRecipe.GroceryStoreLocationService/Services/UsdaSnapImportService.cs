using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Net;
using System.Text;

namespace ExpressRecipe.GroceryStoreLocationService.Services;

/// <summary>
/// Imports US grocery store locations from the USDA SNAP Retailer data CSV.
/// </summary>
public class UsdaSnapImportService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UsdaSnapImportService> _logger;
    private readonly IConfiguration _configuration;

    private const string DefaultCsvUrl = "https://www.fns.usda.gov/sites/default/files/resource-files/store_locations.csv";
    private const string DataSource = "USDA_SNAP";
    private const int MaxExternalIdLength = 200;

    public UsdaSnapImportService(
        HttpClient httpClient,
        ILogger<UsdaSnapImportService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<(List<Data.UpsertGroceryStoreRequest> Stores, string? ErrorMessage)> FetchStoresAsync(
        CancellationToken cancellationToken = default)
    {
        // Check for local file first before trying URLs
        var localFilePath = _configuration["StoreLocationImport:UsdaSnapFilePath"];
        if (!string.IsNullOrWhiteSpace(localFilePath) && File.Exists(localFilePath))
        {
            _logger.LogInformation("Found local USDA SNAP CSV file at {FilePath}, using it instead of downloading", localFilePath);

            try
            {
                await using var fileStream = File.OpenRead(localFilePath);
                var (stores, error) = await ParseCsvStreamAsync(fileStream, cancellationToken);

                if (error == null)
                {
                    _logger.LogInformation("Successfully loaded {Count} stores from local CSV file", stores.Count);
                    return (stores, null);
                }

                _logger.LogWarning("Failed to parse local CSV file: {Error}. Falling back to URL download.", error);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read local CSV file at {FilePath}. Falling back to URL download.", localFilePath);
            }
        }

        // If no local file or local file failed, download from URLs
        Exception? lastError = null;

        foreach (var url in GetCandidateUrls())
        {
            try
            {
                _logger.LogInformation("Downloading USDA SNAP store locations CSV from {Url}", url);

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("User-Agent", "ExpressRecipe.GroceryStoreLocationService/1.0 (+https://github.com/rhale78/ExpressRecipe)");
                request.Headers.TryAddWithoutValidation("Accept", "text/csv,application/octet-stream,*/*");

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("USDA SNAP URL {Url} returned 404; trying next fallback URL", url);
                    lastError = new HttpRequestException($"URL returned 404: {url}");
                    continue;
                }

                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var (downloadedStores, error) = await ParseCsvStreamAsync(stream, cancellationToken);

                if (error == null)
                {
                    _logger.LogInformation("Loaded {Count} stores from USDA SNAP CSV", downloadedStores.Count);
                    return (downloadedStores, null);
                }

                lastError = new Exception(error);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
                _logger.LogWarning(ex, "Failed to fetch USDA SNAP store locations from {Url}; trying next fallback URL", url);
            }
        }

        _logger.LogError(lastError, "Failed to fetch USDA SNAP store locations from all configured URLs");
        return (new List<Data.UpsertGroceryStoreRequest>(), lastError?.Message ?? "Failed to download USDA SNAP CSV from all configured URLs");
    }

    private async Task<(List<Data.UpsertGroceryStoreRequest> Stores, string? ErrorMessage)> ParseCsvStreamAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var stores = new List<Data.UpsertGroceryStoreRequest>();
        int rowCount = 0;

        try
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null,
                BadDataFound = context =>
                    _logger.LogDebug("Bad CSV data at row {Row}: {Field}", context.Context?.Parser?.Row, context.Field)
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
                    var storeName = csv.GetField("Store_Name")?.Trim();
                    if (string.IsNullOrWhiteSpace(storeName)) continue;

                    var address = csv.GetField("Address")?.Trim() ?? string.Empty;
                    var zip = csv.GetField("Zip5")?.Trim() ?? string.Empty;
                    var externalId = $"{storeName}_{address}_{zip}".Trim('_');

                    double? latitude = null;
                    double? longitude = null;

                    if (double.TryParse(csv.GetField("Latitude"), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat))
                        latitude = lat;
                    if (double.TryParse(csv.GetField("Longitude"), NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
                        longitude = lon;

                    stores.Add(new Data.UpsertGroceryStoreRequest
                    {
                        Name = storeName,
                        Address = csv.GetField("Address")?.Trim(),
                        City = csv.GetField("City")?.Trim(),
                        State = csv.GetField("State")?.Trim(),
                        ZipCode = csv.GetField("Zip5")?.Trim(),
                        County = csv.GetField("County")?.Trim(),
                        Latitude = latitude,
                        Longitude = longitude,
                        ExternalId = externalId.Length > MaxExternalIdLength ? externalId[..MaxExternalIdLength] : externalId,
                        DataSource = DataSource,
                        AcceptsSnap = true,
                        IsActive = true
                    });

                    // Progress logging every 10k stores
                    if (stores.Count % 10000 == 0)
                    {
                        var current = stores.Last();
                        _logger.LogInformation("[USDA-SNAP] Progress: {Count:N0} stores loaded. Current: {Name} @ {City}, {State}", 
                            stores.Count, current.Name, current.City, current.State);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Skipping malformed CSV row {Row}", csv.Context?.Parser?.Row);
                }
            }

            if (stores.Count > 0)
            {
                var first = stores.First();
                var last = stores.Last();
                _logger.LogInformation(
                    "[USDA-SNAP] Parsed {Count:N0} stores from {Rows:N0} CSV rows. First: {FirstName} @ {FirstCity}, {FirstState}. Last: {LastName} @ {LastCity}, {LastState}",
                    stores.Count, rowCount,
                    first.Name, first.City, first.State,
                    last.Name, last.City, last.State);
            }

            return (stores, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[USDA-SNAP] Failed to parse CSV stream after {Rows} rows", rowCount);
            return (stores, ex.Message);
        }
    }

    private IEnumerable<string> GetCandidateUrls()
    {
        var primary = _configuration["StoreLocationImport:UsdaSnapUrl"] ?? DefaultCsvUrl;
        var candidates = new List<string> { primary };

        var configuredFallbacks = _configuration.GetSection("StoreLocationImport:UsdaSnapFallbackUrls").Get<string[]>();
        if (configuredFallbacks is { Length: > 0 })
        {
            candidates.AddRange(configuredFallbacks.Where(u => !string.IsNullOrWhiteSpace(u)));
        }
        else
        {
            candidates.Add("https://www.fns.usda.gov/sites/default/files/resource-files/SNAP_Store_Locations.csv");
            candidates.Add("https://www.fns.usda.gov/sites/default/files/resource-files/snap_store_locations.csv");
        }

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
