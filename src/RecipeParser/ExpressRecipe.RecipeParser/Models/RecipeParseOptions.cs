namespace ExpressRecipe.RecipeParser.Models;

public sealed class RecipeParseOptions
{
    public bool IncludeRawText { get; set; } = false;
    public int MaxRecipes { get; set; } = int.MaxValue;
    public string? ForceFormat { get; set; }
    public bool StrictMode { get; set; } = false;
}
