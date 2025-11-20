using ExpressRecipe.RecipeService.Data;
using ExpressRecipe.RecipeService.Parsers;
using ExpressRecipe.Shared.DTOs.Recipe;

namespace ExpressRecipe.RecipeService.Services;

/// <summary>
/// Service for managing recipe imports using various parsers
/// </summary>
public class RecipeImportService
{
    private readonly IRecipeImportRepository _importRepository;
    private readonly IRecipeRepository _recipeRepository;
    private readonly RecipeParserFactory _parserFactory;
    private readonly NutritionExtractionService _nutritionService;
    private readonly AllergenDetectionService _allergenService;
    private readonly ImageDownloadService _imageService;
    private readonly ILogger<RecipeImportService> _logger;

    public RecipeImportService(
        IRecipeImportRepository importRepository,
        IRecipeRepository recipeRepository,
        NutritionExtractionService nutritionService,
        AllergenDetectionService allergenService,
        ImageDownloadService imageService,
        ILogger<RecipeImportService> logger)
    {
        _importRepository = importRepository;
        _recipeRepository = recipeRepository;
        _nutritionService = nutritionService;
        _allergenService = allergenService;
        _imageService = imageService;
        _parserFactory = new RecipeParserFactory();
        _logger = logger;
    }

    /// <summary>
    /// Process an import job by parsing the content and creating recipes
    /// </summary>
    public async Task<ImportJobResult> ProcessImportJobAsync(Guid jobId, Guid userId, string content, ParserContext context)
    {
        var result = new ImportJobResult
        {
            JobId = jobId,
            TotalRecipes = 0,
            SuccessCount = 0,
            FailureCount = 0,
            Errors = new List<string>()
        };

        try
        {
            _logger.LogInformation("Starting import job {JobId} for user {UserId}", jobId, userId);

            // Get the import job to determine which parser to use
            var job = await _importRepository.GetImportJobByIdAsync(jobId);
            if (job == null)
            {
                throw new InvalidOperationException($"Import job {jobId} not found");
            }

            // Get the import source to find the parser
            var source = await _importRepository.GetImportSourceByIdAsync(job.ImportSourceId);
            if (source == null)
            {
                throw new InvalidOperationException($"Import source {job.ImportSourceId} not found");
            }

            // Get the appropriate parser
            var parser = string.IsNullOrEmpty(source.ParserClassName)
                ? _parserFactory.DetectParser(content, context)
                : _parserFactory.GetParserByName(source.ParserClassName);

            if (parser == null)
            {
                throw new InvalidOperationException($"No parser found for source {source.Name}");
            }

            _logger.LogInformation("Using parser {ParserName} for job {JobId}", parser.ParserName, jobId);

            // Parse the content
            List<ParsedRecipe> parsedRecipes;
            try
            {
                parsedRecipes = await parser.ParseAsync(content, context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse recipes for job {JobId}", jobId);
                result.Errors.Add($"Parse error: {ex.Message}");
                return result;
            }

            result.TotalRecipes = parsedRecipes.Count;

            _logger.LogInformation("Parsed {Count} recipes from import job {JobId}", parsedRecipes.Count, jobId);

            // Process each parsed recipe
            foreach (var parsedRecipe in parsedRecipes)
            {
                try
                {
                    // Check for duplicates
                    var duplicate = await _recipeRepository.FindDuplicateRecipeAsync(parsedRecipe.Name, userId);
                    if (duplicate != null)
                    {
                        _logger.LogWarning("Skipping duplicate recipe: {RecipeName}", parsedRecipe.Name);
                        result.FailureCount++;
                        result.Errors.Add($"Recipe '{parsedRecipe.Name}' already exists");
                        continue;
                    }

                    // Download image if URL is provided
                    string? imageUrl = parsedRecipe.ImageUrl;
                    if (!string.IsNullOrWhiteSpace(imageUrl))
                    {
                        try
                        {
                            var downloadedImagePath = await _imageService.DownloadImageAsync(imageUrl, Guid.NewGuid());
                            if (!string.IsNullOrWhiteSpace(downloadedImagePath))
                            {
                                imageUrl = downloadedImagePath;
                            }
                        }
                        catch (Exception imgEx)
                        {
                            _logger.LogWarning(imgEx, "Failed to download image for recipe: {RecipeName}", parsedRecipe.Name);
                            // Continue with original URL
                        }
                    }

                    // Convert parsed recipe to CreateRecipeRequest
                    var createRequest = ConvertToCreateRequest(parsedRecipe, userId, imageUrl);

                    // Create the recipe
                    var recipeId = await _recipeRepository.CreateRecipeAsync(createRequest, userId);

                    _logger.LogInformation("Created recipe {RecipeId}: {RecipeName}", recipeId, parsedRecipe.Name);

                    // Add ingredients
                    if (parsedRecipe.Ingredients.Any())
                    {
                        var ingredients = parsedRecipe.Ingredients.Select(i => new RecipeIngredientDto
                        {
                            IngredientName = i.IngredientName,
                            Quantity = i.Quantity,
                            Unit = i.Unit,
                            OrderIndex = i.Order,
                            PreparationNote = i.Preparation,
                            IsOptional = i.IsOptional,
                            SubstituteNotes = i.SubstituteNotes
                        }).ToList();

                        await _recipeRepository.AddRecipeIngredientsAsync(recipeId, ingredients, userId);
                        _logger.LogDebug("Added {Count} ingredients to recipe {RecipeId}", ingredients.Count, recipeId);
                    }

                    // Add structured instructions if available
                    if (parsedRecipe.Instructions.Any())
                    {
                        var instructionsText = string.Join("\n\n", parsedRecipe.Instructions
                            .OrderBy(i => i.StepNumber)
                            .Select(i => $"{i.StepNumber}. {i.InstructionText}"));

                        await _recipeRepository.UpdateRecipeInstructionsAsync(recipeId, instructionsText);
                    }

                    // Extract and add nutrition data
                    var nutrition = _nutritionService.ExtractNutrition(parsedRecipe);
                    if (nutrition != null)
                    {
                        nutrition.RecipeId = recipeId;
                        await _recipeRepository.AddRecipeNutritionAsync(recipeId, nutrition);
                        _logger.LogDebug("Added nutrition data to recipe {RecipeId}", recipeId);
                    }

                    // Detect and add allergens
                    var allergens = await _allergenService.DetectAllergensAsync(parsedRecipe);
                    if (allergens.Any())
                    {
                        await _recipeRepository.AddRecipeAllergensAsync(recipeId, allergens);
                        _logger.LogInformation("Detected {Count} allergens in recipe {RecipeId}", allergens.Count, recipeId);
                    }

                    // Add tags/categories
                    var tags = new List<string>();
                    if (!string.IsNullOrWhiteSpace(parsedRecipe.Category))
                        tags.Add(parsedRecipe.Category);
                    if (!string.IsNullOrWhiteSpace(parsedRecipe.Cuisine))
                        tags.Add(parsedRecipe.Cuisine);
                    if (!string.IsNullOrWhiteSpace(parsedRecipe.DifficultyLevel))
                        tags.Add(parsedRecipe.DifficultyLevel);

                    if (tags.Any())
                    {
                        await _recipeRepository.AddRecipeTagsAsync(recipeId, tags);
                    }

                    result.SuccessCount++;
                    _logger.LogInformation("Successfully imported recipe {RecipeId}: {RecipeName}", recipeId, parsedRecipe.Name);

                    // Record the import result
                    // await _importRepository.CreateImportResultAsync(jobId, parsedRecipe.Name, recipeId, status: "Success");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to import recipe: {RecipeName}", parsedRecipe.Name);
                    result.FailureCount++;
                    result.Errors.Add($"Recipe '{parsedRecipe.Name}': {ex.Message}");

                    // Record the import failure
                    // await _importRepository.CreateImportResultAsync(jobId, parsedRecipe.Name, null, status: "Failed", error: ex.Message);
                }
            }

            _logger.LogInformation("Completed import job {JobId}: {Success} successful, {Failed} failed",
                jobId, result.SuccessCount, result.FailureCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing import job {JobId}", jobId);
            result.Errors.Add($"Job processing error: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Convert ParsedRecipe to CreateRecipeRequest
    /// </summary>
    private CreateRecipeRequest ConvertToCreateRequest(ParsedRecipe parsed, Guid userId, string? imageUrl = null)
    {
        var request = new CreateRecipeRequest
        {
            Name = parsed.Name,
            Description = parsed.Description,
            Category = parsed.Category,
            Cuisine = parsed.Cuisine,
            DifficultyLevel = parsed.DifficultyLevel,
            PrepTimeMinutes = parsed.PrepTimeMinutes,
            CookTimeMinutes = parsed.CookTimeMinutes,
            Servings = parsed.Servings,
            SourceUrl = parsed.SourceUrl,
            ImageUrl = imageUrl ?? parsed.ImageUrl,
            Notes = parsed.Notes,
            IsPublic = false, // Imported recipes are private by default
            CreatedBy = userId
        };

        return request;
    }

    /// <summary>
    /// Validate if content can be imported
    /// </summary>
    public async Task<ValidationResult> ValidateImportContentAsync(string content, ParserContext context, Guid importSourceId)
    {
        var result = new ValidationResult { IsValid = true, Errors = new List<string>() };

        try
        {
            // Get the import source
            var source = await _importRepository.GetImportSourceByIdAsync(importSourceId);
            if (source == null)
            {
                result.IsValid = false;
                result.Errors.Add("Invalid import source");
                return result;
            }

            // Get parser
            var parser = string.IsNullOrEmpty(source.ParserClassName)
                ? _parserFactory.DetectParser(content, context)
                : _parserFactory.GetParserByName(source.ParserClassName);

            if (parser == null)
            {
                result.IsValid = false;
                result.Errors.Add($"No parser available for {source.Name}");
                return result;
            }

            // Check if parser can handle this content
            if (!parser.CanParse(content, context))
            {
                result.IsValid = false;
                result.Errors.Add($"Content format not compatible with {source.Name} parser");
                return result;
            }

            // Try to parse (dry run)
            var recipes = await parser.ParseAsync(content, context);
            result.RecipeCount = recipes.Count;

            if (recipes.Count == 0)
            {
                result.IsValid = false;
                result.Errors.Add("No recipes found in content");
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Validation error: {ex.Message}");
        }

        return result;
    }
}

/// <summary>
/// Result of import job processing
/// </summary>
public class ImportJobResult
{
    public Guid JobId { get; set; }
    public int TotalRecipes { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Result of import validation
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public int RecipeCount { get; set; }
    public List<string> Errors { get; set; } = new();
}
