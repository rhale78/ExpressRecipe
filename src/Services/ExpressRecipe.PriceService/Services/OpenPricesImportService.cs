using ExpressRecipe.PriceService.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ExpressRecipe.PriceService.Services;

public class OpenPricesImportService
{
    private readonly HttpClient _httpClient;
    private readonly IPriceRepository _priceRepository;
    private readonly ILogger<OpenPricesImportService> _logger;
    private readonly IConfiguration _configuration;

    private static readonly HashSet<string> UsCountryCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "US", "en:us", "United States"
    };

    public OpenPricesImportService(
        HttpClient httpClient,
        IPriceRepository priceRepository,
        ILogger<OpenPricesImportService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _priceRepository = priceRepository;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<ImportResult> ImportFromStreamAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var result = new ImportResult { DataSource = "OpenPrices" };
        var batch = new List<UpsertProductPriceRequest>(500);

        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        string? line;

        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            if (cancellationToken.IsCancellationRequested) break;
            if (string.IsNullOrWhiteSpace(line)) continue;

            result.Processed++;

            try
            {
                var record = JsonSerializer.Deserialize<OpenPriceRecord>(line, JsonOptions);
                if (record == null) { result.Skipped++; continue; }

                // Filter to US only
                if (!IsUsRecord(record)) { result.Skipped++; continue; }

                var productId = await ResolveProductIdAsync(record.ProductCode, cancellationToken);
                if (productId == Guid.Empty) { result.Skipped++; continue; }

                var request = MapToUpsertRequest(record, productId);
                batch.Add(request);

                if (batch.Count >= 500)
                {
                    var imported = await _priceRepository.BulkUpsertProductPricesAsync(batch);
                    result.Imported += imported;
                    batch.Clear();
                }

                if (result.Processed % 1000 == 0)
                    _logger.LogInformation("OpenPrices import progress: {Processed} processed, {Imported} imported, {Skipped} skipped",
                        result.Processed, result.Imported, result.Skipped);
            }
            catch (Exception ex)
            {
                result.Errors++;
                _logger.LogDebug(ex, "Failed to process OpenPrices record at line {Line}", result.Processed);
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
            DataSource = "OpenPrices",
            RecordsProcessed = result.Processed,
            RecordsImported = result.Imported,
            RecordsSkipped = result.Skipped,
            ErrorCount = result.Errors,
            Success = result.Success
        });

        _logger.LogInformation("OpenPrices import complete: {Processed} processed, {Imported} imported, {Errors} errors",
            result.Processed, result.Imported, result.Errors);

        return result;
    }

    public async Task<ImportResult> ImportFromFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);
        return await ImportFromStreamAsync(stream, cancellationToken);
    }

    private bool IsUsRecord(OpenPriceRecord record)
    {
        return UsCountryCodes.Contains(record.LocationCountry ?? string.Empty);
    }

    private async Task<Guid> ResolveProductIdAsync(string? upc, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(upc)) return Guid.Empty;

        try
        {
            var baseUrl = _configuration["ProductService:BaseUrl"] ?? "http://productservice";
            var response = await _httpClient.GetAsync($"{baseUrl}/api/products/barcode/{upc}", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("id", out var idElement) &&
                    idElement.TryGetGuid(out var productId))
                {
                    return productId;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to look up product by UPC {Upc}", upc);
        }

        // Synthetic product ID based on UPC hash for unknown products
        return CreateDeterministicGuid(upc);
    }

    internal static Guid CreateDeterministicGuid(string input)
    {
        // SHA256 produces a 32-byte hash; take the first 16 bytes to form a Guid.
        // This is NOT a cryptographic operation – the Guid is only used as a stable
        // identifier to correlate prices with an unknown product across imports.
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return new Guid(hash[..16]);
    }

    private static UpsertProductPriceRequest MapToUpsertRequest(OpenPriceRecord record, Guid productId) =>
        new()
        {
            ProductId = productId,
            Upc = record.ProductCode,
            ProductName = record.ProductName ?? record.ProductCode ?? string.Empty,
            StoreName = record.LocationName,
            City = record.LocationCity,
            State = null, // Open Prices doesn't provide US state
            Price = record.Price,
            Currency = record.Currency ?? "USD",
            DataSource = "OpenPrices",
            ExternalId = record.Id?.ToString(),
            ObservedAt = record.Date.HasValue ? record.Date.Value.ToDateTime(TimeOnly.MinValue) : DateTime.UtcNow
        };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };
}

public class OpenPriceRecord
{
    public long? Id { get; set; }
    public string? ProductCode { get; set; }
    public string? ProductName { get; set; }
    public decimal Price { get; set; }
    public bool PriceIsDiscounted { get; set; }
    public long? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string? LocationCity { get; set; }
    public string? LocationCountry { get; set; }
    public string? Currency { get; set; }
    public DateOnly? Date { get; set; }
}

public class ImportResult
{
    public string DataSource { get; set; } = string.Empty;
    public int Processed { get; set; }
    public int Imported { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Errors { get; set; }
    public bool Success { get; set; }
}
