namespace ExpressRecipe.RecipeParser.Models;

public sealed class ParseResult
{
    public bool Success { get; set; }
    public string? Format { get; set; }
    public List<ParsedRecipe> Recipes { get; set; } = new();
    public List<ParseError> Errors { get; set; } = new();
}
