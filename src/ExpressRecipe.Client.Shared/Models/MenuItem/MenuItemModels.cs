namespace ExpressRecipe.Client.Shared.Models.MenuItem;

public class MenuItemDto
{
    public Guid Id { get; set; }
    public Guid RestaurantId { get; set; }
    public string RestaurantName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public decimal? Price { get; set; }
    public string Currency { get; set; } = "USD";
    public string? ServingSize { get; set; }
    public string? ServingUnit { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; }
    public bool IsSeasonalItem { get; set; }
    public string ApprovalStatus { get; set; } = "Pending";
    public List<MenuItemIngredientDto> Ingredients { get; set; } = new();
    public MenuItemNutritionDto? Nutrition { get; set; }
    public decimal? AverageRating { get; set; }
    public int RatingCount { get; set; }
}

public class MenuItemIngredientDto
{
    public Guid Id { get; set; }
    public Guid MenuItemId { get; set; }
    public Guid IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public string? Notes { get; set; }
    public string? IngredientListString { get; set; }
    public string? IngredientCategory { get; set; }
}

public class MenuItemNutritionDto
{
    public Guid Id { get; set; }
    public Guid MenuItemId { get; set; }
    public decimal? Calories { get; set; }
    public decimal? TotalFat { get; set; }
    public decimal? SaturatedFat { get; set; }
    public decimal? TransFat { get; set; }
    public decimal? Cholesterol { get; set; }
    public decimal? Sodium { get; set; }
    public decimal? TotalCarbohydrate { get; set; }
    public decimal? DietaryFiber { get; set; }
    public decimal? Sugars { get; set; }
    public decimal? Protein { get; set; }
    public string? AdditionalNutrients { get; set; }
    public decimal? VitaminD { get; set; }
    public decimal? Calcium { get; set; }
    public decimal? Iron { get; set; }
    public decimal? Potassium { get; set; }
}

public class CreateMenuItemRequest
{
    public Guid RestaurantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public decimal? Price { get; set; }
    public string Currency { get; set; } = "USD";
    public string? ServingSize { get; set; }
    public string? ServingUnit { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; } = true;
    public bool IsSeasonalItem { get; set; }
}

public class UpdateMenuItemRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public decimal? Price { get; set; }
    public string Currency { get; set; } = "USD";
    public string? ServingSize { get; set; }
    public string? ServingUnit { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; }
    public bool IsSeasonalItem { get; set; }
}

public class MenuItemSearchRequest
{
    public Guid? RestaurantId { get; set; }
    public string? SearchTerm { get; set; }
    public string? Category { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool? OnlyAvailable { get; set; } = true;
    public bool? OnlyApproved { get; set; } = true;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class MenuItemSearchResult
{
    public List<MenuItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}
