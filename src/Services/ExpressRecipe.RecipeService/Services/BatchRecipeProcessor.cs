using System.Threading.Channels;
using System.Text.Json;
using System.Diagnostics;
using ExpressRecipe.RecipeService.Data;
using ExpressRecipe.RecipeService.Parsers;
using ExpressRecipe.RecipeService.Logging;
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
    private readonly IIngredientServiceClient _ingredientClient;
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

        // 1. PRODUCER: Fetch from DB and write to stagingChannel
        var producerTask = Task.Run(() => FetchStagedRecipesAsync(stagingRepo, stagingChannel.Writer, cancellationToken), cancellationToken);

        // 2. WORKERS: Map StagedRecipe to FullRecipeImportDto (Uses Bulk Parsing)
        var mappingTasks = Enumerable.Range(0, _maxDegreeOfParallelism)
            .Select(_ => Task.Run(() => MapRecipesParallelAsync(stagingChannel.Reader, mappedChannel.Writer, cancellationToken), cancellationToken))
            .ToList();

        // 3. CONSUMER: Bulk insert into DB in batches
        var consumerTask = Task.Run(() => SaveRecipesAsync(recipeRepository, stagingRepo, mappedChannel.Reader, result, stopwatch, cancellationToken), cancellationToken);

        await producerTask;
        stagingChannel.Writer.Complete();

        await Task.WhenAll(mappingTasks);
        mappedChannel.Writer.Complete();

        await consumerTask;

        stopwatch.Stop();
        _logger.LogInformation("Processing complete. Success: {Success}, Failed: {Failed}, Total: {Total}. Rate: {Rate:F2} recipes/sec", 
            result.SuccessCount, result.FailureCount, result.SuccessCount + result.FailureCount, (result.SuccessCount + result.FailureCount) / stopwatch.Elapsed.TotalSeconds);

        return result;
    }

    private async Task FetchStagedRecipesAsync(IRecipeStagingRepository stagingRepo, ChannelWriter<StagedRecipe> writer, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var batch = await stagingRepo.GetPendingRecipesAsync(_batchSize);
                if (!batch.Any()) break;

                foreach (var recipe in batch)
                {
                    if (!await writer.WaitToWriteAsync(ct)) break;
                    await writer.WriteAsync(recipe, ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching recipes from staging.");
        }
    }

    private async Task MapRecipesParallelAsync(ChannelReader<StagedRecipe> reader, ChannelWriter<(StagedRecipe, FullRecipeImportDto?, bool)> writer, CancellationToken ct)
    {
        while (await reader.WaitToReadAsync(ct))
        {
            var batch = new List<StagedRecipe>();
            while (batch.Count < 50 && reader.TryRead(out var staged))
            {
                batch.Add(staged);
            }

            if (!batch.Any()) continue;

            try
            {
                // 1. Collect all unique ingredient strings across this small batch
                var allRawLines = new HashSet<string>();
                foreach(var staged in batch)
                {
                    if (!string.IsNullOrWhiteSpace(staged.IngredientsRaw))
                    {
                        var lines = JsonSerializer.Deserialize<List<string>>(staged.IngredientsRaw);
                        if (lines != null) foreach(var l in lines) allRawLines.Add(l);
                    }
                }

                // 2. Bulk parse them via microservice
                Dictionary<string, ParsedIngredientResult> parsedResults = new();
                if (allRawLines.Any())
                {
                    parsedResults = await _ingredientClient.BulkParseIngredientStringsAsync(allRawLines.ToList());
                }

                // 3. Map each recipe using pre-parsed results
                foreach(var staged in batch)
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

                    var dto = await MapToFullImportDtoAsync(staged, parsedResults);
                    await writer.WriteAsync((staged, dto, false), ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Parallel mapping error: {Msg}", ex.Message);
                foreach(var s in batch) await writer.WriteAsync((s, null, false), ct);
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
                        .SelectMany(i => i.Ingredients ?? new List<RecipeIngredientDto>())
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
                            if (item.Ingredients != null)
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
                    }

                    int createdCount = await recipeRepo.BulkCreateFullRecipesHighSpeedAsync(importBatch);
                    
                    var successfulStagingIds = stagingIds.Take(createdCount).ToList();
                    await stagingRepo.BulkUpdateStatusAsync(successfulStagingIds, "Completed");

                    var failedStagingIds = stagingIds.Skip(createdCount).ToList();
                    if (failedStagingIds.Any())
                    {
                        await stagingRepo.BulkUpdateStatusAsync(failedStagingIds, "Failed", "Bulk Insert Failure Part 2");
                        Interlocked.Add(ref result.FailureCount, failedStagingIds.Count);
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

            if (totalProcessedInSession - lastLogTotal >= 100)
            {
                var recordsPerSec = totalProcessedInSession / stopwatch.Elapsed.TotalSeconds;
                var lagCount = stagingIds.Count + importBatch.Count;
                _logger.LogProcessingProgress(totalProcessedInSession, recordsPerSec, lagCount);
                lastLogTotal = totalProcessedInSession;
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

    private async Task<FullRecipeImportDto> MapToFullImportDtoAsync(StagedRecipe staged, Dictionary<string, ParsedIngredientResult> preParsedIngredients)
    {
        var dto = new FullRecipeImportDto
        {
            Recipe = new CreateRecipeRequest
            {
                Name = staged.Title,
                Description = staged.Description,
                Category = staged.Source, // Using Source as Category if available
                Cuisine = null,
                Difficulty = null,
                PrepTimeMinutes = null,
                CookTimeMinutes = staged.CookingTimeMinutes,
                TotalTimeMinutes = staged.CookingTimeMinutes,
                Servings = staged.Servings,
                Instructions = staged.DirectionsRaw, // Directions as Instructions
                Notes = null,
                IsPublic = true,
                SourceUrl = staged.SourceUrl
            },
            Ingredients = new List<RecipeIngredientDto>(),
            Images = new List<RecipeImageDto>(),
            Steps = new List<CreateRecipeStepRequest>(),
            Tags = new List<string>()
        };

        // IMAGE MAPPING
        if (_imageIndex != null && !string.IsNullOrEmpty(staged.ImageName))
        {
            if (_imageIndex.TryGetValue(staged.ImageName, out var fullSourcePath))
            {
                var extension = Path.GetExtension(fullSourcePath);
                var localFilename = $"{Guid.NewGuid()}{extension}";
                var localPath = Path.Combine(_imageDestPath, localFilename);

                if (!Directory.Exists(_imageDestPath)) Directory.CreateDirectory(_imageDestPath);
                
                try {
                    File.Copy(fullSourcePath, localPath, true);
                    dto.Images.Add(new RecipeImageDto {
                        ImageUrl = localFilename,
                        ImageType = "Front",
                        IsPrimary = true,
                        DisplayOrder = 0
                    });
                    dto.Recipe.ImageUrl = localFilename;
                } catch {}
            }
        }

        // INGREDIENT MAPPING - Use pre-parsed results from microservice
        if (!string.IsNullOrWhiteSpace(staged.IngredientsRaw))
        {
            try 
            {
                var rawLines = JsonSerializer.Deserialize<List<string>>(staged.IngredientsRaw);
                if (rawLines != null)
                {
                    int ingIdx = 0;
                    foreach (var raw in rawLines)
                    {
                        if (preParsedIngredients.TryGetValue(raw, out var parsedResult) && parsedResult.Components.Any())
                        {
                            // Map components to recipe ingredients
                            foreach (var comp in parsedResult.Components)
                            {
                                dto.Ingredients.Add(new RecipeIngredientDto
                                {
                                    IngredientName = comp.CleanName ?? comp.Name,
                                    Quantity = comp.Quantity,
                                    Unit = Truncate(comp.Unit, 50),
                                    OrderIndex = ingIdx++,
                                    IsOptional = IsOptionalIngredient(raw),
                                    OriginalText = raw,
                                    IngredientId = comp.BaseIngredientId // Might be matched if microservice knows it
                                });
                            }
                        }
                        else
                        {
                            // Fallback if not in pre-parsed (shouldn't happen with our logic)
                            dto.Ingredients.Add(new RecipeIngredientDto { 
                                IngredientName = Truncate(raw, 200) ?? "Unknown", 
                                OrderIndex = ingIdx++, 
                                OriginalText = raw 
                            });
                        }
                    }
                }
            } catch {}
        }

        // DIRECTIONS AND TAGS
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
                        foreach(var f in files) {
                            var name = Path.GetFileName(f);
                            if (!index.ContainsKey(name)) index[name] = f;
                        }
                    } catch {}
                }
                _imageIndex = index;
            }
        });
    }
}
