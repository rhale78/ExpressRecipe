namespace ExpressRecipe.RecipeParser.Models;

public sealed class ParsedRecipe
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? Source { get; set; }
    public string? Author { get; set; }
    public string? Url { get; set; }
    public string? Yield { get; set; }
    public string? PrepTime { get; set; }
    public string? CookTime { get; set; }
    public string? TotalTime { get; set; }
    public string? Category { get; set; }
    public string? Cuisine { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<ParsedIngredient> Ingredients { get; set; } = new();
    public List<ParsedInstruction> Instructions { get; set; } = new();
    public ParsedNutrition? Nutrition { get; set; }
    public string? RawText { get; set; }
    public string Format { get; set; } = "";
}
