namespace ExpressRecipe.Shared.Matching;

public interface IIngredientMatchingService
{
    Task<MatchResult> MatchAsync(string rawText, string sourceService,
        Guid? sourceEntityId = null, CancellationToken ct = default);
    Task<List<MatchResult>> MatchBulkAsync(IEnumerable<string> rawTexts, string sourceService,
        Guid? sourceEntityId = null, CancellationToken ct = default);
    Task ConfirmMatchAsync(Guid queueItemId, Guid ingredientId, bool createAlias,
        string resolvedBy, CancellationToken ct = default);
    Task CreateAndResolveAsync(Guid queueItemId, string newIngredientName, string category,
        CancellationToken ct = default);
    Task RejectAsync(Guid queueItemId, string reason, CancellationToken ct = default);
}
