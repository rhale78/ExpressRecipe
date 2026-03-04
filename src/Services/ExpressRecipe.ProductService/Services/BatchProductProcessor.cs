using System.Threading.Channels;
using System.Diagnostics;
using System.Text;
using System.Data;
using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;
using ExpressRecipe.ProductService.Data;
using ExpressRecipe.ProductService.Logging;
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
    private readonly IConfiguration _configuration;
    private readonly IIngredientServiceClient _ingredientClient;
    private readonly int _maxDegreeOfParallelism;
    private readonly int _batchSize;
    private readonly int _bufferSize;

    // In-memory caches to avoid massive DB lookups (thread-safe concurrent collections)
    private ConcurrentDictionary<string, bool>? _barcodeCache;
    private ConcurrentDictionary<string, Guid>? _ingredientCache;

    public BatchProductProcessor(
        ILogger<BatchProductProcessor> logger,
        IConfiguration configuration,
        IIngredientServiceClient ingredientClient,
        int maxDegreeOfParallelism = 4,
        int batchSize = 5000,
        int bufferSize = 50000)
    {
        _logger = logger;
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
            .Select(_ => Task.Run(() => MapProductsParallelAsync(stagingChannel.Reader, mappedChannel.Writer, cancellationToken), cancellationToken))
            .ToList();

        // 3. CONSUMER: Bulk insert into DB in batches
        var consumerTask = Task.Run(() => SaveProductsAsync(productRepo, stagingRepo, mappedChannel.Reader, result, stopwatch, cancellationToken), cancellationToken);

        await producerTask;
        stagingChannel.Writer.Complete();

        await Task.WhenAll(mappingTasks);
        mappedChannel.Writer.Complete();

        await consumerTask;

        stopwatch.Stop();
        _logger.LogInformation("Processing complete. Success: {Success}, Failed: {Failed}, Total: {Total}. Rate: {Rate:F2} products/sec", 
            result.SuccessCount, result.FailureCount, result.SuccessCount + result.FailureCount, (result.SuccessCount + result.FailureCount) / stopwatch.Elapsed.TotalSeconds);

        return result;
    }

    private async Task EnsureCachesLoadedAsync(IProductRepository productRepo, IIngredientRepository ingredientRepo)
    {
        if (_barcodeCache != null) return;
        
        _logger.LogInformation("Pre-loading caches...");
        var sw = Stopwatch.StartNew();

        var barCodes = await productRepo.GetAllBarcodesAsync();
        _barcodeCache = new ConcurrentDictionary<string, bool>(barCodes.Select(b => new KeyValuePair<string, bool>(b, true)));

        // Load all known ingredient mappings from microservice (via our proxy repository)
        var ings = await ingredientRepo.GetAllIngredientNamesAndIdsAsync();
        _ingredientCache = new ConcurrentDictionary<string, Guid>(ings, StringComparer.OrdinalIgnoreCase);

        _logger.LogInformation("Pre-loading finished in {Ms}ms. Barcodes: {B}, Ingredients: {I}", 
            sw.ElapsedMilliseconds, _barcodeCache.Count, _ingredientCache.Count);
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
                if (!batch.Any()) break;

                foreach (var staged in batch)
                {
                    if (!string.IsNullOrEmpty(staged.Barcode) && _barcodeCache!.ContainsKey(staged.Barcode))
                    {
                        totalSkipped++;
                        continue;
                    }

                    if (!await writer.WaitToWriteAsync(ct)) break;
                    await writer.WriteAsync(staged, ct);
                    totalFetched++;
                }

                if (batch.Count < _batchSize) break;
            }
            _logger.LogInformation("Producer finished. Total fetched: {Count}, Total skipped (already exists): {Skipped}", totalFetched, totalSkipped);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Producer error");
        }
    }

    private async Task MapProductsParallelAsync(ChannelReader<StagedProduct> reader, ChannelWriter<(StagedProduct Staged, FullProductImportDto? Dto, bool Skipped)> writer, CancellationToken ct)
    {
        while (await reader.WaitToReadAsync(ct))
        {
            var batch = new List<StagedProduct>();
            while (batch.Count < 50 && reader.TryRead(out var staged))
            {
                batch.Add(staged);
            }

            if (!batch.Any()) continue;

            try 
            {
                // 1. Collect all ingredient texts for bulk parsing
                var textsToParse = batch
                    .Select(s => s.IngredientsTextEn ?? s.IngredientsText)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Cast<string>()
                    .Distinct()
                    .ToList();

                Dictionary<string, List<string>> parsedResults = new();
                if (textsToParse.Any())
                {
                    parsedResults = await _ingredientClient.BulkParseIngredientListsAsync(textsToParse);
                }

                // 2. Map products using parsed results
                foreach (var staged in batch)
                {
                    var ingredientsText = staged.IngredientsTextEn ?? staged.IngredientsText;
                    List<string> parsedNames = new();
                    if (!string.IsNullOrEmpty(ingredientsText) && parsedResults.TryGetValue(ingredientsText, out var names))
                    {
                        parsedNames = names.Take(50).ToList();
                    }

                    var dto = MapToFullImportDto(staged, parsedNames);
                    await writer.WriteAsync((staged, dto, false), ct);

                    if (!string.IsNullOrEmpty(staged.Barcode))
                    {
                        _barcodeCache!.TryAdd(staged.Barcode, true);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Parallel mapper error");
                // Attempt mapping one by one as fallback or just fail the batch segment
                foreach(var s in batch) await writer.WriteAsync((s, null, false), ct);
            }
        }
    }

    private FullProductImportDto MapToFullImportDto(StagedProduct staged, List<string> parsedIngredientNames)
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

        foreach (var name in parsedIngredientNames)
        {
            if (_ingredientCache!.TryGetValue(name, out var ingId))
                dto.IngredientIds.Add(ingId);
            else
                dto.IngredientNames.Add(name);
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
                    // 2a. Pre-create/lookup ingredients using the microservice
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

                    if (createdCount > 0 && importBatch.Count > 0)
                    {
                        var firstProduct = importBatch.First().Product;
                        var lastProduct = importBatch.Last().Product;
                        _logger.LogInformation(
                            "[PRODUCTS] Batch saved: {Created}/{Total} products. First: [{FirstBarcode}] {FirstName} ({FirstBrand}). Last: [{LastBarcode}] {LastName} ({LastBrand})",
                            createdCount, importBatch.Count,
                            firstProduct.Barcode ?? "N/A", firstProduct.Name, firstProduct.Brand ?? "N/A",
                            lastProduct.Barcode ?? "N/A", lastProduct.Name, lastProduct.Brand ?? "N/A");
                    }

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

            if (totalProcessedInSession - lastLogTotal >= 1000)
            {
                var recordsPerSec = totalProcessedInSession / stopwatch.Elapsed.TotalSeconds;
                _logger.LogBatchProcessed(totalProcessedInSession, (long)stopwatch.Elapsed.TotalMilliseconds, recordsPerSec);
                lastLogTotal = totalProcessedInSession;
            }

            // Update staging status in DB periodically
            if (allSuccessIds.Any())
            {
                await stagingRepo.BulkUpdateStatusAsync(allSuccessIds, "Completed");
                allSuccessIds.Clear();
            }
            if (allSkipIds.Any())
            {
                await stagingRepo.BulkUpdateStatusAsync(allSkipIds, "Completed");
                allSkipIds.Clear();
            }
            if (allFailedIds.Any())
            {
                await stagingRepo.BulkUpdateStatusAsync(allFailedIds, "Failed");
                allFailedIds.Clear();
            }
        }

        while (await reader.WaitToReadAsync(ct))
        {
            while (reader.TryRead(out var item))
            {
                if (item.Skipped)
                {
                    skipIds.Add(item.Staged.Id);
                }
                else if (item.Dto != null)
                {
                    importBatch.Add(item.Dto);
                    stagingIds.Add(item.Staged.Id);
                }
                else
                {
                    failedIds.Add(item.Staged.Id);
                }

                if (importBatch.Count >= _batchSize) await FlushAsync();
            }
        }
        await FlushAsync();
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
