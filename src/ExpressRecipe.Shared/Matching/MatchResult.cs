namespace ExpressRecipe.Shared.Matching;

public enum MatchStrategy { Exact, Alias, AlternativeNames, Normalized, TokenOverlap, EditDistance, Unresolved }

public sealed record MatchResult
{
    public Guid? IngredientId { get; init; }
    public string IngredientName { get; init; } = string.Empty;
    public decimal Confidence { get; init; }
    public MatchStrategy Strategy { get; init; } = MatchStrategy.Unresolved;
    public bool IsLowConfidence => Confidence < 0.80m && Confidence > 0m;
    public bool IsResolved => IngredientId.HasValue;
    public string NormalizedInput { get; init; } = string.Empty;
    public string RawInput { get; init; } = string.Empty;

    public static MatchResult Unresolved(string raw, string normalized) => new()
        { RawInput = raw, NormalizedInput = normalized, Strategy = MatchStrategy.Unresolved };
}
