using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Exporters;

public interface IRecipeExporter
{
    string FormatName { get; }
    string DefaultFileExtension { get; }
    string Export(ParsedRecipe recipe);
    string ExportAll(IEnumerable<ParsedRecipe> recipes);

    /// <summary>Export with options. Default implementation ignores options for backwards compatibility.</summary>
    string Export(ParsedRecipe recipe, RecipeExportOptions? options) => Export(recipe);
    /// <summary>ExportAll with options. Default implementation ignores options for backwards compatibility.</summary>
    string ExportAll(IEnumerable<ParsedRecipe> recipes, RecipeExportOptions? options) => ExportAll(recipes);
}
