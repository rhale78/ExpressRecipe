namespace ExpressRecipe.Shared.Units;
public sealed record ConversionResult
{
    public bool Success { get; init; }
    public decimal? Value { get; init; }
    public UnitCode? Unit { get; init; }
    public string? DisplayString { get; init; }
    public ConversionFailureReason? FailureReason { get; init; }
    public string? FailureDetail { get; init; }

    public static ConversionResult Ok(decimal value, UnitCode unit, string? display = null) =>
        new() { Success = true, Value = value, Unit = unit, DisplayString = display };
    public static ConversionResult NeedsDensity(string name) =>
        new() { Success = false, FailureReason = ConversionFailureReason.RequiresDensity,
                FailureDetail = $"Density unknown for: {name}" };
    public static ConversionResult BadUnit(string raw) =>
        new() { Success = false, FailureReason = ConversionFailureReason.UnknownUnit,
                FailureDetail = $"Cannot parse unit: '{raw}'" };
    public static ConversionResult Uncountable(UnitCode code) =>
        new() { Success = false, FailureReason = ConversionFailureReason.Uncountable,
                FailureDetail = $"{code} cannot be converted" };
}
public enum ConversionFailureReason { RequiresDensity, UnknownUnit, Uncountable, DensityNotFound, IncompatibleDimensions }
