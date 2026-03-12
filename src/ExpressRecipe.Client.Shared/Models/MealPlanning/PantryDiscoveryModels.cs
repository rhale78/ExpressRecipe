namespace ExpressRecipe.Client.Shared.Models.MealPlanning;

public sealed record PantryDiscoveryOptionsDto
{
    public decimal MinMatchPercent { get; init; } = 0.80m;
    public string SortBy { get; init; } = "match";   // match | rating | cookTime | added
    public int Limit { get; init; } = 24;
    public bool RespectDietaryRestrictions { get; init; } = true;
}

public sealed record PantryDiscoveryResultDto
{
    public List<PantryRecipeMatchDto> Matches { get; init; } = new();
    public int TotalPantryIngredients { get; init; }
    public DateTime CachedAt { get; init; }
}

public sealed record PantryRecipeMatchDto
{
    public Guid RecipeId { get; init; }
    public string RecipeName { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public int CookTimeMinutes { get; init; }
    public decimal AverageRating { get; init; }
    public decimal MatchPercent { get; init; }
    public int MatchedIngredientCount { get; init; }
    public int TotalIngredientCount { get; init; }
    public List<string> MissingIngredients { get; init; } = new();
}
