using System.Text.RegularExpressions;
using ExpressRecipe.RecipeParser.Models;

namespace ExpressRecipe.RecipeParser.Mappers;

public static class RecipeHandoffMapper
{
    public static RecipeHandoffDto ToHandoffDto(this ParsedRecipe recipe)
    {
        var dto = new RecipeHandoffDto
        {
            Name = recipe.Title,
            Description = recipe.Description,
            PrepTimeMinutes = ParseTimeToMinutes(recipe.PrepTime),
            CookTimeMinutes = ParseTimeToMinutes(recipe.CookTime),
            Servings = ParseServings(recipe.Yield),
            Author = recipe.Author,
            Cuisine = recipe.Cuisine,
            SourceFormat = string.IsNullOrEmpty(recipe.Format) ? null : recipe.Format,
            SourceUrl = recipe.Url ?? recipe.Source,
            Tags = recipe.Tags.ToList(),
        };

        // Categories from Category string
        if (!string.IsNullOrEmpty(recipe.Category))
            dto.Categories = recipe.Category.Split(',').Select(c => c.Trim()).Where(c => c != "").ToList();

        foreach (var ing in recipe.Ingredients)
        {
            dto.Ingredients.Add(new HandoffIngredient
            {
                Name = ing.Name,
                Quantity = ParseQuantity(ing.Quantity),
                Unit = ing.Unit ?? "",
                Notes = ing.Preparation,
                IsOptional = ing.IsOptional
            });
        }

        foreach (var inst in recipe.Instructions)
        {
            dto.Instructions.Add(new HandoffInstruction
            {
                StepNumber = inst.Step,
                Instruction = inst.Text,
                TimeMinutes = ParseTimeToMinutes(inst.TimerText)
            });
        }

        if (recipe.Nutrition != null)
        {
            dto.Nutrition = new HandoffNutrition
            {
                Calories = ParseNutInt(recipe.Nutrition.Calories),
                Protein = ParseNutDecimal(recipe.Nutrition.Protein),
                Carbs = ParseNutDecimal(recipe.Nutrition.Carbohydrates),
                Fat = ParseNutDecimal(recipe.Nutrition.Fat),
                Fiber = ParseNutDecimal(recipe.Nutrition.Fiber),
                Sugar = ParseNutDecimal(recipe.Nutrition.Sugar)
            };
        }

        return dto;
    }

    internal static int? ParseTimeToMinutes(string? time)
    {
        if (string.IsNullOrWhiteSpace(time)) return null;

        // ISO 8601: PT30M, PT1H30M, PT1H, P1DT2H
        var isoMatch = Regex.Match(time, @"P(?:(\d+)D)?T?(?:(\d+)H)?(?:(\d+)M)?(?:(\d+)S)?$", RegexOptions.IgnoreCase);
        if (isoMatch.Success && (isoMatch.Groups[1].Success || isoMatch.Groups[2].Success || isoMatch.Groups[3].Success || isoMatch.Groups[4].Success))
        {
            int days = isoMatch.Groups[1].Success ? int.Parse(isoMatch.Groups[1].Value) : 0;
            int hours = isoMatch.Groups[2].Success ? int.Parse(isoMatch.Groups[2].Value) : 0;
            int mins = isoMatch.Groups[3].Success ? int.Parse(isoMatch.Groups[3].Value) : 0;
            int secs = isoMatch.Groups[4].Success ? int.Parse(isoMatch.Groups[4].Value) : 0;
            return days * 1440 + hours * 60 + mins + (secs / 60);
        }

        int total = 0;
        var hourMatch = Regex.Match(time, @"(\d+)\s*(?:hours?|hr|h)\b", RegexOptions.IgnoreCase);
        var minMatch = Regex.Match(time, @"(\d+)\s*(?:minutes?|min|m)\b", RegexOptions.IgnoreCase);
        if (hourMatch.Success) total += int.Parse(hourMatch.Groups[1].Value) * 60;
        if (minMatch.Success) total += int.Parse(minMatch.Groups[1].Value);

        if (total > 0) return total;

        // Bare number: assume minutes
        if (int.TryParse(time.Trim(), out int bare)) return bare;

        return null;
    }

    internal static int ParseServings(string? yield)
    {
        if (string.IsNullOrWhiteSpace(yield)) return 1;
        var match = Regex.Match(yield, @"\d+");
        return match.Success ? int.Parse(match.Value) : 1;
    }

    internal static decimal ParseQuantity(string? qty)
    {
        if (string.IsNullOrWhiteSpace(qty)) return 0m;
        qty = qty.Trim();

        // "1 1/2"
        var mixedMatch = Regex.Match(qty, @"^(\d+)\s+(\d+)/(\d+)$");
        if (mixedMatch.Success)
        {
            int whole = int.Parse(mixedMatch.Groups[1].Value);
            int num = int.Parse(mixedMatch.Groups[2].Value);
            int den = int.Parse(mixedMatch.Groups[3].Value);
            return whole + (decimal)num / den;
        }

        // "1/2"
        var fracMatch = Regex.Match(qty, @"^(\d+)/(\d+)$");
        if (fracMatch.Success)
        {
            int num = int.Parse(fracMatch.Groups[1].Value);
            int den = int.Parse(fracMatch.Groups[2].Value);
            return (decimal)num / den;
        }

        if (decimal.TryParse(qty, out var d)) return d;
        return 0m;
    }

    private static int? ParseNutInt(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return null;
        var match = Regex.Match(val, @"\d+");
        return match.Success ? int.Parse(match.Value) : null;
    }

    private static decimal? ParseNutDecimal(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return null;
        var match = Regex.Match(val, @"\d+(\.\d+)?");
        return match.Success ? decimal.Parse(match.Value) : null;
    }
}
