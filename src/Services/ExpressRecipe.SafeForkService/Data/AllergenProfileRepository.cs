using ExpressRecipe.Data.Common;
using ExpressRecipe.SafeForkService.Contracts.Requests;
using ExpressRecipe.SafeForkService.Contracts.Responses;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.SafeForkService.Data;

public class AllergenProfileRepository : SqlHelper, IAllergenProfileRepository
{
    public AllergenProfileRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task<List<AllergenProfileEntryDto>> GetByMemberIdAsync(Guid memberId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, MemberId, AllergenId, FreeFormName, FreeFormBrand,
                   IsUnresolved, ExposureThreshold, Severity, HouseholdExclude, CreatedAt
            FROM AllergenProfile
            WHERE MemberId = @MemberId AND IsDeleted = 0
            ORDER BY CreatedAt DESC";

        return await ExecuteReaderAsync(
            sql,
            reader => new AllergenProfileEntryDto
            {
                Id = GetGuid(reader, "Id"),
                MemberId = GetGuid(reader, "MemberId"),
                AllergenId = GetGuidNullable(reader, "AllergenId"),
                FreeFormName = GetString(reader, "FreeFormName"),
                FreeFormBrand = GetString(reader, "FreeFormBrand"),
                IsUnresolved = GetBoolean(reader, "IsUnresolved"),
                ExposureThreshold = GetString(reader, "ExposureThreshold") ?? "IngestionOnly",
                Severity = GetString(reader, "Severity") ?? "Moderate",
                HouseholdExclude = GetBoolean(reader, "HouseholdExclude"),
                CreatedAt = GetDateTime(reader, "CreatedAt")
            },
            CreateParameter("@MemberId", memberId));
    }

    public async Task<Guid> AddCuratedEntryAsync(Guid memberId, AddCuratedAllergenRequest request, Guid? createdBy = null, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO AllergenProfile
                (Id, MemberId, AllergenId, ExposureThreshold, Severity, IsUnresolved, CreatedBy, CreatedAt)
            VALUES
                (@Id, @MemberId, @AllergenId, @ExposureThreshold, @Severity, 0, @CreatedBy, GETUTCDATE())";

        Guid newId = Guid.NewGuid();

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", newId),
            CreateParameter("@MemberId", memberId),
            CreateParameter("@AllergenId", request.AllergenId),
            CreateParameter("@ExposureThreshold", request.ExposureThreshold),
            CreateParameter("@Severity", request.Severity),
            CreateParameter("@CreatedBy", createdBy));

        return newId;
    }

    public async Task<Guid> AddFreeformEntryAsync(Guid memberId, string freeFormText, string? brand, Guid? createdBy = null, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO AllergenProfile
                (Id, MemberId, FreeFormName, FreeFormBrand, IsUnresolved, CreatedBy, CreatedAt)
            VALUES
                (@Id, @MemberId, @FreeFormName, @FreeFormBrand, 1, @CreatedBy, GETUTCDATE())";

        Guid newId = Guid.NewGuid();

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", newId),
            CreateParameter("@MemberId", memberId),
            CreateParameter("@FreeFormName", freeFormText),
            CreateParameter("@FreeFormBrand", brand),
            CreateParameter("@CreatedBy", createdBy));

        return newId;
    }

    public async Task<bool> SoftDeleteEntryAsync(Guid entryId, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE AllergenProfile
            SET IsDeleted = 1, UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        int rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", entryId));

        return rowsAffected > 0;
    }

    public async Task<bool> SetHouseholdExcludeAsync(Guid entryId, bool value, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE AllergenProfile
            SET HouseholdExclude = @Value, UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        int rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", entryId),
            CreateParameter("@Value", value));

        return rowsAffected > 0;
    }

    public async Task<bool> SetUnresolvedAsync(Guid entryId, bool isUnresolved, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE AllergenProfile
            SET IsUnresolved = @IsUnresolved,
                ResolvedAt = CASE WHEN @IsUnresolved = 0 THEN GETUTCDATE() ELSE NULL END,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        int rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", entryId),
            CreateParameter("@IsUnresolved", isUnresolved));

        return rowsAffected > 0;
    }

    public async Task AddLinkAsync(Guid allergenProfileId, string linkType, Guid linkedId, string matchMethod, decimal confidenceScore = 1.000m, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO AllergenProfileLink
                (Id, AllergenProfileId, LinkType, LinkedId, MatchMethod, ConfidenceScore, CreatedAt)
            VALUES
                (NEWID(), @AllergenProfileId, @LinkType, @LinkedId, @MatchMethod, @ConfidenceScore, GETUTCDATE())";

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@AllergenProfileId", allergenProfileId),
            CreateParameter("@LinkType", linkType),
            CreateParameter("@LinkedId", linkedId),
            CreateParameter("@MatchMethod", matchMethod),
            CreateParameter("@ConfidenceScore", confidenceScore));
    }

    public async Task<int> CountLinksByMemberIngredientAsync(Guid memberId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT COUNT(*)
            FROM AllergenProfileLink apl
            INNER JOIN AllergenProfile ap ON apl.AllergenProfileId = ap.Id
            WHERE ap.MemberId = @MemberId
              AND ap.IsDeleted = 0
              AND apl.LinkType = 'Ingredient'";

        int? result = await ExecuteScalarAsync<int>(
            sql,
            CreateParameter("@MemberId", memberId));

        return result ?? 0;
    }

    public async Task<List<(Guid LinkedIngredientId, int Count)>> GetTopIngredientLinksAsync(Guid memberId, int minCount = 5, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT apl.LinkedId, COUNT(*) AS LinkCount
            FROM AllergenProfileLink apl
            INNER JOIN AllergenProfile ap ON apl.AllergenProfileId = ap.Id
            WHERE ap.MemberId = @MemberId
              AND ap.IsDeleted = 0
              AND apl.LinkType = 'Ingredient'
            GROUP BY apl.LinkedId
            HAVING COUNT(*) >= @MinCount
            ORDER BY LinkCount DESC";

        return await ExecuteReaderAsync(
            sql,
            reader => (GetGuid(reader, "LinkedId"), GetInt32(reader, "LinkCount")),
            CreateParameter("@MemberId", memberId),
            CreateParameter("@MinCount", minCount));
    }

    public async Task<List<AllergenProfileEntryDto>> GetHouseholdHardExcludesAsync(IReadOnlyList<Guid> memberIds, CancellationToken ct = default)
    {
        if (memberIds.Count == 0)
        {
            return new List<AllergenProfileEntryDto>();
        }

        // Build parameterized IN clause with individual parameters
        List<System.Data.Common.DbParameter> parameters = new List<System.Data.Common.DbParameter>();
        List<string> paramNames = new List<string>();

        for (int i = 0; i < memberIds.Count; i++)
        {
            string paramName = $"@MemberId{i}";
            paramNames.Add(paramName);
            parameters.Add(CreateParameter(paramName, memberIds[i]));
        }

        string inClause = string.Join(",", paramNames);
        string sql = $@"
            SELECT Id, MemberId, AllergenId, FreeFormName, FreeFormBrand,
                   IsUnresolved, ExposureThreshold, Severity, HouseholdExclude, CreatedAt
            FROM AllergenProfile
            WHERE MemberId IN ({inClause})
              AND HouseholdExclude = 1
              AND IsDeleted = 0
            ORDER BY MemberId, CreatedAt DESC";

        return await ExecuteReaderAsync(
            sql,
            reader => new AllergenProfileEntryDto
            {
                Id = GetGuid(reader, "Id"),
                MemberId = GetGuid(reader, "MemberId"),
                AllergenId = GetGuidNullable(reader, "AllergenId"),
                FreeFormName = GetString(reader, "FreeFormName"),
                FreeFormBrand = GetString(reader, "FreeFormBrand"),
                IsUnresolved = GetBoolean(reader, "IsUnresolved"),
                ExposureThreshold = GetString(reader, "ExposureThreshold") ?? "IngestionOnly",
                Severity = GetString(reader, "Severity") ?? "Moderate",
                HouseholdExclude = GetBoolean(reader, "HouseholdExclude"),
                CreatedAt = GetDateTime(reader, "CreatedAt")
            },
            parameters.ToArray());
    }

    public async Task SoftDeleteAllForMemberAsync(Guid memberId, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE AllergenProfile
            SET IsDeleted = 1, UpdatedAt = GETUTCDATE()
            WHERE MemberId = @MemberId AND IsDeleted = 0";

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@MemberId", memberId));
    }
}
