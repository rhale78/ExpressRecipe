using ExpressRecipe.Shared.DTOs.User;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.UserService.Data;

// ── Engine DTOs ──────────────────────────────────────────────────────────────

/// <summary>One product row from the allergy incident cross-join (for analysis).</summary>
public sealed record AllergyIncidentProductRow
{
    public Guid    IncidentId    { get; init; }
    public Guid?   ProductId     { get; init; }
    public bool    HadReaction   { get; init; }
    public string  SeverityLevel { get; init; } = string.Empty;
}

/// <summary>Lightweight member reference inside an engine incident.</summary>
public sealed record IncidentMemberDto
{
    public Guid?  MemberId   { get; init; }
    public string MemberName { get; init; } = string.Empty;
}

/// <summary>Incident record as consumed by <see cref="Services.AllergyAnalysisWorker"/>.</summary>
public sealed class AllergyIncidentEngineDto
{
    public Guid                   Id          { get; init; }
    public Guid                   HouseholdId { get; init; }
    public List<IncidentMemberDto> Members    { get; init; } = new();
}

// ── Repository interface ─────────────────────────────────────────────────────

public interface IAllergyIncidentRepository
{
    // ── Incidents (CRUD) ─────────────────────────────────────────────────────

    Task<Guid> CreateIncidentAsync(Guid householdId, CreateAllergyIncidentV2Request request,
        CancellationToken ct = default);
    Task<AllergyIncidentV2Dto?> GetIncidentByIdAsync(Guid id,
        CancellationToken ct = default);
    Task<List<AllergyIncidentV2Dto>> GetIncidentsAsync(Guid householdId, Guid? memberId,
        int limit = 100, CancellationToken ct = default);

    // ── Engine-specific reads ────────────────────────────────────────────────

    /// <summary>Returns the engine view of an incident (HouseholdId + Members) for the analysis worker.</summary>
    Task<AllergyIncidentEngineDto?> GetIncidentForEngineAsync(Guid incidentId,
        CancellationToken ct = default);

    /// <summary>Returns all incident-product rows for this household member (reaction + control rows).</summary>
    Task<List<AllergyIncidentProductRow>> GetReactionProductsForMemberAsync(
        Guid householdId, Guid? memberId, CancellationToken ct = default);

    /// <summary>Returns incident IDs whose <c>AnalysisRun</c> flag is 0.</summary>
    Task<List<Guid>> GetUnanalyzedIncidentIdsAsync(CancellationToken ct = default);

    // ── Suspected allergens ──────────────────────────────────────────────────

    Task<List<SuspectedAllergenDto>> GetSuspectedAllergensAsync(Guid householdId, Guid? memberId,
        CancellationToken ct = default);
    Task<SuspectedAllergenDto?> GetSuspectedAllergenByIdAsync(Guid id,
        CancellationToken ct = default);
    Task UpsertSuspectedAllergenAsync(Guid householdId, Guid? memberId, string ingredientName,
        decimal confidenceScore, int incidentCount, CancellationToken ct = default);
    Task UpsertSuspectedAllergenAsync(
        Guid householdId, Guid? memberId, string memberName,
        string ingredientName, Guid? ingredientId,
        decimal confidence, int incidentCount,
        CancellationToken ct = default);
    Task PromoteSuspectedAllergenAsync(Guid id, CancellationToken ct = default);
    Task MarkAnalysisRunAsync(Guid incidentId, CancellationToken ct = default);

    // ── User-initiated clear ─────────────────────────────────────────────────

    Task DeleteSuspectedAllergenAsync(Guid suspectedAllergenId, CancellationToken ct = default);
    Task InsertUserClearedIngredientAsync(Guid suspectedAllergenId, Guid clearedByUserId,
        CancellationToken ct = default);
    Task ClearSuspectTransactionalAsync(Guid suspectedAllergenId, Guid clearedByUserId,
        CancellationToken ct = default);
    Task<List<ConfirmedAllergenDto>> GetConfirmedAllergensAsync(Guid householdId, Guid? memberId,
        CancellationToken ct = default);

    // ── Cleared ingredients ──────────────────────────────────────────────────

    Task<List<ClearedIngredientDto>> GetClearedIngredientsAsync(Guid householdId, Guid? memberId,
        CancellationToken ct = default);
    Task<bool> IsIngredientClearedAsync(Guid householdId, Guid? memberId, string ingredientName,
        CancellationToken ct = default);
    Task InsertClearedIngredientAsync(
        Guid householdId, Guid? memberId, string memberName,
        string ingredientName, Guid? ingredientId,
        CancellationToken ct = default);

    // ── Product reaction stats ────────────────────────────────────────────────

    Task<List<(string ProductName, int ReactionCount, int TotalCount)>>
        GetProductReactionStatsAsync(Guid householdId, Guid? memberId, int lookbackDays = 180,
        CancellationToken ct = default);
}
