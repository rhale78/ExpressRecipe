namespace ExpressRecipe.Shared.Services.FeatureGates;

/// <summary>
/// Service for evaluating feature flag access.
/// Layer 1 = global admin toggle; Layer 2 = per-user override; Layer 3 = subscription tier.
/// </summary>
public interface IFeatureFlagService
{
    /// <summary>
    /// Returns <c>true</c> when the feature is accessible to the given user and tier.
    /// Applies all layers: global flag → user override → tier requirement.
    /// </summary>
    Task<bool> IsEnabledAsync(string featureKey, Guid userId, string userTier,
        CancellationToken ct = default);

    /// <summary>
    /// Returns <c>true</c> when the feature's global admin toggle is on.
    /// Does not check user overrides or tier requirements.
    /// </summary>
    Task<bool> IsGloballyEnabledAsync(string featureKey, CancellationToken ct = default);
}
