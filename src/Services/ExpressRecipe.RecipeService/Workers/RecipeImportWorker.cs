using System.Text.Json;
using System.Threading.Channels;
using System.Diagnostics;
using ExpressRecipe.RecipeService.Data;
using ExpressRecipe.RecipeService.Services;
using ExpressRecipe.RecipeService.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ExpressRecipe.RecipeService.Workers;

/// <summary>
/// High-performance background service that imports recipes using a Producer-Consumer pipeline.
/// Uses System.Threading.Channels for backpressure and parallel processing.
/// </summary>
public class RecipeImportWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RecipeImportWorker> _logger;
    private readonly IConfiguration _configuration;
    
    // In-memory cache of existing ExternalIds to avoid DB roundtrips during import
    private HashSet<string>? _existingIds;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    public RecipeImportWorker(
        IServiceProvider serviceProvider,
        ILogger<RecipeImportWorker> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RecipeImportWorker starting...");

        // Check if auto-import is enabled (allows disabling at config level)
        var autoImport = _configuration.GetValue<bool>("RecipeImport:AutoImport", true);
        if (!autoImport)
        {
            _logger.LogInformation("RecipeImportWorker: auto-import is disabled in configuration. Worker will not run.");
            return;
        }

        var importIntervalHours = _configuration.GetValue<int>("RecipeImport:ImportIntervalHours", 24);
        var importInterval = TimeSpan.FromHours(importIntervalHours);
        DateTime lastImportTime = DateTime.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var filePath = _configuration["RecipeImport:FilePath"];
                if (DateTime.UtcNow - lastImportTime >= importInterval)
                {
                    if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
                    {
                        var fileInfo = new FileInfo(filePath);
                        _logger.LogInformation("Import trigger: File {Path} found ({Size:F2} MB). Starting pipeline.", 
                            filePath, fileInfo.Length / 1024.0 / 1024.0);
                        
                        await RunImportPipelineAsync(filePath, stoppingToken);
                        lastImportTime = DateTime.UtcNow;
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(filePath))
                            _logger.LogWarning("Import file not found: {FilePath}", filePath);
                        lastImportTime = DateTime.UtcNow;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error in recipe import cycle.");
            }

            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        }
    }

    private async Task RunImportPipelineAsync(string filePath, CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var stagingRepo = scope.ServiceProvider.GetRequiredService<IRecipeStagingRepository>();

        // 0. Ensure ID Cache is loaded
        await EnsureIdCacheLoadedAsync(stagingRepo);

        var bufferSize = _configuration.GetValue<int>("RecipeImport:BufferSize", 10000);
        var channel = Channel.CreateBounded<RecipeJsonDto>(new BoundedChannelOptions(bufferSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = true
        });

        var stopwatch = Stopwatch.StartNew();
        var metrics = new SharedMetrics();

        // 1. Start the Producer (File Reader)
        var producerTask = Task.Run(() => ProduceRecipesAsync(filePath, channel.Writer, metrics, stoppingToken), stoppingToken);

        // 2. Start multiple Consumers (DB Processors)
        var consumerCount = _configuration.GetValue<int>("RecipeImport:ConsumerCount", 4); 
        _logger.LogInformation("Starting {Count} consumers for import pipeline using in-memory existence checks...", consumerCount);
        
        var consumerTasks = Enumerable.Range(0, consumerCount)
            .Select(i => Task.Run(() => ConsumeRecipesAsync(i, channel.Reader, metrics, stopwatch, stoppingToken), stoppingToken))
            .ToList();

        try
        {
            await producerTask;
            await Task.WhenAll(consumerTasks);

            stopwatch.Stop();
            var totalProcessed = metrics.TotalImported + metrics.TotalSkipped;
            var totalMinutes = stopwatch.Elapsed.TotalMinutes;
            _logger.LogImportCompleted(totalProcessed, totalMinutes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import pipeline failed or was aborted.");
        }
    }

    private async Task EnsureIdCacheLoadedAsync(IRecipeStagingRepository stagingRepo)
    {
        if (_existingIds != null) return;

        await _cacheLock.WaitAsync();
        try
        {
            if (_existingIds != null) return;

            _logger.LogInformation("Building in-memory ExternalId cache from staging table...");
            var sw = Stopwatch.StartNew();
            _existingIds = await stagingRepo.GetAllExternalIdsAsync();
            _logger.LogInformation("ID cache built with {Count} records in {Time}ms", _existingIds.Count, sw.ElapsedMilliseconds);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private async Task ProduceRecipesAsync(string filePath, ChannelWriter<RecipeJsonDto> writer, SharedMetrics metrics, CancellationToken stoppingToken)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, AllowTrailingCommas = true };
            
            var elements = JsonSerializer.DeserializeAsyncEnumerable<JsonElement>(stream, options, stoppingToken);

            await foreach (var element in elements.WithCancellation(stoppingToken))
            {
                var cloned = element.Clone();
                var recipe = cloned.Deserialize<RecipeJsonDto>(options);

                if (recipe != null && !string.IsNullOrWhiteSpace(recipe.Title))
                {
                    recipe.RawJsonText = cloned.GetRawText();
                    
                    // Safe cloning for JsonElement properties
                    if (recipe.Id.ValueKind != JsonValueKind.Undefined) recipe.Id = recipe.Id.Clone();
                    if (recipe.Ingredients.ValueKind != JsonValueKind.Undefined) recipe.Ingredients = recipe.Ingredients.Clone();
                    if (recipe.IngredientsCsv.ValueKind != JsonValueKind.Undefined) recipe.IngredientsCsv = recipe.IngredientsCsv.Clone();
                    if (recipe.Directions.ValueKind != JsonValueKind.Undefined) recipe.Directions = recipe.Directions.Clone();
                    if (recipe.DirectionsCsv.ValueKind != JsonValueKind.Undefined) recipe.DirectionsCsv = recipe.DirectionsCsv.Clone();
                    if (recipe.Ner.ValueKind != JsonValueKind.Undefined) recipe.Ner = recipe.Ner.Clone();
                    if (recipe.Source.ValueKind != JsonValueKind.Undefined) recipe.Source = recipe.Source.Clone();
                    if (recipe.CookingTime.ValueKind != JsonValueKind.Undefined) recipe.CookingTime = recipe.CookingTime.Clone();
                    if (recipe.Servings.ValueKind != JsonValueKind.Undefined) recipe.Servings = recipe.Servings.Clone();
                    if (recipe.Ratings.ValueKind != JsonValueKind.Undefined) recipe.Ratings = recipe.Ratings.Clone();
                    if (recipe.Tags.ValueKind != JsonValueKind.Undefined) recipe.Tags = recipe.Tags.Clone();

                    await writer.WriteAsync(recipe, stoppingToken);

                    var current = Interlocked.Increment(ref metrics.TotalRead);
                    if (current % 25000 == 0)
                    {
                        _logger.LogRecipesRead(current);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error reading recipe file.");
        }
        finally
        {
            writer.Complete();
            _logger.LogInformation("Producer finished. Total records read: {Count}", metrics.TotalRead);
        }
    }

    private async Task ConsumeRecipesAsync(int consumerId, ChannelReader<RecipeJsonDto> reader, SharedMetrics metrics, Stopwatch stopwatch, CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var stagingRepo = scope.ServiceProvider.GetRequiredService<IRecipeStagingRepository>();
        var chunkSize = _configuration.GetValue<int>("RecipeImport:ImportChunkSize", 5000); 
        
        var batch = new List<RecipeJsonDto>();
        int consumerImported = 0;
        int consumerSkipped = 0;
        int lastLogTotal = 0;

        try
        {
            while (await reader.WaitToReadAsync(stoppingToken))
            {
                while (batch.Count < chunkSize && reader.TryRead(out var recipe))
                {
                    batch.Add(recipe);
                }

                if (batch.Any())
                {
                    var result = await ProcessImportBatchAsync(stagingRepo, batch, stoppingToken);
                    
                    Interlocked.Add(ref metrics.TotalImported, result.Imported);
                    Interlocked.Add(ref metrics.TotalSkipped, result.Skipped);
                    
                    consumerImported += result.Imported;
                    consumerSkipped += result.Skipped;
                    batch.Clear();

                    // Inter-item delay to prevent overwhelming CPU/disk (configured via RecipeImport:BatchDelayMs)
                    var batchDelayMs = _configuration.GetValue<int>("RecipeImport:BatchDelayMs", 0);
                    if (batchDelayMs > 0)
                        await Task.Delay(batchDelayMs, stoppingToken);

                    int currentTotal = consumerImported + consumerSkipped;
                    if (currentTotal >= lastLogTotal + 10000)
                    {
                        var elapsed = stopwatch.Elapsed.TotalSeconds;
                        var globalProcessed = metrics.TotalImported + metrics.TotalSkipped;
                        var rps = elapsed > 0 ? globalProcessed / elapsed : 0;
                        var lag = metrics.TotalRead - globalProcessed;

                        _logger.LogInformation("Consumer {Id}: Processed {Total} | Speed: {RPS:F1} rec/sec | Lag: {Lag} records", 
                            consumerId, currentTotal, rps, lag);
                        lastLogTotal = currentTotal;
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            _logger.LogInformation("Consumer {Id} COMPLETED. Staged: {I}, Skipped: {S}", 
                consumerId, consumerImported, consumerSkipped);
        }
    }

    private async Task<(int Imported, int Skipped)> ProcessImportBatchAsync(
        IRecipeStagingRepository stagingRepo, 
        List<RecipeJsonDto> rawBatch, 
        CancellationToken stoppingToken)
    {
        var toInsert = new List<StagedRecipe>();
        var skipped = 0;

        foreach (var recipe in rawBatch)
        {
            var externalId = GetExternalId(recipe);
            
            if (!string.IsNullOrEmpty(externalId))
            {
                bool exists = false;
                lock(_existingIds!)
                {
                    exists = _existingIds!.Contains(externalId);
                }

                if (exists)
                {
                    skipped++;
                    continue;
                }
            }

            try
            {
                var (rating, ratingCount) = ParseRatings(recipe.Ratings);

                toInsert.Add(new StagedRecipe
                {
                    ExternalId = externalId,
                    Title = recipe.Title ?? "Untitled",
                    Description = recipe.Description,
                    IngredientsRaw = GetRawJson(recipe.Ingredients) ?? GetRawJson(recipe.IngredientsCsv),
                    DirectionsRaw = GetRawJson(recipe.Directions) ?? GetRawJson(recipe.DirectionsCsv),
                    NerIngredientsRaw = GetRawJson(recipe.Ner),
                    Source = GetRawJson(recipe.Source),
                    SourceUrl = recipe.Link,
                    CookingTimeMinutes = ParseInt(recipe.CookingTime),
                    Servings = ParseInt(recipe.Servings),
                    Rating = rating,
                    RatingCount = ratingCount,
                    TagsRaw = GetRawJson(recipe.Tags),
                    PublishDate = recipe.PublishDate,
                    ImageName = recipe.Image,
                    RawJson = recipe.RawJsonText
                });
                
                if (!string.IsNullOrEmpty(externalId))
                {
                    lock(_existingIds!) { _existingIds!.Add(externalId); }
                }
            }
            catch { skipped++; }
        }

        int count = toInsert.Any() ? await stagingRepo.BulkInsertStagingRecipesAsync(toInsert) : 0;
        return (count, skipped);
    }

    private string? GetExternalId(RecipeJsonDto recipe)
    {
        if (recipe.Id.ValueKind == JsonValueKind.Undefined || recipe.Id.ValueKind == JsonValueKind.Null)
            return null;

        return recipe.Id.ValueKind == JsonValueKind.String 
            ? recipe.Id.GetString() 
            : recipe.Id.GetRawText();
    }

    private string? GetRawJson(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Undefined || element.ValueKind == JsonValueKind.Null)
            return null;

        return element.GetRawText();
    }

    private int? ParseInt(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number) return element.GetInt32();
        if (element.ValueKind == JsonValueKind.String)
        {
            if (int.TryParse(element.GetString(), out var val)) return val;
        }
        return null;
    }

    private (decimal? Rating, int? Count) ParseRatings(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            decimal? rating = null;
            int? count = null;

            if (element.TryGetProperty("rating", out var r) && r.ValueKind == JsonValueKind.Number)
                rating = r.GetDecimal();
            
            if (element.TryGetProperty("count", out var c) && c.ValueKind == JsonValueKind.Number)
                count = c.GetInt32();

            return (rating, count);
        }
        
        if (element.ValueKind == JsonValueKind.Number)
        {
            return (element.GetDecimal(), null);
        }

        return (null, null);
    }

    private class SharedMetrics
    {
        public int TotalRead;
        public int TotalImported;
        public int TotalSkipped;
    }
}
