using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Exporters;

public static class RecipeExportEngine
{
    private static readonly IRecipeExporter[] Exporters =
    [
        new JsonLdRecipeExporter(),
        new JsonRecipeExporter(),
        new YamlRecipeExporter(),
        new OpenRecipeFormatExporter(),
        new RecipeMLExporter(),
        new MealMasterExporter(),
        new MasterCookExporter(),
        new CookLangExporter(),
        new HtmlRecipeExporter(),
        new PdfRecipeExporter(),
    ];

    private static readonly HtmlRecipeExporter HtmlExporter = new();
    private static readonly PdfRecipeExporter PdfExporter = new();

    public static IReadOnlyList<string> SupportedFormats { get; } =
        Exporters.Select(e => e.FormatName).ToArray();

    public static string Export(ParsedRecipe recipe, string format, RecipeExportOptions? options = null)
    {
        var exporter = GetExporter(format);
        return exporter.Export(recipe);
    }

    public static string ExportAll(IEnumerable<ParsedRecipe> recipes, string format, RecipeExportOptions? options = null)
    {
        var exporter = GetExporter(format);
        return exporter.ExportAll(recipes);
    }

    // ── HTML helpers ─────────────────────────────────────────────────────────

    /// <summary>Export a single recipe to a self-contained HTML page.</summary>
    public static string ExportRecipeHtml(RecipeExportData data, HtmlExportOptions? options = null) =>
        HtmlExporter.ExportRecipePage(data, options);

    /// <summary>Export a collection of recipes as an HTML index page with cards.</summary>
    public static string ExportIndexHtml(IEnumerable<RecipeIndexEntry> entries, HtmlExportOptions? options = null) =>
        HtmlExporter.ExportIndexPage(entries, options);

    /// <summary>Append a new recipe card to an existing HTML index page.</summary>
    public static string AddToIndexHtml(string existingIndexHtml, RecipeIndexEntry entry, HtmlExportOptions? options = null) =>
        HtmlExporter.AddToIndexPage(existingIndexHtml, entry, options);

    // ── PDF helpers ──────────────────────────────────────────────────────────

    /// <summary>Export a single recipe to PDF bytes.</summary>
    public static byte[] ExportRecipePdf(RecipeExportData data, PdfExportOptions? options = null) =>
        PdfExporter.ExportRecipePdf(data, options);

    /// <summary>Export multiple recipes as a single PDF with an optional table of contents.</summary>
    public static byte[] ExportCookbookPdf(IEnumerable<RecipeExportData> recipes, PdfExportOptions? options = null) =>
        PdfExporter.ExportCookbookPdf(recipes, options);

    /// <summary>Combine existing recipes with a new recipe and return as PDF bytes.</summary>
    public static byte[] AddToPdf(IEnumerable<RecipeExportData> existingRecipes, RecipeExportData newRecipe, PdfExportOptions? options = null) =>
        PdfExporter.AddToPdf(existingRecipes, newRecipe, options);

    private static IRecipeExporter GetExporter(string format)
    {
        var exporter = Exporters.FirstOrDefault(e => e.FormatName.Equals(format, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotSupportedException($"Export format '{format}' is not supported. Supported formats: {string.Join(", ", SupportedFormats)}");
        return exporter;
    }
}
