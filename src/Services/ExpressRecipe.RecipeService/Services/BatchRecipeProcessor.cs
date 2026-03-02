using System.Threading.Channels;
using System.Text.Json;
using System.Diagnostics;
using ExpressRecipe.RecipeService.Data;
using ExpressRecipe.RecipeService.Parsers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using ExpressRecipe.Shared.DTOs.Recipe;
using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.Client.Shared.Services;

namespace ExpressRecipe.RecipeService.Services;

/// <summary>
/// Result of a batch processing operation
/// </summary>
public class ProcessingResult
{
    public int SuccessCount;
    public int FailureCount;
}

/// <summary>
/// High-performance batch processor for recipes using a Producer-Consumer pipeline.
/// Uses System.Threading.Channels for backpressure and concurrent mapping.
/// </summary>
public class BatchRecipeProcessor : RecipeParserBase
{
    private readonly ILogger<BatchRecipeProcessor> _logger;
    private readonly IConfiguration _configuration;
    private readonly IngredientServiceClient _ingredientClient;
    private readonly string _imageSourcePath;
    private readonly string _imageDestPath;
    private readonly int _maxDegreeOfParallelism;
    private readonly int _batchSize;
    private readonly int _bufferSize;
    
    // STATIC CACHE: Shared across all instances
    private static Dictionary<string, string>? _imageIndex;
    private static Dictionary<string, bool>? _recipeCompleteness; 
    private static readonly object _indexLock = new();
    private static readonly object _titleLock = new();

    public override string ParserName => "BatchRecipeProcessor";
    public override string SourceType => "Batch";

    public BatchRecipeProcessor(
        ILogger<BatchRecipeProcessor> logger,
        IConfiguration configuration,
        IngredientServiceClient ingredientClient,
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
        
        _imageSourcePath = _configuration["RecipeImport:ImageSourcePath"] ?? "c:\\Recipes\\Source";
        _imageDestPath = _configuration["ImageStorage:Path"] ?? "c:\\Recipes";
    }

    public override Task<List<ParsedRecipe>> ParseAsync(string content, ParserContext context)
    {
        throw new NotImplementedException("Use ProcessStagedRecipesAsync instead");
    }

    public override bool CanParse(string content, ParserContext context) => false;

    public async Task<ProcessingResult> ProcessStagedRecipesAsync(
        IRecipeStagingRepository stagingRepo,
        IRecipeRepository recipeRepository,
        CancellationToken cancellationToken = default)
    {
        var result = new ProcessingResult();
        var startTime = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        
        await EnsureImageIndexAsync();
        await EnsureRecipeTitleIndexAsync(recipeRepository);

        // CHANNEL 1: Pending Staged Recipes -> Mappers
        var stagingChannel = Channel.CreateBounded<StagedRecipe>(new BoundedChannelOptions(_bufferSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = true
        });

        // CHANNEL 2: Mapped Recipes -> DB Writer
        var mappedChannel = Channel.CreateBounded<(StagedRecipe Staged, FullRecipeImportDto? Dto, bool Skipped)>(new BoundedChannelOptions(_bufferSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

        _logger.LogInformation("Starting high-performance recipe processing pipeline. Pipeline: {BufSize} buffer, {Batch} batch, {Parallel} workers", 
            _bufferSize, _batchSize, _maxDegreeOfParallelism);

        // 1. PRODUCER: Fetch from DB and write to stagingChannel
        var producerTask = Task.Run(() => FetchStagedRecipesAsync(stagingRepo, stagingChannel.Writer, cancellationToken), cancellationToken);

        // 2. WORKERS: Map StagedRecipe to FullRecipeImportDto
        var mappingTasks = Enumerable.Range(0, _maxDegreeOfParallelism)
            .Select(_ => Task.Run(() => MapRecipesAsync(stagingChannel.Reader, mappedChannel.Writer, cancellationToken), cancellationToken))
            .ToList();

        // 3. CONSUMER: Bulk insert into DB in batches
        var consumerTask = Task.Run(() => SaveRecipesAsync(recipeRepository, stagingRepo, mappedChannel.Reader, result, stopwatch, cancellationToken), cancellationToken);

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
            _logger.LogError(ex, "Recipe processing pipeline failed.");
        }

        stopwatch.Stop();
        var totalTime = stopwatch.Elapsed;
        var totalProcessed = result.SuccessCount + result.FailureCount;
        _logger.LogInformation("Batch Processing Finished: {Success} successful, {Failed} failed. Time: {Elapsed}. Speed: {RPS:F1} rec/sec",
            result.SuccessCount, result.FailureCount, totalTime, totalProcessed / totalTime.TotalSeconds);

        return result;
    }

    private async Task FetchStagedRecipesAsync(IRecipeStagingRepository stagingRepo, ChannelWriter<StagedRecipe> writer, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var batch = await stagingRepo.GetPendingRecipesAsync(_batchSize * 2);
                if (batch.Count == 0) break;

                foreach (var recipe in batch)
                {
                    await writer.WriteAsync(recipe, ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching recipes from staging.");
        }
    }

    private async Task MapRecipesAsync(ChannelReader<StagedRecipe> reader, ChannelWriter<(StagedRecipe, FullRecipeImportDto?, bool)> writer, CancellationToken ct)
    {
        while (await reader.WaitToReadAsync(ct))
        {
            while (reader.TryRead(out var staged))
            {
                try
                {
                    var title = staged.Title.Trim();
                    bool alreadyComplete = false;
                    
                    lock (_titleLock)
                    {
                        if (_recipeCompleteness != null && _recipeCompleteness.TryGetValue(title, out var isComplete))
                        {
                            if (isComplete) alreadyComplete = true;
                        }
                    }

                    if (alreadyComplete)
                    {
                        await writer.WriteAsync((staged, null, true), ct);
                        continue;
                    }

                    var dto = await MapToFullImportDtoAsync(staged);
                    await writer.WriteAsync((staged, dto, false), ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Mapping error for '{Title}': {Msg}", staged.Title, ex.Message);
                    await writer.WriteAsync((staged, null, false), ct);
                }
            }
        }
    }

    private async Task SaveRecipesAsync(IRecipeRepository recipeRepo, IRecipeStagingRepository stagingRepo, ChannelReader<(StagedRecipe Staged, FullRecipeImportDto? Dto, bool Skipped)> reader, ProcessingResult result, Stopwatch stopwatch, CancellationToken ct)
    {
        var importBatch = new List<FullRecipeImportDto>();
        var stagingIds = new List<Guid>();
        var skipIds = new List<Guid>();
        var failedIds = new List<Guid>();
        int totalProcessedInSession = 0;
        int lastLogTotal = 0;

        async Task FlushAsync()
        {
            totalProcessedInSession += skipIds.Count + importBatch.Count + failedIds.Count;

            if (skipIds.Any())
            {
                await stagingRepo.BulkUpdateStatusAsync(skipIds, "Completed", "Skipped: Already exists and complete");
                Interlocked.Add(ref result.SuccessCount, skipIds.Count);
                skipIds.Clear();
            }

            if (importBatch.Any())
            {
                try
                {
                    // CENTRALIZED INGREDIENT LOOKUP/CREATE
                    var allIngredientNames = importBatch
                        .SelectMany(i => i.Ingredients)
                        .Select(ing => ing.IngredientName)
                        .Where(name => !string.IsNullOrEmpty(name))
                        .Cast<string>()
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (allIngredientNames.Any())
                    {
                        await _ingredientClient.BulkCreateIngredientsAsync(allIngredientNames);
                        var nameToId = await _ingredientClient.LookupIngredientIdsAsync(allIngredientNames);

                        foreach(var item in importBatch)
                        {
                            foreach(var ing in item.Ingredients)
                            {
                                if (ing.IngredientName != null && nameToId.TryGetValue(ing.IngredientName, out var id))
                                {
                                    ing.IngredientId = id;
                                }
                            }
                        }
                    }

                    int createdCount = await recipeRepo.BulkCreateFullRecipesHighSpeedAsync(importBatch);
                    
                    var successfulStagingIds = stagingIds.Take(createdCount).ToList();
                    await stagingRepo.BulkUpdateStatusAsync(successfulStagingIds, "Completed");
                    
                    var failedStagingIds = stagingIds.Skip(createdCount).ToList();
                    if (failedStagingIds.Any())
                    {
                        await stagingRepo.BulkUpdateStatusAsync(failedStagingIds, "Failed", "Bulk insert did not include this record");
                        Interlocked.Add(ref result.FailureCount, failedStagingIds.Count);
                    }

                    lock(_titleLock)
                    {
                        if (_recipeCompleteness != null)
                        {
                            foreach(var item in importBatch.Take(createdCount))
                            {
                                if (item.Recipe.Name != null) _recipeCompleteness[item.Recipe.Name.Trim()] = true;
                            }
                        }
                    }

                    Interlocked.Add(ref result.SuccessCount, createdCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Writer: Bulk insert failed for batch of {Count}", importBatch.Count);
                    await stagingRepo.BulkUpdateStatusAsync(stagingIds, "Failed", ex.Message);
                    Interlocked.Add(ref result.FailureCount, stagingIds.Count);
                }
                
                importBatch.Clear();
                stagingIds.Clear();
            }

            if (failedIds.Any())
            {
                await stagingRepo.BulkUpdateStatusAsync(failedIds, "Failed", "Mapping failed");
                Interlocked.Add(ref result.FailureCount, failedIds.Count);
                failedIds.Clear();
            }

            if (totalProcessedInSession >= lastLogTotal + 1000)
            {
                var elapsed = stopwatch.Elapsed.TotalSeconds;
                var rps = elapsed > 0 ? totalProcessedInSession / elapsed : 0;
                var channelLag = reader.Count; 
                
                _logger.LogInformation("Writer: Processed {Total} | Speed: {RPS:F1} rec/sec | Lag: {Lag} records", 
                    totalProcessedInSession, rps, channelLag);
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
    }

    private async Task<FullRecipeImportDto> MapToFullImportDtoAsync(StagedRecipe staged)
    {
        var dto = new FullRecipeImportDto
        {
            Recipe = new CreateRecipeRequest
            {
                Name = Truncate(staged.Title, 450) ?? "Untitled Recipe",
                Description = staged.Description,
                CookTimeMinutes = staged.CookingTimeMinutes,
                Servings = staged.Servings,
                SourceUrl = staged.SourceUrl,
                Notes = "Batch Import",
                IsPublic = true,
                CreatedBy = Guid.Empty
            },
            Ingredients = new List<RecipeIngredientDto>(),
            Images = new List<RecipeImageDto>(),
            Steps = new List<CreateRecipeStepRequest>(),
            Tags = new List<string>()
        };

        if (!string.IsNullOrWhiteSpace(staged.ImageName))
        {
            var names = staged.ImageName.Split(',', StringSplitOptions.RemoveEmptyEntries);
            int idx = 0;
            foreach (var name in names)
            {
                var localPath = await FindAndCopyImageAsync(name.Trim());
                if (localPath != null)
                {
                    dto.Images.Add(new RecipeImageDto
                    {
                        ImageUrl = Truncate(name.Trim(), 2000) ?? string.Empty,
                        LocalPath = localPath,
                        IsPrimary = idx == 0,
                        DisplayOrder = idx++
                    });
                    if (idx == 1) dto.Recipe.ImageUrl = localPath;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(staged.IngredientsRaw))
        {
            try 
            {
                var rawIngredients = JsonSerializer.Deserialize<List<string>>(staged.IngredientsRaw);
                if (rawIngredients != null)
                {
                    int ingIdx = 0;
                    foreach (var raw in rawIngredients)
                    {
                        var (qty, unit, name) = ParseQuantityAndUnit(raw);
                        var (ingredName, prep) = ExtractPreparation(name);
                        dto.Ingredients.Add(new RecipeIngredientDto
                        {
                            IngredientName = ingredName,
                            Quantity = qty,
                            Unit = Truncate(unit, 50),
                            OrderIndex = ingIdx++,
                            PreparationNote = prep,
                            IsOptional = IsOptionalIngredient(raw),
                            OriginalText = raw
                        });
                    }
                }
            } catch {}
        }

        if (!string.IsNullOrWhiteSpace(staged.DirectionsRaw))
        {
            try
            {
                var directions = JsonSerializer.Deserialize<List<string>>(staged.DirectionsRaw);
                if (directions != null)
                {
                    int stepIdx = 0;
                    foreach (var step in directions)
                    {
                        dto.Steps.Add(new CreateRecipeStepRequest { OrderIndex = stepIdx++, Instruction = step });
                    }
                }
            } catch {}
        }

        if (!string.IsNullOrWhiteSpace(staged.TagsRaw))
        {
            try {
                var tagsObj = JsonSerializer.Deserialize<JsonElement>(staged.TagsRaw);
                if (tagsObj.ValueKind == JsonValueKind.Object) {
                    foreach (var prop in tagsObj.EnumerateObject()) {
                        if (prop.Value.ValueKind == JsonValueKind.Array) {
                            foreach (var item in prop.Value.EnumerateArray()) {
                                var v = item.GetString(); 
                                if (v != null) dto.Tags.Add(Truncate(v, 450) ?? string.Empty);
                            }
                        }
                    }
                }
            } catch {}
        }

        return dto;
    }

    private string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }

    private async Task EnsureRecipeTitleIndexAsync(IRecipeRepository repository)
    {
        if (_recipeCompleteness != null) return;
        var completenessMap = await repository.GetAllRecipeTitlesCompletenessAsync();
        lock (_titleLock)
        {
            if (_recipeCompleteness == null) _recipeCompleteness = completenessMap;
        }
    }

    private async Task EnsureImageIndexAsync()
    {
        if (_imageIndex != null) return;
        await Task.Run(() =>
        {
            lock (_indexLock)
            {
                if (_imageIndex != null) return;
                var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (Directory.Exists(_imageSourcePath))
                {
                    try
                    {
                        var files = Directory.EnumerateFiles(_imageSourcePath, "*.*", SearchOption.AllDirectories);
                        foreach (var file in files)
                        {
                            var fileName = Path.GetFileName(file);
                            if (!index.ContainsKey(fileName)) index[fileName] = file;
                            if (index.Count > 500000) break;
                        }
                    } catch {}
                }
                _imageIndex = index;
            }
        });
    }

    private async Task<string?> FindAndCopyImageAsync(string imageName)
    {
        string? sourcePath = null;
        if (_imageIndex == null || !_imageIndex.TryGetValue(imageName, out sourcePath))
        {
            var extensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            foreach (var ext in extensions)
            {
                if (_imageIndex != null && _imageIndex.TryGetValue(imageName + ext, out sourcePath)) break;
            }
            if (sourcePath == null) return null;
        }

        try
        {
            var prefix = Guid.NewGuid().ToString().Substring(0, 8);
            var extension = Path.GetExtension(sourcePath!);
            var safeName = Path.GetFileNameWithoutExtension(imageName);
            if (safeName.Length > 50) safeName = safeName.Substring(0, 50);
            var destFileName = $"{prefix}_{safeName}{extension}";
            var destPath = Path.Combine(_imageDestPath, destFileName);
            using (var sourceStream = File.OpenRead(sourcePath!))
            using (var destStream = File.Create(destPath))
            {
                await sourceStream.CopyToAsync(destStream);
            }
            return $"/images/recipes/{destFileName}";
        } catch { return null; }
    }
}
