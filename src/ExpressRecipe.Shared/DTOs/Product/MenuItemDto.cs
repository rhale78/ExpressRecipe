using System.ComponentModel.DataAnnotations;

namespace ExpressRecipe.Shared.DTOs.Product;

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
}

public class CreateMenuItemRequest
{
    [Required]
    public Guid RestaurantId { get; set; }

    [Required]
    [StringLength(300, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [StringLength(100)]
    public string? Category { get; set; }

    [Range(0.01, 999999.99)]
    public decimal? Price { get; set; }

    [StringLength(10)]
    public string Currency { get; set; } = "USD";

    [StringLength(100)]
    public string? ServingSize { get; set; }

    [Url]
    [StringLength(500)]
    public string? ImageUrl { get; set; }

    public bool IsAvailable { get; set; } = true;
    public bool IsSeasonalItem { get; set; }

    public List<Guid> IngredientIds { get; set; } = new();
}

public class UpdateMenuItemRequest
{
    [Required]
    [StringLength(300, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [StringLength(100)]
    public string? Category { get; set; }

    [Range(0.01, 999999.99)]
    public decimal? Price { get; set; }

    [StringLength(10)]
    public string Currency { get; set; } = "USD";

    [StringLength(100)]
    public string? ServingSize { get; set; }

    [Url]
    [StringLength(500)]
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

public class UserMenuItemRatingDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid MenuItemId { get; set; }
    public string MenuItemName { get; set; } = string.Empty;
    public Guid RestaurantId { get; set; }
    public string RestaurantName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? Review { get; set; }
    public bool? WouldOrderAgain { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RateMenuItemRequest
{
    [Required]
    [Range(1, 5)]
    public int Rating { get; set; }

    [StringLength(2000)]
    public string? Review { get; set; }

    public bool? WouldOrderAgain { get; set; }
}

public class AddMenuItemIngredientRequest
{
    [Required]
    public Guid IngredientId { get; set; }

    public int OrderIndex { get; set; }
    public string? Notes { get; set; }
}
