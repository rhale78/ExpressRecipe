namespace ExpressRecipe.Shared.Services.FeatureGates;

/// <summary>
/// Represents a feature flag definition stored in the database.
/// </summary>
public sealed class FeatureFlagDto
{
    public Guid Id { get; set; }
    public string FeatureKey { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public int RolloutPercentage { get; set; } = 100;
    /// <summary>Minimum tier required to access this feature. <c>null</c> = no tier restriction.</summary>
    public string? RequiredTier { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Represents a per-user override for a feature flag (e.g. beta access).
/// </summary>
public sealed class UserFeatureFlagOverrideDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string FeatureKey { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
