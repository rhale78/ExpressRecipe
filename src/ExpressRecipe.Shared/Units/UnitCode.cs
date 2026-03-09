namespace ExpressRecipe.Shared.Units;

/// <summary>
/// Canonical unit codes used throughout the application.
/// </summary>
public enum UnitCode
{
    Unknown = 0,

    // Volume - US
    Teaspoon,
    Tablespoon,
    FluidOunce,
    Cup,
    Pint,
    Quart,
    Gallon,

    // Volume - Metric
    Milliliter,
    Liter,

    // Weight - US
    Ounce,
    Pound,

    // Weight - Metric
    Gram,
    Kilogram,

    // Count / uncountable
    Whole,
    Pinch,
    Dash,
    Drop,
    Clove,
    Slice,
    Piece,
    Bunch,
    Head,
    Stick,
    Package,
    Can,
    Sprig
}
