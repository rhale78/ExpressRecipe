using ExpressRecipe.RecipeService.Data;
using ExpressRecipe.Shared.CQRS;
using ExpressRecipe.Shared.DTOs.Recipe;

namespace ExpressRecipe.RecipeService.CQRS.Queries
{
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
            List<ExpressRecipe.Shared.DTOs.Recipe.RecipeDto> recipes;
            if (!string.IsNullOrWhiteSpace(query.SearchTerm))
            {
                recipes = await _repository.SearchRecipesAsync(query.SearchTerm, 1000, 0);
            }
            else
            {
                recipes = await _repository.GetAllRecipesAsync(1000, 0);
            }

            // Apply filters
            IEnumerable<RecipeDto> filtered = recipes.AsEnumerable();

            if (query.UserId.HasValue)
            {
                filtered = filtered.Where(r => r.AuthorId == query.UserId.Value); // Use AuthorId instead of UserId
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
            List<RecipeDto> filteredList = filtered.ToList();

            // Apply rating filter
            List<(RecipeDto recipe, decimal rating, int count)> recipesWithRatings = [];
            foreach (RecipeDto recipe in filteredList)
            {
                (decimal AverageRating, int RatingCount) ratingInfo = await _repository.GetAverageRatingAsync(recipe.Id);

                if (query.MinRating.HasValue && ratingInfo.AverageRating < query.MinRating.Value)
                {
                    continue;
                }

                recipesWithRatings.Add((recipe, ratingInfo.AverageRating, ratingInfo.RatingCount));
            }

            // Sort
            IOrderedEnumerable<(RecipeDto recipe, decimal rating, int count)> sorted = query.SortBy.ToLowerInvariant() switch
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
            List<(RecipeDto recipe, decimal rating, int count)> paged = sorted.Skip(offset).Take(pageSize).ToList();

            // Build result DTOs
            List<RecipeSummaryDto> results = [];
            foreach ((RecipeDto? recipe, decimal rating, int ratingCount) in paged)
            {
                List<string> categories = await _repository.GetRecipeCategoriesAsync(recipe.Id);
                List<string> tags = await _repository.GetRecipeTagsAsync(recipe.Id);

                results.Add(new RecipeSummaryDto
                {
                    Id = recipe.Id,
                    Name = recipe.Name,
                    Description = recipe.Description,
                    ImageUrl = recipe.ImageUrl,
                    TotalTimeMinutes = recipe.TotalTimeMinutes,
                    Servings = recipe.Servings ?? 0, // Provide default value for nullable
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
}
