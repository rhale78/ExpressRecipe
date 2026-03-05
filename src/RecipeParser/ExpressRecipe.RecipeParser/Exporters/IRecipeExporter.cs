using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Exporters;

public interface IRecipeExporter
{
    string FormatName { get; }
    string DefaultFileExtension { get; }
    string Export(ParsedRecipe recipe);
    string ExportAll(IEnumerable<ParsedRecipe> recipes);
}
