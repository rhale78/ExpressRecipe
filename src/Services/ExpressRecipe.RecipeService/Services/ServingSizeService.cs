using ExpressRecipe.Shared.DTOs.Recipe;
using System.Text.RegularExpressions;

namespace ExpressRecipe.RecipeService.Services;

/// <summary>
/// Service for calculating scaled recipe servings and ingredient quantities
/// </summary>
public class ServingSizeService
{
    /// <summary>
    /// Scale a recipe to a new serving size
    /// </summary>
    public ScaledRecipeDto ScaleRecipe(RecipeDto recipe, int newServings)
    {
        if (recipe == null)
            throw new ArgumentNullException(nameof(recipe));

        if (newServings <= 0)
            throw new ArgumentException("New servings must be greater than zero", nameof(newServings));

        var originalServings = recipe.Servings ?? 1;
        if (originalServings <= 0)
            originalServings = 1;

        var scaleFactor = (decimal)newServings / originalServings;

        var scaledRecipe = new ScaledRecipeDto
        {
            OriginalRecipe = recipe,
            OriginalServings = originalServings,
            NewServings = newServings,
            ScaleFactor = scaleFactor,
            ScaledIngredients = new List<ScaledIngredientDto>()
        };

        // Scale ingredients
        if (recipe.Ingredients != null)
        {
            foreach (var ingredient in recipe.Ingredients)
            {
                scaledRecipe.ScaledIngredients.Add(ScaleIngredient(ingredient, scaleFactor));
            }
        }

        return scaledRecipe;
    }

    /// <summary>
    /// Scale a single ingredient
    /// </summary>
    private ScaledIngredientDto ScaleIngredient(RecipeIngredientDto ingredient, decimal scaleFactor)
    {
        var scaled = new ScaledIngredientDto
        {
            OriginalIngredient = ingredient,
            Name = ingredient.IngredientName ?? string.Empty,
            Unit = ingredient.Unit ?? string.Empty,
            OriginalQuantity = ingredient.Quantity ?? 0,
            ScaleFactor = scaleFactor
        };

        if (ingredient.Quantity.HasValue && ingredient.Quantity.Value > 0)
        {
            var scaledQuantity = ingredient.Quantity.Value * scaleFactor;
            scaled.ScaledQuantity = scaledQuantity;

            // Format the scaled quantity nicely
            scaled.DisplayQuantity = FormatQuantity(scaledQuantity);
            scaled.FractionalDisplay = ConvertToFraction(scaledQuantity);
        }
        else
        {
            scaled.DisplayQuantity = string.Empty;
            scaled.FractionalDisplay = string.Empty;
        }

        return scaled;
    }

    /// <summary>
    /// Format quantity to a readable string
    /// </summary>
    private string FormatQuantity(decimal quantity)
    {
        // Round to reasonable precision
        if (quantity >= 10)
        {
            // For large quantities, round to 1 decimal place
            return Math.Round(quantity, 1).ToString("0.#");
        }
        else
        {
            // For small quantities, round to 2 decimal places
            return Math.Round(quantity, 2).ToString("0.##");
        }
    }

    /// <summary>
    /// Convert decimal quantity to mixed fraction format (e.g., 2.5 → "2 1/2")
    /// </summary>
    private string ConvertToFraction(decimal quantity)
    {
        if (quantity == 0)
            return "0";

        var wholePart = (int)Math.Floor(quantity);
        var fractionalPart = quantity - wholePart;

        // Common fractions
        var fractions = new Dictionary<decimal, string>
        {
            { 0.125m, "1/8" },
            { 0.25m, "1/4" },
            { 0.333m, "1/3" },
            { 0.375m, "3/8" },
            { 0.5m, "1/2" },
            { 0.625m, "5/8" },
            { 0.666m, "2/3" },
            { 0.75m, "3/4" },
            { 0.875m, "7/8" }
        };

        // Find closest fraction
        string? fractionStr = null;
        decimal closestDiff = 0.05m; // Tolerance

        foreach (var kvp in fractions)
        {
            var diff = Math.Abs(fractionalPart - kvp.Key);
            if (diff < closestDiff)
            {
                closestDiff = diff;
                fractionStr = kvp.Value;
            }
        }

        // Build result
        if (wholePart > 0 && !string.IsNullOrEmpty(fractionStr))
        {
            return $"{wholePart} {fractionStr}";
        }
        else if (wholePart > 0)
        {
            return wholePart.ToString();
        }
        else if (!string.IsNullOrEmpty(fractionStr))
        {
            return fractionStr;
        }
        else
        {
            // No good fraction match, use decimal
            return FormatQuantity(quantity);
        }
    }

    /// <summary>
    /// Get serving size suggestions for a recipe
    /// </summary>
    public List<int> GetServingSizeSuggestions(int originalServings)
    {
        var suggestions = new List<int>();

        // Add the original
        suggestions.Add(originalServings);

        // Add halves and doubles
        if (originalServings >= 2)
        {
            suggestions.Add(originalServings / 2);
        }
        suggestions.Add(originalServings * 2);

        // Add common serving sizes
        var commonSizes = new[] { 1, 2, 4, 6, 8, 12, 16, 24 };
        foreach (var size in commonSizes)
        {
            if (!suggestions.Contains(size) && size != originalServings)
            {
                suggestions.Add(size);
            }
        }

        // Sort and return
        return suggestions.OrderBy(s => s).Distinct().ToList();
    }

    /// <summary>
    /// Calculate time adjustments for serving size changes
    /// Note: Cook time doesn't always scale linearly, this provides estimates
    /// </summary>
    public TimeAdjustmentDto AdjustTimings(int originalServings, int newServings, int? prepTime, int? cookTime)
    {
        var scaleFactor = (decimal)newServings / originalServings;

        return new TimeAdjustmentDto
        {
            OriginalPrepTime = prepTime,
            OriginalCookTime = cookTime,
            EstimatedPrepTime = prepTime.HasValue ? (int)Math.Ceiling(prepTime.Value * scaleFactor) : null,
            // Cook time typically doesn't scale linearly - apply a dampened factor
            EstimatedCookTime = cookTime.HasValue ? (int)Math.Ceiling(cookTime.Value * (1 + (scaleFactor - 1) * 0.3m)) : null,
            Note = scaleFactor > 2 ? "Cook times are estimates. Larger batches may need longer cooking time." :
                   scaleFactor < 0.5m ? "Cook times are estimates. Smaller batches may cook faster." :
                   null
        };
    }
}

/// <summary>
/// Scaled recipe with adjusted quantities
/// </summary>
public class ScaledRecipeDto
{
    public RecipeDto OriginalRecipe { get; set; } = null!;
    public int OriginalServings { get; set; }
    public int NewServings { get; set; }
    public decimal ScaleFactor { get; set; }
    public List<ScaledIngredientDto> ScaledIngredients { get; set; } = new();
    public TimeAdjustmentDto? TimeAdjustment { get; set; }
}

/// <summary>
/// Scaled ingredient with multiple display formats
/// </summary>
public class ScaledIngredientDto
{
    public RecipeIngredientDto OriginalIngredient { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal OriginalQuantity { get; set; }
    public decimal ScaledQuantity { get; set; }
    public decimal ScaleFactor { get; set; }
    
    /// <summary>
    /// Decimal display (e.g., "2.5")
    /// </summary>
    public string DisplayQuantity { get; set; } = string.Empty;
    
    /// <summary>
    /// Fractional display (e.g., "2 1/2")
    /// </summary>
    public string FractionalDisplay { get; set; } = string.Empty;
}

/// <summary>
/// Time adjustment information
/// </summary>
public class TimeAdjustmentDto
{
    public int? OriginalPrepTime { get; set; }
    public int? OriginalCookTime { get; set; }
    public int? EstimatedPrepTime { get; set; }
    public int? EstimatedCookTime { get; set; }
    public string? Note { get; set; }
}
