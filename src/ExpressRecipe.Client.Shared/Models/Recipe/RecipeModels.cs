namespace ExpressRecipe.Client.Shared.Models.Recipe;

public class RecipeDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ImageUrl { get; set; } // Primary image URL for backward compatibility
    public List<RecipeImageDto> Images { get; set; } = new(); // All recipe images
    public int PrepTimeMinutes { get; set; }
    public int CookTimeMinutes { get; set; }
    public int TotalTimeMinutes => PrepTimeMinutes + CookTimeMinutes;
    public int Servings { get; set; }
    public string Difficulty { get; set; } = "Medium"; // Easy, Medium, Hard
    public string? Category { get; set; } // e.g., Breakfast, Main Dish
    public string? Cuisine { get; set; } // e.g., Italian, Mexican
    public RecipeSourceDto? SourceMetadata { get; set; }
    public string? Source { get; set; }
    public string? SourceUrl { get; set; }
    public List<RecipeIngredientDto> Ingredients { get; set; } = new();
    public List<RecipeStepDto> Steps { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public List<string> DietaryInfo { get; set; } = new(); // Vegetarian, Vegan, Gluten-Free, etc.
    public List<string> Allergens { get; set; } = new();
    public NutritionInfoDto? Nutrition { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsPublic { get; set; }
    public int ViewCount { get; set; }
    public int FavoriteCount { get; set; }
}

public class RecipeImageDto
{
    public Guid Id { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public int OrderIndex { get; set; }
    public string? Caption { get; set; }
}

public class RecipeIngredientDto
{
    public Guid Id { get; set; }
    public int OrderIndex { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsOptional { get; set; }
    public string? GroupName { get; set; } // For grouping ingredients (e.g., "For the sauce", "For the dough")
}

public class RecipeStepDto
{
    public Guid Id { get; set; }
    public int OrderIndex { get; set; }
    public string Instruction { get; set; } = string.Empty;
    public int? DurationMinutes { get; set; }
    public string? ImageUrl { get; set; } // Primary image for backward compatibility
    public List<RecipeStepImageDto> Images { get; set; } = new(); // Multiple step images
    public string? Tips { get; set; }
}

public class RecipeStepImageDto
{
    public Guid Id { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public int OrderIndex { get; set; }
    public string? Caption { get; set; }
}

public class NutritionInfoDto
{
    public int Calories { get; set; }
    public decimal Protein { get; set; } // grams
    public decimal Carbohydrates { get; set; } // grams
    public decimal Fat { get; set; } // grams
    public decimal Fiber { get; set; } // grams
    public decimal Sugar { get; set; } // grams
    public decimal Sodium { get; set; } // mg
}

public class RecipeSearchRequest
{
    public string? SearchTerm { get; set; }
    public string? Category { get; set; }
    public List<string>? Tags { get; set; }
    public List<string>? DietaryInfo { get; set; }
    public List<string>? ExcludeAllergens { get; set; }
    public List<string>? ExcludeIngredients { get; set; } // Foods/ingredients user dislikes (preference, not medical)
    public int? MaxPrepTime { get; set; }
    public int? MaxCookTime { get; set; }
    public string? Difficulty { get; set; }
    //public string? Category { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SortBy { get; set; } = "CreatedAt"; // CreatedAt, ViewCount, FavoriteCount, Title
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

public class CreateRecipeRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ImageUrl { get; set; } // Primary image for backward compatibility
    public List<CreateRecipeImageRequest> Images { get; set; } = new(); // Multiple images
    public int PrepTimeMinutes { get; set; }
    public int CookTimeMinutes { get; set; }
    public int Servings { get; set; } = 4;
    public string Difficulty { get; set; } = "Medium";
    public RecipeSourceDto? SourceMetadata { get; set; }
    public string? Source { get; set; }
    public string? SourceUrl { get; set; }
    //public RecipeSourceDto? SourceMetadata { get; set; }
    public string? Cuisine { get; set; }
    public string? Category { get; set; }
    public List<CreateRecipeIngredientRequest> Ingredients { get; set; } = new();
    public List<CreateRecipeStepRequest> Steps { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public List<string> DietaryInfo { get; set; } = new();
    public NutritionInfoDto? Nutrition { get; set; }
    public bool IsPublic { get; set; } = true;
}

public class CreateRecipeImageRequest
{
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public int OrderIndex { get; set; }
    public string? Caption { get; set; }
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
    public string? ImageUrl { get; set; } // Primary image for backward compatibility
    public List<CreateRecipeStepImageRequest> Images { get; set; } = new(); // Multiple step images
    public string? Tips { get; set; }
}

public class CreateRecipeStepImageRequest
{
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public int OrderIndex { get; set; }
    public string? Caption { get; set; }
}

public class UpdateRecipeRequest : CreateRecipeRequest
{
}

public class ImportRecipeRequest
{
    public string Source { get; set; } = string.Empty; // "File", "Paste", "Url"
    public string Content { get; set; } = string.Empty;
    public string? ContentType { get; set; } // "json", "text", "html", etc.
    public string? SourceUrl { get; set; }
}

public class ImportRecipeResponse
{
    public bool Success { get; set; }
    public RecipeDto? Recipe { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public string? Message { get; set; }
}

public class RecipeImportValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class ParsedRecipeResult
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int PrepTimeMinutes { get; set; }
    public int CookTimeMinutes { get; set; }
    public int Servings { get; set; } = 4;
    public string Difficulty { get; set; } = "Medium";
    public string? Cuisine { get; set; }
    public string? Category { get; set; }
    public List<CreateRecipeIngredientRequest> Ingredients { get; set; } = new();
    public List<CreateRecipeStepRequest> Steps { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public List<string> DietaryInfo { get; set; } = new();
    public double Confidence { get; set; }
    public bool IsAIParsed { get; set; }
}

public class RecipeSourceDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = "Website";
    public string Name { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Author { get; set; }
    public string? Notes { get; set; }
}

public class RecipeDraftData
{
    public Guid DraftId { get; set; } = Guid.NewGuid();
    public string RecipeText { get; set; } = string.Empty;
    public CreateRecipeRequest RecipeData { get; set; } = new();
    public List<RecipeSourceDto> Sources { get; set; } = new();
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;
}

public class RecipeShareTokenDto
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public int ViewCount { get; set; }
    public RecipeDto? Recipe { get; set; }
}

public class HouseholdShareRequest
{
    public bool Shared { get; set; }
    public Guid? HouseholdId { get; set; }
}

public class ShareRecipeEmailRequest
{
    public string ToEmail { get; set; } = string.Empty;
    public string? FromName { get; set; }
    public string? Message { get; set; }
    public string? ShareUrl { get; set; }
}
