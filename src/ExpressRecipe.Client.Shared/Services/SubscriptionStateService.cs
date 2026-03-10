namespace ExpressRecipe.Client.Shared.Services;

/// <summary>
/// Scoped per-circuit cache for subscription feature flags and tier name.
/// Ensures all <see cref="FeatureGuard"/> instances on the same page share
/// a single <c>/api/Subscriptions/features</c> fetch instead of issuing one
/// request each.
/// </summary>
public class SubscriptionStateService
{
    private readonly ISubscriptionApiClient _client;

    private Dictionary<string, bool>? _featureMap;
    private string? _tierName;

    public SubscriptionStateService(ISubscriptionApiClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Returns <c>true</c> when the signed-in user can access <paramref name="featureKey"/>.
    /// The feature map is fetched at most once per Blazor circuit/scope.
    /// </summary>
    public async Task<bool> HasFeatureAccessAsync(string featureKey)
    {
        _featureMap ??= await _client.GetFeatureAccessAsync();
        return _client.HasFeatureAccess(featureKey, _featureMap);
    }

    /// <summary>
    /// Returns the current user's tier name (e.g. "Free", "Plus", "Premium").
    /// Fetched at most once per Blazor circuit/scope.
    /// </summary>
    public async Task<string> GetCurrentTierNameAsync()
    {
        _tierName ??= await _client.GetCurrentTierNameAsync();
        return _tierName;
    }

    /// <summary>
    /// Returns the CSS modifier class for a given tier name.
    /// Shared by <c>FeatureGuard</c> and <c>UserInfoBar</c> to keep the
    /// mapping in one place.
    /// </summary>
    public static string GetTierCssClass(string tierName)
    {
        if (string.IsNullOrWhiteSpace(tierName))
            return "tier-free";

        return tierName.Trim().ToLowerInvariant() switch
        {
            "premium"            => "tier-premium",
            "plus"               => "tier-plus",
            "ad-free" or "adfree" => "tier-adfree",
            _                    => "tier-free",
        };
    }
}
