using Microsoft.Data.SqlClient;

namespace ExpressRecipe.UserService.Data;

// ── DTOs used by the analysis engine ────────────────────────────────────────

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
    // ── Incident read ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns all incident-product rows for this household member (reaction + control rows).
    /// </summary>
    Task<List<AllergyIncidentProductRow>> GetReactionProductsForMemberAsync(
        Guid householdId, Guid? memberId, CancellationToken ct = default);

    /// <summary>Returns incident IDs whose <c>AnalysisRun</c> flag is 0.</summary>
    Task<List<Guid>> GetUnanalyzedIncidentIdsAsync(CancellationToken ct = default);

    /// <summary>Returns the engine view of an incident (HouseholdId + Members).</summary>
    Task<AllergyIncidentEngineDto?> GetIncidentByIdAsync(Guid incidentId, CancellationToken ct = default);

    // ── Incident write ─────────────────────────────────────────────────────

    /// <summary>Marks an incident as having had its analysis run.</summary>
    Task MarkAnalysisRunAsync(Guid incidentId, CancellationToken ct = default);

    // ── Analysis output ────────────────────────────────────────────────────

    /// <summary>
    /// Upserts a suspected allergen record.  Updates Confidence / IncidentCount if the row exists.
    /// </summary>
    Task UpsertSuspectedAllergenAsync(
        Guid householdId, Guid? memberId, string memberName,
        string ingredientName, Guid? ingredientId,
        decimal confidence, int incidentCount,
        CancellationToken ct = default);

    /// <summary>Records that an ingredient has been cleared from suspicion.</summary>
    Task InsertClearedIngredientAsync(
        Guid householdId, Guid? memberId, string memberName,
        string ingredientName, Guid? ingredientId,
        CancellationToken ct = default);
}
