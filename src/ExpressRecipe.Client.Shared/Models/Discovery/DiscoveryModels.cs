namespace ExpressRecipe.Client.Shared.Models.Discovery;

public class PantryDiscoveryResult
{
    public List<PantryRecipeMatch> Matches { get; set; } = new();
    public int TotalPantryIngredients { get; set; }
    public DateTime CachedAt { get; set; }
}

public class PantryRecipeMatch
{
    public Guid RecipeId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int CookTimeMinutes { get; set; }
    public decimal AverageRating { get; set; }
    public decimal MatchPercent { get; set; }
    public int MatchedIngredientCount { get; set; }
    public int TotalIngredientCount { get; set; }
    public List<string> MissingIngredients { get; set; } = new();
    public bool HasDietaryConflict { get; set; }

    /// <summary>Formatted match percentage for display (e.g. "92%").</summary>
    public string MatchPercentFormatted => $"{MatchPercent:P0}";

    /// <summary>True when all recipe ingredients are available in the pantry.</summary>
    public bool IsFullMatch => MatchPercent >= 1.0m;
}

public class PantryDiscoveryRequest
{
    public decimal MinMatch { get; set; } = 0.80m;
    public string SortBy { get; set; } = "match";
    public int Limit { get; set; } = 24;
    public bool RespectDiet { get; set; } = true;
}
