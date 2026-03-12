using ExpressRecipe.Data.Common;
using ExpressRecipe.PreferencesService.Contracts.Requests;
using ExpressRecipe.PreferencesService.Contracts.Responses;
using ExpressRecipe.Shared.Services;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.PreferencesService.Data;

public class CookProfileRepository : SqlHelper, ICookProfileRepository
{
    private readonly HybridCacheService? _cache;
    private const string CachePrefix = "cookprofile:";

    public CookProfileRepository(string connectionString, HybridCacheService? cache = null)
        : base(connectionString)
    {
        _cache = cache;
    }

    public async Task<CookProfileDto?> GetByMemberIdAsync(Guid memberId, CancellationToken ct = default)
    {
        if (_cache != null)
        {
            return await _cache.GetOrSetAsync(
                $"{CachePrefix}member:{memberId}",
                async (_ct) => await GetByMemberIdFromDbAsync(memberId, _ct),
                expiration: TimeSpan.FromHours(1),
                cancellationToken: ct);
        }

        return await GetByMemberIdFromDbAsync(memberId, ct);
    }

    private async Task<CookProfileDto?> GetByMemberIdFromDbAsync(Guid memberId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, MemberId, CooksForHousehold, CookingFrequency,
                   OverallSkillLevel, CookRole, EatingDisorderRecovery,
                   CreatedAt, UpdatedAt
            FROM CookProfile
            WHERE MemberId = @MemberId AND IsDeleted = 0";

        List<CookProfileDto> results = await ExecuteReaderAsync(
            sql,
            reader => new CookProfileDto
            {
                Id = GetGuid(reader, "Id"),
                MemberId = GetGuid(reader, "MemberId"),
                CooksForHousehold = GetBoolean(reader, "CooksForHousehold"),
                CookingFrequency = GetString(reader, "CookingFrequency") ?? "Regular",
                OverallSkillLevel = GetString(reader, "OverallSkillLevel") ?? "HomeCook",
                CookRole = GetString(reader, "CookRole") ?? "PrimaryHomeChef",
                EatingDisorderRecovery = GetBoolean(reader, "EatingDisorderRecovery"),
                CreatedAt = GetDateTime(reader, "CreatedAt"),
                UpdatedAt = GetNullableDateTime(reader, "UpdatedAt")
            },
            CreateParameter("@MemberId", memberId));

        return results.FirstOrDefault();
    }

    public async Task<Guid> UpsertAsync(Guid memberId, UpsertCookProfileRequest request, CancellationToken ct = default)
    {
        const string sql = @"
            DECLARE @ExistingId UNIQUEIDENTIFIER;
            SELECT @ExistingId = Id FROM CookProfile WHERE MemberId = @MemberId AND IsDeleted = 0;

            IF @ExistingId IS NOT NULL
            BEGIN
                UPDATE CookProfile
                SET CooksForHousehold      = @CooksForHousehold,
                    CookingFrequency       = @CookingFrequency,
                    OverallSkillLevel      = @OverallSkillLevel,
                    CookRole               = @CookRole,
                    EatingDisorderRecovery = @EatingDisorderRecovery,
                    UpdatedAt              = GETUTCDATE()
                WHERE Id = @ExistingId;
                SELECT @ExistingId;
            END
            ELSE
            BEGIN
                DECLARE @NewId UNIQUEIDENTIFIER = NEWID();
                INSERT INTO CookProfile
                    (Id, MemberId, CooksForHousehold, CookingFrequency,
                     OverallSkillLevel, CookRole, EatingDisorderRecovery, IsDeleted, CreatedAt)
                VALUES
                    (@NewId, @MemberId, @CooksForHousehold, @CookingFrequency,
                     @OverallSkillLevel, @CookRole, @EatingDisorderRecovery, 0, GETUTCDATE());
                SELECT @NewId;
            END";

        Guid? result = await ExecuteScalarAsync<Guid>(
            sql,
            CreateParameter("@MemberId", memberId),
            CreateParameter("@CooksForHousehold", request.CooksForHousehold),
            CreateParameter("@CookingFrequency", request.CookingFrequency),
            CreateParameter("@OverallSkillLevel", request.OverallSkillLevel),
            CreateParameter("@CookRole", request.CookRole),
            CreateParameter("@EatingDisorderRecovery", request.EatingDisorderRecovery));

        if (_cache != null)
            await _cache.RemoveAsync($"{CachePrefix}member:{memberId}");

        return result ?? Guid.Empty;
    }

    public async Task<TechniqueComfortDto?> GetTechniqueComfortAsync(Guid memberId, string techniqueCode, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, MemberId, TechniqueCode, ComfortLevel
            FROM TechniqueComfort
            WHERE MemberId = @MemberId AND TechniqueCode = @TechniqueCode";

        List<TechniqueComfortDto> results = await ExecuteReaderAsync(
            sql,
            reader => new TechniqueComfortDto
            {
                Id = GetGuid(reader, "Id"),
                MemberId = GetGuid(reader, "MemberId"),
                TechniqueCode = GetString(reader, "TechniqueCode") ?? string.Empty,
                ComfortLevel = GetString(reader, "ComfortLevel") ?? string.Empty
            },
            CreateParameter("@MemberId", memberId),
            CreateParameter("@TechniqueCode", techniqueCode));

        return results.FirstOrDefault();
    }

    public async Task UpsertTechniqueComfortAsync(Guid memberId, string techniqueCode, SetTechniqueComfortRequest request, CancellationToken ct = default)
    {
        const string sql = @"
            MERGE TechniqueComfort AS target
            USING (SELECT @MemberId AS MemberId, @TechniqueCode AS TechniqueCode) AS source
            ON (target.MemberId = source.MemberId AND target.TechniqueCode = source.TechniqueCode)
            WHEN MATCHED THEN
                UPDATE SET ComfortLevel = @ComfortLevel, UpdatedAt = GETUTCDATE()
            WHEN NOT MATCHED THEN
                INSERT (Id, MemberId, TechniqueCode, ComfortLevel, CreatedAt)
                VALUES (NEWID(), @MemberId, @TechniqueCode, @ComfortLevel, GETUTCDATE());";

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@MemberId", memberId),
            CreateParameter("@TechniqueCode", techniqueCode),
            CreateParameter("@ComfortLevel", request.ComfortLevel));
    }

    public async Task<List<DismissedTipDto>> GetDismissedTipsAsync(Guid memberId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, MemberId, TipId, DismissedAt
            FROM DismissedTip
            WHERE MemberId = @MemberId
            ORDER BY DismissedAt DESC";

        return await ExecuteReaderAsync(
            sql,
            reader => new DismissedTipDto
            {
                Id = GetGuid(reader, "Id"),
                MemberId = GetGuid(reader, "MemberId"),
                TipId = GetGuid(reader, "TipId"),
                DismissedAt = GetDateTime(reader, "DismissedAt")
            },
            CreateParameter("@MemberId", memberId));
    }

    public async Task DismissTipAsync(Guid memberId, Guid tipId, CancellationToken ct = default)
    {
        const string sql = @"
            IF NOT EXISTS (SELECT 1 FROM DismissedTip WHERE MemberId = @MemberId AND TipId = @TipId)
            BEGIN
                INSERT INTO DismissedTip (Id, MemberId, TipId, DismissedAt)
                VALUES (NEWID(), @MemberId, @TipId, GETUTCDATE());
            END";

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@MemberId", memberId),
            CreateParameter("@TipId", tipId));
    }

    public async Task RestoreTipAsync(Guid memberId, Guid tipId, CancellationToken ct = default)
    {
        const string sql = @"
            DELETE FROM DismissedTip
            WHERE MemberId = @MemberId AND TipId = @TipId";

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@MemberId", memberId),
            CreateParameter("@TipId", tipId));
    }

    public async Task<List<TechniqueComfortDto>> GetAllTechniqueComfortsAsync(Guid memberId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, MemberId, TechniqueCode, ComfortLevel
            FROM TechniqueComfort
            WHERE MemberId = @MemberId
            ORDER BY TechniqueCode";

        return await ExecuteReaderAsync(
            sql,
            reader => new TechniqueComfortDto
            {
                Id = GetGuid(reader, "Id"),
                MemberId = GetGuid(reader, "MemberId"),
                TechniqueCode = GetString(reader, "TechniqueCode") ?? string.Empty,
                ComfortLevel = GetString(reader, "ComfortLevel") ?? string.Empty
            },
            CreateParameter("@MemberId", memberId));
    }

    public async Task InitializeCookProfileAsync(Guid memberId, CancellationToken ct = default)
    {
        const string sql = @"
            IF NOT EXISTS (SELECT 1 FROM CookProfile WHERE MemberId = @MemberId)
            BEGIN
                INSERT INTO CookProfile
                    (Id, MemberId, CooksForHousehold, CookingFrequency,
                     OverallSkillLevel, CookRole, EatingDisorderRecovery, IsDeleted, CreatedAt)
                VALUES
                    (NEWID(), @MemberId, 1, 'Regular', 'HomeCook', 'PrimaryHomeChef', 0, 0, GETUTCDATE());
            END";

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@MemberId", memberId));
    }

    public async Task SoftDeleteCookProfileAsync(Guid memberId, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE CookProfile
            SET IsDeleted = 1,
                UpdatedAt = GETUTCDATE()
            WHERE MemberId = @MemberId AND IsDeleted = 0";

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@MemberId", memberId));

        if (_cache != null)
            await _cache.RemoveAsync($"{CachePrefix}member:{memberId}");
    }

    public async Task DeleteMemberDataAsync(Guid memberId, CancellationToken ct = default)
    {
        const string sql = @"
DELETE FROM DismissedTip      WHERE MemberId = @MemberId;
DELETE FROM TechniqueComfort  WHERE MemberId = @MemberId;
DELETE FROM CookProfile       WHERE MemberId = @MemberId;";

        await ExecuteNonQueryAsync(sql, CreateParameter("@MemberId", memberId));

        if (_cache != null)
            await _cache.RemoveAsync($"{CachePrefix}member:{memberId}");
    }
}
