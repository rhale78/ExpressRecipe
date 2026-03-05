using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Exporters;

public sealed class JsonLdRecipeExporter : IRecipeExporter
{
    public string FormatName => "GoogleStructuredData";
    public string DefaultFileExtension => "json";

    public string Export(ParsedRecipe recipe)
    {
        var obj = BuildRecipeObject(recipe);
        return JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
    }

    public string Export(ParsedRecipe recipe, RecipeExportOptions? options)
    {
        var obj = BuildRecipeObject(recipe);
        return JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = options?.PrettyPrint != false });
    }

    public string ExportAll(IEnumerable<ParsedRecipe> recipes)
    {
        var graph = recipes.Select(r => BuildRecipeObject(r)).ToList();
        var root = new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org",
            ["@graph"] = graph
        };
        return JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true });
    }

    public string ExportAll(IEnumerable<ParsedRecipe> recipes, RecipeExportOptions? options)
    {
        var graph = recipes.Select(r => BuildRecipeObject(r)).ToList();
        var root = new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org",
            ["@graph"] = graph
        };
        return JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = options?.PrettyPrint != false });
    }

    private static Dictionary<string, object?> BuildRecipeObject(ParsedRecipe recipe)
    {
        var obj = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Recipe",
            ["name"] = recipe.Title
        };

        if (!string.IsNullOrEmpty(recipe.Description)) obj["description"] = recipe.Description;
        if (!string.IsNullOrEmpty(recipe.Author)) obj["author"] = new Dictionary<string, string> { ["@type"] = "Person", ["name"] = recipe.Author };
        if (!string.IsNullOrEmpty(recipe.Yield)) obj["recipeYield"] = recipe.Yield;
        if (!string.IsNullOrEmpty(recipe.PrepTime)) obj["prepTime"] = ToIsoDuration(recipe.PrepTime);
        if (!string.IsNullOrEmpty(recipe.CookTime)) obj["cookTime"] = ToIsoDuration(recipe.CookTime);
        if (!string.IsNullOrEmpty(recipe.TotalTime)) obj["totalTime"] = ToIsoDuration(recipe.TotalTime);
        if (!string.IsNullOrEmpty(recipe.Category)) obj["recipeCategory"] = recipe.Category;
        if (!string.IsNullOrEmpty(recipe.Cuisine)) obj["recipeCuisine"] = recipe.Cuisine;
        if (!string.IsNullOrEmpty(recipe.Url)) obj["url"] = recipe.Url;

        if (recipe.Tags.Count > 0) obj["keywords"] = string.Join(", ", recipe.Tags);

        if (recipe.Ingredients.Count > 0)
            obj["recipeIngredient"] = recipe.Ingredients.Select(FormatIngredient).ToArray();

        if (recipe.Instructions.Count > 0)
            obj["recipeInstructions"] = recipe.Instructions
                .Select(i => new Dictionary<string, string> { ["@type"] = "HowToStep", ["text"] = i.Text })
                .ToArray();

        if (recipe.Nutrition != null)
        {
            var nut = new Dictionary<string, string?> { ["@type"] = "NutritionInformation" };
            if (recipe.Nutrition.Calories != null) nut["calories"] = recipe.Nutrition.Calories;
            if (recipe.Nutrition.Fat != null) nut["fatContent"] = recipe.Nutrition.Fat;
            if (recipe.Nutrition.Carbohydrates != null) nut["carbohydrateContent"] = recipe.Nutrition.Carbohydrates;
            if (recipe.Nutrition.Protein != null) nut["proteinContent"] = recipe.Nutrition.Protein;
            if (recipe.Nutrition.Fiber != null) nut["fiberContent"] = recipe.Nutrition.Fiber;
            if (recipe.Nutrition.Sugar != null) nut["sugarContent"] = recipe.Nutrition.Sugar;
            if (recipe.Nutrition.Sodium != null) nut["sodiumContent"] = recipe.Nutrition.Sodium;
            if (recipe.Nutrition.Cholesterol != null) nut["cholesterolContent"] = recipe.Nutrition.Cholesterol;
            obj["nutrition"] = nut;
        }

        return obj;
    }

    internal static string ToIsoDuration(string time)
    {
        if (string.IsNullOrEmpty(time)) return time;
        // Already ISO
        if (Regex.IsMatch(time, @"^P", RegexOptions.IgnoreCase)) return time;

        int totalMinutes = 0;
        // "1 hour 30 min", "1h 30m", "30 min", "45 minutes", "2 hours"
        var hourMatch = Regex.Match(time, @"(\d+)\s*(?:hours?|hr|h)\b", RegexOptions.IgnoreCase);
        var minMatch = Regex.Match(time, @"(\d+)\s*(?:minutes?|min|m)\b", RegexOptions.IgnoreCase);

        if (hourMatch.Success) totalMinutes += int.Parse(hourMatch.Groups[1].Value) * 60;
        if (minMatch.Success) totalMinutes += int.Parse(minMatch.Groups[1].Value);

        if (totalMinutes == 0 && int.TryParse(time.Trim(), out int raw)) totalMinutes = raw;

        if (totalMinutes == 0) return time;

        int hours = totalMinutes / 60;
        int mins = totalMinutes % 60;

        if (hours > 0 && mins > 0) return $"PT{hours}H{mins}M";
        if (hours > 0) return $"PT{hours}H";
        return $"PT{mins}M";
    }

    private static string FormatIngredient(ParsedIngredient ing)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(ing.Quantity)) sb.Append(ing.Quantity).Append(' ');
        if (!string.IsNullOrEmpty(ing.Unit)) sb.Append(ing.Unit).Append(' ');
        sb.Append(ing.Name);
        if (!string.IsNullOrEmpty(ing.Preparation)) sb.Append(", ").Append(ing.Preparation);
        return sb.ToString().Trim();
    }
}
