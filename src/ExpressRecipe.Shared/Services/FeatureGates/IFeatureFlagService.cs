namespace ExpressRecipe.Shared.Services.FeatureGates;

/// <summary>
/// Service for evaluating feature flag access.
/// Layer 1 = global admin toggle; Layer 2 = per-user override; Layer 3 = subscription tier.
/// </summary>
public interface IFeatureFlagService
{
    /// <summary>
    /// Evaluates all layers and returns a <see cref="FeatureCheckResult"/> that includes
    /// both the access decision and the specific reason.  Use <see cref="FeatureCheckReason"/>
    /// to distinguish 402 (tier) from 403 (disabled / not rolled out / user revoked) responses.
    /// </summary>
    Task<FeatureCheckResult> IsEnabledAsync(string featureKey, Guid userId, string userTier,
        CancellationToken ct = default);

    /// <summary>
    /// Returns <c>true</c> when the feature's global admin toggle is on.
    /// Does not check user overrides or tier requirements.
    /// </summary>
    Task<bool> IsGloballyEnabledAsync(string featureKey, CancellationToken ct = default);
}
