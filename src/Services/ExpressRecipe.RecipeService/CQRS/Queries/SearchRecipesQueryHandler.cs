using ExpressRecipe.RecipeService.Data;
using ExpressRecipe.Shared.CQRS;

namespace ExpressRecipe.RecipeService.CQRS.Queries;

/// <summary>
/// Handler for searching recipes with filtering and pagination
/// </summary>
public class SearchRecipesQueryHandler : IQueryHandler<SearchRecipesQuery, SearchRecipesResult>
{
    private readonly IRecipeRepository _repository;
    private readonly ILogger<SearchRecipesQueryHandler> _logger;

    public SearchRecipesQueryHandler(
        IRecipeRepository repository,
        ILogger<SearchRecipesQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<SearchRecipesResult> HandleAsync(SearchRecipesQuery query, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Searching recipes with term '{SearchTerm}'", query.SearchTerm ?? "ALL");

        // Validate pagination
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var offset = (page - 1) * pageSize;

        // Get recipes based on search term
        List<RecipeDto> recipes;
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            recipes = await _repository.SearchRecipesAsync(query.SearchTerm, 1000, 0);
        }
        else
        {
            recipes = await _repository.GetAllRecipesAsync(1000, 0);
        }

        // Apply filters
        var filtered = recipes.AsEnumerable();

        if (query.UserId.HasValue)
        {
            filtered = filtered.Where(r => r.UserId == query.UserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Difficulty))
        {
            filtered = filtered.Where(r => r.Difficulty.Equals(query.Difficulty, StringComparison.OrdinalIgnoreCase));
        }

        if (query.MaxPrepTime.HasValue)
        {
            filtered = filtered.Where(r => r.PrepTimeMinutes <= query.MaxPrepTime.Value);
        }

        if (query.MaxTotalTime.HasValue)
        {
            filtered = filtered.Where(r => r.TotalTimeMinutes <= query.MaxTotalTime.Value);
        }

        // Filter by categories and tags (if needed, fetch from repository)
        var filteredList = filtered.ToList();

        // Apply rating filter
        var recipesWithRatings = new List<(RecipeDto recipe, decimal rating, int count)>();
        foreach (var recipe in filteredList)
        {
            var ratingInfo = await _repository.GetAverageRatingAsync(recipe.Id);

            if (query.MinRating.HasValue && ratingInfo.AverageRating < query.MinRating.Value)
                continue;

            recipesWithRatings.Add((recipe, ratingInfo.AverageRating, ratingInfo.RatingCount));
        }

        // Sort
        var sorted = query.SortBy.ToLowerInvariant() switch
        {
            "rating" => query.SortDescending
                ? recipesWithRatings.OrderByDescending(r => r.rating)
                : recipesWithRatings.OrderBy(r => r.rating),
            "viewcount" => query.SortDescending
                ? recipesWithRatings.OrderByDescending(r => r.recipe.ViewCount)
                : recipesWithRatings.OrderBy(r => r.recipe.ViewCount),
            "name" => query.SortDescending
                ? recipesWithRatings.OrderByDescending(r => r.recipe.Name)
                : recipesWithRatings.OrderBy(r => r.recipe.Name),
            _ => query.SortDescending
                ? recipesWithRatings.OrderByDescending(r => r.recipe.CreatedAt)
                : recipesWithRatings.OrderBy(r => r.recipe.CreatedAt)
        };

        var totalCount = recipesWithRatings.Count;
        var paged = sorted.Skip(offset).Take(pageSize).ToList();

        // Build result DTOs
        var results = new List<RecipeSummaryDto>();
        foreach (var (recipe, rating, ratingCount) in paged)
        {
            var categories = await _repository.GetRecipeCategoriesAsync(recipe.Id);
            var tags = await _repository.GetRecipeTagsAsync(recipe.Id);

            results.Add(new RecipeSummaryDto
            {
                Id = recipe.Id,
                Name = recipe.Name,
                Description = recipe.Description,
                ImageUrl = recipe.ImageUrl,
                TotalTimeMinutes = recipe.TotalTimeMinutes,
                Servings = recipe.Servings,
                Difficulty = recipe.Difficulty,
                AverageRating = rating,
                RatingCount = ratingCount,
                ViewCount = recipe.ViewCount,
                Categories = categories,
                Tags = tags,
                CreatedAt = recipe.CreatedAt
            });
        }

        _logger.LogInformation("Found {TotalCount} recipes, returning page {Page} of {TotalPages}",
            totalCount, page, (int)Math.Ceiling(totalCount / (double)pageSize));

        return new SearchRecipesResult
        {
            Recipes = results,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
