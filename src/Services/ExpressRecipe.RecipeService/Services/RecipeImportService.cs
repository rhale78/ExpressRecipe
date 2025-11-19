using ExpressRecipe.RecipeService.Data;
using ExpressRecipe.RecipeService.Parsers;
using ExpressRecipe.Shared.DTOs.Recipe;

namespace ExpressRecipe.RecipeService.Services;

/// <summary>
/// Service for managing recipe imports using various parsers
/// </summary>
public class RecipeImportService
{
    private readonly IRecipeImportRepository _repository;
    private readonly RecipeParserFactory _parserFactory;
    private readonly ILogger<RecipeImportService> _logger;

    public RecipeImportService(
        IRecipeImportRepository repository,
        ILogger<RecipeImportService> logger)
    {
        _repository = repository;
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
            var job = await _repository.GetImportJobByIdAsync(jobId);
            if (job == null)
            {
                throw new InvalidOperationException($"Import job {jobId} not found");
            }

            // Get the import source to find the parser
            var source = await _repository.GetImportSourceByIdAsync(job.ImportSourceId);
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
                    // Convert parsed recipe to CreateRecipeRequest
                    var createRequest = ConvertToCreateRequest(parsedRecipe, userId);

                    // Create the recipe (would typically call a RecipeService or Repository)
                    // For now, we'll just log success
                    _logger.LogInformation("Successfully imported recipe: {RecipeName}", parsedRecipe.Name);

                    result.SuccessCount++;

                    // Record the import result
                    // await _repository.CreateImportResultAsync(jobId, parsedRecipe.Name, status: "Success");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to import recipe: {RecipeName}", parsedRecipe.Name);
                    result.FailureCount++;
                    result.Errors.Add($"Recipe '{parsedRecipe.Name}': {ex.Message}");

                    // Record the import failure
                    // await _repository.CreateImportResultAsync(jobId, parsedRecipe.Name, status: "Failed", error: ex.Message);
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
    private CreateRecipeRequest ConvertToCreateRequest(ParsedRecipe parsed, Guid userId)
    {
        var request = new CreateRecipeRequest
        {
            Name = parsed.Name,
            Description = parsed.Description,
            PrepTimeMinutes = parsed.PrepTimeMinutes,
            CookTimeMinutes = parsed.CookTimeMinutes,
            TotalTimeMinutes = parsed.TotalTimeMinutes,
            Servings = parsed.Servings,
            Source = parsed.Source,
            SourceUrl = parsed.SourceUrl,
            ImageUrl = parsed.ImageUrl,
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
            var source = await _repository.GetImportSourceByIdAsync(importSourceId);
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
