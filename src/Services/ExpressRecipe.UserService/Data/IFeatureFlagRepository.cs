using ExpressRecipe.Shared.Services.FeatureGates;

namespace ExpressRecipe.UserService.Data;

public interface IFeatureFlagRepository
{
    /// <summary>Returns the flag definition, or <c>null</c> if the key is unknown.</summary>
    Task<FeatureFlagDto?> GetFlagAsync(string featureKey, CancellationToken ct = default);

    /// <summary>Returns all feature flags (admin listing).</summary>
    Task<List<FeatureFlagDto>> GetAllFlagsAsync(CancellationToken ct = default);

    /// <summary>Creates or updates a feature flag.</summary>
    Task UpsertFlagAsync(FeatureFlagDto flag, CancellationToken ct = default);

    /// <summary>
    /// Returns the active user override, or <c>null</c> if none / expired.
    /// </summary>
    Task<UserFeatureFlagOverrideDto?> GetActiveUserOverrideAsync(Guid userId, string featureKey,
        CancellationToken ct = default);

    /// <summary>Returns all active overrides for a feature key (admin listing).</summary>
    Task<List<UserFeatureFlagOverrideDto>> GetOverridesForFeatureAsync(string featureKey,
        CancellationToken ct = default);

    /// <summary>Sets or replaces the override for a specific user.</summary>
    Task SetUserOverrideAsync(Guid userId, string featureKey, bool isEnabled,
        DateTime? expiresAt, CancellationToken ct = default);

    /// <summary>Removes the override for a specific user.</summary>
    Task RemoveUserOverrideAsync(Guid userId, string featureKey, CancellationToken ct = default);
}
