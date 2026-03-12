using ExpressRecipe.PriceService.Data;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Diagnostics;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using Parquet;
using Parquet.Data;

namespace ExpressRecipe.PriceService.Services;

public class OpenPricesImportService : IOpenPricesImportService
{
    private readonly HttpClient _httpClient;
    private readonly IProductServiceClient _productServiceClient;
    private readonly IPriceRepository _priceRepository;
    private readonly ILogger<OpenPricesImportService> _logger;
    private readonly IConfiguration _configuration;

    private static readonly HashSet<string> UsCountryCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "US", "en:us", "United States", "USA"
    };

    public OpenPricesImportService(
        HttpClient httpClient,
        IProductServiceClient productServiceClient,
        IPriceRepository priceRepository,
        ILogger<OpenPricesImportService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _productServiceClient = productServiceClient;
        _priceRepository = priceRepository;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<ImportResult> ImportFromUrlAsync(string url, string format, CancellationToken cancellationToken = default)
    {
        Exception? lastError = null;

        foreach (var candidateUrl in GetCandidateUrls(url, format))
        {
            try
            {
                _logger.LogInformation("Starting download from {Url} (Format: {Format})", candidateUrl, format);

                using var request = new HttpRequestMessage(HttpMethod.Get, candidateUrl);
                request.Headers.TryAddWithoutValidation("User-Agent", "ExpressRecipe.PriceService/1.0 (+https://github.com/rhale78/ExpressRecipe)");
                request.Headers.TryAddWithoutValidation("Accept", "*/*");

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    _logger.LogWarning("OpenPrices URL {Url} returned {StatusCode}; trying next fallback URL", candidateUrl, (int)response.StatusCode);
                    lastError = new HttpRequestException($"OpenPrices URL returned {(int)response.StatusCode} ({response.ReasonPhrase})");
                    continue;
                }

                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

                if (format.Equals("parquet", StringComparison.OrdinalIgnoreCase))
                {
                    return await ImportFromParquetStreamAsync(stream, cancellationToken);
                }
                else if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
                {
                    return await ImportFromCsvStreamAsync(stream, cancellationToken);
                }
                else
                {
                    return await ImportFromJsonlStreamAsync(stream, cancellationToken);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                lastError = ex;
                _logger.LogWarning(ex, "Failed to import from OpenPrices URL {Url}; trying next fallback URL", candidateUrl);
            }
        }

        throw new HttpRequestException(
            "Unable to download OpenPrices data from all configured URLs. Configure PriceImport:OpenPricesUrl and optional PriceImport:FallbackUrls.",
            lastError);
    }

    private IEnumerable<string> GetCandidateUrls(string primaryUrl, string format)
    {
        var candidates = new List<string> { primaryUrl };

        if (primaryUrl.Contains("huggingface.co", StringComparison.OrdinalIgnoreCase) && !primaryUrl.Contains("download=1", StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(primaryUrl.Contains('?') ? $"{primaryUrl}&download=1" : $"{primaryUrl}?download=1");
        }

        var configuredFallbacks = _configuration.GetSection("PriceImport:FallbackUrls").Get<string[]>();
        if (configuredFallbacks is { Length: > 0 })
        {
            candidates.AddRange(configuredFallbacks.Where(u => !string.IsNullOrWhiteSpace(u)));
        }
        else
        {
            if (format.Equals("parquet", StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add("https://static.openfoodfacts.org/data/openprices/prices.parquet");
                candidates.Add("https://static.openfoodfacts.org/data/open-prices/prices.parquet");
            }
            else if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add("https://static.openfoodfacts.org/data/openprices/prices.csv");
                candidates.Add("https://static.openfoodfacts.org/data/open-prices/prices.csv");
            }
        }

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<ImportResult> ImportFromParquetStreamAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var result = new ImportResult { DataSource = "OpenPrices-Parquet" };
        var batch = new List<UpsertProductPriceRequest>(1000);
        UpsertProductPriceRequest? firstItem = null;
        UpsertProductPriceRequest? lastItem = null;
        int batchNumber = 0;

        try
        {
            _logger.LogInformation("[PARQUET] Starting import from Parquet stream...");

            MemoryStream ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken);
            ms.Position = 0;

            using var reader = await ParquetReader.CreateAsync(ms, cancellationToken: cancellationToken);
            _logger.LogInformation("[PARQUET] Loaded Parquet file with {RowGroups} row groups", reader.RowGroupCount);

            for (int i = 0; i < reader.RowGroupCount; i++)
            {
                var columns = await reader.ReadEntireRowGroupAsync(i);
                var rowCount = columns[0].Data.Length;
                _logger.LogInformation("[PARQUET] Processing row group {Group}/{Total} with {Rows} rows", 
                    i + 1, reader.RowGroupCount, rowCount);

                for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
                {
                    result.Processed++;

                    try 
                    {
                        var record = new OpenPriceRecord
                        {
                            ProductCode = GetColumnValue<string>(columns, rowIndex, "product_code"),
                            ProductName = GetColumnValue<string>(columns, rowIndex, "product_name"),
                            Price = GetColumnValue<decimal>(columns, rowIndex, "price"),
                            LocationName = GetColumnValue<string>(columns, rowIndex, "location_name"),
                            LocationCity = GetColumnValue<string>(columns, rowIndex, "location_city"),
                            LocationCountry = GetColumnValue<string>(columns, rowIndex, "location_country"),
                            Currency = GetColumnValue<string>(columns, rowIndex, "currency")
                        };

                        if (!IsUsRecord(record)) { result.Skipped++; continue; }

                        var productId = await ResolveProductIdAsync(record.ProductCode, cancellationToken);
                        if (productId == Guid.Empty) { result.Skipped++; continue; }

                        var priceRequest = MapToUpsertRequest(record, productId);
                        batch.Add(priceRequest);

                        // Track first and last items for logging
                        if (firstItem == null) firstItem = priceRequest;
                        lastItem = priceRequest;

                        if (batch.Count >= 1000)
                        {
                            batchNumber++;
                            var imported = await _priceRepository.BulkUpsertProductPricesAsync(batch);
                            result.Imported += imported;

                            // Log batch details with first/last items
                            _logger.LogInformation(
                                "[PARQUET] Batch #{Batch} complete: {Imported} prices saved. First: [{FirstBarcode}] {FirstProduct} ${FirstPrice} @ {FirstLocation}. Last: [{LastBarcode}] {LastProduct} ${LastPrice} @ {LastLocation}",
                                batchNumber, imported,
                                firstItem?.Upc ?? "N/A", firstItem?.ProductName ?? "N/A", firstItem?.Price ?? 0, firstItem?.StoreName ?? "N/A",
                                lastItem?.Upc ?? "N/A", lastItem?.ProductName ?? "N/A", lastItem?.Price ?? 0, lastItem?.StoreName ?? "N/A");

                            batch.Clear();
                            firstItem = null;
                            lastItem = null;

                            // Inter-item delay to prevent overwhelming CPU/disk (configured via PriceImport:BatchDelayMs)
                            var batchDelayMs = _configuration.GetValue<int>("PriceImport:BatchDelayMs", 0);
                            if (batchDelayMs > 0)
                                await Task.Delay(batchDelayMs, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Errors++;
                        if (result.Errors < 10) _logger.LogDebug(ex, "Error processing parquet row {Row}", rowIndex);
                    }

                    // Progress logging every 10k rows
                    if (result.Processed % 10000 == 0)
                    {
                        _logger.LogInformation("[PARQUET] Progress: {Processed:N0} processed, {Imported:N0} imported, {Skipped:N0} skipped, {Errors} errors", 
                            result.Processed, result.Imported, result.Skipped, result.Errors);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PARQUET] Failed to read Parquet stream");
            result.Success = false;
            return result;
        }

        // Save final batch
        if (batch.Count > 0)
        {
            batchNumber++;
            var imported = await _priceRepository.BulkUpsertProductPricesAsync(batch);
            result.Imported += imported;

            _logger.LogInformation(
                "[PARQUET] Final batch #{Batch} complete: {Imported} prices saved. First: [{FirstBarcode}] {FirstProduct} ${FirstPrice}. Last: [{LastBarcode}] {LastProduct} ${LastPrice}",
                batchNumber, imported,
                firstItem?.Upc ?? "N/A", firstItem?.ProductName ?? "N/A", firstItem?.Price ?? 0,
                lastItem?.Upc ?? "N/A", lastItem?.ProductName ?? "N/A", lastItem?.Price ?? 0);
        }

        await FinalizeImportAsync(result);
        return result;
    }

    private T? GetColumnValue<T>(DataColumn[] columns, int rowIndex, string columnName)
    {
        try
        {
            var column = columns.FirstOrDefault(c => c.Field.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));
            if (column == null) return default;

            var val = column.Data.GetValue(rowIndex);
            if (val == null) return default;
            
            if (typeof(T) == typeof(decimal))
            {
                return (T)(object)Convert.ToDecimal(val);
            }
            
            return (T)Convert.ChangeType(val, typeof(T));
        }
        catch { return default; }
    }

    public async Task<ImportResult> ImportFromCsvStreamAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var result = new ImportResult { DataSource = "OpenPrices-CSV" };
        var batch = new List<UpsertProductPriceRequest>(1000);
        UpsertProductPriceRequest? firstItem = null;
        UpsertProductPriceRequest? lastItem = null;
        int batchNumber = 0;

        _logger.LogInformation("[CSV] Starting import from CSV stream...");

        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            HeaderValidated = null,
            MissingFieldFound = null,
            PrepareHeaderForMatch = args => args.Header.ToLower().Replace("_", "")
        });

        await foreach (var record in csv.GetRecordsAsync<OpenPriceRecord>(cancellationToken))
        {
            result.Processed++;

            if (!IsUsRecord(record)) { result.Skipped++; continue; }

            var productId = await ResolveProductIdAsync(record.ProductCode, cancellationToken);
            if (productId == Guid.Empty) { result.Skipped++; continue; }

            var priceRequest = MapToUpsertRequest(record, productId);
            batch.Add(priceRequest);

            if (firstItem == null) firstItem = priceRequest;
            lastItem = priceRequest;

            if (batch.Count >= 1000)
            {
                batchNumber++;
                var imported = await _priceRepository.BulkUpsertProductPricesAsync(batch);
                result.Imported += imported;

                _logger.LogInformation(
                    "[CSV] Batch #{Batch} complete: {Imported} prices saved. First: [{FirstBarcode}] {FirstProduct} ${FirstPrice}. Last: [{LastBarcode}] {LastProduct} ${LastPrice}",
                    batchNumber, imported,
                    firstItem?.Upc ?? "N/A", firstItem?.ProductName ?? "N/A", firstItem?.Price ?? 0,
                    lastItem?.Upc ?? "N/A", lastItem?.ProductName ?? "N/A", lastItem?.Price ?? 0);

                batch.Clear();
                firstItem = null;
                lastItem = null;

                // Inter-item delay to prevent overwhelming CPU/disk (configured via PriceImport:BatchDelayMs)
                var batchDelayMs = _configuration.GetValue<int>("PriceImport:BatchDelayMs", 0);
                if (batchDelayMs > 0)
                    await Task.Delay(batchDelayMs, cancellationToken).ConfigureAwait(false);
            }

            if (result.Processed % 10000 == 0)
                _logger.LogInformation("[CSV] Progress: {Processed:N0} processed, {Imported:N0} imported, {Skipped:N0} skipped", 
                    result.Processed, result.Imported, result.Skipped);
        }

        if (batch.Count > 0)
        {
            batchNumber++;
            var imported = await _priceRepository.BulkUpsertProductPricesAsync(batch);
            result.Imported += imported;

            _logger.LogInformation(
                "[CSV] Final batch #{Batch} complete: {Imported} prices saved. First: [{FirstBarcode}] {FirstProduct} ${FirstPrice}. Last: [{LastBarcode}] {LastProduct} ${LastPrice}",
                batchNumber, imported,
                firstItem?.Upc ?? "N/A", firstItem?.ProductName ?? "N/A", firstItem?.Price ?? 0,
                lastItem?.Upc ?? "N/A", lastItem?.ProductName ?? "N/A", lastItem?.Price ?? 0);
        }

        await FinalizeImportAsync(result);
        return result;
    }

    public async Task<ImportResult> ImportFromJsonlStreamAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var result = new ImportResult { DataSource = "OpenPrices-JSONL" };
        var batch = new List<UpsertProductPriceRequest>(1000);
        UpsertProductPriceRequest? firstItem = null;
        UpsertProductPriceRequest? lastItem = null;
        int batchNumber = 0;

        _logger.LogInformation("[JSONL] Starting import from JSONL stream...");

        using var reader = new StreamReader(stream, Encoding.UTF8);
        string? line;

        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            result.Processed++;

            try
            {
                var record = JsonSerializer.Deserialize<OpenPriceRecord>(line, JsonOptions);
                if (record == null || !IsUsRecord(record)) { result.Skipped++; continue; }

                var productId = await ResolveProductIdAsync(record.ProductCode, cancellationToken);
                if (productId == Guid.Empty) { result.Skipped++; continue; }

                var priceRequest = MapToUpsertRequest(record, productId);
                batch.Add(priceRequest);

                if (firstItem == null) firstItem = priceRequest;
                lastItem = priceRequest;

                if (batch.Count >= 1000)
                {
                    batchNumber++;
                    var imported = await _priceRepository.BulkUpsertProductPricesAsync(batch);
                    result.Imported += imported;

                    _logger.LogInformation(
                        "[JSONL] Batch #{Batch} complete: {Imported} prices saved. First: [{FirstBarcode}] {FirstProduct} ${FirstPrice}. Last: [{LastBarcode}] {LastProduct} ${LastPrice}",
                        batchNumber, imported,
                        firstItem?.Upc ?? "N/A", firstItem?.ProductName ?? "N/A", firstItem?.Price ?? 0,
                        lastItem?.Upc ?? "N/A", lastItem?.ProductName ?? "N/A", lastItem?.Price ?? 0);

                    batch.Clear();
                    firstItem = null;
                    lastItem = null;
                }

                if (result.Processed % 10000 == 0)
                {
                    _logger.LogInformation("[JSONL] Progress: {Processed:N0} processed, {Imported:N0} imported, {Skipped:N0} skipped", 
                        result.Processed, result.Imported, result.Skipped);
                }
            }
            catch 
            { 
                result.Errors++; 
            }
        }

        if (batch.Count > 0)
        {
            batchNumber++;
            var imported = await _priceRepository.BulkUpsertProductPricesAsync(batch);
            result.Imported += imported;

            _logger.LogInformation(
                "[JSONL] Final batch #{Batch} complete: {Imported} prices saved. First: [{FirstBarcode}] {FirstProduct} ${FirstPrice}. Last: [{LastBarcode}] {LastProduct} ${LastPrice}",
                batchNumber, imported,
                firstItem?.Upc ?? "N/A", firstItem?.ProductName ?? "N/A", firstItem?.Price ?? 0,
                lastItem?.Upc ?? "N/A", lastItem?.ProductName ?? "N/A", lastItem?.Price ?? 0);
        }

        await FinalizeImportAsync(result);
        return result;
    }

    public async Task<ImportResult> ImportFromFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(filePath).ToLower();
        await using var stream = File.OpenRead(filePath);
        
        return ext switch
        {
            ".parquet" => await ImportFromParquetStreamAsync(stream, cancellationToken),
            ".csv" => await ImportFromCsvStreamAsync(stream, cancellationToken),
            _ => await ImportFromJsonlStreamAsync(stream, cancellationToken)
        };
    }

    public bool FileExists(string filePath) => File.Exists(filePath);

    private async Task FinalizeImportAsync(ImportResult result)
    {
        result.Success = result.Errors == 0 || result.Imported > 0;

        await _priceRepository.LogImportAsync(new PriceImportLogDto
        {
            DataSource = result.DataSource,
            RecordsProcessed = result.Processed,
            RecordsImported = result.Imported,
            RecordsSkipped = result.Skipped,
            ErrorCount = result.Errors,
            Success = result.Success
        });

        _logger.LogInformation("{Source} import complete: {Processed} processed, {Imported} imported",
            result.DataSource, result.Processed, result.Imported);
    }

    private bool IsUsRecord(OpenPriceRecord record)
    {
        return string.IsNullOrWhiteSpace(record.LocationCountry) || 
               UsCountryCodes.Contains(record.LocationCountry);
    }

    private async Task<Guid> ResolveProductIdAsync(string? upc, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(upc)) return Guid.Empty;

        try
        {
            var product = await _productServiceClient.GetProductByBarcodeAsync(upc, cancellationToken);
            if (product != null)
            {
                return product.Id;
            }
        }
        catch { }

        return CreateDeterministicGuid(upc);
    }

    internal static Guid CreateDeterministicGuid(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return new Guid(hash[..16]);
    }

    private static UpsertProductPriceRequest MapToUpsertRequest(OpenPriceRecord record, Guid productId) =>
        new()
        {
            ProductId = productId,
            Upc = record.ProductCode,
            ProductName = record.ProductName ?? record.ProductCode ?? "Unknown",
            StoreName = record.LocationName,
            City = record.LocationCity,
            Price = record.Price,
            Currency = record.Currency ?? "USD",
            DataSource = "OpenPrices",
            ObservedAt = record.Date.HasValue ? record.Date.Value.ToDateTime(TimeOnly.MinValue) : DateTime.UtcNow
        };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };
}
