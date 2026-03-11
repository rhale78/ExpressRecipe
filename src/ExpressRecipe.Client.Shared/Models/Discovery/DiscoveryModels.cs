namespace ExpressRecipe.Client.Shared.Models.Discovery;

public class PantryDiscoveryResult
{
    public List<PantryRecipeMatch> Matches { get; init; } = new();
    public int TotalPantryIngredients { get; init; }
    public DateTime CachedAt { get; init; }
}

public class PantryRecipeMatch
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
    public bool HasDietaryConflict { get; init; }

    /// <summary>Formatted match percentage for display (e.g. "92%").</summary>
    public string MatchPercentFormatted => $"{MatchPercent:P0}";

    /// <summary>True when all recipe ingredients are available in the pantry.</summary>
    public bool IsFullMatch => MatchPercent >= 1.0m;
}

public class PantryDiscoveryRequest
{
    public decimal MinMatch { get; init; } = 0.80m;
    public string SortBy { get; init; } = "match";
    public int Limit { get; init; } = 24;
    public bool RespectDiet { get; init; } = true;
}
