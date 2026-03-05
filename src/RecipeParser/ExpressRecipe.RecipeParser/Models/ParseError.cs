namespace ExpressRecipe.RecipeParser.Models;

public sealed class ParseError
{
    public string Level { get; set; } = "";
    public string Message { get; set; } = "";
    public Exception? Exception { get; set; }
    public int? RecipeIndex { get; set; }
    public string? RecipeTitle { get; set; }
}
