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
    ];

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

    private static IRecipeExporter GetExporter(string format)
    {
        var exporter = Exporters.FirstOrDefault(e => e.FormatName.Equals(format, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotSupportedException($"Export format '{format}' is not supported. Supported formats: {string.Join(", ", SupportedFormats)}");
        return exporter;
    }
}
