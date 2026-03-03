using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace ExpressRecipe.GroceryStoreLocationService.Services;

/// <summary>
/// Imports US grocery store locations from the USDA SNAP Retailer data CSV.
/// CSV URL: https://www.fns.usda.gov/sites/default/files/resource-files/store_locations.csv
/// CSV columns: Store_Name, Longitude, Latitude, Address, City, State, Zip5, County
/// </summary>
public class UsdaSnapImportService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UsdaSnapImportService> _logger;

    private const string CsvUrl =
        "https://www.fns.usda.gov/sites/default/files/resource-files/store_locations.csv";
    private const string DataSource = "USDA_SNAP";
    private const int MaxExternalIdLength = 200;

    public UsdaSnapImportService(HttpClient httpClient, ILogger<UsdaSnapImportService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<(List<Data.UpsertGroceryStoreRequest> Stores, string? ErrorMessage)> FetchStoresAsync(
        CancellationToken cancellationToken = default)
    {
        var stores = new List<Data.UpsertGroceryStoreRequest>();

        try
        {
            _logger.LogInformation("Downloading USDA SNAP store locations CSV from {Url}", CsvUrl);

            using var response = await _httpClient.GetAsync(CsvUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

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
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Skipping malformed CSV row {Row}", csv.Context?.Parser?.Row);
                }
            }

            _logger.LogInformation("Loaded {Count} stores from USDA SNAP CSV", stores.Count);
            return (stores, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch USDA SNAP store locations");
            return (stores, ex.Message);
        }
    }
}
