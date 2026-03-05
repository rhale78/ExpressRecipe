using System.Text.Json;
using System.Text.Json.Serialization;
using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Exporters;

public sealed class JsonRecipeExporter : IRecipeExporter
{
    public string FormatName => "JSON";
    public string DefaultFileExtension => "json";

    private static readonly JsonSerializerOptions PrettyOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string Export(ParsedRecipe recipe)
        => JsonSerializer.Serialize(ToDto(recipe), PrettyOptions);

    public string ExportAll(IEnumerable<ParsedRecipe> recipes)
        => JsonSerializer.Serialize(recipes.Select(ToDto).ToArray(), PrettyOptions);

    private static object ToDto(ParsedRecipe r) => new
    {
        r.Title,
        r.Description,
        r.Author,
        r.Source,
        r.Url,
        r.Yield,
        r.PrepTime,
        r.CookTime,
        r.TotalTime,
        r.Category,
        r.Cuisine,
        r.Tags,
        Ingredients = r.Ingredients.Select(i => new { i.Quantity, i.Unit, i.Name, i.Preparation, i.IsOptional, i.GroupHeading }),
        Instructions = r.Instructions.Select(i => new { i.Step, i.Text }),
        Nutrition = r.Nutrition == null ? null : new
        {
            r.Nutrition.Calories,
            r.Nutrition.Fat,
            r.Nutrition.Carbohydrates,
            r.Nutrition.Protein,
            r.Nutrition.Fiber,
            r.Nutrition.Sodium,
            r.Nutrition.Sugar,
            r.Nutrition.Cholesterol
        }
    };
}
