using ExpressRecipe.Shared.Services;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.UserService.Data;

/// <summary>
/// ADO.NET implementation of IFeatureFlagRepository backed by the UserService SQL database.
/// </summary>
public sealed class FeatureFlagRepository : IFeatureFlagRepository
{
    private readonly string _connectionString;

    public FeatureFlagRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<FeatureFlagDto?> GetFlagAsync(string featureKey, CancellationToken ct = default)
    {
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(
            "SELECT FeatureKey, IsEnabled, RolloutPercent, RequiresTier, Description " +
            "FROM FeatureFlag WHERE FeatureKey = @Key", conn);
        cmd.Parameters.AddWithValue("@Key", featureKey);
        await using SqlDataReader r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) { return null; }
        return new FeatureFlagDto
        {
            FeatureKey     = r.GetString(0),
            IsEnabled      = r.GetBoolean(1),
            RolloutPercent = r.GetInt32(2),
            RequiresTier   = r.IsDBNull(3) ? null : r.GetString(3),
            Description    = r.IsDBNull(4) ? null : r.GetString(4)
        };
    }

    public async Task<List<FeatureFlagDto>> GetAllFlagsAsync(CancellationToken ct = default)
    {
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(
            "SELECT FeatureKey, IsEnabled, RolloutPercent, RequiresTier, Description " +
            "FROM FeatureFlag ORDER BY FeatureKey", conn);
        await using SqlDataReader r = await cmd.ExecuteReaderAsync(ct);
        List<FeatureFlagDto> results = [];
        while (await r.ReadAsync(ct))
        {
            results.Add(new FeatureFlagDto
            {
                FeatureKey     = r.GetString(0),
                IsEnabled      = r.GetBoolean(1),
                RolloutPercent = r.GetInt32(2),
                RequiresTier   = r.IsDBNull(3) ? null : r.GetString(3),
                Description    = r.IsDBNull(4) ? null : r.GetString(4)
            });
        }
        return results;
    }

    public async Task UpsertFlagAsync(string featureKey, bool isEnabled, int rolloutPercent,
        string? requiresTier, string? description, Guid updatedBy, CancellationToken ct = default)
    {
        const string sql = @"MERGE FeatureFlag AS t
            USING (SELECT @Key) AS s(FeatureKey)
            ON t.FeatureKey = s.FeatureKey
            WHEN MATCHED THEN UPDATE SET IsEnabled = @En, RolloutPercent = @Pct,
                RequiresTier = @Tier, Description = @Desc, UpdatedBy = @By, UpdatedAt = GETUTCDATE()
            WHEN NOT MATCHED THEN INSERT (FeatureKey, IsEnabled, RolloutPercent, RequiresTier,
                Description, UpdatedBy, UpdatedAt)
                VALUES (@Key, @En, @Pct, @Tier, @Desc, @By, GETUTCDATE());";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@Key",  featureKey);
        cmd.Parameters.AddWithValue("@En",   isEnabled);
        cmd.Parameters.AddWithValue("@Pct",  rolloutPercent);
        cmd.Parameters.AddWithValue("@Tier", (object?)requiresTier ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Desc", (object?)description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@By",   updatedBy);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<UserFeatureOverrideDto?> GetUserOverrideAsync(Guid userId,
        string featureKey, CancellationToken ct = default)
    {
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(
            "SELECT UserId, FeatureKey, IsEnabled, ExpiresAt " +
            "FROM UserFeatureOverride WHERE UserId = @UserId AND FeatureKey = @Key " +
            "AND (ExpiresAt IS NULL OR ExpiresAt > GETUTCDATE())", conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@Key",    featureKey);
        await using SqlDataReader r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) { return null; }
        return new UserFeatureOverrideDto
        {
            UserId     = r.GetGuid(0),
            FeatureKey = r.GetString(1),
            IsEnabled  = r.GetBoolean(2),
            ExpiresAt  = r.IsDBNull(3) ? null : r.GetDateTime(3)
        };
    }

    public async Task<List<UserFeatureOverrideDto>> GetUserOverridesAsync(Guid userId,
        CancellationToken ct = default)
    {
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(
            "SELECT UserId, FeatureKey, IsEnabled, ExpiresAt " +
            "FROM UserFeatureOverride WHERE UserId = @UserId " +
            "AND (ExpiresAt IS NULL OR ExpiresAt > GETUTCDATE())", conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        await using SqlDataReader r = await cmd.ExecuteReaderAsync(ct);
        List<UserFeatureOverrideDto> results = [];
        while (await r.ReadAsync(ct))
        {
            results.Add(new UserFeatureOverrideDto
            {
                UserId     = r.GetGuid(0),
                FeatureKey = r.GetString(1),
                IsEnabled  = r.GetBoolean(2),
                ExpiresAt  = r.IsDBNull(3) ? null : r.GetDateTime(3)
            });
        }
        return results;
    }

    public async Task UpsertUserOverrideAsync(Guid userId, string featureKey, bool isEnabled,
        string? reason, Guid? grantedBy, DateTime? expiresAt, CancellationToken ct = default)
    {
        const string sql = @"MERGE UserFeatureOverride AS t
            USING (SELECT @UserId, @Key) AS s(UserId, FeatureKey)
            ON t.UserId = s.UserId AND t.FeatureKey = s.FeatureKey
            WHEN MATCHED THEN UPDATE SET IsEnabled = @En, Reason = @Reason,
                GrantedBy = @GrantedBy, ExpiresAt = @Expires
            WHEN NOT MATCHED THEN INSERT (UserId, FeatureKey, IsEnabled, Reason,
                GrantedBy, ExpiresAt, CreatedAt)
                VALUES (@UserId, @Key, @En, @Reason, @GrantedBy, @Expires, GETUTCDATE());";
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.AddWithValue("@UserId",   userId);
        cmd.Parameters.AddWithValue("@Key",      featureKey);
        cmd.Parameters.AddWithValue("@En",       isEnabled);
        cmd.Parameters.AddWithValue("@Reason",   (object?)reason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@GrantedBy", (object?)grantedBy ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Expires",  (object?)expiresAt ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteUserOverrideAsync(Guid userId, string featureKey,
        CancellationToken ct = default)
    {
        await using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        await using SqlCommand cmd = new(
            "DELETE FROM UserFeatureOverride WHERE UserId = @UserId AND FeatureKey = @Key", conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@Key",    featureKey);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
