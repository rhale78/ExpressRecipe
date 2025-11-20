using ExpressRecipe.RecipeService.Parsers;
using ExpressRecipe.Shared.DTOs.Recipe;
using System.Text.RegularExpressions;

namespace ExpressRecipe.RecipeService.Services;

/// <summary>
/// Service for extracting and estimating nutrition data from parsed recipes
/// </summary>
public class NutritionExtractionService
{
    private readonly ILogger<NutritionExtractionService> _logger;

    public NutritionExtractionService(ILogger<NutritionExtractionService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Extract nutrition data from parsed recipe
    /// Looks for nutrition info in description, notes, or explicit nutrition fields
    /// </summary>
    public RecipeNutritionDto? ExtractNutrition(ParsedRecipe recipe)
    {
        var nutrition = new RecipeNutritionDto
        {
            RecipeId = Guid.Empty, // Will be set when saved
            ServingSize = recipe.Servings > 0 ? $"1/{recipe.Servings} of recipe" : null
        };

        bool hasAnyData = false;

        // Try to extract from nutrition field if present
        if (!string.IsNullOrWhiteSpace(recipe.Nutrition))
        {
            hasAnyData = ExtractFromNutritionField(recipe.Nutrition, nutrition);
        }

        // Try to extract from description
        if (!hasAnyData && !string.IsNullOrWhiteSpace(recipe.Description))
        {
            hasAnyData = ExtractFromText(recipe.Description, nutrition);
        }

        // Try to extract from notes
        if (!hasAnyData && !string.IsNullOrWhiteSpace(recipe.Notes))
        {
            hasAnyData = ExtractFromText(recipe.Notes, nutrition);
        }

        return hasAnyData ? nutrition : null;
    }

    /// <summary>
    /// Extract nutrition from structured nutrition field
    /// </summary>
    private bool ExtractFromNutritionField(string nutritionText, RecipeNutritionDto nutrition)
    {
        bool found = false;

        // Calories
        var caloriesMatch = Regex.Match(nutritionText, @"(\d+(?:\.\d+)?)\s*(?:cal|kcal|calories)", RegexOptions.IgnoreCase);
        if (caloriesMatch.Success && decimal.TryParse(caloriesMatch.Groups[1].Value, out var calories))
        {
            nutrition.Calories = calories;
            found = true;
        }

        // Fat
        var fatMatch = Regex.Match(nutritionText, @"(?:total\s+)?fat[:\s]+(\d+(?:\.\d+)?)\s*g", RegexOptions.IgnoreCase);
        if (fatMatch.Success && decimal.TryParse(fatMatch.Groups[1].Value, out var fat))
        {
            nutrition.TotalFat = fat;
            found = true;
        }

        // Saturated Fat
        var satFatMatch = Regex.Match(nutritionText, @"saturated\s+fat[:\s]+(\d+(?:\.\d+)?)\s*g", RegexOptions.IgnoreCase);
        if (satFatMatch.Success && decimal.TryParse(satFatMatch.Groups[1].Value, out var satFat))
        {
            nutrition.SaturatedFat = satFat;
            found = true;
        }

        // Cholesterol
        var cholMatch = Regex.Match(nutritionText, @"cholesterol[:\s]+(\d+(?:\.\d+)?)\s*mg", RegexOptions.IgnoreCase);
        if (cholMatch.Success && decimal.TryParse(cholMatch.Groups[1].Value, out var cholesterol))
        {
            nutrition.Cholesterol = cholesterol;
            found = true;
        }

        // Sodium
        var sodiumMatch = Regex.Match(nutritionText, @"sodium[:\s]+(\d+(?:\.\d+)?)\s*mg", RegexOptions.IgnoreCase);
        if (sodiumMatch.Success && decimal.TryParse(sodiumMatch.Groups[1].Value, out var sodium))
        {
            nutrition.Sodium = sodium;
            found = true;
        }

        // Carbohydrates
        var carbMatch = Regex.Match(nutritionText, @"(?:total\s+)?carb(?:ohydrate)?s?[:\s]+(\d+(?:\.\d+)?)\s*g", RegexOptions.IgnoreCase);
        if (carbMatch.Success && decimal.TryParse(carbMatch.Groups[1].Value, out var carbs))
        {
            nutrition.TotalCarbohydrates = carbs;
            found = true;
        }

        // Fiber
        var fiberMatch = Regex.Match(nutritionText, @"(?:dietary\s+)?fiber[:\s]+(\d+(?:\.\d+)?)\s*g", RegexOptions.IgnoreCase);
        if (fiberMatch.Success && decimal.TryParse(fiberMatch.Groups[1].Value, out var fiber))
        {
            nutrition.DietaryFiber = fiber;
            found = true;
        }

        // Sugar
        var sugarMatch = Regex.Match(nutritionText, @"(?:total\s+)?sugar(?:s)?[:\s]+(\d+(?:\.\d+)?)\s*g", RegexOptions.IgnoreCase);
        if (sugarMatch.Success && decimal.TryParse(sugarMatch.Groups[1].Value, out var sugar))
        {
            nutrition.Sugars = sugar;
            found = true;
        }

        // Protein
        var proteinMatch = Regex.Match(nutritionText, @"protein[:\s]+(\d+(?:\.\d+)?)\s*g", RegexOptions.IgnoreCase);
        if (proteinMatch.Success && decimal.TryParse(proteinMatch.Groups[1].Value, out var protein))
        {
            nutrition.Protein = protein;
            found = true;
        }

        return found;
    }

    /// <summary>
    /// Extract nutrition from free-form text (description/notes)
    /// </summary>
    private bool ExtractFromText(string text, RecipeNutritionDto nutrition)
    {
        // Look for common patterns like "Per serving: 250 calories, 10g fat, 30g carbs"
        var perServingMatch = Regex.Match(text,
            @"per\s+serving[:\s]+.*?(\d+)\s*(?:cal|kcal|calories)",
            RegexOptions.IgnoreCase);

        if (perServingMatch.Success && decimal.TryParse(perServingMatch.Groups[1].Value, out var calories))
        {
            nutrition.Calories = calories;

            // Try to extract other values from the same line
            var line = perServingMatch.Value;
            ExtractFromNutritionField(line, nutrition);

            return true;
        }

        return false;
    }

    /// <summary>
    /// Estimate basic nutrition from ingredients (very rough approximation)
    /// This is a placeholder for more sophisticated nutrition calculation
    /// </summary>
    public RecipeNutritionDto? EstimateNutrition(ParsedRecipe recipe)
    {
        // In a full implementation, this would:
        // 1. Look up each ingredient in a nutrition database
        // 2. Calculate based on quantities
        // 3. Sum up the totals
        // 4. Divide by servings

        // For now, return null (indicating no estimation available)
        // Future enhancement: integrate with USDA FoodData Central API

        _logger.LogDebug("Nutrition estimation not yet implemented for recipe: {RecipeName}", recipe.Name);
        return null;
    }
}
