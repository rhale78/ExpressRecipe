using System.Threading.Channels;
using System.Diagnostics;
using System.Text;
using System.Data;
using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;
using ExpressRecipe.ProductService.Data;
using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.Client.Shared.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace ExpressRecipe.ProductService.Services;

/// <summary>
/// Result of a batch processing operation
/// </summary>
public class ProcessingResult
{
    public int SuccessCount;
    public int FailureCount;
}

/// <summary>
/// High-performance batch processor for products using a Producer-Consumer pipeline.
/// Uses System.Threading.Channels for backpressure and parallel processing.
/// </summary>
public class BatchProductProcessor
{
    private readonly ILogger<BatchProductProcessor> _logger;
    private readonly IIngredientListParser _ingredientListParser;
    private readonly IConfiguration _configuration;
    private readonly IngredientServiceClient _ingredientClient;
    private readonly int _maxDegreeOfParallelism;
    private readonly int _batchSize;
    private readonly int _bufferSize;

    // In-memory caches to avoid massive DB lookups (thread-safe concurrent collections)
    private ConcurrentDictionary<string, bool>? _barcodeCache;
    private ConcurrentDictionary<string, Guid>? _ingredientCache;

    public BatchProductProcessor(
        ILogger<BatchProductProcessor> logger,
        IIngredientListParser ingredientListParser,
        IConfiguration configuration,
        IngredientServiceClient ingredientClient,
        int maxDegreeOfParallelism = 4,
        int batchSize = 5000,
        int bufferSize = 50000)
    {
        _logger = logger;
        _ingredientListParser = ingredientListParser;
        _configuration = configuration;
        _ingredientClient = ingredientClient;
        _maxDegreeOfParallelism = Math.Max(1, maxDegreeOfParallelism);
        _batchSize = batchSize;
        _bufferSize = bufferSize;
    }

    public async Task<ProcessingResult> ProcessStagedProductsAsync(
        IProductStagingRepository stagingRepo,
        IProductRepository productRepo,
        IIngredientRepository ingredientRepo,
        IProductImageRepository productImageRepo,
        CancellationToken cancellationToken = default)
    {
        var result = new ProcessingResult();
        var stopwatch = Stopwatch.StartNew();

        // 0. Ensure Caches are loaded
        await EnsureCachesLoadedAsync(productRepo, ingredientRepo);

        _logger.LogInformation("Starting high-performance product processing pipeline. Pipeline: {BufSize} buffer, {Batch} batch, {Parallel} workers", 
            _bufferSize, _batchSize, _maxDegreeOfParallelism);

        // CHANNEL 1: Pending Staged Products -> Mappers
        var stagingChannel = Channel.CreateBounded<StagedProduct>(new BoundedChannelOptions(_bufferSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = true
        });

        // CHANNEL 2: Mapped Products -> DB Writer
        var mappedChannel = Channel.CreateBounded<(StagedProduct Staged, FullProductImportDto? Dto, bool Skipped)>(new BoundedChannelOptions(_bufferSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

        // 1. PRODUCER: Fetch from DB and write to stagingChannel
        var producerTask = Task.Run(() => FetchStagedProductsAsync(stagingRepo, stagingChannel.Writer, cancellationToken), cancellationToken);

        // 2. WORKERS: Map StagedProduct to FullProductImportDto
        var mappingTasks = Enumerable.Range(0, _maxDegreeOfParallelism)
            .Select(_ => Task.Run(() => MapProductsAsync(stagingChannel.Reader, mappedChannel.Writer, cancellationToken), cancellationToken))
            .ToList();

        // 3. CONSUMER: Bulk insert into DB in batches
        var consumerTask = Task.Run(() => SaveProductsAsync(productRepo, stagingRepo, mappedChannel.Reader, result, stopwatch, cancellationToken), cancellationToken);

        try
        {
            await producerTask;
            stagingChannel.Writer.Complete();
            
            await Task.WhenAll(mappingTasks);
            mappedChannel.Writer.Complete();
            
            await consumerTask;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Product processing pipeline failed.");
        }

        stopwatch.Stop();
        var totalTime = stopwatch.Elapsed;
        var totalProcessed = result.SuccessCount + result.FailureCount;
        _logger.LogInformation("Batch Product Processing Finished: {Success} successful, {Failed} failed. Time: {Elapsed}. Speed: {RPS:F1} rec/sec",
            result.SuccessCount, result.FailureCount, totalTime, totalProcessed / totalTime.TotalSeconds);

        return result;
    }

    private async Task EnsureCachesLoadedAsync(IProductRepository productRepo, IIngredientRepository ingredientRepo)
    {
        if (_barcodeCache != null && _ingredientCache != null) return;

        // Load barcode cache
        if (_barcodeCache == null)
        {
            _logger.LogInformation("Building in-memory Barcode cache...");
            var barcodes = await productRepo.GetAllBarcodesAsync();
            _barcodeCache = new ConcurrentDictionary<string, bool>(
                barcodes.Select(b => new KeyValuePair<string, bool>(b, true)),
                StringComparer.OrdinalIgnoreCase);
            _logger.LogInformation("Barcode cache built with {Count} records", _barcodeCache.Count);
        }

        // Load ingredient cache
        if (_ingredientCache == null)
        {
            _logger.LogInformation("Building in-memory Ingredient cache...");
            var ingredients = await ingredientRepo.GetAllIngredientNamesAndIdsAsync();
            _ingredientCache = new ConcurrentDictionary<string, Guid>(ingredients, StringComparer.OrdinalIgnoreCase);
            _logger.LogInformation("Ingredient cache built with {Count} records", _ingredientCache.Count);
        }
    }

    private async Task FetchStagedProductsAsync(IProductStagingRepository stagingRepo, ChannelWriter<StagedProduct> writer, CancellationToken ct)
    {
        try
        {
            int totalFetched = 0;
            int totalSkipped = 0;
            while (!ct.IsCancellationRequested)
            {
                var batch = await stagingRepo.GetPendingProductsAsync(_batchSize * 2);
                if (batch.Count == 0) break;

                foreach (var product in batch)
                {
                    if (!string.IsNullOrEmpty(product.Barcode) && 
                        _barcodeCache!.ContainsKey(product.Barcode))
                    {
                        totalSkipped++;
                        continue; 
                    }

                    await writer.WriteAsync(product, ct);
                    totalFetched++;
                }

                if ((totalFetched + totalSkipped) % 10000 == 0 && _logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("Producer: Fetched {Total} products ({Skipped} pre-filtered as duplicates)", 
                        totalFetched, totalSkipped);
            }

            if (totalSkipped > 0)
                _logger.LogInformation("Producer: Pre-filtered {Skipped} duplicate barcodes (saved {Percent:F1}% processing time)", 
                    totalSkipped, (totalSkipped * 100.0) / (totalFetched + totalSkipped));
        }
        catch (Exception ex) { _logger.LogError(ex, "Error fetching products from staging."); }
    }

    private async Task MapProductsAsync(ChannelReader<StagedProduct> reader, ChannelWriter<(StagedProduct, FullProductImportDto?, bool)> writer, CancellationToken ct)
    {
        while (await reader.WaitToReadAsync(ct))
        {
            while (reader.TryRead(out var staged))
            {
                try
                {
                    if (!string.IsNullOrEmpty(staged.Barcode) && 
                        _barcodeCache!.ContainsKey(staged.Barcode))
                    {
                        await writer.WriteAsync((staged, null, true), ct);
                        continue;
                    }

                    var dto = MapToFullImportDto(staged);
                    await writer.WriteAsync((staged, dto, false), ct);

                    if (!string.IsNullOrEmpty(staged.Barcode))
                    {
                        _barcodeCache.TryAdd(staged.Barcode, true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Mapping error for product '{Name}': {Msg}", staged.ProductName, ex.Message);
                    await writer.WriteAsync((staged, null, false), ct);
                }
            }
        }
    }

    private async Task SaveProductsAsync(IProductRepository productRepo, IProductStagingRepository stagingRepo, ChannelReader<(StagedProduct Staged, FullProductImportDto? Dto, bool Skipped)> reader, ProcessingResult result, Stopwatch stopwatch, CancellationToken ct)
    {
        var importBatch = new List<FullProductImportDto>();
        var stagingIds = new List<Guid>();
        var skipIds = new List<Guid>();
        var failedIds = new List<Guid>();

        var allSkipIds = new List<Guid>();
        var allSuccessIds = new List<Guid>();
        var allFailedIds = new List<Guid>();

        int totalProcessedInSession = 0;
        int lastLogTotal = 0;

        async Task FlushAsync()
        {
            totalProcessedInSession += skipIds.Count + importBatch.Count + failedIds.Count;

            if (skipIds.Any())
            {
                allSkipIds.AddRange(skipIds);
                Interlocked.Add(ref result.SuccessCount, skipIds.Count);
                skipIds.Clear();
            }

            if (importBatch.Any())
            {
                try
                {
                    // 2a. Pre-create/lookup ingredients using the new centralized service
                    var missingIngredientNames = importBatch
                        .SelectMany(i => i.IngredientNames)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (missingIngredientNames.Any())
                    {
                        // Bulk create/get IDs from the microservice
                        await _ingredientClient.BulkCreateIngredientsAsync(missingIngredientNames);
                        var newIngs = await _ingredientClient.LookupIngredientIdsAsync(missingIngredientNames);

                        foreach(var kvp in newIngs)
                        {
                            _ingredientCache!.TryAdd(kvp.Key, kvp.Value);
                        }

                        foreach(var item in importBatch)
                        {
                            foreach(var name in item.IngredientNames)
                            {
                                if (newIngs.TryGetValue(name, out var id)) item.IngredientIds.Add(id);
                            }
                            item.IngredientNames.Clear();
                        }
                    }

                    int createdCount = await productRepo.BulkCreateFullProductsHighSpeedAsync(importBatch);

                    var successfulStagingIds = stagingIds.Take(createdCount).ToList();
                    allSuccessIds.AddRange(successfulStagingIds);

                    var failedStagingIds = stagingIds.Skip(createdCount).ToList();
                    if (failedStagingIds.Any())
                    {
                        allFailedIds.AddRange(failedStagingIds);
                        Interlocked.Add(ref result.FailureCount, failedStagingIds.Count);
                    }

                    Interlocked.Add(ref result.SuccessCount, createdCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Writer: Bulk insert failed for batch of {Count}", importBatch.Count);
                    allFailedIds.AddRange(stagingIds);
                    Interlocked.Add(ref result.FailureCount, stagingIds.Count);
                }
                importBatch.Clear();
                stagingIds.Clear();
            }

            if (failedIds.Any())
            {
                allFailedIds.AddRange(failedIds);
                Interlocked.Add(ref result.FailureCount, failedIds.Count);
                failedIds.Clear();
            }

            if (totalProcessedInSession >= lastLogTotal + 1000)
            {
                var elapsed = stopwatch.Elapsed.TotalSeconds;
                var rps = elapsed > 0 ? totalProcessedInSession / elapsed : 0;
                var lag = reader.Count;
                _logger.LogInformation("Writer: Processed {Total} | Speed: {RPS:F1} rec/sec | Lag: {Lag} records", 
                    totalProcessedInSession, rps, lag);
                lastLogTotal = totalProcessedInSession;
            }
        }

        while (await reader.WaitToReadAsync(ct))
        {
            while (reader.TryRead(out var item))
            {
                if (item.Skipped) skipIds.Add(item.Staged.Id);
                else if (item.Dto != null) { importBatch.Add(item.Dto); stagingIds.Add(item.Staged.Id); }
                else failedIds.Add(item.Staged.Id);

                if (importBatch.Count + skipIds.Count + failedIds.Count >= _batchSize) await FlushAsync();
            }
        }
        await FlushAsync();

        if (allSkipIds.Any())
            await stagingRepo.BulkUpdateStatusAsync(allSkipIds, "Completed", "Skipped: Already exists in Product table");

        if (allSuccessIds.Any())
            await stagingRepo.BulkUpdateStatusAsync(allSuccessIds, "Completed");

        if (allFailedIds.Any())
            await stagingRepo.BulkUpdateStatusAsync(allFailedIds, "Failed", "Processing error");
    }

    private FullProductImportDto MapToFullImportDto(StagedProduct staged)
    {
        var dto = new FullProductImportDto
        {
            Product = new CreateProductRequest
            {
                Name = staged.ProductName ?? "Unknown Product",
                Brand = staged.Brands,
                Barcode = staged.Barcode,
                BarcodeType = DetermineBarcodeType(staged.Barcode),
                Description = staged.GenericName,
                Category = ExtractPrimaryCategory(staged.Categories),
                ImageUrl = staged.ImageUrl
            },
            ExternalId = staged.ExternalId,
            ExternalSource = "OpenFoodFacts"
        };

        var ingredientsText = staged.IngredientsTextEn ?? staged.IngredientsText;
        if (!string.IsNullOrWhiteSpace(ingredientsText))
        {
            var parsedNames = _ingredientListParser.ParseIngredients(ingredientsText).Take(50).ToList();

            foreach (var name in parsedNames)
            {
                if (_ingredientCache!.TryGetValue(name, out var ingId))
                    dto.IngredientIds.Add(ingId);
                else
                    dto.IngredientNames.Add(name);
            }
        }

        if (!string.IsNullOrWhiteSpace(staged.ImageUrl))
        {
            dto.Images.Add(new ProductImageDto {
                ImageUrl = staged.ImageUrl,
                ImageType = DetermineImageType(staged.ImageUrl),
                IsPrimary = true,
                DisplayOrder = 0,
                SourceSystem = "OpenFoodFacts"
            });
        }

        if (!string.IsNullOrWhiteSpace(staged.Allergens))
        {
            dto.Allergens = staged.Allergens.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(20).ToList();
        }

        if (!string.IsNullOrWhiteSpace(staged.NutritionData))
        {
            dto.Metadata["nutrition_json"] = staged.NutritionData;
        }

        return dto;
    }

    private static string? DetermineBarcodeType(string? barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return null;
        return barcode.Length switch { 8 => "EAN-8", 12 => "UPC-A", 13 => "EAN-13", 14 => "ITF-14", _ => "Unknown" };
    }

    private static string? ExtractPrimaryCategory(string? categories)
    {
        if (string.IsNullOrWhiteSpace(categories)) return "General";
        var parts = categories.Split(',', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0].Trim() : "General";
    }

    private static string DetermineImageType(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return "Front";
        var urlLower = imageUrl.ToLowerInvariant();
        if (urlLower.Contains("nutrition")) return "Nutrition";
        if (urlLower.Contains("ingredient")) return "Ingredients";
        if (urlLower.Contains("packaging")) return "Packaging";
        return "Front";
    }
}
