using ExpressRecipe.Shared.CQRS;

namespace ExpressRecipe.RecipeService.CQRS.Queries;

/// <summary>
/// Query to get full recipe details
/// </summary>
public record GetRecipeDetailsQuery : IQuery<RecipeDetailsDto?>
{
    public Guid RecipeId { get; init; }
    public Guid? UserId { get; init; } // For tracking views
}

/// <summary>
/// Complete recipe details DTO
/// </summary>
public class RecipeDetailsDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int? PrepTimeMinutes { get; set; }
    public int? CookTimeMinutes { get; set; }
    public int? TotalTimeMinutes { get; set; }
    public int Servings { get; set; }
    public string Difficulty { get; set; } = "Medium";
    public bool IsPublic { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Aggregate data
    public decimal AverageRating { get; set; }
    public int RatingCount { get; set; }
    public int ViewCount { get; set; }
    public int SaveCount { get; set; }

    // Related entities
    public List<string> Categories { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public List<RecipeIngredientDto> Ingredients { get; set; } = new();
    public List<RecipeInstructionDto> Instructions { get; set; } = new();
    public RecipeNutritionDto? Nutrition { get; set; }
}

public class RecipeIngredientDto
{
    public Guid Id { get; set; }
    public Guid? ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsOptional { get; set; }
    public int SortOrder { get; set; }
}

public class RecipeInstructionDto
{
    public Guid Id { get; set; }
    public int StepNumber { get; set; }
    public string Instruction { get; set; } = string.Empty;
    public int? TimeMinutes { get; set; }
}

public class RecipeNutritionDto
{
    public int? Calories { get; set; }
    public decimal? Protein { get; set; }
    public decimal? Carbs { get; set; }
    public decimal? Fat { get; set; }
    public decimal? Fiber { get; set; }
    public decimal? Sugar { get; set; }
    public decimal? Sodium { get; set; }
}
