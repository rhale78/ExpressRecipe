using ExpressRecipe.Data.Common;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.UserService.Data;

public sealed class AllergyIncidentRepository : SqlHelper, IAllergyIncidentRepository
{
    public AllergyIncidentRepository(string connectionString) : base(connectionString) { }

    // ── Incident read ──────────────────────────────────────────────────────

    public async Task<List<AllergyIncidentProductRow>> GetReactionProductsForMemberAsync(
        Guid householdId, Guid? memberId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT ip.IncidentId,
                   ip.ProductId,
                   ip.HadReaction,
                   m.SeverityLevel
            FROM   HouseholdAllergyIncidentProduct ip
            INNER JOIN HouseholdAllergyIncidentMember m  ON m.IncidentId = ip.IncidentId
            INNER JOIN HouseholdAllergyIncident        i ON i.Id          = ip.IncidentId
            WHERE  i.HouseholdId = @HouseholdId
              AND  (@MemberId IS NULL AND m.MemberId IS NULL
                   OR m.MemberId = @MemberId)";

        return await ExecuteReaderAsync(
            sql,
            reader => new AllergyIncidentProductRow
            {
                IncidentId    = GetGuid(reader, "IncidentId"),
                ProductId     = reader.IsDBNull(reader.GetOrdinal("ProductId"))
                                    ? null
                                    : reader.GetGuid(reader.GetOrdinal("ProductId")),
                HadReaction   = reader.GetBoolean(reader.GetOrdinal("HadReaction")),
                SeverityLevel = GetString(reader, "SeverityLevel") ?? string.Empty
            },
            CreateParameter("@HouseholdId", householdId),
            CreateParameter("@MemberId", memberId.HasValue ? memberId.Value : DBNull.Value));
    }

    public async Task<List<Guid>> GetUnanalyzedIncidentIdsAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id FROM HouseholdAllergyIncident
            WHERE  AnalysisRun = 0
            ORDER  BY CreatedAt";

        return await ExecuteReaderAsync(
            sql,
            reader => reader.GetGuid(0));
    }

    public async Task<AllergyIncidentEngineDto?> GetIncidentByIdAsync(
        Guid incidentId, CancellationToken ct = default)
    {
        const string incidentSql = @"
            SELECT Id, HouseholdId
            FROM   HouseholdAllergyIncident
            WHERE  Id = @Id";

        List<AllergyIncidentEngineDto> incidents = await ExecuteReaderAsync(
            incidentSql,
            reader => new AllergyIncidentEngineDto
            {
                Id          = GetGuid(reader, "Id"),
                HouseholdId = GetGuid(reader, "HouseholdId")
            },
            CreateParameter("@Id", incidentId));

        AllergyIncidentEngineDto? incident = incidents.FirstOrDefault();
        if (incident is null) { return null; }

        const string memberSql = @"
            SELECT MemberId, MemberName
            FROM   HouseholdAllergyIncidentMember
            WHERE  IncidentId = @IncidentId";

        List<IncidentMemberDto> members = await ExecuteReaderAsync(
            memberSql,
            reader => new IncidentMemberDto
            {
                MemberId   = reader.IsDBNull(reader.GetOrdinal("MemberId"))
                                 ? null
                                 : reader.GetGuid(reader.GetOrdinal("MemberId")),
                MemberName = GetString(reader, "MemberName") ?? string.Empty
            },
            CreateParameter("@IncidentId", incidentId));

        incident.Members.AddRange(members);
        return incident;
    }

    // ── Incident write ─────────────────────────────────────────────────────

    public async Task MarkAnalysisRunAsync(Guid incidentId, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE HouseholdAllergyIncident
            SET    AnalysisRun   = 1,
                   AnalysisRunAt = GETUTCDATE()
            WHERE  Id = @Id";

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", incidentId));
    }

    // ── Analysis output ────────────────────────────────────────────────────

    public async Task UpsertSuspectedAllergenAsync(
        Guid householdId, Guid? memberId, string memberName,
        string ingredientName, Guid? ingredientId,
        decimal confidence, int incidentCount,
        CancellationToken ct = default)
    {
        const string sql = @"
            MERGE SuspectedAllergen AS target
            USING (SELECT @HouseholdId AS HouseholdId,
                          @MemberId    AS MemberId,
                          @IngredientName AS IngredientName) AS source
            ON (    target.HouseholdId    = source.HouseholdId
                AND target.IngredientName = source.IngredientName
                AND (target.MemberId = source.MemberId
                     OR (target.MemberId IS NULL AND source.MemberId IS NULL)))
            WHEN MATCHED THEN
                UPDATE SET
                    Confidence    = @Confidence,
                    IncidentCount = @IncidentCount,
                    IsActive      = 1,
                    UpdatedAt     = GETUTCDATE()
            WHEN NOT MATCHED THEN
                INSERT (HouseholdId, MemberId, MemberName, IngredientName, IngredientId,
                        Confidence, IncidentCount, IsActive, DetectedAt)
                VALUES (@HouseholdId, @MemberId, @MemberName, @IngredientName, @IngredientId,
                        @Confidence, @IncidentCount, 1, GETUTCDATE());";

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@HouseholdId",    householdId),
            CreateParameter("@MemberId",        memberId.HasValue ? memberId.Value : DBNull.Value),
            CreateParameter("@MemberName",      memberName),
            CreateParameter("@IngredientName",  ingredientName),
            CreateParameter("@IngredientId",    ingredientId.HasValue ? ingredientId.Value : DBNull.Value),
            CreateParameter("@Confidence",      confidence),
            CreateParameter("@IncidentCount",   incidentCount));
    }

    public async Task InsertClearedIngredientAsync(
        Guid householdId, Guid? memberId, string memberName,
        string ingredientName, Guid? ingredientId,
        CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO ClearedIngredient
                (HouseholdId, MemberId, MemberName, IngredientName, IngredientId, ClearedAt)
            VALUES
                (@HouseholdId, @MemberId, @MemberName, @IngredientName, @IngredientId, GETUTCDATE())";

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@HouseholdId",   householdId),
            CreateParameter("@MemberId",       memberId.HasValue ? memberId.Value : DBNull.Value),
            CreateParameter("@MemberName",     memberName),
            CreateParameter("@IngredientName", ingredientName),
            CreateParameter("@IngredientId",   ingredientId.HasValue ? ingredientId.Value : DBNull.Value));
    }
}
