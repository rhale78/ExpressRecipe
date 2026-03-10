using ExpressRecipe.Shared.DTOs.User;

namespace ExpressRecipe.Client.Shared.Services;

/// <summary>
/// API client for subscription tier and feature-gate queries.
/// </summary>
public interface ISubscriptionApiClient
{
    /// <summary>Returns the full feature-access map for the signed-in user.</summary>
    Task<Dictionary<string, bool>> GetFeatureAccessAsync();

    /// <summary>
    /// Returns <c>true</c> when the signed-in user's active subscription includes
    /// the specified <paramref name="featureKey"/> (e.g. "advanced-reports").
    /// </summary>
    Task<bool> HasFeatureAccessAsync(string featureKey);

    /// <summary>Returns the tier name for the signed-in user (e.g. "Free", "Plus", "Premium").</summary>
    Task<string> GetCurrentTierNameAsync();
}

public class SubscriptionApiClient : ApiClientBase, ISubscriptionApiClient
{
    // Maps the UI-facing kebab-case keys used by FeatureGuard to the
    // PascalCase keys returned by the backend feature-access endpoint.
    private static readonly Dictionary<string, string> FeatureKeyMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["advanced-reports"]  = "AdvancedReports",
            ["inventory-tracking"] = "InventoryTracking",
            ["price-tracking"]    = "PriceTracking",
            ["price-comparison"]  = "PriceComparison",
            ["offline-sync"]      = "OfflineSync",
            ["recipe-import"]     = "RecipeImport",
            ["menu-planning"]     = "MenuPlanning",
        };

    public SubscriptionApiClient(HttpClient httpClient, ITokenProvider tokenProvider)
        : base(httpClient, tokenProvider) { }

    /// <inheritdoc/>
    public async Task<Dictionary<string, bool>> GetFeatureAccessAsync()
    {
        try
        {
            return await GetAsync<Dictionary<string, bool>>("/api/Subscriptions/features")
                   ?? new Dictionary<string, bool>();
        }
        catch (ApiException)
        {
            // Feature endpoint not yet reachable; treat as no features granted.
            return new Dictionary<string, bool>();
        }
    }

    /// <inheritdoc/>
    public async Task<bool> HasFeatureAccessAsync(string featureKey)
    {
        var features = await GetFeatureAccessAsync();

        // Try the mapped PascalCase key first, then fall back to the raw key.
        var lookupKey = FeatureKeyMap.TryGetValue(featureKey, out var mapped) ? mapped : featureKey;

        return features.TryGetValue(lookupKey, out var hasAccess) && hasAccess;
    }

    /// <inheritdoc/>
    public async Task<string> GetCurrentTierNameAsync()
    {
        try
        {
            var subscription = await GetAsync<UserSubscriptionDto>("/api/Subscriptions/current");
            return subscription?.TierName ?? "Free";
        }
        catch (ApiException)
        {
            // Subscription endpoint not yet reachable; default to Free tier.
            return "Free";
        }
    }
}
