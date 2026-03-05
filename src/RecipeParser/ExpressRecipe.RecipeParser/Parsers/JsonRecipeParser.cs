using System.Text.Json;
using ExpressRecipe.RecipeParser.Helpers;
using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Parsers;

public sealed class JsonRecipeParser : IRecipeParser
{
    public string FormatName => "Json";

    public bool CanParse(string text, string? fileExtension = null)
    {
        if (fileExtension?.EndsWith("json", StringComparison.OrdinalIgnoreCase) == true) return true;
        var t = text.TrimStart();
        return t.StartsWith("{") || t.StartsWith("[");
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
                    try
                    {
                        var recipe = ParseElement(elem, options);
                        result.Recipes.Add(recipe);
                    }
                    catch (Exception ex)
                    {
                        LoggingHelper.LogRecipeError(null, errors, i, null, "Failed to parse JSON recipe element", ex);
                    }
                    i++;
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("recipes", out var recipesArr) && recipesArr.ValueKind == JsonValueKind.Array)
                {
                    int i = 0;
                    foreach (var elem in recipesArr.EnumerateArray())
                    {
                        try { result.Recipes.Add(ParseElement(elem, options)); }
                        catch (Exception ex) { LoggingHelper.LogRecipeError(null, errors, i, null, "Failed to parse recipe", ex); }
                        i++;
                    }
                }
                else
                {
                    result.Recipes.Add(ParseElement(root, options));
                }
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            LoggingHelper.LogBatchError(null, errors, "Failed to parse JSON", ex);
        }

        result.Errors = errors;
        return result;
    }

    private static ParsedRecipe ParseElement(JsonElement elem, RecipeParseOptions? options)
    {
        var recipe = new ParsedRecipe { Format = "Json" };

        recipe.Title = GetStr(elem, "title", "name", "Title", "Name") ?? "";
        recipe.Description = GetStr(elem, "description", "Description", "notes", "Notes");
        recipe.Source = GetStr(elem, "source", "Source", "sourceName");
        recipe.Author = GetStr(elem, "author", "Author", "authorName");
        recipe.Url = GetStr(elem, "url", "Url", "link", "sourceUrl");
        recipe.Yield = GetStr(elem, "yield", "servings", "Servings", "serves");
        recipe.PrepTime = GetStr(elem, "prepTime", "prep_time", "PrepTime");
        recipe.CookTime = GetStr(elem, "cookTime", "cook_time", "CookTime");
        recipe.TotalTime = GetStr(elem, "totalTime", "total_time", "TotalTime");
        recipe.Category = GetStr(elem, "category", "Category", "categories");
        recipe.Cuisine = GetStr(elem, "cuisine", "Cuisine");

        if (elem.TryGetProperty("tags", out var tagsElem) || elem.TryGetProperty("Tags", out tagsElem))
        {
            if (tagsElem.ValueKind == JsonValueKind.Array)
                recipe.Tags = tagsElem.EnumerateArray().Select(t => t.GetString() ?? "").Where(t => t != "").ToList();
            else if (tagsElem.ValueKind == JsonValueKind.String)
                recipe.Tags = (tagsElem.GetString() ?? "").Split(',').Select(t => t.Trim()).Where(t => t != "").ToList();
        }

        if (elem.TryGetProperty("ingredients", out var ingsElem) || elem.TryGetProperty("Ingredients", out ingsElem))
        {
            if (ingsElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var ing in ingsElem.EnumerateArray())
                    recipe.Ingredients.Add(ParseIngredient(ing));
            }
        }

        if (elem.TryGetProperty("instructions", out var instElem) || elem.TryGetProperty("Instructions", out instElem)
            || elem.TryGetProperty("directions", out instElem) || elem.TryGetProperty("Directions", out instElem))
        {
            if (instElem.ValueKind == JsonValueKind.Array)
            {
                int step = 1;
                foreach (var inst in instElem.EnumerateArray())
                {
                    string? stepText = inst.ValueKind == JsonValueKind.String
                        ? inst.GetString()
                        : GetStr(inst, "text", "description", "step");
                    if (!string.IsNullOrWhiteSpace(stepText))
                        recipe.Instructions.Add(new ParsedInstruction { Step = step++, Text = stepText });
                }
            }
            else if (instElem.ValueKind == JsonValueKind.String)
            {
                var instText = instElem.GetString() ?? "";
                var parts = instText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < parts.Length; i++)
                    recipe.Instructions.Add(new ParsedInstruction { Step = i + 1, Text = parts[i].Trim() });
            }
        }

        if (elem.TryGetProperty("nutrition", out var nutElem) || elem.TryGetProperty("Nutrition", out nutElem))
        {
            recipe.Nutrition = new ParsedNutrition
            {
                Calories = GetStr(nutElem, "calories", "Calories"),
                Fat = GetStr(nutElem, "fat", "Fat", "fatContent"),
                Carbohydrates = GetStr(nutElem, "carbohydrates", "Carbohydrates", "carbohydrateContent"),
                Protein = GetStr(nutElem, "protein", "Protein", "proteinContent"),
                Fiber = GetStr(nutElem, "fiber", "Fiber", "fiberContent"),
                Sodium = GetStr(nutElem, "sodium", "Sodium", "sodiumContent"),
                Sugar = GetStr(nutElem, "sugar", "Sugar", "sugarContent"),
                Cholesterol = GetStr(nutElem, "cholesterol", "Cholesterol", "cholesterolContent")
            };
        }

        return recipe;
    }

    private static ParsedIngredient ParseIngredient(JsonElement elem)
    {
        if (elem.ValueKind == JsonValueKind.String)
        {
            var line = elem.GetString() ?? "";
            var (qty, unit, name) = TextParserHelper.ParseIngredientLine(line);
            string n = name;
            string p = TextParserHelper.ExtractPreparation(ref n);
            return new ParsedIngredient { Quantity = qty, Unit = unit, Name = n, Preparation = string.IsNullOrEmpty(p) ? null : p };
        }

        return new ParsedIngredient
        {
            Quantity = GetStr(elem, "quantity", "amount", "qty", "Quantity", "Amount"),
            Unit = GetStr(elem, "unit", "Unit", "measure"),
            Name = GetStr(elem, "name", "ingredient", "Name", "item") ?? "",
            Preparation = GetStr(elem, "preparation", "prep", "Preparation", "notes")
        };
    }

    private static string? GetStr(JsonElement elem, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (elem.TryGetProperty(key, out var val))
            {
                if (val.ValueKind == JsonValueKind.String) return val.GetString();
                if (val.ValueKind == JsonValueKind.Number) return val.GetRawText();
            }
        }
        return null;
    }
}
