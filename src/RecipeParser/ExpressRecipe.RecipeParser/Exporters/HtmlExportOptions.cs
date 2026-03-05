namespace ExpressRecipe.RecipeParser.Exporters;

public sealed class HtmlExportOptions
{
    public string SiteTitle { get; set; } = "My Recipes";
    public string? CustomCss { get; set; }          // injected into <style> block
    public bool IncludeImages { get; set; } = true;
    public bool IncludeRatings { get; set; } = true;
    public bool IncludeNutrition { get; set; } = true;
    public bool IncludePrintButton { get; set; } = true;
    public string? LogoUrl { get; set; }
    public string? BaseUrl { get; set; }             // for absolute URLs in index hrefs
}
