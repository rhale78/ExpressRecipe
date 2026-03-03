using System.Text.Json;

namespace ExpressRecipe.GroceryStoreLocationService.Services;

/// <summary>
/// Imports grocery store locations from the OpenStreetMap Overpass API.
/// Focuses on supermarket nodes/ways in US states (defaults to NC).
/// </summary>
public class OpenStreetMapImportService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenStreetMapImportService> _logger;

    private const string OverpassApiUrl = "https://overpass-api.de/api/interpreter";
    private const string DataSource = "OSM";

    public OpenStreetMapImportService(HttpClient httpClient, ILogger<OpenStreetMapImportService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
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
  node[""shop""=""supermarket""](area.a);
  way[""shop""=""supermarket""](area.a);
  node[""shop""=""grocery""](area.a);
  way[""shop""=""grocery""](area.a);
);
out body;";

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
                    var osmId = element.GetProperty("id").GetInt64().ToString();
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

                    stores.Add(new Data.UpsertGroceryStoreRequest
                    {
                        Name = name,
                        Chain = brand,
                        StoreType = "Supermarket",
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

            _logger.LogInformation("Loaded {Count} stores from OSM for state {State}", stores.Count, stateCode);
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
}
