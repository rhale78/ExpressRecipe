namespace ExpressRecipe.Shared.Attributes;

/// <summary>
/// The payload returned with a 402 or 403 when a feature gate blocks access.
/// </summary>
public sealed record FeatureGateResult
{
    public string FeatureKey { get; init; } = string.Empty;
    /// <summary><c>SubscriptionRequired</c> or <c>FeatureDisabled</c>.</summary>
    public string Reason { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    /// <summary>Present only when <see cref="Reason"/> is <c>SubscriptionRequired</c>.</summary>
    public string? UpgradeUrl { get; init; }
}
