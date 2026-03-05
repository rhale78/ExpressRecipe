using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Parsers;

public interface IRecipeParser
{
    string FormatName { get; }
    bool CanParse(string text, string? fileExtension = null);
    ParseResult Parse(string text, RecipeParseOptions? options = null);
}
