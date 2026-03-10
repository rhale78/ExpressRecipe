namespace ExpressRecipe.Shared.Services;

/// <summary>
/// Data transfer object for a global feature flag record.
/// </summary>
public sealed record FeatureFlagDto
{
    public string FeatureKey { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    public int RolloutPercent { get; init; }
    public string? RequiresTier { get; init; }
    public string? Description { get; init; }
}

/// <summary>
/// Data transfer object for a per-user feature flag override record.
/// </summary>
public sealed record UserFeatureOverrideDto
{
    public Guid UserId { get; init; }
    public string FeatureKey { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    public DateTime? ExpiresAt { get; init; }
}

/// <summary>
/// Repository interface for feature flag data access.
/// Implemented by FeatureFlagRepository in ExpressRecipe.UserService.
/// </summary>
public interface IFeatureFlagRepository
{
    Task<FeatureFlagDto?> GetFlagAsync(string featureKey, CancellationToken ct = default);
    Task<List<FeatureFlagDto>> GetAllFlagsAsync(CancellationToken ct = default);
    Task UpsertFlagAsync(string featureKey, bool isEnabled, int rolloutPercent,
        string? requiresTier, string? description, Guid updatedBy, CancellationToken ct = default);

    Task<UserFeatureOverrideDto?> GetUserOverrideAsync(Guid userId, string featureKey,
        CancellationToken ct = default);
    Task<List<UserFeatureOverrideDto>> GetUserOverridesAsync(Guid userId,
        CancellationToken ct = default);
    Task UpsertUserOverrideAsync(Guid userId, string featureKey, bool isEnabled,
        string? reason, Guid? grantedBy, DateTime? expiresAt, CancellationToken ct = default);
    Task DeleteUserOverrideAsync(Guid userId, string featureKey, CancellationToken ct = default);
}
