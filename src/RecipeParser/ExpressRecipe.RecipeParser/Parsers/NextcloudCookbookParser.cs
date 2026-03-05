using System.Text.Json;
using ExpressRecipe.RecipeParser.Helpers;
using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Parsers;

/// <summary>
/// Parses Nextcloud Cookbook JSON format which uses schema.org/Recipe.
/// </summary>
public sealed class NextcloudCookbookParser : IRecipeParser
{
    public string FormatName => "NextcloudCookbook";

    public bool CanParse(string text, string? fileExtension = null)
    {
        if (fileExtension?.EndsWith("json", StringComparison.OrdinalIgnoreCase) != true &&
            !text.TrimStart().StartsWith("{")) return false;
        return text.Contains("\"@type\"") && text.Contains("Recipe") ||
               text.Contains("recipeIngredient") || text.Contains("recipeInstructions");
    }

    public ParseResult Parse(string text, RecipeParseOptions? options = null)
    {
        var result = new ParseResult { Format = FormatName };
        var errors = new List<ParseError>();

        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                int i = 0;
                foreach (var elem in root.EnumerateArray())
                {
                    try { result.Recipes.Add(ParseSchemaOrgRecipe(elem, options)); }
                    catch (Exception ex) { LoggingHelper.LogRecipeError(null, errors, i, null, "Failed to parse Nextcloud recipe", ex); }
                    i++;
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                result.Recipes.Add(ParseSchemaOrgRecipe(root, options));
            }

            result.Success = true;
        }
        catch (Exception ex) { LoggingHelper.LogBatchError(null, errors, "Failed to parse Nextcloud Cookbook JSON", ex); }

        result.Errors = errors;
        return result;
    }

    private static ParsedRecipe ParseSchemaOrgRecipe(JsonElement elem, RecipeParseOptions? options)
    {
        var recipe = new ParsedRecipe { Format = "NextcloudCookbook" };

        recipe.Title = GetStr(elem, "name") ?? "";
        recipe.Description = GetStr(elem, "description");
        recipe.Author = GetAuthor(elem);
        recipe.Url = GetStr(elem, "url");
        recipe.Yield = GetStr(elem, "recipeYield");
        recipe.PrepTime = GetStr(elem, "prepTime");
        recipe.CookTime = GetStr(elem, "cookTime");
        recipe.TotalTime = GetStr(elem, "totalTime");
        recipe.Category = GetStr(elem, "recipeCategory");
        recipe.Cuisine = GetStr(elem, "recipeCuisine");

        // Keywords/tags
        if (elem.TryGetProperty("keywords", out var kw))
        {
            if (kw.ValueKind == JsonValueKind.String)
                recipe.Tags = (kw.GetString() ?? "").Split(',').Select(t => t.Trim()).Where(t => t != "").ToList();
            else if (kw.ValueKind == JsonValueKind.Array)
                recipe.Tags = kw.EnumerateArray().Select(t => t.GetString() ?? "").Where(t => t != "").ToList();
        }

        // Ingredients - array of strings
        if (elem.TryGetProperty("recipeIngredient", out var ings) && ings.ValueKind == JsonValueKind.Array)
        {
            foreach (var ing in ings.EnumerateArray())
            {
                var line = ing.GetString() ?? "";
                if (string.IsNullOrWhiteSpace(line)) continue;
                var (qty, unit, name) = TextParserHelper.ParseIngredientLine(line);
                string n = name;
                string p = TextParserHelper.ExtractPreparation(ref n);
                recipe.Ingredients.Add(new ParsedIngredient { Quantity = qty, Unit = unit, Name = n, Preparation = string.IsNullOrEmpty(p) ? null : p });
            }
        }

        // Instructions - array of strings or HowToStep objects
        if (elem.TryGetProperty("recipeInstructions", out var insts))
        {
            int step = 1;
            if (insts.ValueKind == JsonValueKind.Array)
            {
                foreach (var inst in insts.EnumerateArray())
                {
                    string? t = null;
                    if (inst.ValueKind == JsonValueKind.String) t = inst.GetString();
                    else if (inst.ValueKind == JsonValueKind.Object)
                        t = GetStr(inst, "text", "name", "description");
                    if (!string.IsNullOrWhiteSpace(t))
                        recipe.Instructions.Add(new ParsedInstruction { Step = step++, Text = t });
                }
            }
            else if (insts.ValueKind == JsonValueKind.String)
            {
                var parts = (insts.GetString() ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < parts.Length; i++)
                {
                    var t = parts[i].Trim();
                    if (!string.IsNullOrWhiteSpace(t))
                        recipe.Instructions.Add(new ParsedInstruction { Step = i + 1, Text = t });
                }
            }
        }

        // Nutrition
        if (elem.TryGetProperty("nutrition", out var nut))
        {
            recipe.Nutrition = new ParsedNutrition
            {
                Calories = GetStr(nut, "calories"),
                Fat = GetStr(nut, "fatContent"),
                Carbohydrates = GetStr(nut, "carbohydrateContent"),
                Protein = GetStr(nut, "proteinContent"),
                Fiber = GetStr(nut, "fiberContent"),
                Sodium = GetStr(nut, "sodiumContent"),
                Sugar = GetStr(nut, "sugarContent"),
                Cholesterol = GetStr(nut, "cholesterolContent")
            };
        }

        return recipe;
    }

    private static string? GetAuthor(JsonElement elem)
    {
        if (!elem.TryGetProperty("author", out var auth)) return null;
        if (auth.ValueKind == JsonValueKind.String) return auth.GetString();
        if (auth.ValueKind == JsonValueKind.Object) return GetStr(auth, "name");
        return null;
    }

    private static string? GetStr(JsonElement elem, params string[] keys)
    {
        foreach (var key in keys)
            if (elem.TryGetProperty(key, out var val) && val.ValueKind == JsonValueKind.String)
                return val.GetString();
        return null;
    }
}
