namespace ExpressRecipe.Shared.Events;

public sealed record PointsEarnedEvent
{
    public Guid UserId { get; init; }
    public string Reason { get; init; } = string.Empty;  // ProductApproved|RecipePublished|ReferralConverted|etc.
    public int Points { get; init; }
    public Guid? RelatedEntityId { get; init; }
}
