using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.User;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.UserService.Data;

public class AllergyIncidentRepository : SqlHelper, IAllergyIncidentRepository
{
    public AllergyIncidentRepository(string connectionString) : base(connectionString) { }

    // ─── Incidents ────────────────────────────────────────────────────────────

    public async Task<Guid> CreateIncidentAsync(Guid householdId,
        CreateAllergyIncidentV2Request request, CancellationToken ct = default)
    {
        return await ExecuteTransactionAsync<Guid>(async (conn, tx) =>
        {
            Guid incidentId = Guid.NewGuid();

            // Insert incident header
            const string incidentSql = @"
                INSERT INTO AllergyIncident2
                    (Id, HouseholdId, IncidentDate, ExposureType, ReactionLatency, Notes)
                VALUES
                    (@Id, @HouseholdId, @IncidentDate, @ExposureType, @ReactionLatency, @Notes)";

            await using (var cmd = new SqlCommand(incidentSql, conn, tx))
            {
                cmd.Parameters.AddWithValue("@Id",             incidentId);
                cmd.Parameters.AddWithValue("@HouseholdId",    householdId);
                cmd.Parameters.AddWithValue("@IncidentDate",   request.IncidentDate);
                cmd.Parameters.AddWithValue("@ExposureType",   request.ExposureType);
                cmd.Parameters.AddWithValue("@ReactionLatency", (object?)request.ReactionLatency ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Notes",           (object?)request.Notes ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            // Insert products
            foreach (var p in request.Products)
            {
                const string productSql = @"
                    INSERT INTO AllergyIncidentProduct (Id, IncidentId, ProductId, ProductName, HadReaction)
                    VALUES (NEWID(), @IncidentId, @ProductId, @ProductName, @HadReaction)";

                await using var cmd = new SqlCommand(productSql, conn, tx);
                cmd.Parameters.AddWithValue("@IncidentId",  incidentId);
                cmd.Parameters.AddWithValue("@ProductId",   (object?)p.ProductId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ProductName", p.ProductName);
                cmd.Parameters.AddWithValue("@HadReaction", p.HadReaction);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            // Insert members
            foreach (var m in request.Members)
            {
                const string memberSql = @"
                    INSERT INTO AllergyIncidentMember
                        (Id, IncidentId, MemberId, MemberName, Severity, ReactionTypes,
                         TreatmentType, TreatmentDose, ResolutionTimeMinutes, RequiredEpipen, RequiredER)
                    VALUES
                        (NEWID(), @IncidentId, @MemberId, @MemberName, @Severity, @ReactionTypes,
                         @TreatmentType, @TreatmentDose, @ResolutionTimeMinutes, @RequiredEpipen, @RequiredER)";

                await using var cmd = new SqlCommand(memberSql, conn, tx);
                cmd.Parameters.AddWithValue("@IncidentId",             incidentId);
                cmd.Parameters.AddWithValue("@MemberId",               (object?)m.MemberId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@MemberName",             m.MemberName);
                cmd.Parameters.AddWithValue("@Severity",               m.Severity);
                cmd.Parameters.AddWithValue("@ReactionTypes",          (object?)m.ReactionTypes ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@TreatmentType",          (object?)m.TreatmentType ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@TreatmentDose",          (object?)m.TreatmentDose ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ResolutionTimeMinutes",  (object?)m.ResolutionTimeMinutes ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@RequiredEpipen",         m.RequiredEpipen);
                cmd.Parameters.AddWithValue("@RequiredER",             m.RequiredER);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            return incidentId;
        });
    }

    public async Task<AllergyIncidentV2Dto?> GetIncidentByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT i.Id, i.HouseholdId, i.IncidentDate, i.ExposureType, i.ReactionLatency, i.Notes, i.CreatedAt
            FROM AllergyIncident2 i
            WHERE i.Id = @Id AND i.IsDeleted = 0";

        var incidents = await ExecuteReaderAsync(sql, MapIncident, CreateParameter("@Id", id));
        var incident = incidents.FirstOrDefault();
        if (incident == null) return null;

        incident.Products = await GetIncidentProductsAsync(id);
        incident.Members  = await GetIncidentMembersAsync(id);
        return incident;
    }

    public async Task<List<AllergyIncidentV2Dto>> GetIncidentsAsync(Guid householdId,
        Guid? memberId, int limit = 100, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT DISTINCT i.Id, i.HouseholdId, i.IncidentDate, i.ExposureType,
                            i.ReactionLatency, i.Notes, i.CreatedAt
            FROM AllergyIncident2 i
            LEFT JOIN AllergyIncidentMember m ON m.IncidentId = i.Id
            WHERE i.HouseholdId = @HouseholdId
              AND i.IsDeleted = 0
              AND (@MemberId IS NULL OR m.MemberId = @MemberId OR (m.MemberId IS NULL AND @MemberId IS NULL))
            ORDER BY i.IncidentDate DESC
            OFFSET 0 ROWS FETCH NEXT @Limit ROWS ONLY";

        var incidents = await ExecuteReaderAsync(sql, MapIncident,
            CreateParameter("@HouseholdId", householdId),
            CreateParameter("@MemberId",    (object?)memberId ?? DBNull.Value),
            CreateParameter("@Limit",       limit));

        foreach (var incident in incidents)
        {
            incident.Products = await GetIncidentProductsAsync(incident.Id);
            incident.Members  = await GetIncidentMembersAsync(incident.Id);
        }

        return incidents;
    }

    private async Task<List<AllergyIncidentProductDto>> GetIncidentProductsAsync(Guid incidentId)
    {
        const string sql = @"
            SELECT Id, ProductId, ProductName, HadReaction
            FROM AllergyIncidentProduct
            WHERE IncidentId = @IncidentId";

        return await ExecuteReaderAsync(sql, reader => new AllergyIncidentProductDto
        {
            Id          = GetGuid(reader, "Id"),
            ProductId   = GetGuidNullable(reader, "ProductId"),
            ProductName = GetString(reader, "ProductName") ?? string.Empty,
            HadReaction = GetBoolean(reader, "HadReaction")
        }, CreateParameter("@IncidentId", incidentId));
    }

    private async Task<List<AllergyIncidentMemberDto>> GetIncidentMembersAsync(Guid incidentId)
    {
        const string sql = @"
            SELECT Id, MemberId, MemberName, Severity, ReactionTypes,
                   TreatmentType, TreatmentDose, ResolutionTimeMinutes, RequiredEpipen, RequiredER
            FROM AllergyIncidentMember
            WHERE IncidentId = @IncidentId";

        return await ExecuteReaderAsync(sql, reader => new AllergyIncidentMemberDto
        {
            Id                    = GetGuid(reader, "Id"),
            MemberId              = GetGuidNullable(reader, "MemberId"),
            MemberName            = GetString(reader, "MemberName") ?? string.Empty,
            Severity              = GetString(reader, "Severity") ?? string.Empty,
            ReactionTypes         = GetString(reader, "ReactionTypes"),
            TreatmentType         = GetString(reader, "TreatmentType"),
            TreatmentDose         = GetString(reader, "TreatmentDose"),
            ResolutionTimeMinutes = GetIntNullable(reader, "ResolutionTimeMinutes"),
            RequiredEpipen        = GetBoolean(reader, "RequiredEpipen"),
            RequiredER            = GetBoolean(reader, "RequiredER")
        }, CreateParameter("@IncidentId", incidentId));
    }

    private static AllergyIncidentV2Dto MapIncident(System.Data.IDataRecord reader) => new()
    {
        Id              = GetGuid(reader, "Id"),
        HouseholdId     = GetGuid(reader, "HouseholdId"),
        IncidentDate    = GetDateTime(reader, "IncidentDate"),
        ExposureType    = GetString(reader, "ExposureType") ?? "Ingestion",
        ReactionLatency = GetString(reader, "ReactionLatency"),
        Notes           = GetString(reader, "Notes"),
        CreatedAt       = GetDateTime(reader, "CreatedAt")
    };

    // ─── Suspected Allergens ──────────────────────────────────────────────────

    public async Task<List<SuspectedAllergenDto>> GetSuspectedAllergensAsync(Guid householdId,
        Guid? memberId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, HouseholdId, MemberId, IngredientName, ConfidenceScore,
                   IncidentCount, FirstSeenAt, LastUpdatedAt, IsPromotedToConfirmed
            FROM SuspectedAllergen
            WHERE HouseholdId = @HouseholdId
              AND (@MemberId IS NULL OR MemberId = @MemberId)
              AND IsDeleted = 0
            ORDER BY ConfidenceScore DESC, IngredientName";

        return await ExecuteReaderAsync(sql, MapSuspected,
            CreateParameter("@HouseholdId", householdId),
            CreateParameter("@MemberId",    (object?)memberId ?? DBNull.Value));
    }

    public async Task<SuspectedAllergenDto?> GetSuspectedAllergenByIdAsync(Guid id,
        CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, HouseholdId, MemberId, IngredientName, ConfidenceScore,
                   IncidentCount, FirstSeenAt, LastUpdatedAt, IsPromotedToConfirmed
            FROM SuspectedAllergen
            WHERE Id = @Id AND IsDeleted = 0";

        var rows = await ExecuteReaderAsync(sql, MapSuspected, CreateParameter("@Id", id));
        return rows.FirstOrDefault();
    }

    public async Task UpsertSuspectedAllergenAsync(Guid householdId, Guid? memberId,
        string ingredientName, decimal confidenceScore, int incidentCount, CancellationToken ct = default)
    {
        const string sql = @"
            MERGE SuspectedAllergen AS target
            USING (SELECT @HouseholdId AS HouseholdId,
                          @MemberId   AS MemberId,
                          @Name       AS IngredientName) AS src
            ON target.HouseholdId   = src.HouseholdId
               AND (target.MemberId = src.MemberId OR (target.MemberId IS NULL AND src.MemberId IS NULL))
               AND target.IngredientName = src.IngredientName
               AND target.IsDeleted = 0
            WHEN MATCHED THEN
                UPDATE SET ConfidenceScore = @ConfidenceScore,
                           IncidentCount   = @IncidentCount,
                           LastUpdatedAt   = GETUTCDATE()
            WHEN NOT MATCHED THEN
                INSERT (Id, HouseholdId, MemberId, IngredientName, ConfidenceScore, IncidentCount)
                VALUES (NEWID(), @HouseholdId, @MemberId, @Name, @ConfidenceScore, @IncidentCount);";

        await ExecuteNonQueryAsync(sql,
            CreateParameter("@HouseholdId",    householdId),
            CreateParameter("@MemberId",       (object?)memberId ?? DBNull.Value),
            CreateParameter("@Name",           ingredientName),
            CreateParameter("@ConfidenceScore", confidenceScore),
            CreateParameter("@IncidentCount",  incidentCount));
    }

    public async Task PromoteSuspectedAllergenAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE SuspectedAllergen
            SET IsPromotedToConfirmed = 1, PromotedAt = GETUTCDATE()
            WHERE Id = @Id";

        await ExecuteNonQueryAsync(sql, CreateParameter("@Id", id));
    }

    public async Task DeleteSuspectedAllergenAsync(Guid suspectedAllergenId, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE SuspectedAllergen
            SET IsDeleted = 1, DeletedAt = GETUTCDATE()
            WHERE Id = @Id";

        await ExecuteNonQueryAsync(sql, CreateParameter("@Id", suspectedAllergenId));
    }

    public async Task InsertUserClearedIngredientAsync(Guid suspectedAllergenId,
        Guid clearedByUserId, CancellationToken ct = default)
    {
        // Fetch the suspected allergen first to get HouseholdId/MemberId/IngredientName
        const string fetchSql = @"
            SELECT HouseholdId, MemberId, IngredientName
            FROM SuspectedAllergen
            WHERE Id = @Id";

        var rows = await ExecuteReaderAsync(fetchSql,
            reader => (
                HouseholdId:    GetGuid(reader, "HouseholdId"),
                MemberId:       GetGuidNullable(reader, "MemberId"),
                IngredientName: GetString(reader, "IngredientName") ?? string.Empty
            ),
            CreateParameter("@Id", suspectedAllergenId));

        var row = rows.FirstOrDefault();
        if (row == default) return;

        // Upsert into ClearedIngredient (ignore if already exists)
        const string clearSql = @"
            IF NOT EXISTS (
                SELECT 1 FROM ClearedIngredient
                WHERE HouseholdId = @HouseholdId
                  AND (@MemberId IS NULL OR MemberId = @MemberId)
                  AND IngredientName = @IngredientName
            )
            BEGIN
                INSERT INTO ClearedIngredient
                    (Id, HouseholdId, MemberId, IngredientName, ClearedByUserId, ClearingIncidentId)
                VALUES
                    (NEWID(), @HouseholdId, @MemberId, @IngredientName, @ClearedBy, NULL)
            END";

        await ExecuteNonQueryAsync(clearSql,
            CreateParameter("@HouseholdId",    row.HouseholdId),
            CreateParameter("@MemberId",       (object?)row.MemberId ?? DBNull.Value),
            CreateParameter("@IngredientName", row.IngredientName),
            CreateParameter("@ClearedBy",      clearedByUserId));

        // Soft-delete the suspect row
        await DeleteSuspectedAllergenAsync(suspectedAllergenId, ct);
    }

    // ─── Cleared Ingredients ──────────────────────────────────────────────────

    public async Task<List<ClearedIngredientDto>> GetClearedIngredientsAsync(Guid householdId,
        Guid? memberId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, MemberId, IngredientName, ClearedAt
            FROM ClearedIngredient
            WHERE HouseholdId = @HouseholdId
              AND (@MemberId IS NULL OR MemberId = @MemberId)
            ORDER BY IngredientName";

        return await ExecuteReaderAsync(sql,
            reader => new ClearedIngredientDto
            {
                Id             = GetGuid(reader, "Id"),
                MemberId       = GetGuidNullable(reader, "MemberId"),
                IngredientName = GetString(reader, "IngredientName") ?? string.Empty,
                ClearedAt      = GetDateTime(reader, "ClearedAt")
            },
            CreateParameter("@HouseholdId", householdId),
            CreateParameter("@MemberId",    (object?)memberId ?? DBNull.Value));
    }

    public async Task<bool> IsIngredientClearedAsync(Guid householdId, Guid? memberId,
        string ingredientName, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT COUNT(1) FROM ClearedIngredient
            WHERE HouseholdId = @HouseholdId
              AND (@MemberId IS NULL OR MemberId = @MemberId)
              AND IngredientName = @IngredientName";

        var count = await ExecuteScalarAsync<int>(sql,
            CreateParameter("@HouseholdId",    householdId),
            CreateParameter("@MemberId",       (object?)memberId ?? DBNull.Value),
            CreateParameter("@IngredientName", ingredientName));

        return count > 0;
    }

    // ─── Reaction Stats (for analyzer) ────────────────────────────────────────

    public async Task<List<(string ProductName, int ReactionCount, int TotalCount)>>
        GetProductReactionStatsAsync(Guid householdId, Guid? memberId,
        int lookbackDays = 180, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT p.ProductName,
                   SUM(CASE WHEN p.HadReaction = 1 THEN 1 ELSE 0 END) AS ReactionCount,
                   COUNT(*)                                             AS TotalCount
            FROM AllergyIncident2       i
            JOIN AllergyIncidentProduct p ON p.IncidentId = i.Id
            LEFT JOIN AllergyIncidentMember m ON m.IncidentId = i.Id
            WHERE i.HouseholdId = @HouseholdId
              AND i.IsDeleted   = 0
              AND i.IncidentDate >= DATEADD(DAY, -@LookbackDays, GETUTCDATE())
              AND (@MemberId IS NULL
                   OR m.MemberId = @MemberId
                   OR (m.MemberId IS NULL AND @MemberId IS NULL))
            GROUP BY p.ProductName";

        return await ExecuteReaderAsync(sql,
            reader => (
                ProductName:   GetString(reader, "ProductName") ?? string.Empty,
                ReactionCount: GetInt32(reader, "ReactionCount"),
                TotalCount:    GetInt32(reader, "TotalCount")
            ),
            CreateParameter("@HouseholdId",  householdId),
            CreateParameter("@MemberId",     (object?)memberId ?? DBNull.Value),
            CreateParameter("@LookbackDays", lookbackDays));
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static SuspectedAllergenDto MapSuspected(System.Data.IDataRecord reader) => new()
    {
        Id                    = GetGuid(reader, "Id"),
        HouseholdId           = GetGuid(reader, "HouseholdId"),
        MemberId              = GetGuidNullable(reader, "MemberId"),
        IngredientName        = GetString(reader, "IngredientName") ?? string.Empty,
        ConfidenceScore       = GetDecimal(reader, "ConfidenceScore"),
        IncidentCount         = GetInt32(reader, "IncidentCount"),
        FirstSeenAt           = GetDateTime(reader, "FirstSeenAt"),
        LastUpdatedAt         = GetDateTime(reader, "LastUpdatedAt"),
        IsPromotedToConfirmed = GetBoolean(reader, "IsPromotedToConfirmed")
    };
}