namespace ExpressRecipe.Shared.Units;
public static class UnitFormatter
{
    public static string Format(decimal canonicalAmount, UnitCode canonicalUnit, UnitSystemPreference preference)
    {
        UnitCode displayUnit = canonicalUnit switch
        {
            UnitCode.Gram       => preference == UnitSystemPreference.US ? UnitCode.Ounce : UnitCode.Gram,
            UnitCode.Milliliter => preference == UnitSystemPreference.US ? UnitCode.Cup : UnitCode.Milliliter,
            UnitCode.Celsius    => preference == UnitSystemPreference.US ? UnitCode.Fahrenheit : UnitCode.Celsius,
            _                   => canonicalUnit
        };
        ConversionResult result = UnitConverter.FromCanonical(canonicalAmount, canonicalUnit, displayUnit);
        if (!result.Success) { return $"{canonicalAmount} {canonicalUnit}"; }
        return $"{result.Value:G4} {UnitCodeToLabel(displayUnit)}";
    }

    private static string UnitCodeToLabel(UnitCode code) => code switch
    {
        UnitCode.Gram       => "g",
        UnitCode.Kilogram   => "kg",
        UnitCode.Ounce      => "oz",
        UnitCode.Pound      => "lb",
        UnitCode.Milliliter => "ml",
        UnitCode.Liter      => "L",
        UnitCode.Teaspoon   => "tsp",
        UnitCode.Tablespoon => "tbsp",
        UnitCode.Cup        => "cup",
        UnitCode.FluidOunce => "fl oz",
        UnitCode.Celsius    => "°C",
        UnitCode.Fahrenheit => "°F",
        UnitCode.GasMark    => "Gas Mark",
        UnitCode.Each       => "",
        _                   => code.ToString()
    };
}
