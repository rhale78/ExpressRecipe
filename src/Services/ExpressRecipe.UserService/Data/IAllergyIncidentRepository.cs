using ExpressRecipe.Shared.DTOs.User;

namespace ExpressRecipe.UserService.Data;

public interface IAllergyIncidentRepository
{
    // Incidents
    Task<Guid> CreateIncidentAsync(Guid householdId, CreateAllergyIncidentV2Request request,
        CancellationToken ct = default);
    Task<AllergyIncidentV2Dto?> GetIncidentByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<AllergyIncidentV2Dto>> GetIncidentsAsync(Guid householdId, Guid? memberId,
        int limit = 100, CancellationToken ct = default);

    // Suspected allergens
    Task<List<SuspectedAllergenDto>> GetSuspectedAllergensAsync(Guid householdId, Guid? memberId,
        CancellationToken ct = default);
    Task<SuspectedAllergenDto?> GetSuspectedAllergenByIdAsync(Guid id, CancellationToken ct = default);
    Task UpsertSuspectedAllergenAsync(Guid householdId, Guid? memberId, string ingredientName,
        decimal confidenceScore, int incidentCount, CancellationToken ct = default);
    Task PromoteSuspectedAllergenAsync(Guid id, CancellationToken ct = default);

    // User-initiated clear
    Task DeleteSuspectedAllergenAsync(Guid suspectedAllergenId, CancellationToken ct = default);
    Task InsertUserClearedIngredientAsync(Guid suspectedAllergenId, Guid clearedByUserId,
        CancellationToken ct = default);
    Task ClearSuspectTransactionalAsync(Guid suspectedAllergenId, Guid clearedByUserId,
        CancellationToken ct = default);
    Task<List<ConfirmedAllergenDto>> GetConfirmedAllergensAsync(Guid householdId, Guid? memberId,
        CancellationToken ct = default);

    // Cleared ingredients
    Task<List<ClearedIngredientDto>> GetClearedIngredientsAsync(Guid householdId, Guid? memberId,
        CancellationToken ct = default);
    Task<bool> IsIngredientClearedAsync(Guid householdId, Guid? memberId, string ingredientName,
        CancellationToken ct = default);

    // Reaction product/member counts for the analyzer
    Task<List<(string ProductName, int ReactionCount, int TotalCount)>>
        GetProductReactionStatsAsync(Guid householdId, Guid? memberId, int lookbackDays = 180,
        CancellationToken ct = default);
}