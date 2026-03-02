using CsvHelper;
using CsvHelper.Configuration;
using ExpressRecipe.PriceService.Data;
using System.Globalization;

namespace ExpressRecipe.PriceService.Services;

public class GroceryDbImportService
{
    private readonly HttpClient _httpClient;
    private readonly IPriceRepository _priceRepository;
    private readonly ILogger<GroceryDbImportService> _logger;
    private readonly IConfiguration _configuration;

    private static readonly Dictionary<string, string> RetailerChainMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["walmart"] = "Walmart",
        ["target"] = "Target",
        ["wholefoods"] = "Whole Foods",
        ["whole foods"] = "Whole Foods",
        ["kroger"] = "Kroger"
    };

    public GroceryDbImportService(
        HttpClient httpClient,
        IPriceRepository priceRepository,
        ILogger<GroceryDbImportService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _priceRepository = priceRepository;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<ImportResult> ImportFromFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);
        return await ImportFromStreamAsync(stream, cancellationToken);
    }

    public async Task<ImportResult> ImportFromStreamAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var result = new ImportResult { DataSource = "GroceryDB" };
        var batch = new List<UpsertProductPriceRequest>(500);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null
        };

        using var reader = new StreamReader(stream, leaveOpen: true);
        using var csv = new CsvReader(reader, config);

        await csv.ReadAsync();
        csv.ReadHeader();

        while (await csv.ReadAsync())
        {
            if (cancellationToken.IsCancellationRequested) break;

            result.Processed++;

            try
            {
                var record = csv.GetRecord<GroceryDbRecord>();
                if (record == null || string.IsNullOrWhiteSpace(record.Name)) { result.Skipped++; continue; }
                if (record.Price <= 0) { result.Skipped++; continue; }

                var productId = await ResolveProductIdAsync(record.UpcCode, record.Name, cancellationToken);

                var retailer = string.IsNullOrWhiteSpace(record.Retailer) ? "Unknown" : record.Retailer.Trim();
                var chainName = NormalizeChain(retailer);
                var externalId = !string.IsNullOrWhiteSpace(record.UpcCode)
                    ? $"{retailer}:{record.UpcCode}"
                    : $"{retailer}:{record.Name}";

                var request = new UpsertProductPriceRequest
                {
                    ProductId = productId,
                    Upc = string.IsNullOrWhiteSpace(record.UpcCode) ? null : record.UpcCode,
                    ProductName = record.Name,
                    StoreName = chainName,
                    StoreChain = chainName,
                    Price = record.Price,
                    Currency = "USD",
                    Unit = record.ServingSize,
                    DataSource = "GroceryDB",
                    ExternalId = externalId,
                    ObservedAt = DateTime.UtcNow
                };

                batch.Add(request);

                if (batch.Count >= 500)
                {
                    var imported = await _priceRepository.BulkUpsertProductPricesAsync(batch);
                    result.Imported += imported;
                    batch.Clear();
                }

                if (result.Processed % 1000 == 0)
                    _logger.LogInformation("GroceryDB import progress: {Processed} processed, {Imported} imported",
                        result.Processed, result.Imported);
            }
            catch (Exception ex)
            {
                result.Errors++;
                _logger.LogDebug(ex, "Failed to process GroceryDB record at row {Row}", result.Processed);
            }
        }

        if (batch.Count > 0)
        {
            var imported = await _priceRepository.BulkUpsertProductPricesAsync(batch);
            result.Imported += imported;
        }

        result.Success = result.Errors == 0 || result.Imported > 0;

        await _priceRepository.LogImportAsync(new PriceImportLogDto
        {
            DataSource = "GroceryDB",
            RecordsProcessed = result.Processed,
            RecordsImported = result.Imported,
            RecordsSkipped = result.Skipped,
            ErrorCount = result.Errors,
            Success = result.Success
        });

        _logger.LogInformation("GroceryDB import complete: {Processed} processed, {Imported} imported, {Errors} errors",
            result.Processed, result.Imported, result.Errors);

        return result;
    }

    private async Task<Guid> ResolveProductIdAsync(string? upc, string productName, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(upc))
        {
            try
            {
                var baseUrl = _configuration["ProductService:BaseUrl"] ?? "http://productservice";
                var response = await _httpClient.GetAsync($"{baseUrl}/api/products/barcode/{upc}", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(cancellationToken);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("id", out var idEl) && idEl.TryGetGuid(out var pid))
                        return pid;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to look up product by UPC {Upc}", upc);
            }

            return OpenPricesImportService.CreateDeterministicGuid(upc);
        }

        // Fall back to name-based hash
        return OpenPricesImportService.CreateDeterministicGuid(productName.ToLowerInvariant());
    }

    private static string NormalizeChain(string? retailer)
    {
        if (string.IsNullOrWhiteSpace(retailer)) return "Unknown";
        return RetailerChainMap.TryGetValue(retailer.Trim(), out var name) ? name : retailer.Trim();
    }
}

public class GroceryDbRecord
{
    [CsvHelper.Configuration.Attributes.Name("retailer")]
    public string? Retailer { get; set; }

    [CsvHelper.Configuration.Attributes.Name("brand_name")]
    public string? BrandName { get; set; }

    [CsvHelper.Configuration.Attributes.Name("name")]
    public string Name { get; set; } = string.Empty;

    [CsvHelper.Configuration.Attributes.Name("upc_code")]
    public string? UpcCode { get; set; }

    [CsvHelper.Configuration.Attributes.Name("price")]
    public decimal Price { get; set; }

    [CsvHelper.Configuration.Attributes.Name("price_per_serving")]
    public decimal? PricePerServing { get; set; }

    [CsvHelper.Configuration.Attributes.Name("serving_size")]
    public string? ServingSize { get; set; }
}
