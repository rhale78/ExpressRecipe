namespace ExpressRecipe.RecipeParser.Models;

public sealed class ParsedNutrition
{
    public string? Calories { get; set; }
    public string? Fat { get; set; }
    public string? Carbohydrates { get; set; }
    public string? Protein { get; set; }
    public string? Fiber { get; set; }
    public string? Sodium { get; set; }
    public string? Sugar { get; set; }
    public string? Cholesterol { get; set; }
    public Dictionary<string, string> Other { get; set; } = new();
}
