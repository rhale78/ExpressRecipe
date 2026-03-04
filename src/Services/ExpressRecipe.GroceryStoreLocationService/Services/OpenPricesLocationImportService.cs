using System.Text.Json;

namespace ExpressRecipe.GroceryStoreLocationService.Services;

/// <summary>
/// Importer for Open Prices location data.
/// Supports both remote URLs and local file system paths (JSONL format).
/// Filters to US locations and maps to the internal store model.
/// Expected JSONL fields: location_id, osm_id, name, city, country, type, lat, lon, address
/// </summary>
public class OpenPricesLocationImportService : IOpenPricesLocationImportService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenPricesLocationImportService> _logger;

    private const string DataSource = "OpenPrices";

    public OpenPricesLocationImportService(HttpClient httpClient, ILogger<OpenPricesLocationImportService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<(List<Data.UpsertGroceryStoreRequest> Stores, string? ErrorMessage)> FetchStoresAsync(
        string? pathOrUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pathOrUrl))
        {
            return (new List<Data.UpsertGroceryStoreRequest>(), "No path or URL provided");
        }

        if (Uri.TryCreate(pathOrUrl, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return await FetchStoresFromUrlAsync(pathOrUrl, cancellationToken);
        }
        else
        {
            return await FetchStoresFromFileAsync(pathOrUrl, cancellationToken);
        }
    }

    public async Task<(List<Data.UpsertGroceryStoreRequest> Stores, string? ErrorMessage)> FetchStoresFromFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Open Prices locations file not found: {Path}", filePath);
            return (new List<Data.UpsertGroceryStoreRequest>(), $"File not found: {filePath}");
        }

        try
        {
            _logger.LogInformation("Reading Open Prices locations from local file: {Path}", filePath);
            using var stream = File.OpenRead(filePath);
            return await ProcessStreamAsync(stream, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read stores from file: {Path}", filePath);
            return (new List<Data.UpsertGroceryStoreRequest>(), ex.Message);
        }
    }

    public async Task<(List<Data.UpsertGroceryStoreRequest> Stores, string? ErrorMessage)> FetchStoresFromUrlAsync(
        string jsonlUrl,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Downloading Open Prices locations JSONL from {Url}", jsonlUrl);

            using var response = await _httpClient.GetAsync(jsonlUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await ProcessStreamAsync(stream, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch stores from Open Prices JSONL at {Url}", jsonlUrl);
            return (new List<Data.UpsertGroceryStoreRequest>(), ex.Message);
        }
    }

    public bool FileExists(string filePath) => File.Exists(filePath);

    private async Task<(List<Data.UpsertGroceryStoreRequest> Stores, string? ErrorMessage)> ProcessStreamAsync(
        Stream stream, 
        CancellationToken cancellationToken)
    {
        var stores = new List<Data.UpsertGroceryStoreRequest>();
        using var reader = new StreamReader(stream);

        string? line;
        var lineNum = 0;
        var usStoreCount = 0;

        _logger.LogInformation("[OpenPrices-Locations] Starting to parse JSONL stream...");

        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lineNum++;

            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                // Filter to US locations - check both standard fields and osm_ prefixed fields
                var country = root.TryGetProperty("country", out var countryProp)
                    ? countryProp.GetString()
                    : root.TryGetProperty("osm_address_country", out var osmCountryProp)
                        ? osmCountryProp.GetString()
                        : null;

                var countryCode = root.TryGetProperty("country_code", out var ccProp)
                    ? ccProp.GetString()
                    : root.TryGetProperty("osm_address_country_code", out var osmCcProp)
                        ? osmCcProp.GetString()
                        : null;

                if (!IsUnitedStates(country, countryCode)) continue;

                usStoreCount++;

                // Try osm_name first, then name
                var name = root.TryGetProperty("osm_name", out var osmNameProp) && osmNameProp.ValueKind != JsonValueKind.Null
                    ? osmNameProp.GetString()
                    : root.TryGetProperty("name", out var nameProp)
                        ? nameProp.GetString()
                        : null;

                if (string.IsNullOrWhiteSpace(name)) continue;

                var locationId = root.TryGetProperty("location_id", out var idProp)
                    ? idProp.GetRawText().Trim('"')
                    : root.TryGetProperty("id", out var osmIdProp)
                        ? osmIdProp.GetRawText().Trim('"')
                        : lineNum.ToString();

                double? latitude = null;
                double? longitude = null;

                // Try lat/lon first, then osm_lat/osm_lon
                if (root.TryGetProperty("lat", out var latProp) && latProp.ValueKind == JsonValueKind.Number)
                    latitude = latProp.GetDouble();
                else if (root.TryGetProperty("osm_lat", out var osmLatProp))
                {
                    var latStr = osmLatProp.GetString();
                    if (!string.IsNullOrEmpty(latStr) && double.TryParse(latStr, out var latVal))
                        latitude = latVal;
                }

                if (root.TryGetProperty("lon", out var lonProp) && lonProp.ValueKind == JsonValueKind.Number)
                    longitude = lonProp.GetDouble();
                else if (root.TryGetProperty("osm_lon", out var osmLonProp))
                {
                    var lonStr = osmLonProp.GetString();
                    if (!string.IsNullOrEmpty(lonStr) && double.TryParse(lonStr, out var lonVal))
                        longitude = lonVal;
                }

                // Try osm_address_city first, then city
                var city = root.TryGetProperty("osm_address_city", out var osmCityProp) && osmCityProp.ValueKind != JsonValueKind.Null
                    ? osmCityProp.GetString()
                    : root.TryGetProperty("city", out var cityProp)
                        ? cityProp.GetString()
                        : null;

                var address = root.TryGetProperty("osm_display_name", out var osmDisplayProp) && osmDisplayProp.ValueKind != JsonValueKind.Null
                    ? osmDisplayProp.GetString()
                    : root.TryGetProperty("address", out var addrProp)
                        ? addrProp.GetString()
                        : null;

                var storeType = root.TryGetProperty("osm_tag_value", out var osmTypeProp) && osmTypeProp.ValueKind != JsonValueKind.Null
                    ? osmTypeProp.GetString()
                    : root.TryGetProperty("type", out var typeProp)
                        ? typeProp.GetString()
                        : null;

                stores.Add(new Data.UpsertGroceryStoreRequest
                {
                    Name = name,
                    StoreType = storeType,
                    Address = address,
                    City = city,
                    Latitude = latitude,
                    Longitude = longitude,
                    ExternalId = locationId.Length > 200 ? locationId[..200] : locationId,
                    DataSource = DataSource,
                    AcceptsSnap = false,
                    IsActive = true
                });

                // Progress logging every 1000 US stores
                if (stores.Count % 1000 == 0)
                {
                    var current = stores.Last();
                    _logger.LogInformation(
                        "[OpenPrices-Locations] Progress: {USStores:N0} US stores found from {TotalLines:N0} lines. Current: {Name} ({Type}) @ {City}",
                        stores.Count, lineNum, current.Name, current.StoreType ?? "Unknown", current.City);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Skipping malformed JSONL line {LineNum}", lineNum);
            }
        }

        if (stores.Count > 0)
        {
            var first = stores.First();
            var last = stores.Last();
            _logger.LogInformation(
                "[OpenPrices-Locations] Loaded {Count:N0} US stores from {TotalLines:N0} JSONL lines. First: {FirstName} @ {FirstCity}. Last: {LastName} @ {LastCity}",
                stores.Count, lineNum,
                first.Name, first.City,
                last.Name, last.City);
        }
        else
        {
            _logger.LogWarning("[OpenPrices-Locations] No US stores found in {Lines:N0} lines (found {USStores} US-tagged locations)", 
                lineNum, usStoreCount);
        }

        return (stores, null);
    }

    private static bool IsUnitedStates(string? country, string? countryCode)
    {
        if (countryCode != null &&
            (countryCode.Equals("US", StringComparison.OrdinalIgnoreCase) ||
             countryCode.Equals("USA", StringComparison.OrdinalIgnoreCase)))
            return true;

        if (country != null &&
            (country.Equals("US", StringComparison.OrdinalIgnoreCase) ||
             country.Equals("USA", StringComparison.OrdinalIgnoreCase) ||
             country.Equals("United States", StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }
}
