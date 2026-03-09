namespace ExpressRecipe.MealPlanningService.Services;

public class SuggestionRequest
{
    public Guid UserId { get; init; }
    public Guid? HouseholdId { get; init; }
    public string MealType { get; init; } = "Dinner";
    public string SuggestionMode { get; init; } = SuggestionModes.Balanced;
    public int InventorySlider { get; init; } = 50;   // 0 = use on-hand, 100 = shop fresh
    public int MaxCookMinutes { get; init; }           // 0 = any
    public int Count { get; init; } = 5;
    public bool ExcludeRecentDays { get; init; } = true;
    public int RecentDaysCutoff { get; init; } = 14;
    public List<Guid> ExcludeRecipeIds { get; init; } = new();
}

public static class SuggestionModes
{
    public const string TriedAndTrue   = "TriedAndTrue";
    public const string SomethingNew   = "SomethingNew";
    public const string Balanced       = "Balanced";
}

public class MealSuggestion
{
    public Guid RecipeId { get; init; }
    public string RecipeName { get; init; } = string.Empty;
    public int CookMinutes { get; init; }
    public decimal UserRating { get; init; }
    public decimal GlobalRating { get; init; }
    public int UserCookCount { get; init; }
    public decimal InventoryMatchPct { get; init; }
    public bool IsAllergenSafe { get; init; }
    public decimal Score { get; init; }
    public List<string> MissingIngredients { get; init; } = new();
    public List<string> Tags { get; init; } = new();
}
