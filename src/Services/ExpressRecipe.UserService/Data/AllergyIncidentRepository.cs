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

            const string incidentSql = @"
                INSERT INTO AllergyIncident2
                    (Id, HouseholdId, IncidentDate, ExposureType, ReactionLatency, Notes)
                VALUES
                    (@Id, @HouseholdId, @IncidentDate, @ExposureType, @ReactionLatency, @Notes)";

            await using (var cmd = new SqlCommand(incidentSql, conn, tx))
            {
                cmd.Parameters.AddWithValue("@Id",              incidentId);
                cmd.Parameters.AddWithValue("@HouseholdId",     householdId);
                cmd.Parameters.AddWithValue("@IncidentDate",    request.IncidentDate);
                cmd.Parameters.AddWithValue("@ExposureType",    request.ExposureType);
                cmd.Parameters.AddWithValue("@ReactionLatency", (object?)request.ReactionLatency ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Notes",           (object?)request.Notes ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync(ct);
            }

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

        var incidents = await ExecuteReaderAsync(sql, MapIncident, ct, CreateParameter("@Id", id));
        var incident  = incidents.FirstOrDefault();
        if (incident == null) return null;

        incident.Products = await GetIncidentProductsAsync(incident.Id, ct);
        incident.Members  = await GetIncidentMembersAsync(incident.Id, ct);
        return incident;
    }

    public async Task<List<AllergyIncidentV2Dto>> GetIncidentsAsync(Guid householdId,
        Guid? memberId, int limit = 100, CancellationToken ct = default)
    {
        // Use EXISTS to filter by member without JOIN side-effects (avoids duplicate rows
        // when an incident has multiple members and memberId is null).
        const string sql = @"
            SELECT i.Id, i.HouseholdId, i.IncidentDate, i.ExposureType,
                   i.ReactionLatency, i.Notes, i.CreatedAt
            FROM AllergyIncident2 i
            WHERE i.HouseholdId = @HouseholdId
              AND i.IsDeleted = 0
              AND (
                    @MemberId IS NULL
                    AND EXISTS (SELECT 1 FROM AllergyIncidentMember m
                                WHERE m.IncidentId = i.Id AND m.MemberId IS NULL)
                  OR EXISTS (SELECT 1 FROM AllergyIncidentMember m
                             WHERE m.IncidentId = i.Id AND m.MemberId = @MemberId)
              )
            ORDER BY i.IncidentDate DESC
            OFFSET 0 ROWS FETCH NEXT @Limit ROWS ONLY";

        var incidents = await ExecuteReaderAsync(sql, MapIncident, ct,
            CreateParameter("@HouseholdId", householdId),
            CreateParameter("@MemberId",    (object?)memberId ?? DBNull.Value),
            CreateParameter("@Limit",       limit));

        foreach (var incident in incidents)
        {
            incident.Products = await GetIncidentProductsAsync(incident.Id, ct);
            incident.Members  = await GetIncidentMembersAsync(incident.Id, ct);
        }

        return incidents;
    }

    private async Task<List<AllergyIncidentProductDto>> GetIncidentProductsAsync(Guid incidentId, CancellationToken ct = default)
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
        }, ct, CreateParameter("@IncidentId", incidentId));
    }

    private async Task<List<AllergyIncidentMemberDto>> GetIncidentMembersAsync(Guid incidentId, CancellationToken ct = default)
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
        }, ct, CreateParameter("@IncidentId", incidentId));
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
              AND (MemberId = @MemberId OR (MemberId IS NULL AND @MemberId IS NULL))
              AND IsDeleted = 0
              AND IsPromotedToConfirmed = 0
            ORDER BY ConfidenceScore DESC, IngredientName";

        return await ExecuteReaderAsync(sql, MapSuspected, ct,
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

        var rows = await ExecuteReaderAsync(sql, MapSuspected, ct, CreateParameter("@Id", id));
        return rows.FirstOrDefault();
    }

    /// <summary>
    /// Upserts a SuspectedAllergen row. Returns true if the row was newly inserted (first detection).
    /// The MERGE matches on HouseholdId + MemberKey + IngredientName regardless of IsDeleted,
    /// so soft-deleted rows are revived rather than violating the unique index.
    /// </summary>
    public async Task<bool> UpsertSuspectedAllergenAsync(Guid householdId, Guid? memberId,
        string ingredientName, decimal confidenceScore, int incidentCount, CancellationToken ct = default)
    {
        const string sql = @"
            DECLARE @Action NVARCHAR(10);
            MERGE SuspectedAllergen AS target
            USING (SELECT @HouseholdId AS HouseholdId,
                          @MemberId   AS MemberId,
                          @Name       AS IngredientName) AS src
            ON target.HouseholdId = src.HouseholdId
               AND (target.MemberId = src.MemberId OR (target.MemberId IS NULL AND src.MemberId IS NULL))
               AND target.IngredientName = src.IngredientName
            WHEN MATCHED THEN
                UPDATE SET ConfidenceScore = @ConfidenceScore,
                           IncidentCount   = @IncidentCount,
                           LastUpdatedAt   = GETUTCDATE(),
                           IsDeleted       = 0,
                           DeletedAt       = NULL
            WHEN NOT MATCHED THEN
                INSERT (Id, HouseholdId, MemberId, IngredientName, ConfidenceScore, IncidentCount)
                VALUES (NEWID(), @HouseholdId, @MemberId, @Name, @ConfidenceScore, @IncidentCount)
            OUTPUT $action INTO @Action;
            SELECT @Action;";

        var result = await ExecuteScalarAsync<string>(sql, ct,
            CreateParameter("@HouseholdId",    householdId),
            CreateParameter("@MemberId",       (object?)memberId ?? DBNull.Value),
            CreateParameter("@Name",           ingredientName),
            CreateParameter("@ConfidenceScore", confidenceScore),
            CreateParameter("@IncidentCount",  incidentCount));

        return string.Equals(result, "INSERT", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Marks the suspect as notified (sets LastNotifiedAt) so we do not spam the user on every analysis run.
    /// </summary>
    public async Task MarkSuspectNotifiedAsync(Guid householdId, Guid? memberId,
        string ingredientName, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE SuspectedAllergen
            SET LastNotifiedAt = GETUTCDATE()
            WHERE HouseholdId = @HouseholdId
              AND (MemberId = @MemberId OR (MemberId IS NULL AND @MemberId IS NULL))
              AND IngredientName = @IngredientName
              AND IsDeleted = 0";

        await ExecuteNonQueryAsync(sql, ct,
            CreateParameter("@HouseholdId",    householdId),
            CreateParameter("@MemberId",       (object?)memberId ?? DBNull.Value),
            CreateParameter("@IngredientName", ingredientName));
    }

    public async Task PromoteSuspectedAllergenAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE SuspectedAllergen
            SET IsPromotedToConfirmed = 1, PromotedAt = GETUTCDATE()
            WHERE Id = @Id";

        await ExecuteNonQueryAsync(sql, ct, CreateParameter("@Id", id));
    }

    public async Task DeleteSuspectedAllergenAsync(Guid suspectedAllergenId, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE SuspectedAllergen
            SET IsDeleted = 1, DeletedAt = GETUTCDATE()
            WHERE Id = @Id";

        await ExecuteNonQueryAsync(sql, ct, CreateParameter("@Id", suspectedAllergenId));
    }

    /// <summary>
    /// Atomically clears a suspect: inserts a ClearedIngredient row and soft-deletes the suspect
    /// in a single transaction so the two operations are always consistent.
    /// </summary>
    public async Task ClearSuspectTransactionalAsync(Guid suspectedAllergenId,
        Guid clearedByUserId, CancellationToken ct = default)
    {
        await ExecuteTransactionAsync<int>(async (conn, tx) =>
        {
            // Fetch the suspect
            string fetchSql = @"
                SELECT HouseholdId, MemberId, IngredientName
                FROM SuspectedAllergen
                WHERE Id = @Id AND IsDeleted = 0";

            Guid householdId      = Guid.Empty;
            Guid? memberId        = null;
            string ingredientName = string.Empty;

            await using (var cmd = new SqlCommand(fetchSql, conn, tx))
            {
                cmd.Parameters.AddWithValue("@Id", suspectedAllergenId);
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct)) return 0;
                householdId    = reader.GetGuid(reader.GetOrdinal("HouseholdId"));
                var midOrd     = reader.GetOrdinal("MemberId");
                memberId       = reader.IsDBNull(midOrd) ? null : reader.GetGuid(midOrd);
                ingredientName = reader.GetString(reader.GetOrdinal("IngredientName"));
            }

            if (ingredientName == string.Empty) return 0;

            // Upsert ClearedIngredient (ignore if already cleared)
            const string clearSql = @"
                IF NOT EXISTS (
                    SELECT 1 FROM ClearedIngredient
                    WHERE HouseholdId = @HouseholdId
                      AND (MemberId = @MemberId OR (MemberId IS NULL AND @MemberId IS NULL))
                      AND IngredientName = @IngredientName
                )
                INSERT INTO ClearedIngredient
                    (Id, HouseholdId, MemberId, IngredientName, ClearedByUserId, ClearingIncidentId)
                VALUES
                    (NEWID(), @HouseholdId, @MemberId, @IngredientName, @ClearedBy, NULL)";

            await using (var cmd = new SqlCommand(clearSql, conn, tx))
            {
                cmd.Parameters.AddWithValue("@HouseholdId",    householdId);
                cmd.Parameters.AddWithValue("@MemberId",       (object?)memberId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@IngredientName", ingredientName);
                cmd.Parameters.AddWithValue("@ClearedBy",      clearedByUserId);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            // Soft-delete the suspect
            const string deleteSql = @"
                UPDATE SuspectedAllergen
                SET IsDeleted = 1, DeletedAt = GETUTCDATE()
                WHERE Id = @Id";

            await using (var cmd = new SqlCommand(deleteSql, conn, tx))
            {
                cmd.Parameters.AddWithValue("@Id", suspectedAllergenId);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            return 1;
        });
    }

    // ─── Cleared Ingredients ──────────────────────────────────────────────────

    public async Task<List<ClearedIngredientDto>> GetClearedIngredientsAsync(Guid householdId,
        Guid? memberId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, MemberId, IngredientName, ClearedAt
            FROM ClearedIngredient
            WHERE HouseholdId = @HouseholdId
              AND (MemberId = @MemberId OR (MemberId IS NULL AND @MemberId IS NULL))
            ORDER BY IngredientName";

        return await ExecuteReaderAsync(sql,
            reader => new ClearedIngredientDto
            {
                Id             = GetGuid(reader, "Id"),
                MemberId       = GetGuidNullable(reader, "MemberId"),
                IngredientName = GetString(reader, "IngredientName") ?? string.Empty,
                ClearedAt      = GetDateTime(reader, "ClearedAt")
            }, ct,
            CreateParameter("@HouseholdId", householdId),
            CreateParameter("@MemberId",    (object?)memberId ?? DBNull.Value));
    }

    public async Task<bool> IsIngredientClearedAsync(Guid householdId, Guid? memberId,
        string ingredientName, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT COUNT(1) FROM ClearedIngredient
            WHERE HouseholdId = @HouseholdId
              AND (MemberId = @MemberId OR (MemberId IS NULL AND @MemberId IS NULL))
              AND IngredientName = @IngredientName";

        var count = await ExecuteScalarAsync<int>(sql, ct,
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
        // Use EXISTS to filter by member so the product aggregate is not inflated when
        // an incident has multiple members (LEFT JOIN would multiply product rows).
        const string sql = @"
            SELECT p.ProductName,
                   SUM(CASE WHEN p.HadReaction = 1 THEN 1 ELSE 0 END) AS ReactionCount,
                   COUNT(*)                                             AS TotalCount
            FROM AllergyIncident2       i
            JOIN AllergyIncidentProduct p ON p.IncidentId = i.Id
            WHERE i.HouseholdId = @HouseholdId
              AND i.IsDeleted   = 0
              AND i.IncidentDate >= DATEADD(DAY, -@LookbackDays, GETUTCDATE())
              AND (
                    @MemberId IS NULL
                    AND EXISTS (SELECT 1 FROM AllergyIncidentMember m
                                WHERE m.IncidentId = i.Id AND m.MemberId IS NULL)
                  OR EXISTS (SELECT 1 FROM AllergyIncidentMember m
                             WHERE m.IncidentId = i.Id AND m.MemberId = @MemberId)
              )
            GROUP BY p.ProductName";

        return await ExecuteReaderAsync(sql,
            reader => (
                ProductName:   GetString(reader, "ProductName") ?? string.Empty,
                ReactionCount: GetInt32(reader, "ReactionCount"),
                TotalCount:    GetInt32(reader, "TotalCount")
            ), ct,
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
