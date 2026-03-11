namespace ExpressRecipe.Shared.Services.FeatureGates;

/// <summary>
/// Describes why a feature gate evaluation returned a specific result.
/// </summary>
public enum FeatureCheckReason
{
    /// <summary>The feature is enabled for this user.</summary>
    Enabled,
    /// <summary>The global admin toggle is off and no active user override enables it.</summary>
    GloballyDisabled,
    /// <summary>The feature is globally on and rolled out, but the user's tier is below the requirement.</summary>
    TierInsufficient,
    /// <summary>An explicit per-user override disables this feature for this user.</summary>
    UserDisabled,
    /// <summary>The feature is globally on but not yet rolled out to this user's bucket.</summary>
    NotInRollout
}

/// <summary>
/// Result of a feature flag evaluation, including the reason for the outcome.
/// </summary>
/// <param name="IsEnabled">Whether the feature is enabled for this user.</param>
/// <param name="Reason">The reason for the evaluation outcome.</param>
public sealed record FeatureCheckResult(bool IsEnabled, FeatureCheckReason Reason = FeatureCheckReason.Enabled);
