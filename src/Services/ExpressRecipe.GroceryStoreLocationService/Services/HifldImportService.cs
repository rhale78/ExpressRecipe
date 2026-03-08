using System.Text.Json;

namespace ExpressRecipe.GroceryStoreLocationService.Services;

/// <summary>
/// Imports US grocery store verification data from the HIFLD Supermarkets dataset.
/// Source: https://hifld-geoplatform.hub.arcgis.com/datasets/supermarkets
/// This is a VERIFICATION LAYER ONLY — it sets IsVerified=1 and stores HifldId
/// but does NOT overwrite primary location data (name, address, coordinates) from other sources.
/// Enabled: false by default in config.
/// </summary>
public class HifldImportService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HifldImportService> _logger;
    private readonly IConfiguration _configuration;

    private const string DataSource = "HIFLD";
    private const string DefaultGeoJsonUrl =
        "https://opendata.arcgis.com/datasets/a2817bf9632a43f5ad1c6b0c153b0fab_0.geojson";

    public HifldImportService(
        HttpClient httpClient,
        ILogger<HifldImportService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Fetches HIFLD supermarket records, returning lightweight verification records.
    /// These are used to mark existing stores as verified (IsVerified=1) by cross-matching
    /// on Address+ZipCode.
    /// </summary>
    public async Task<(List<Data.UpsertGroceryStoreRequest> Stores, string? ErrorMessage)> FetchVerificationRecordsAsync(
        CancellationToken cancellationToken = default)
    {
        var localFilePath = _configuration["StoreLocationImport:HifldFilePath"];
        if (!string.IsNullOrWhiteSpace(localFilePath) && File.Exists(localFilePath))
        {
            _logger.LogInformation("[HIFLD] Loading from local file {Path}", localFilePath);
            try
            {
                await using var fileStream = File.OpenRead(localFilePath);
                return await ParseGeoJsonAsync(fileStream, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[HIFLD] Failed to read local file, falling back to URL");
            }
        }

        var url = _configuration["StoreLocationImport:HifldUrl"] ?? DefaultGeoJsonUrl;

        try
        {
            _logger.LogInformation("[HIFLD] Downloading from {Url}", url);
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await ParseGeoJsonAsync(stream, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HIFLD] Failed to download HIFLD dataset");
            return (new List<Data.UpsertGroceryStoreRequest>(), ex.Message);
        }
    }

    private async Task<(List<Data.UpsertGroceryStoreRequest> Stores, string? ErrorMessage)> ParseGeoJsonAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var stores = new List<Data.UpsertGroceryStoreRequest>();

        try
        {
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!doc.RootElement.TryGetProperty("features", out var features))
            {
                _logger.LogWarning("[HIFLD] No 'features' element found in GeoJSON");
                return (stores, "No features in GeoJSON");
            }

            foreach (var feature in features.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (!feature.TryGetProperty("properties", out var props)) continue;

                    var name = GetString(props, "NAME") ?? GetString(props, "STORE_NAME");
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    var hifldId = GetString(props, "OBJECTID") ?? GetString(props, "ID");
                    var address = GetString(props, "ADDRESS") ?? GetString(props, "STREET");
                    var city = GetString(props, "CITY");
                    var state = GetString(props, "STATE");
                    var zipCode = GetString(props, "ZIP") ?? GetString(props, "ZIPCODE");

                    double? latitude = null;
                    double? longitude = null;

                    if (feature.TryGetProperty("geometry", out var geometry) &&
                        geometry.TryGetProperty("coordinates", out var coords) &&
                        coords.ValueKind == JsonValueKind.Array)
                    {
                        var coordArray = coords.EnumerateArray().ToArray();
                        if (coordArray.Length >= 2)
                        {
                            longitude = coordArray[0].GetDouble();
                            latitude = coordArray[1].GetDouble();
                        }
                    }

                    var externalId = !string.IsNullOrWhiteSpace(hifldId)
                        ? $"HIFLD_{hifldId}"
                        : $"{name}_{address}_{zipCode}".Trim('_');

                    stores.Add(new Data.UpsertGroceryStoreRequest
                    {
                        Name = name,
                        StoreType = "Supermarket",
                        Address = address,
                        City = city,
                        State = state,
                        ZipCode = zipCode,
                        Latitude = latitude,
                        Longitude = longitude,
                        ExternalId = externalId.Length > 200 ? externalId[..200] : externalId,
                        HifldId = hifldId?.Length > 50 ? hifldId[..50] : hifldId,
                        DataSource = DataSource,
                        AcceptsSnap = false,
                        IsActive = true
                    });

                    if (stores.Count % 1000 == 0)
                    {
                        _logger.LogInformation("[HIFLD] Progress: {Count:N0} records loaded", stores.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[HIFLD] Skipping feature due to parse error");
                }
            }

            _logger.LogInformation("[HIFLD] Parsed {Count:N0} verification records", stores.Count);
            return (stores, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HIFLD] Failed to parse GeoJSON");
            return (stores, ex.Message);
        }
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) &&
            prop.ValueKind == JsonValueKind.String)
        {
            var value = prop.GetString()?.Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        return null;
    }
}
