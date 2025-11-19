using ExpressRecipe.Shared.CQRS;

namespace ExpressRecipe.RecipeService.CQRS.Commands;

/// <summary>
/// Command to create a new recipe
/// </summary>
public record CreateRecipeCommand : ICommand<Guid>
{
    public Guid UserId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int? PrepTimeMinutes { get; init; }
    public int? CookTimeMinutes { get; init; }
    public int Servings { get; init; }
    public string Difficulty { get; init; } = "Medium";
    public List<string> Categories { get; init; } = new();
    public List<string> Tags { get; init; } = new();
    public List<IngredientItem> Ingredients { get; init; } = new();
    public List<InstructionStep> Instructions { get; init; } = new();
    public NutritionInfo? Nutrition { get; init; }
}

public record IngredientItem
{
    public Guid? ProductId { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string Unit { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public bool IsOptional { get; init; }
}

public record InstructionStep
{
    public int StepNumber { get; init; }
    public string Instruction { get; init; } = string.Empty;
    public int? TimeMinutes { get; init; }
}

public record NutritionInfo
{
    public int? Calories { get; init; }
    public decimal? Protein { get; init; }
    public decimal? Carbs { get; init; }
    public decimal? Fat { get; init; }
    public decimal? Fiber { get; init; }
    public decimal? Sugar { get; init; }
}
