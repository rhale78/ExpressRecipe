namespace ExpressRecipe.Client.Shared.Models.User;

public sealed record SuspectedAllergenSummaryDto
{
    public Guid    Id              { get; init; }
    public string  IngredientName  { get; init; } = string.Empty;
    public string  MemberName      { get; init; } = string.Empty;
    public decimal ConfidenceScore { get; init; }
    public int     IncidentCount   { get; init; }
}
