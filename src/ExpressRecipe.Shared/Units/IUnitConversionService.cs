namespace ExpressRecipe.Shared.Units;
public interface IUnitConversionService
{
    Task<ConversionResult> ToCanonicalAsync(decimal amount, UnitCode fromUnit,
        Guid? ingredientId = null, string? ingredientName = null, CancellationToken ct = default);
    ConversionResult ToDisplay(decimal canonicalAmount, UnitCode canonicalUnit,
        UnitSystemPreference preference, UnitCode? forceUnit = null);
    decimal Scale(decimal canonicalAmount, decimal fromServings, decimal toServings);
    Task<int?> CompareAsync(decimal amount1, UnitCode unit1, decimal amount2, UnitCode unit2,
        Guid? ingredientId = null, string? ingredientName = null, CancellationToken ct = default);
}
