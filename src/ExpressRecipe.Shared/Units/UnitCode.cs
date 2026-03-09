namespace ExpressRecipe.Shared.Units;
public enum UnitCode
{
    // Mass — canonical: gram
    Gram, Kilogram, Milligram, Ounce, Pound,
    // Volume — canonical: milliliter
    Milliliter, Liter,
    Teaspoon, UkTeaspoon, Tablespoon, UkTablespoon,
    FluidOunce, UkFluidOunce, Cup, UkCup, MetricCup,
    UsPint, UkPint, UsQuart, UkQuart, UsGallon, UkGallon,
    // Temperature — canonical: Celsius
    Celsius, Fahrenheit, GasMark,
    // Count
    Each,
    // Uncountable
    Pinch, Dash, Smidgen, ToTaste, Handful, Sprig, Clove, Slice, Bunch,
    Unknown
}
