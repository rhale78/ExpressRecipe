using ExpressRecipe.Shared.DTOs.Recipe;

namespace ExpressRecipe.RecipeService.Services;

/// <summary>
/// Service for shopping list integration from recipes
/// Handles ingredient-to-product matching and package optimization
/// </summary>
public class ShoppingListIntegrationService
{
    /// <summary>
    /// Prepare recipe ingredients for shopping list
    /// </summary>
    public ShoppingListPreparationDto PrepareRecipeForShopping(
        RecipeDto recipe,
        List<RecipeIngredientDto> ingredients,
        int? customServings = null)
    {
        var servings = customServings ?? recipe.Servings ?? 4;
        var scaleFactor = servings / (decimal)(recipe.Servings ?? 4);

        var result = new ShoppingListPreparationDto
        {
            RecipeId = recipe.Id,
            RecipeName = recipe.Name,
            Servings = servings,
            ScaleFactor = scaleFactor,
            Items = new List<ShoppingListItemDto>()
        };

        foreach (var ingredient in ingredients)
        {
            var item = new ShoppingListItemDto
            {
                IngredientId = ingredient.IngredientId,
                IngredientName = ingredient.IngredientName ?? "Unknown",
                OriginalQuantity = ingredient.Quantity ?? 0,
                OriginalUnit = ingredient.Unit ?? "",
                ScaledQuantity = (ingredient.Quantity ?? 0) * scaleFactor,
                Unit = ingredient.Unit ?? "",
                IsOptional = ingredient.IsOptional,
                Notes = ingredient.PreparationNote
            };

            // Normalize to standard units for product matching
            item.NormalizedQuantity = NormalizeQuantity(item.ScaledQuantity, item.Unit, out var normalizedUnit);
            item.NormalizedUnit = normalizedUnit;

            result.Items.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Normalize quantity to standard units for product matching
    /// Converts to base units (oz, g, ml, etc.)
    /// </summary>
    private decimal NormalizeQuantity(decimal quantity, string unit, out string normalizedUnit)
    {
        unit = unit.ToLower().Trim();

        // Volume conversions to fl oz
        switch (unit)
        {
            case "cup":
            case "cups":
                normalizedUnit = "fl oz";
                return quantity * 8m; // 1 cup = 8 fl oz

            case "tbsp":
            case "tablespoon":
            case "tablespoons":
                normalizedUnit = "fl oz";
                return quantity * 0.5m; // 1 tbsp = 0.5 fl oz

            case "tsp":
            case "teaspoon":
            case "teaspoons":
                normalizedUnit = "fl oz";
                return quantity * 0.166667m; // 1 tsp = ~0.167 fl oz

            case "pint":
            case "pints":
            case "pt":
                normalizedUnit = "fl oz";
                return quantity * 16m; // 1 pint = 16 fl oz

            case "quart":
            case "quarts":
            case "qt":
                normalizedUnit = "fl oz";
                return quantity * 32m; // 1 quart = 32 fl oz

            case "gallon":
            case "gallons":
            case "gal":
                normalizedUnit = "fl oz";
                return quantity * 128m; // 1 gallon = 128 fl oz

            case "ml":
            case "milliliter":
            case "milliliters":
                normalizedUnit = "ml";
                return quantity;

            case "l":
            case "liter":
            case "liters":
                normalizedUnit = "ml";
                return quantity * 1000m; // 1 liter = 1000 ml

            // Weight conversions to oz
            case "lb":
            case "lbs":
            case "pound":
            case "pounds":
                normalizedUnit = "oz";
                return quantity * 16m; // 1 lb = 16 oz

            case "oz":
            case "ounce":
            case "ounces":
                normalizedUnit = "oz";
                return quantity;

            case "g":
            case "gram":
            case "grams":
                normalizedUnit = "g";
                return quantity;

            case "kg":
            case "kilogram":
            case "kilograms":
                normalizedUnit = "g";
                return quantity * 1000m; // 1 kg = 1000 g

            // Count-based (no conversion)
            case "piece":
            case "pieces":
            case "whole":
            case "clove":
            case "cloves":
            case "slice":
            case "slices":
            case "each":
                normalizedUnit = "count";
                return quantity;

            // Can't normalize
            case "pinch":
            case "dash":
            case "to taste":
                normalizedUnit = "special";
                return 0;

            default:
                normalizedUnit = unit;
                return quantity;
        }
    }

    /// <summary>
    /// Optimize package sizes for shopping
    /// Given needed amount and available package sizes, find the best match
    /// </summary>
    public PackageOptimizationDto OptimizePackageSize(
        decimal neededQuantity,
        string unit,
        List<AvailablePackageDto> availablePackages)
    {
        if (!availablePackages.Any())
        {
            return new PackageOptimizationDto
            {
                NeededQuantity = neededQuantity,
                Unit = unit,
                RecommendedPackage = null,
                Reason = "No packages available"
            };
        }

        // Find packages of the same unit
        var matchingPackages = availablePackages
            .Where(p => p.Unit.Equals(unit, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Quantity)
            .ToList();

        if (!matchingPackages.Any())
        {
            return new PackageOptimizationDto
            {
                NeededQuantity = neededQuantity,
                Unit = unit,
                RecommendedPackage = null,
                Reason = "No packages available in matching unit"
            };
        }

        // Find smallest package that meets the need
        var smallestSufficient = matchingPackages.FirstOrDefault(p => p.Quantity >= neededQuantity);

        if (smallestSufficient != null)
        {
            return new PackageOptimizationDto
            {
                NeededQuantity = neededQuantity,
                Unit = unit,
                RecommendedPackage = smallestSufficient,
                Reason = $"Smallest package that meets requirement ({smallestSufficient.Quantity} {smallestSufficient.Unit})",
                QuantityExcess = smallestSufficient.Quantity - neededQuantity
            };
        }

        // Need multiple packages - find most efficient combination
        var largestPackage = matchingPackages.Last();
        var packagesNeeded = (int)Math.Ceiling(neededQuantity / largestPackage.Quantity);

        return new PackageOptimizationDto
        {
            NeededQuantity = neededQuantity,
            Unit = unit,
            RecommendedPackage = largestPackage,
            PackageCount = packagesNeeded,
            Reason = $"Need {packagesNeeded} packages of {largestPackage.Quantity} {largestPackage.Unit}",
            QuantityExcess = (largestPackage.Quantity * packagesNeeded) - neededQuantity
        };
    }
}

/// <summary>
/// Shopping list preparation result
/// </summary>
public class ShoppingListPreparationDto
{
    public Guid RecipeId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public int Servings { get; set; }
    public decimal ScaleFactor { get; set; }
    public List<ShoppingListItemDto> Items { get; set; } = new();
}

/// <summary>
/// Shopping list item from recipe
/// </summary>
public class ShoppingListItemDto
{
    public Guid? IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public decimal OriginalQuantity { get; set; }
    public string OriginalUnit { get; set; } = string.Empty;
    public decimal ScaledQuantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal NormalizedQuantity { get; set; }
    public string NormalizedUnit { get; set; } = string.Empty;
    public bool IsOptional { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Available product package size
/// </summary>
public class AvailablePackageDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal? Price { get; set; }
}

/// <summary>
/// Package size optimization result
/// </summary>
public class PackageOptimizationDto
{
    public decimal NeededQuantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public AvailablePackageDto? RecommendedPackage { get; set; }
    public int PackageCount { get; set; } = 1;
    public decimal QuantityExcess { get; set; }
    public string Reason { get; set; } = string.Empty;
}
