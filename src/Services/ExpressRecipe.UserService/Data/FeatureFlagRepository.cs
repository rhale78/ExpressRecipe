using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.Services;

namespace ExpressRecipe.UserService.Data;

/// <summary>
/// ADO.NET implementation of IFeatureFlagRepository backed by the UserService SQL database.
/// Inherits SqlHelper for built-in deadlock retry, parameterized queries, and named column helpers.
/// </summary>
public sealed class FeatureFlagRepository : SqlHelper, IFeatureFlagRepository
{
    public FeatureFlagRepository(string connectionString) : base(connectionString) { }

    public async Task<FeatureFlagDto?> GetFlagAsync(string featureKey, CancellationToken ct = default)
    {
        const string sql =
            "SELECT FeatureKey, IsEnabled, RolloutPercent, RequiresTier, Description " +
            "FROM FeatureFlag WHERE FeatureKey = @Key AND IsDeleted = 0";

        List<FeatureFlagDto> results = await ExecuteReaderAsync(
            sql,
            r => new FeatureFlagDto
            {
                FeatureKey     = GetString(r, "FeatureKey")!,
                IsEnabled      = GetBoolean(r, "IsEnabled"),
                RolloutPercent = GetInt32(r, "RolloutPercent"),
                RequiresTier   = GetString(r, "RequiresTier"),
                Description    = GetString(r, "Description")
            },
            ct,
            CreateParameter("@Key", featureKey));

        return results.FirstOrDefault();
    }

    public async Task<List<FeatureFlagDto>> GetAllFlagsAsync(CancellationToken ct = default)
    {
        const string sql =
            "SELECT FeatureKey, IsEnabled, RolloutPercent, RequiresTier, Description " +
            "FROM FeatureFlag WHERE IsDeleted = 0 ORDER BY FeatureKey";

        return await ExecuteReaderAsync(
            sql,
            r => new FeatureFlagDto
            {
                FeatureKey     = GetString(r, "FeatureKey")!,
                IsEnabled      = GetBoolean(r, "IsEnabled"),
                RolloutPercent = GetInt32(r, "RolloutPercent"),
                RequiresTier   = GetString(r, "RequiresTier"),
                Description    = GetString(r, "Description")
            },
            ct);
    }

    public async Task UpsertFlagAsync(string featureKey, bool isEnabled, int rolloutPercent,
        string? requiresTier, string? description, Guid updatedBy, CancellationToken ct = default)
    {
        const string sql = @"MERGE FeatureFlag AS t
            USING (SELECT @Key) AS s(FeatureKey)
            ON t.FeatureKey = s.FeatureKey AND t.IsDeleted = 0
            WHEN MATCHED THEN UPDATE SET IsEnabled = @En, RolloutPercent = @Pct,
                RequiresTier = @Tier, Description = @Desc,
                UpdatedBy = @By, UpdatedAt = GETUTCDATE()
            WHEN NOT MATCHED THEN INSERT (FeatureKey, IsEnabled, RolloutPercent, RequiresTier,
                Description, CreatedBy, CreatedAt)
                VALUES (@Key, @En, @Pct, @Tier, @Desc, @By, GETUTCDATE());";

        await ExecuteNonQueryAsync(sql, ct,
            CreateParameter("@Key",  featureKey),
            CreateParameter("@En",   isEnabled),
            CreateParameter("@Pct",  rolloutPercent),
            CreateParameter("@Tier", requiresTier),
            CreateParameter("@Desc", description),
            CreateParameter("@By",   updatedBy));
    }

    public async Task<UserFeatureOverrideDto?> GetUserOverrideAsync(Guid userId,
        string featureKey, CancellationToken ct = default)
    {
        const string sql =
            "SELECT UserId, FeatureKey, IsEnabled, ExpiresAt " +
            "FROM UserFeatureOverride " +
            "WHERE UserId = @UserId AND FeatureKey = @Key AND IsDeleted = 0 " +
            "AND (ExpiresAt IS NULL OR ExpiresAt > GETUTCDATE())";

        List<UserFeatureOverrideDto> results = await ExecuteReaderAsync(
            sql,
            r => new UserFeatureOverrideDto
            {
                UserId     = GetGuid(r, "UserId"),
                FeatureKey = GetString(r, "FeatureKey")!,
                IsEnabled  = GetBoolean(r, "IsEnabled"),
                ExpiresAt  = GetNullableDateTime(r, "ExpiresAt")
            },
            ct,
            CreateParameter("@UserId", userId),
            CreateParameter("@Key",    featureKey));

        return results.FirstOrDefault();
    }

    public async Task<List<UserFeatureOverrideDto>> GetUserOverridesAsync(Guid userId,
        CancellationToken ct = default)
    {
        const string sql =
            "SELECT UserId, FeatureKey, IsEnabled, ExpiresAt " +
            "FROM UserFeatureOverride " +
            "WHERE UserId = @UserId AND IsDeleted = 0 " +
            "AND (ExpiresAt IS NULL OR ExpiresAt > GETUTCDATE())";

        return await ExecuteReaderAsync(
            sql,
            r => new UserFeatureOverrideDto
            {
                UserId     = GetGuid(r, "UserId"),
                FeatureKey = GetString(r, "FeatureKey")!,
                IsEnabled  = GetBoolean(r, "IsEnabled"),
                ExpiresAt  = GetNullableDateTime(r, "ExpiresAt")
            },
            ct,
            CreateParameter("@UserId", userId));
    }

    public async Task UpsertUserOverrideAsync(Guid userId, string featureKey, bool isEnabled,
        string? reason, Guid? grantedBy, DateTime? expiresAt, CancellationToken ct = default)
    {
        const string sql = @"MERGE UserFeatureOverride AS t
            USING (SELECT @UserId, @Key) AS s(UserId, FeatureKey)
            ON t.UserId = s.UserId AND t.FeatureKey = s.FeatureKey AND t.IsDeleted = 0
            WHEN MATCHED THEN UPDATE SET IsEnabled = @En, Reason = @Reason,
                GrantedBy = @GrantedBy, ExpiresAt = @Expires,
                UpdatedBy = @GrantedBy, UpdatedAt = GETUTCDATE()
            WHEN NOT MATCHED THEN INSERT (UserId, FeatureKey, IsEnabled, Reason,
                GrantedBy, ExpiresAt, CreatedBy, CreatedAt)
                VALUES (@UserId, @Key, @En, @Reason, @GrantedBy, @Expires, @GrantedBy, GETUTCDATE());";

        await ExecuteNonQueryAsync(sql, ct,
            CreateParameter("@UserId",    userId),
            CreateParameter("@Key",       featureKey),
            CreateParameter("@En",        isEnabled),
            CreateParameter("@Reason",    reason),
            CreateParameter("@GrantedBy", grantedBy),
            CreateParameter("@Expires",   expiresAt));
    }

    public async Task DeleteUserOverrideAsync(Guid userId, string featureKey,
        CancellationToken ct = default)
    {
        const string sql =
            "DELETE FROM UserFeatureOverride WHERE UserId = @UserId AND FeatureKey = @Key";

        await ExecuteNonQueryAsync(sql, ct,
            CreateParameter("@UserId", userId),
            CreateParameter("@Key",    featureKey));
    }
}
