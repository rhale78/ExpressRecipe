using ExpressRecipe.Data.Common;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.UserService.Data;

public interface IReferralRepository
{
    Task<string?> GetActiveCodeForUserAsync(Guid userId, CancellationToken ct = default);
    Task<string> CreateReferralCodeAsync(Guid userId, string code, CancellationToken ct = default);
    Task<int> CountActiveCodesAsync(Guid userId, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(string code, CancellationToken ct = default);
    Task<ReferralCodeDto?> GetCodeByValueAsync(string code, CancellationToken ct = default);
    Task<bool> ApplyCodeToUserAsync(Guid newUserId, string code, CancellationToken ct = default);
    Task<int> CountConversionsThisMonthAsync(Guid referrerId, CancellationToken ct = default);
    Task RecordConversionAsync(Guid referralCodeId, Guid referrerId, Guid referredUserId, int pointsAwarded, CancellationToken ct = default);
    Task IncrementCodeUsageAsync(Guid codeId, CancellationToken ct = default);
    Task<string?> GetReferredByCodeAsync(Guid userId, CancellationToken ct = default);

    // Share links
    Task<string> CreateShareLinkAsync(Guid userId, string entityType, Guid entityId, string token, DateTime expiresAt, CancellationToken ct = default);
    Task<int> CountShareLinksCreatedTodayAsync(Guid userId, CancellationToken ct = default);
    Task<ShareLinkDto?> GetShareLinkByTokenAsync(string token, CancellationToken ct = default);
    Task IncrementShareLinkViewCountAsync(Guid linkId, CancellationToken ct = default);
}

public sealed record ReferralCodeDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string Code { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public int UsageCount { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record ShareLinkDto
{
    public Guid Id { get; init; }
    public Guid CreatedBy { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public Guid EntityId { get; init; }
    public string Token { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
    public int ViewCount { get; init; }
    public DateTime CreatedAt { get; init; }
}

public class ReferralRepository : SqlHelper, IReferralRepository
{
    public ReferralRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task<string?> GetActiveCodeForUserAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT TOP 1 Code
            FROM ReferralCode
            WHERE UserId = @UserId AND IsActive = 1
            ORDER BY CreatedAt ASC";

        return await ExecuteScalarAsync<string?>(sql, ct, new SqlParameter("@UserId", userId));
    }

    public async Task<string> CreateReferralCodeAsync(Guid userId, string code, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO ReferralCode (Id, UserId, Code, IsActive, UsageCount, CreatedAt)
            VALUES (NEWID(), @UserId, @Code, 1, 0, GETUTCDATE())";

        await ExecuteNonQueryAsync(sql, ct,
            new SqlParameter("@UserId", userId),
            new SqlParameter("@Code", code));

        return code;
    }

    public async Task<int> CountActiveCodesAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT COUNT(*)
            FROM ReferralCode
            WHERE UserId = @UserId AND IsActive = 1";

        return await ExecuteScalarAsync<int>(sql, ct, new SqlParameter("@UserId", userId));
    }

    public async Task<bool> CodeExistsAsync(string code, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT COUNT(*)
            FROM ReferralCode
            WHERE Code = @Code";

        return await ExecuteScalarAsync<int>(sql, ct, new SqlParameter("@Code", code)) > 0;
    }

    public async Task<ReferralCodeDto?> GetCodeByValueAsync(string code, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, UserId, Code, IsActive, UsageCount, CreatedAt
            FROM ReferralCode
            WHERE Code = @Code AND IsActive = 1";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new ReferralCodeDto
            {
                Id = GetGuid(reader, "Id"),
                UserId = GetGuid(reader, "UserId"),
                Code = GetString(reader, "Code") ?? string.Empty,
                IsActive = GetBoolean(reader, "IsActive"),
                UsageCount = GetInt(reader, "UsageCount") ?? 0,
                CreatedAt = GetNullableDateTime(reader, "CreatedAt") ?? DateTime.UtcNow
            },
            ct,
            new SqlParameter("@Code", code));

        return results.FirstOrDefault();
    }

    public async Task<bool> ApplyCodeToUserAsync(Guid newUserId, string code, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE UserProfile
            SET ReferredByCode = @Code
            WHERE UserId = @UserId AND ReferredByCode IS NULL";

        var rowsAffected = await ExecuteNonQueryAsync(sql, ct,
            new SqlParameter("@UserId", newUserId),
            new SqlParameter("@Code", code));

        return rowsAffected > 0;
    }

    public async Task<int> CountConversionsThisMonthAsync(Guid referrerId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT COUNT(*)
            FROM ReferralConversion
            WHERE ReferrerId = @ReferrerId
              AND YEAR(ConvertedAt) = YEAR(GETUTCDATE())
              AND MONTH(ConvertedAt) = MONTH(GETUTCDATE())";

        return await ExecuteScalarAsync<int>(sql, ct, new SqlParameter("@ReferrerId", referrerId));
    }

    public async Task RecordConversionAsync(Guid referralCodeId, Guid referrerId, Guid referredUserId, int pointsAwarded, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO ReferralConversion (Id, ReferralCodeId, ReferrerId, ReferredUserId, ConvertedAt, PointsAwarded)
            VALUES (NEWID(), @ReferralCodeId, @ReferrerId, @ReferredUserId, GETUTCDATE(), @PointsAwarded)";

        await ExecuteNonQueryAsync(sql, ct,
            new SqlParameter("@ReferralCodeId", referralCodeId),
            new SqlParameter("@ReferrerId", referrerId),
            new SqlParameter("@ReferredUserId", referredUserId),
            new SqlParameter("@PointsAwarded", pointsAwarded));
    }

    public async Task IncrementCodeUsageAsync(Guid codeId, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE ReferralCode
            SET UsageCount = UsageCount + 1
            WHERE Id = @Id";

        await ExecuteNonQueryAsync(sql, ct, new SqlParameter("@Id", codeId));
    }

    public async Task<string?> GetReferredByCodeAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT ReferredByCode
            FROM UserProfile
            WHERE UserId = @UserId";

        return await ExecuteScalarAsync<string?>(sql, ct, new SqlParameter("@UserId", userId));
    }

    public async Task<string> CreateShareLinkAsync(Guid userId, string entityType, Guid entityId, string token, DateTime expiresAt, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO ShareLink (Id, CreatedBy, EntityType, EntityId, Token, ExpiresAt, ViewCount, CreatedAt)
            VALUES (NEWID(), @CreatedBy, @EntityType, @EntityId, @Token, @ExpiresAt, 0, GETUTCDATE())";

        await ExecuteNonQueryAsync(sql, ct,
            new SqlParameter("@CreatedBy", userId),
            new SqlParameter("@EntityType", entityType),
            new SqlParameter("@EntityId", entityId),
            new SqlParameter("@Token", token),
            new SqlParameter("@ExpiresAt", expiresAt));

        return token;
    }

    public async Task<int> CountShareLinksCreatedTodayAsync(Guid userId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT COUNT(*)
            FROM ShareLink
            WHERE CreatedBy = @CreatedBy
              AND CAST(CreatedAt AS DATE) = CAST(GETUTCDATE() AS DATE)";

        return await ExecuteScalarAsync<int>(sql, ct, new SqlParameter("@CreatedBy", userId));
    }

    public async Task<ShareLinkDto?> GetShareLinkByTokenAsync(string token, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, CreatedBy, EntityType, EntityId, Token, ExpiresAt, ViewCount, CreatedAt
            FROM ShareLink
            WHERE Token = @Token";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new ShareLinkDto
            {
                Id = GetGuid(reader, "Id"),
                CreatedBy = GetGuid(reader, "CreatedBy"),
                EntityType = GetString(reader, "EntityType") ?? string.Empty,
                EntityId = GetGuid(reader, "EntityId"),
                Token = GetString(reader, "Token") ?? string.Empty,
                ExpiresAt = GetNullableDateTime(reader, "ExpiresAt") ?? DateTime.MaxValue,
                ViewCount = GetInt(reader, "ViewCount") ?? 0,
                CreatedAt = GetNullableDateTime(reader, "CreatedAt") ?? DateTime.UtcNow
            },
            ct,
            new SqlParameter("@Token", token));

        return results.FirstOrDefault();
    }

    public async Task IncrementShareLinkViewCountAsync(Guid linkId, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE ShareLink
            SET ViewCount = ViewCount + 1
            WHERE Id = @Id";

        await ExecuteNonQueryAsync(sql, ct, new SqlParameter("@Id", linkId));
    }
}
