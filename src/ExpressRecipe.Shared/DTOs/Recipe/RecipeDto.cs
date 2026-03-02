using System.ComponentModel.DataAnnotations;

namespace ExpressRecipe.Shared.DTOs.Recipe;

public class RecipeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Title => Name; // Alias for compatibility
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? Cuisine { get; set; }
    public string? DifficultyLevel { get; set; }
    public string Difficulty => DifficultyLevel ?? "Medium"; // Alias for compatibility
    public int? PrepTimeMinutes { get; set; }
    public int? CookTimeMinutes { get; set; }
    public int? TotalTimeMinutes { get; set; }
    public int? Servings { get; set; }
    public string? ImageUrl { get; set; }
    public string? VideoUrl { get; set; }
    public string? Instructions { get; set; }
    public string? Notes { get; set; }
    public bool IsPublic { get; set; }
    public bool IsApproved { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public string? SourceUrl { get; set; }
    public Guid AuthorId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int ViewCount { get; set; }
    public int FavoriteCount { get; set; }

    // Navigation properties (populated on demand)
    public List<RecipeIngredientDto>? Ingredients { get; set; }
    public RecipeNutritionDto? Nutrition { get; set; }
    public List<RecipeTagDto>? Tags { get; set; }
    public List<RecipeAllergenWarningDto>? AllergenWarnings { get; set; }
    public List<string> DietaryInfo { get; set; } = new(); // Vegetarian, Vegan, etc.
    public decimal? AverageRating { get; set; }
    public int? RatingCount { get; set; }
}

public class RecipeIngredientDto
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public Guid? IngredientId { get; set; }
    public Guid? BaseIngredientId { get; set; }
    public string? IngredientName { get; set; }
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public int OrderIndex { get; set; }
    public string? PreparationNote { get; set; }
    public bool IsOptional { get; set; }
    public string? SubstituteNotes { get; set; }
    public string? GroupName { get; set; }
    public string? OriginalText { get; set; }
}

public class RecipeNutritionDto
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public string? ServingSize { get; set; }
    public decimal? Calories { get; set; }
    public decimal? TotalFat { get; set; }
    public decimal? SaturatedFat { get; set; }
    public decimal? TransFat { get; set; }
    public decimal? Cholesterol { get; set; }
    public decimal? Sodium { get; set; }
    public decimal? TotalCarbohydrates { get; set; }
    public decimal? DietaryFiber { get; set; }
    public decimal? Sugars { get; set; }
    public decimal? Protein { get; set; }
    public decimal? VitaminD { get; set; }
    public decimal? Calcium { get; set; }
    public decimal? Iron { get; set; }
    public decimal? Potassium { get; set; }
}

public class RecipeTagDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class RecipeAllergenWarningDto
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public Guid AllergenId { get; set; }
    public string AllergenName { get; set; } = string.Empty;
    public Guid? SourceIngredientId { get; set; }
}

public class CreateRecipeRequest
{
    [Required]
    [StringLength(300, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [StringLength(100)]
    public string? Category { get; set; }

    [StringLength(100)]
    public string? Cuisine { get; set; }

    [StringLength(50)]
    public string? Difficulty { get; set; }

    [Range(0, 10000)]
    public int? PrepTimeMinutes { get; set; }

    [Range(0, 10000)]
    public int? CookTimeMinutes { get; set; }

    [Range(0, 10000)]
    public int? TotalTimeMinutes { get; set; }

    [Range(1, 100)]
    public int? Servings { get; set; }

    [Url]
    [StringLength(500)]
    public string? ImageUrl { get; set; }

    [Url]
    [StringLength(500)]
    public string? VideoUrl { get; set; }

    public string? Instructions { get; set; }
    public string? Notes { get; set; }
    public bool IsPublic { get; set; }

    [Url]
    [StringLength(500)]
    public string? SourceUrl { get; set; }

    public Guid CreatedBy { get; set; }
    
    public List<CreateRecipeIngredientRequest>? Ingredients { get; set; }
    public List<CreateRecipeStepRequest>? Steps { get; set; }
    public List<string>? Tags { get; set; }
}

public class UpdateRecipeRequest
{
    [Required]
    [StringLength(300, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [StringLength(100)]
    public string? Category { get; set; }

    [StringLength(100)]
    public string? Cuisine { get; set; }

    [StringLength(50)]
    public string? Difficulty { get; set; }

    [Range(0, 10000)]
    public int? PrepTimeMinutes { get; set; }

    [Range(0, 10000)]
    public int? CookTimeMinutes { get; set; }

    [Range(0, 10000)]
    public int? TotalTimeMinutes { get; set; }

    [Range(1, 100)]
    public int? Servings { get; set; }

    [Url]
    [StringLength(500)]
    public string? ImageUrl { get; set; }

    [Url]
    [StringLength(500)]
    public string? VideoUrl { get; set; }

    public string? Instructions { get; set; }
    public string? Notes { get; set; }
    public bool IsPublic { get; set; }

    [Url]
    [StringLength(500)]
    public string? SourceUrl { get; set; }

    public List<CreateRecipeIngredientRequest>? Ingredients { get; set; }
    public List<CreateRecipeStepRequest>? Steps { get; set; }
    public List<string>? Tags { get; set; }
}

public class CreateRecipeIngredientRequest
{
    public int OrderIndex { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsOptional { get; set; }
    public string? GroupName { get; set; }
}

public class CreateRecipeStepRequest
{
    public int OrderIndex { get; set; }
    public string Instruction { get; set; } = string.Empty;
    public int? DurationMinutes { get; set; }
    public string? ImageUrl { get; set; }
    public string? Tips { get; set; }
}

public class RecipeSearchRequest
{
    public string? SearchTerm { get; set; }
    public string? Category { get; set; }
    public string? Cuisine { get; set; }
    public string? Difficulty { get; set; }
    public List<string>? Tags { get; set; }
    public List<string>? DietaryInfo { get; set; }
    public List<string>? ExcludeAllergens { get; set; } // Filter out recipes with these allergens
    public List<string>? ExcludeIngredients { get; set; } // Foods/ingredients user dislikes
    public int? MaxPrepTime { get; set; }
    public int? MaxCookTime { get; set; }
    public int? MaxPrepTimeMinutes { get; set; }
    public int? MaxTotalTimeMinutes { get; set; }
    public List<Guid>? TagIds { get; set; }
    public List<Guid>? ExcludeAllergenIds { get; set; }
    public Guid? AuthorId { get; set; }
    public bool? OnlyPublic { get; set; } = true;
    public bool? OnlyApproved { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SortBy { get; set; } = "CreatedAt";
    public bool SortDescending { get; set; } = true;
}

public class RecipeSearchResult
{
    public List<RecipeDto> Recipes { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public class AddRecipeIngredientRequest
{
    public Guid? IngredientId { get; set; }
    public Guid? BaseIngredientId { get; set; }
    public string? IngredientName { get; set; }

    [Range(0.01, 10000)]
    public decimal? Quantity { get; set; }

    [StringLength(50)]
    public string? Unit { get; set; }

    public int OrderIndex { get; set; }

    [StringLength(500)]
    public string? PreparationNote { get; set; }

    public bool IsOptional { get; set; }

    [StringLength(500)]
    public string? SubstituteNotes { get; set; }
}

public class UpdateRecipeIngredientRequest
{
    [Range(0.01, 10000)]
    public decimal? Quantity { get; set; }

    [StringLength(50)]
    public string? Unit { get; set; }

    public int OrderIndex { get; set; }

    [StringLength(500)]
    public string? PreparationNote { get; set; }

    public bool IsOptional { get; set; }

    [StringLength(500)]
    public string? SubstituteNotes { get; set; }
}

public class UserRecipeRatingDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid RecipeId { get; set; }
    public int Rating { get; set; }
    public string? Review { get; set; }
    public bool? WouldMakeAgain { get; set; }
    public int MadeItCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class RateRecipeRequest
{
    [Required]
    [Range(1, 5)]
    public int Rating { get; set; }

    public string? Review { get; set; }
    public bool? WouldMakeAgain { get; set; }

    [Range(0, 1000)]
    public int MadeItCount { get; set; } = 1;
}

public class RecipeImageDto
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? ImageType { get; set; }
    public string? LocalPath { get; set; }
    public bool IsPrimary { get; set; }
    public int DisplayOrder { get; set; }
    public string? SourceSystem { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class FullRecipeImportDto
{
    public CreateRecipeRequest Recipe { get; set; } = new();
    public List<RecipeIngredientDto>? Ingredients { get; set; }
    public List<RecipeImageDto>? Images { get; set; }
    public List<CreateRecipeStepRequest>? Steps { get; set; }
    public List<string>? Tags { get; set; }
}
