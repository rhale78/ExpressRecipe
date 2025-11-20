using ExpressRecipe.Shared.CQRS;

namespace ExpressRecipe.RecipeService.CQRS.Queries;

/// <summary>
/// Query to search recipes with filters
/// </summary>
public record SearchRecipesQuery : IQuery<SearchRecipesResult>
{
    public string? SearchTerm { get; init; }
    public Guid? UserId { get; init; } // Filter by creator
    public List<string> Categories { get; init; } = new();
    public List<string> Tags { get; init; } = new();
    public string? Difficulty { get; init; }
    public int? MaxPrepTime { get; init; }
    public int? MaxTotalTime { get; init; }
    public decimal? MinRating { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string SortBy { get; init; } = "CreatedAt"; // CreatedAt, Rating, ViewCount, Name
    public bool SortDescending { get; init; } = true;
}

/// <summary>
/// Search results with pagination
/// </summary>
public class SearchRecipesResult
{
    public List<RecipeSummaryDto> Recipes { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>
/// Recipe summary for search results
/// </summary>
public class RecipeSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int? TotalTimeMinutes { get; set; }
    public int Servings { get; set; }
    public string Difficulty { get; set; } = "Medium";
    public decimal AverageRating { get; set; }
    public int RatingCount { get; set; }
    public int ViewCount { get; set; }
    public List<string> Categories { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}
