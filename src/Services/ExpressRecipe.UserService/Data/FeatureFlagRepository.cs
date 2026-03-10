using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.Services.FeatureGates;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.UserService.Data;

public class FeatureFlagRepository : SqlHelper, IFeatureFlagRepository
{
    public FeatureFlagRepository(string connectionString) : base(connectionString) { }

    public async Task<FeatureFlagDto?> GetFlagAsync(string featureKey,
        CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, FeatureKey, Description, IsEnabled, RolloutPercentage, RequiredTier,
                   CreatedAt, UpdatedAt
            FROM FeatureFlag
            WHERE FeatureKey = @FeatureKey";

        var rows = await ExecuteReaderAsync(sql, MapFlag,
            new SqlParameter("@FeatureKey", featureKey));
        return rows.FirstOrDefault();
    }

    public async Task<List<FeatureFlagDto>> GetAllFlagsAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, FeatureKey, Description, IsEnabled, RolloutPercentage, RequiredTier,
                   CreatedAt, UpdatedAt
            FROM FeatureFlag
            ORDER BY FeatureKey";

        return await ExecuteReaderAsync(sql, MapFlag);
    }

    public async Task UpsertFlagAsync(FeatureFlagDto flag, CancellationToken ct = default)
    {
        const string sql = @"
            IF EXISTS (SELECT 1 FROM FeatureFlag WHERE FeatureKey = @FeatureKey)
            BEGIN
                UPDATE FeatureFlag
                SET Description       = @Description,
                    IsEnabled         = @IsEnabled,
                    RolloutPercentage = @RolloutPercentage,
                    RequiredTier      = @RequiredTier,
                    UpdatedAt         = GETUTCDATE()
                WHERE FeatureKey = @FeatureKey
            END
            ELSE
            BEGIN
                INSERT INTO FeatureFlag
                    (Id, FeatureKey, Description, IsEnabled, RolloutPercentage, RequiredTier, CreatedAt)
                VALUES
                    (NEWID(), @FeatureKey, @Description, @IsEnabled, @RolloutPercentage, @RequiredTier, GETUTCDATE())
            END";

        await ExecuteNonQueryAsync(sql, ct,
            new SqlParameter("@FeatureKey",        flag.FeatureKey),
            new SqlParameter("@Description",       (object?)flag.Description ?? DBNull.Value),
            new SqlParameter("@IsEnabled",         flag.IsEnabled),
            new SqlParameter("@RolloutPercentage", flag.RolloutPercentage),
            new SqlParameter("@RequiredTier",      (object?)flag.RequiredTier ?? DBNull.Value));
    }

    public async Task<UserFeatureFlagOverrideDto?> GetActiveUserOverrideAsync(Guid userId,
        string featureKey, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, UserId, FeatureKey, IsEnabled, ExpiresAt, CreatedAt
            FROM UserFeatureFlagOverride
            WHERE UserId    = @UserId
              AND FeatureKey = @FeatureKey
              AND (ExpiresAt IS NULL OR ExpiresAt > GETUTCDATE())";

        var rows = await ExecuteReaderAsync(sql, MapOverride,
            new SqlParameter("@UserId",     userId),
            new SqlParameter("@FeatureKey", featureKey));
        return rows.FirstOrDefault();
    }

    public async Task<List<UserFeatureFlagOverrideDto>> GetOverridesForFeatureAsync(
        string featureKey, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT Id, UserId, FeatureKey, IsEnabled, ExpiresAt, CreatedAt
            FROM UserFeatureFlagOverride
            WHERE FeatureKey = @FeatureKey
            ORDER BY CreatedAt DESC";

        return await ExecuteReaderAsync(sql, MapOverride,
            new SqlParameter("@FeatureKey", featureKey));
    }

    public async Task SetUserOverrideAsync(Guid userId, string featureKey, bool isEnabled,
        DateTime? expiresAt, CancellationToken ct = default)
    {
        const string sql = @"
            IF EXISTS (SELECT 1 FROM UserFeatureFlagOverride
                       WHERE UserId = @UserId AND FeatureKey = @FeatureKey)
            BEGIN
                UPDATE UserFeatureFlagOverride
                SET IsEnabled  = @IsEnabled,
                    ExpiresAt  = @ExpiresAt
                WHERE UserId = @UserId AND FeatureKey = @FeatureKey
            END
            ELSE
            BEGIN
                INSERT INTO UserFeatureFlagOverride
                    (Id, UserId, FeatureKey, IsEnabled, ExpiresAt, CreatedAt)
                VALUES
                    (NEWID(), @UserId, @FeatureKey, @IsEnabled, @ExpiresAt, GETUTCDATE())
            END";

        await ExecuteNonQueryAsync(sql, ct,
            new SqlParameter("@UserId",     userId),
            new SqlParameter("@FeatureKey", featureKey),
            new SqlParameter("@IsEnabled",  isEnabled),
            new SqlParameter("@ExpiresAt",  (object?)expiresAt ?? DBNull.Value));
    }

    public async Task RemoveUserOverrideAsync(Guid userId, string featureKey,
        CancellationToken ct = default)
    {
        const string sql = @"
            DELETE FROM UserFeatureFlagOverride
            WHERE UserId = @UserId AND FeatureKey = @FeatureKey";

        await ExecuteNonQueryAsync(sql, ct,
            new SqlParameter("@UserId",     userId),
            new SqlParameter("@FeatureKey", featureKey));
    }

    // ── mappers ──────────────────────────────────────────────────────────────

    private static FeatureFlagDto MapFlag(SqlDataReader r) => new()
    {
        Id                = GetGuid(r, "Id"),
        FeatureKey        = GetString(r, "FeatureKey") ?? string.Empty,
        Description       = GetString(r, "Description") ?? string.Empty,
        IsEnabled         = GetBoolean(r, "IsEnabled"),
        RolloutPercentage = GetInt32(r, "RolloutPercentage"),
        RequiredTier      = GetNullableString(r, "RequiredTier"),
        CreatedAt         = GetDateTime(r, "CreatedAt"),
        UpdatedAt         = GetNullableDateTime(r, "UpdatedAt")
    };

    private static UserFeatureFlagOverrideDto MapOverride(SqlDataReader r) => new()
    {
        Id         = GetGuid(r, "Id"),
        UserId     = GetGuid(r, "UserId"),
        FeatureKey = GetString(r, "FeatureKey") ?? string.Empty,
        IsEnabled  = GetBoolean(r, "IsEnabled"),
        ExpiresAt  = GetNullableDateTime(r, "ExpiresAt"),
        CreatedAt  = GetDateTime(r, "CreatedAt")
    };
}
