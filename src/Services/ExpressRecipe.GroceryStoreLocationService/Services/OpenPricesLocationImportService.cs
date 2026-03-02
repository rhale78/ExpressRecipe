using System.Text.Json;

namespace ExpressRecipe.GroceryStoreLocationService.Services;

/// <summary>
/// Stub importer for Open Prices location data (JSONL format).
/// Filters to US locations and maps to the internal store model.
/// Expected JSONL fields: location_id, osm_id, name, city, country, type, lat, lon, address
/// </summary>
public class OpenPricesLocationImportService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenPricesLocationImportService> _logger;

    private const string DataSource = "OpenPrices";

    public OpenPricesLocationImportService(HttpClient httpClient, ILogger<OpenPricesLocationImportService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<(List<Data.UpsertGroceryStoreRequest> Stores, string? ErrorMessage)> FetchStoresFromUrlAsync(
        string jsonlUrl,
        CancellationToken cancellationToken = default)
    {
        var stores = new List<Data.UpsertGroceryStoreRequest>();

        try
        {
            _logger.LogInformation("Downloading Open Prices locations JSONL from {Url}", jsonlUrl);

            using var response = await _httpClient.GetAsync(jsonlUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            string? line;
            var lineNum = 0;

            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                lineNum++;

                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;

                    // Filter to US locations
                    var country = root.TryGetProperty("country", out var countryProp)
                        ? countryProp.GetString()
                        : null;
                    var countryCode = root.TryGetProperty("country_code", out var ccProp)
                        ? ccProp.GetString()
                        : null;

                    if (!IsUnitedStates(country, countryCode)) continue;

                    var name = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    var locationId = root.TryGetProperty("location_id", out var idProp)
                        ? idProp.GetRawText().Trim('"')
                        : lineNum.ToString();

                    double? latitude = null;
                    double? longitude = null;

                    if (root.TryGetProperty("lat", out var latProp) && latProp.ValueKind == JsonValueKind.Number)
                        latitude = latProp.GetDouble();
                    if (root.TryGetProperty("lon", out var lonProp) && lonProp.ValueKind == JsonValueKind.Number)
                        longitude = lonProp.GetDouble();

                    var city = root.TryGetProperty("city", out var cityProp) ? cityProp.GetString() : null;
                    var address = root.TryGetProperty("address", out var addrProp) ? addrProp.GetString() : null;
                    var storeType = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;

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
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Skipping malformed JSONL line {LineNum}", lineNum);
                }
            }

            _logger.LogInformation("Loaded {Count} US stores from Open Prices JSONL", stores.Count);
            return (stores, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch stores from Open Prices JSONL at {Url}", jsonlUrl);
            return (stores, ex.Message);
        }
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
