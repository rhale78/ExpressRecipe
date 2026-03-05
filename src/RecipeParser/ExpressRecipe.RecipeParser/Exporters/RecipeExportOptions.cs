namespace ExpressRecipe.RecipeParser.Exporters;

public sealed class RecipeExportOptions
{
    public bool PrettyPrint { get; set; } = true;
    public string? ForceFormat { get; set; }
    public bool IncludeNutrition { get; set; } = true;
}
