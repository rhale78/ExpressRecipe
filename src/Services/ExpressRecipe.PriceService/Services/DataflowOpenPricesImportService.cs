using ExpressRecipe.PriceService.Data;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using System.Threading.Tasks.Dataflow;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using Parquet;
using Parquet.Data;

namespace ExpressRecipe.PriceService.Services;

/// <summary>
/// Optimized OpenPrices import service using TPL Dataflow for batched product lookups and price inserts
/// </summary>
public class DataflowOpenPricesImportService : IOpenPricesImportService
{
    private readonly HttpClient _httpClient;
    private readonly IProductServiceClient _productServiceClient;
    private readonly IPriceRepository _priceRepository;
    private readonly ILogger<DataflowOpenPricesImportService> _logger;
    private readonly IConfiguration _configuration;
    
    private readonly int _productLookupBatchSize;
    private readonly int _priceInsertBatchSize;

    private static readonly HashSet<string> UsCountryCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "US", "en:us", "United States", "USA"
    };

    public DataflowOpenPricesImportService(
        HttpClient httpClient,
        IProductServiceClient productServiceClient,
        IPriceRepository priceRepository,
        ILogger<DataflowOpenPricesImportService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _productServiceClient = productServiceClient;
        _priceRepository = priceRepository;
        _logger = logger;
        _configuration = configuration;
        
        _productLookupBatchSize = configuration.GetValue("PriceImport:ProductLookupBatchSize", 100);
        _priceInsertBatchSize = configuration.GetValue("PriceImport:PriceInsertBatchSize", 1000);
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
        var counters = new ImportCounters();
        var sw = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("[PARQUET-DF] Starting dataflow import from Parquet stream...");

            MemoryStream ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken);
            ms.Position = 0;

            using var reader = await ParquetReader.CreateAsync(ms, cancellationToken: cancellationToken);
            _logger.LogInformation("[PARQUET-DF] Loaded Parquet file with {RowGroups} row groups", reader.RowGroupCount);

            // Set up dataflow pipeline
            await ProcessWithDataflowAsync(reader, counters, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PARQUET-DF] Failed to read Parquet stream");
            return new ImportResult 
            { 
                DataSource = "OpenPrices-Parquet-Dataflow",
                Success = false,
                Processed = counters.Processed,
                Imported = counters.Imported,
                Skipped = counters.Skipped,
                Errors = counters.Errors
            };
        }

        sw.Stop();
        
        var result = new ImportResult
        {
            DataSource = "OpenPrices-Parquet-Dataflow",
            Processed = counters.Processed,
            Imported = counters.Imported,
            Skipped = counters.Skipped,
            Errors = counters.Errors
        };
        
        await FinalizeImportAsync(result);
        
        _logger.LogInformation("[PARQUET-DF] Import completed in {Elapsed:F1}s. Rate: {Rate:F0} records/sec",
            sw.Elapsed.TotalSeconds, result.Processed / sw.Elapsed.TotalSeconds);
        
        return result;
    }

    private async Task ProcessWithDataflowAsync(ParquetReader reader, ImportCounters counters, CancellationToken cancellationToken)
    {
        // Stage 1: Parse raw records from Parquet
        var parseBlock = new TransformManyBlock<int, OpenPriceRecord>(
            async rowGroupIndex => await ParseRowGroupAsync(reader, rowGroupIndex, counters, cancellationToken),
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 2,
                BoundedCapacity = 4
            });

        // Stage 2: Filter US records
        var filterBlock = new TransformBlock<OpenPriceRecord, OpenPriceRecord>(
            record =>
            {
                if (IsUsRecord(record))
                    return record;
                
                counters.IncrementSkipped();
                return null!;
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 4,
                BoundedCapacity = 10000
            });

        // Stage 3: Batch barcodes for product lookup
        var barcodeBatchBlock = new BatchBlock<OpenPriceRecord>(
            _productLookupBatchSize,
            new GroupingDataflowBlockOptions
            {
                BoundedCapacity = _productLookupBatchSize * 20
            });

        // Stage 4: Resolve products in bulk
        var productResolveBlock = new TransformManyBlock<OpenPriceRecord[], (OpenPriceRecord Record, Guid ProductId)>(
            async batch => await ResolveBulkProductsAsync(batch, counters, cancellationToken),
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 3,
                BoundedCapacity = 10
            });

        // Stage 5: Map to price request
        var mapBlock = new TransformBlock<(OpenPriceRecord Record, Guid ProductId), UpsertProductPriceRequest>(
            item => MapToUpsertRequest(item.Record, item.ProductId),
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 4,
                BoundedCapacity = 10000
            });

        // Stage 6: Batch price inserts
        var priceBatchBlock = new BatchBlock<UpsertProductPriceRequest>(
            _priceInsertBatchSize,
            new GroupingDataflowBlockOptions
            {
                BoundedCapacity = _priceInsertBatchSize * 10
            });

        // Stage 7: Insert prices to database
        var insertBlock = new ActionBlock<UpsertProductPriceRequest[]>(
            async batch => await InsertPriceBatchAsync(batch, counters, cancellationToken),
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 2,
                BoundedCapacity = 5
            });

        // Link the pipeline
        var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };
        parseBlock.LinkTo(filterBlock, linkOptions, record => record != null);
        filterBlock.LinkTo(barcodeBatchBlock, linkOptions, record => record != null);
        barcodeBatchBlock.LinkTo(productResolveBlock, linkOptions);
        productResolveBlock.LinkTo(mapBlock, linkOptions);
        mapBlock.LinkTo(priceBatchBlock, linkOptions);
        priceBatchBlock.LinkTo(insertBlock, linkOptions);

        // Start timeout triggers for batching
        var barcodeTimerTask = TriggerBatchTimeoutAsync(barcodeBatchBlock, TimeSpan.FromSeconds(1), cancellationToken);
        var priceTimerTask = TriggerBatchTimeoutAsync(priceBatchBlock, TimeSpan.FromSeconds(2), cancellationToken);

        // Feed row groups into the pipeline
        for (int i = 0; i < reader.RowGroupCount; i++)
        {
            await parseBlock.SendAsync(i, cancellationToken);
        }

        // Signal completion and wait for pipeline to drain
        parseBlock.Complete();
        await insertBlock.Completion;
    }

    private async Task<IEnumerable<OpenPriceRecord>> ParseRowGroupAsync(
        ParquetReader reader,
        int rowGroupIndex,
        ImportCounters counters,
        CancellationToken cancellationToken)
    {
        var records = new List<OpenPriceRecord>();
        
        try
        {
            var columns = await reader.ReadEntireRowGroupAsync(rowGroupIndex);
            var rowCount = columns[0].Data.Length;
            
            _logger.LogInformation("[PARQUET-DF] Parsing row group {Group}/{Total} with {Rows} rows",
                rowGroupIndex + 1, reader.RowGroupCount, rowCount);

            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                counters.IncrementProcessed();

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

                    records.Add(record);
                }
                catch
                {
                    counters.IncrementErrors();
                }

                // Progress logging
                if (counters.Processed % 10000 == 0)
                {
                    _logger.LogInformation("[PARQUET-DF] Progress: {Processed:N0} processed, {Imported:N0} imported, {Skipped:N0} skipped",
                        counters.Processed, counters.Imported, counters.Skipped);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PARQUET-DF] Error parsing row group {Group}", rowGroupIndex);
        }

        return records;
    }

    private async Task<IEnumerable<(OpenPriceRecord Record, Guid ProductId)>> ResolveBulkProductsAsync(
        OpenPriceRecord[] batch,
        ImportCounters counters,
        CancellationToken cancellationToken)
    {
        var output = new List<(OpenPriceRecord, Guid)>();
        
        try
        {
            // Extract unique barcodes from this batch
            var barcodes = batch
                .Select(r => r.ProductCode)
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!barcodes.Any())
            {
                counters.AddSkipped(batch.Length);
                return output;
            }

            _logger.LogDebug("[DATAFLOW] Resolving {Count} products for batch of {BatchSize} records", barcodes.Count, batch.Length);

            // Bulk lookup from ProductService
            var productMap = await _productServiceClient.GetProductsByBarcodesAsync(barcodes, cancellationToken);

            // Map records to product IDs
            foreach (var record in batch)
            {
                if (string.IsNullOrWhiteSpace(record.ProductCode))
                {
                    counters.IncrementSkipped();
                    continue;
                }

                Guid productId;
                
                if (productMap.TryGetValue(record.ProductCode, out var product))
                {
                    productId = product.Id;
                }
                else
                {
                    // Create deterministic GUID for unknown products
                    productId = CreateDeterministicGuid(record.ProductCode);
                }

                output.Add((record, productId));
            }

            _logger.LogDebug("[DATAFLOW] Resolved {Matched}/{Total} products from ProductService",
                productMap.Count, barcodes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DATAFLOW] Error during bulk product resolution");
            counters.AddErrors(batch.Length);
        }

        return output;
    }

    private async Task InsertPriceBatchAsync(
        UpsertProductPriceRequest[] batch,
        ImportCounters counters,
        CancellationToken cancellationToken)
    {
        if (batch.Length == 0) return;

        try
        {
            var sw = Stopwatch.StartNew();
            
            var inserted = await _priceRepository.BulkUpsertProductPricesAsync(batch);
            counters.AddImported(inserted);
            
            sw.Stop();

            var first = batch[0];
            var last = batch[^1];

            _logger.LogInformation(
                "[DATAFLOW] Batch insert: {Inserted}/{Total} prices in {Ms}ms ({Rate:F0}/sec). First: [{FirstUpc}] {FirstProduct} ${FirstPrice}. Last: [{LastUpc}] {LastProduct} ${LastPrice}",
                inserted, batch.Length, sw.ElapsedMilliseconds, inserted / sw.Elapsed.TotalSeconds,
                first.Upc ?? "N/A", first.ProductName, first.Price,
                last.Upc ?? "N/A", last.ProductName, last.Price);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DATAFLOW] Failed to insert price batch of {Count} items", batch.Length);
            counters.AddErrors(batch.Length);
        }
    }

    private async Task TriggerBatchTimeoutAsync<T>(BatchBlock<T> batchBlock, TimeSpan interval, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, cancellationToken);
                batchBlock.TriggerBatch();
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async Task<ImportResult> ImportFromCsvStreamAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("[CSV] Using legacy import path. Dataflow version not yet implemented.");
        
        var legacyService = new OpenPricesImportService(
            _httpClient, _productServiceClient, _priceRepository, 
            _logger as ILogger<OpenPricesImportService> ?? throw new InvalidOperationException("Logger cast failed"),
            _configuration);
        
        return await legacyService.ImportFromCsvStreamAsync(stream, cancellationToken);
    }

    public async Task<ImportResult> ImportFromJsonlStreamAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("[JSONL] Using legacy import path. Dataflow version not yet implemented.");
        
        var legacyService = new OpenPricesImportService(
            _httpClient, _productServiceClient, _priceRepository,
            _logger as ILogger<OpenPricesImportService> ?? throw new InvalidOperationException("Logger cast failed"),
            _configuration);
        
        return await legacyService.ImportFromJsonlStreamAsync(stream, cancellationToken);
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
}

/// <summary>
/// Thread-safe counters for import operations
/// </summary>
internal class ImportCounters
{
    private int _processed;
    private int _imported;
    private int _skipped;
    private int _errors;

    public int Processed => _processed;
    public int Imported => _imported;
    public int Skipped => _skipped;
    public int Errors => _errors;

    public void IncrementProcessed() => Interlocked.Increment(ref _processed);
    public void IncrementImported() => Interlocked.Increment(ref _imported);
    public void IncrementSkipped() => Interlocked.Increment(ref _skipped);
    public void IncrementErrors() => Interlocked.Increment(ref _errors);

    public void AddProcessed(int count) => Interlocked.Add(ref _processed, count);
    public void AddImported(int count) => Interlocked.Add(ref _imported, count);
    public void AddSkipped(int count) => Interlocked.Add(ref _skipped, count);
    public void AddErrors(int count) => Interlocked.Add(ref _errors, count);
}
