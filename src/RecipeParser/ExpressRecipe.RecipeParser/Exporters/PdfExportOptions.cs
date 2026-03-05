namespace ExpressRecipe.RecipeParser.Exporters;

public sealed class PdfExportOptions
{
    public string Title { get; set; } = "Recipe";
    public bool IncludeImages { get; set; } = true;
    public bool IncludeNutrition { get; set; } = true;
    public bool IncludeTableOfContents { get; set; } = true; // for multi-recipe
    public string PrimaryColor { get; set; } = "#E67E22";    // orange
    public string FontFamily { get; set; } = "Arial";
    public bool AddPageNumbers { get; set; } = true;
}
