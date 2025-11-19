namespace ExpressRecipe.Client.Shared.Models.Recipe;

public class RecipeDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int PrepTimeMinutes { get; set; }
    public int CookTimeMinutes { get; set; }
    public int TotalTimeMinutes => PrepTimeMinutes + CookTimeMinutes;
    public int Servings { get; set; }
    public string Difficulty { get; set; } = "Medium"; // Easy, Medium, Hard
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
    public string? ImageUrl { get; set; }
    public string? Tips { get; set; }
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
    public List<string>? Tags { get; set; }
    public List<string>? DietaryInfo { get; set; }
    public List<string>? ExcludeAllergens { get; set; }
    public int? MaxPrepTime { get; set; }
    public int? MaxCookTime { get; set; }
    public string? Difficulty { get; set; }
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
    public string? ImageUrl { get; set; }
    public int PrepTimeMinutes { get; set; }
    public int CookTimeMinutes { get; set; }
    public int Servings { get; set; } = 4;
    public string Difficulty { get; set; } = "Medium";
    public string? Source { get; set; }
    public string? SourceUrl { get; set; }
    public List<CreateRecipeIngredientRequest> Ingredients { get; set; } = new();
    public List<CreateRecipeStepRequest> Steps { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public List<string> DietaryInfo { get; set; } = new();
    public NutritionInfoDto? Nutrition { get; set; }
    public bool IsPublic { get; set; } = true;
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
