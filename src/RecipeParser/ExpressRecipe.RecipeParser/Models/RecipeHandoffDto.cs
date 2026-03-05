namespace ExpressRecipe.RecipeParser.Models;

/// <summary>
/// A transfer object that mirrors the shape of CreateRecipeCommand in ExpressRecipe.RecipeService.
/// Use this to hand off a parsed recipe to the recipe service without creating a circular dependency.
/// Map via RecipeHandoffMapper.ToHandoffDto(parsedRecipe).
/// </summary>
public sealed class RecipeHandoffDto
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int? PrepTimeMinutes { get; set; }
    public int? CookTimeMinutes { get; set; }
    public int Servings { get; set; } = 1;
    public string Difficulty { get; set; } = "Medium";
    public List<string> Categories { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public List<HandoffIngredient> Ingredients { get; set; } = new();
    public List<HandoffInstruction> Instructions { get; set; } = new();
    public HandoffNutrition? Nutrition { get; set; }
    public string? SourceFormat { get; set; }
    public string? SourceUrl { get; set; }
    public string? Author { get; set; }
    public string? Cuisine { get; set; }
}

public sealed class HandoffIngredient
{
    public string Name { get; set; } = "";
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "";
    public string? Notes { get; set; }
    public bool IsOptional { get; set; }
}

public sealed class HandoffInstruction
{
    public int StepNumber { get; set; }
    public string Instruction { get; set; } = "";
    public int? TimeMinutes { get; set; }
}

public sealed class HandoffNutrition
{
    public int? Calories { get; set; }
    public decimal? Protein { get; set; }
    public decimal? Carbs { get; set; }
    public decimal? Fat { get; set; }
    public decimal? Fiber { get; set; }
    public decimal? Sugar { get; set; }
}
