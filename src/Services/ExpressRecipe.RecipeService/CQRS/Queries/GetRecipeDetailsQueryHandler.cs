using ExpressRecipe.RecipeService.Data;
using ExpressRecipe.Shared.CQRS;
using ExpressRecipe.Shared.Services;

namespace ExpressRecipe.RecipeService.CQRS.Queries;

/// <summary>
/// Handler for getting recipe details with caching
/// </summary>
public class GetRecipeDetailsQueryHandler : IQueryHandler<GetRecipeDetailsQuery, RecipeDetailsDto?>
{
    private readonly IRecipeRepository _repository;
    private readonly CacheService _cacheService;
    private readonly ILogger<GetRecipeDetailsQueryHandler> _logger;

    public GetRecipeDetailsQueryHandler(
        IRecipeRepository repository,
        CacheService cacheService,
        ILogger<GetRecipeDetailsQueryHandler> logger)
    {
        _repository = repository;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<RecipeDetailsDto?> HandleAsync(GetRecipeDetailsQuery query, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"recipe:details:{query.RecipeId}";

        // Try to get from cache
        var cached = await _cacheService.GetAsync<RecipeDetailsDto>(cacheKey);
        if (cached != null)
        {
            _logger.LogDebug("Recipe {RecipeId} found in cache", query.RecipeId);

            // Track view asynchronously (fire and forget)
            if (query.UserId.HasValue)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _repository.IncrementViewCountAsync(query.RecipeId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to track view for recipe {RecipeId}", query.RecipeId);
                    }
                }, cancellationToken);
            }

            return cached;
        }

        // Get from database
        var recipe = await _repository.GetRecipeDetailsAsync(query.RecipeId);
        if (recipe == null)
        {
            _logger.LogWarning("Recipe {RecipeId} not found", query.RecipeId);
            return null;
        }

        // Get related data
        var categories = await _repository.GetRecipeCategoriesAsync(query.RecipeId);
        var tags = await _repository.GetRecipeTagsAsync(query.RecipeId);
        var ingredients = await _repository.GetIngredientsAsync(query.RecipeId);
        var instructions = await _repository.GetInstructionsAsync(query.RecipeId);
        var nutrition = await _repository.GetNutritionAsync(query.RecipeId);
        var rating = await _repository.GetAverageRatingAsync(query.RecipeId);

        // Build complete DTO
        var dto = new RecipeDetailsDto
        {
            Id = recipe.Id,
            UserId = recipe.UserId,
            Name = recipe.Name,
            Description = recipe.Description,
            ImageUrl = recipe.ImageUrl,
            PrepTimeMinutes = recipe.PrepTimeMinutes,
            CookTimeMinutes = recipe.CookTimeMinutes,
            TotalTimeMinutes = recipe.TotalTimeMinutes,
            Servings = recipe.Servings,
            Difficulty = recipe.Difficulty,
            IsPublic = recipe.IsPublic,
            CreatedAt = recipe.CreatedAt,
            UpdatedAt = recipe.UpdatedAt,
            AverageRating = rating.AverageRating,
            RatingCount = rating.RatingCount,
            ViewCount = recipe.ViewCount,
            SaveCount = recipe.SaveCount,
            Categories = categories,
            Tags = tags,
            Ingredients = ingredients.Select(i => new RecipeIngredientDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                Name = i.Name,
                Quantity = i.Quantity,
                Unit = i.Unit,
                Notes = i.Notes,
                IsOptional = i.IsOptional,
                SortOrder = i.SortOrder
            }).ToList(),
            Instructions = instructions.Select(i => new RecipeInstructionDto
            {
                Id = i.Id,
                StepNumber = i.StepNumber,
                Instruction = i.Instruction,
                TimeMinutes = i.TimeMinutes
            }).ToList(),
            Nutrition = nutrition != null ? new RecipeNutritionDto
            {
                Calories = nutrition.Calories,
                Protein = nutrition.Protein,
                Carbs = nutrition.Carbs,
                Fat = nutrition.Fat,
                Fiber = nutrition.Fiber,
                Sugar = nutrition.Sugar,
                Sodium = nutrition.Sodium
            } : null
        };

        // Cache for 10 minutes
        await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(10));

        // Track view
        if (query.UserId.HasValue)
        {
            await _repository.IncrementViewCountAsync(query.RecipeId);
        }

        _logger.LogInformation("Retrieved details for recipe {RecipeId}", query.RecipeId);

        return dto;
    }
}
