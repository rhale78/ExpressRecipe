using System.Text.Json;

namespace ExpressRecipe.GroceryStoreLocationService.Services;

/// <summary>
/// Imports grocery store locations from the OpenStreetMap Overpass API.
/// Focuses on supermarket, grocery, and wholesale nodes/ways in US states.
/// Per Overpass fair-use policy: max 1 request per 60s; queries by state in sequence.
/// </summary>
public class OpenStreetMapImportService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenStreetMapImportService> _logger;
    private readonly IConfiguration _configuration;

    private string OverpassApiUrl => _configuration["StoreLocationImport:OsmOverpassApiUrl"] ?? "https://overpass-api.de/api/interpreter";
    private const string DataSource = "OPENSTREETMAP";

    public OpenStreetMapImportService(
        HttpClient httpClient,
        ILogger<OpenStreetMapImportService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<(List<Data.UpsertGroceryStoreRequest> Stores, string? ErrorMessage)> FetchStoresForStateAsync(
        string stateCode = "NC",
        CancellationToken cancellationToken = default)
    {
        var stores = new List<Data.UpsertGroceryStoreRequest>();

        try
        {
            var query = $@"[out:json][timeout:180];
area[""ISO3166-2""~""US-{stateCode}""]->.a;
(
  node[""shop""~""^(supermarket|grocery|wholesale)$""][""addr:country""=""US""](area.a);
  way[""shop""~""^(supermarket|grocery|wholesale)$""][""addr:country""=""US""](area.a);
  node[""shop""~""^(supermarket|grocery|wholesale)$""](area.a);
  way[""shop""~""^(supermarket|grocery|wholesale)$""](area.a);
);
out center tags;";

            _logger.LogInformation("Querying OpenStreetMap Overpass API for state {State}", stateCode);

            var content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("data", query) });
            using var response = await _httpClient.PostAsync(OverpassApiUrl, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("elements", out var elements))
            {
                _logger.LogWarning("No elements found in OSM response for state {State}", stateCode);
                return (stores, null);
            }

            foreach (var element in elements.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var osmId = element.GetProperty("id").GetInt64();
                    var elementType = element.GetProperty("type").GetString();

                    if (!element.TryGetProperty("tags", out var tags)) continue;

                    var name = tags.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    double? latitude = null;
                    double? longitude = null;

                    if (elementType == "node")
                    {
                        if (element.TryGetProperty("lat", out var latProp))
                            latitude = latProp.GetDouble();
                        if (element.TryGetProperty("lon", out var lonProp))
                            longitude = lonProp.GetDouble();
                    }
                    else if (elementType == "way")
                    {
                        // Ways return a center element
                        if (element.TryGetProperty("center", out var center))
                        {
                            if (center.TryGetProperty("lat", out var latProp))
                                latitude = latProp.GetDouble();
                            if (center.TryGetProperty("lon", out var lonProp))
                                longitude = lonProp.GetDouble();
                        }
                    }

                    var address = tags.TryGetProperty("addr:housenumber", out var houseNum) &&
                                  tags.TryGetProperty("addr:street", out var street)
                        ? $"{houseNum.GetString()} {street.GetString()}"
                        : null;

                    var city = tags.TryGetProperty("addr:city", out var cityProp) ? cityProp.GetString() : null;
                    var zipCode = tags.TryGetProperty("addr:postcode", out var zipProp) ? zipProp.GetString() : null;
                    var phone = tags.TryGetProperty("phone", out var phoneProp) ? phoneProp.GetString() : null;
                    var website = tags.TryGetProperty("website", out var webProp) ? webProp.GetString() : null;
                    var openingHours = tags.TryGetProperty("opening_hours", out var hoursProp) ? hoursProp.GetString() : null;
                    var brand = tags.TryGetProperty("brand", out var brandProp) ? brandProp.GetString() : null;
                    var shop = tags.TryGetProperty("shop", out var shopProp) ? shopProp.GetString() : "supermarket";

                    stores.Add(new Data.UpsertGroceryStoreRequest
                    {
                        Name = name,
                        Chain = brand,
                        StoreType = MapOsmShopToStoreType(shop),
                        Address = address,
                        City = city,
                        State = stateCode,
                        ZipCode = zipCode,
                        Latitude = latitude,
                        Longitude = longitude,
                        PhoneNumber = phone,
                        Website = website,
                        OpeningHours = openingHours,
                        ExternalId = $"{elementType}/{osmId}",
                        OsmId = osmId,
                        DataSource = DataSource,
                        AcceptsSnap = false,
                        IsActive = true
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Skipping OSM element due to parse error");
                }
            }

            if (stores.Count % 1000 == 0 || stores.Count > 0)
            {
                _logger.LogInformation("Loaded {Count} stores from OSM for state {State}", stores.Count, stateCode);
            }

            return (stores, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch stores from OpenStreetMap for state {State}", stateCode);
            return (stores, ex.Message);
        }
    }

    /// <summary>
    /// Fetches stores for multiple US states sequentially with a 60-second delay between requests
    /// to comply with Overpass fair-use policy.
    /// </summary>
    public async Task<(List<Data.UpsertGroceryStoreRequest> Stores, string? ErrorMessage)> FetchStoresForStatesAsync(
        IEnumerable<string> stateCodes,
        CancellationToken cancellationToken = default)
    {
        var allStores = new List<Data.UpsertGroceryStoreRequest>();
        string? lastError = null;
        var stateList = stateCodes.ToList();

        for (int i = 0; i < stateList.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (stores, error) = await FetchStoresForStateAsync(stateList[i], cancellationToken);
            allStores.AddRange(stores);

            if (error != null)
            {
                lastError = error;
                _logger.LogWarning("[OSM] Error fetching state {State}: {Error}", stateList[i], error);
            }

            // Respect Overpass fair-use: max 1 request / 60s
            if (i < stateList.Count - 1)
            {
                _logger.LogInformation("[OSM] Waiting 60s before next state query (Overpass fair-use)...");
                await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken);
            }
        }

        _logger.LogInformation("[OSM] Multi-state import complete: {Count} stores across {States} states",
            allStores.Count, stateList.Count);

        return (allStores, lastError);
    }

    private static string MapOsmShopToStoreType(string? shop)
    {
        return shop?.ToLowerInvariant() switch
        {
            "supermarket" => "Supermarket",
            "wholesale" => "Super Store",
            "grocery" => "Large Grocery Store",
            _ => "Supermarket"
        };
    }
}
