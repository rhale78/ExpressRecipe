using Microsoft.Extensions.Configuration;

namespace ExpressRecipe.Shared.Services;

/// <summary>
/// Determines whether advertisements should be shown and provides ad slot markup.
/// </summary>
public interface IAdService
{
    /// <summary>
    /// Returns <c>true</c> when ads should be shown for the supplied subscription tier name.
    /// Ads are only shown for Free-tier users; all paid tiers suppress ads.
    /// </summary>
    bool ShouldShowAds(string subscriptionTier);

    /// <summary>
    /// Returns the HTML snippet for an AdSense banner slot, or <c>null</c> when ads are suppressed.
    /// </summary>
    string? GetBannerAdSlot(string placementId);
}

/// <summary>
/// Google AdSense integration. Ads are suppressed when the ADS_DISABLED environment
/// variable is set to "true" or when no publisher ID is configured.
/// </summary>
public sealed class AdSenseAdService : IAdService
{
    private readonly bool _adsEnabled;
    private readonly string _publisherId;

    public AdSenseAdService(IConfiguration config)
    {
        bool envDisabled = string.Equals(
            config["ADS_DISABLED"], "true", StringComparison.OrdinalIgnoreCase);

        _publisherId = config["AdSense:PublisherId"] ?? string.Empty;
        _adsEnabled = !envDisabled && !string.IsNullOrEmpty(_publisherId);
    }

    /// <inheritdoc/>
    public bool ShouldShowAds(string subscriptionTier)
        => _adsEnabled && string.Equals(subscriptionTier, "Free", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public string? GetBannerAdSlot(string placementId)
    {
        if (!_adsEnabled)
        {
            return null;
        }

        // Allow only alphanumeric, hyphens, and underscores in the placement ID to
        // prevent attribute injection via an untrusted or misconfigured placement ID.
        string safeSlot = System.Text.RegularExpressions.Regex.Replace(
            placementId, @"[^A-Za-z0-9\-_]", string.Empty);

        return $"<ins class=\"adsbygoogle\" " +
               $"data-ad-client=\"{_publisherId}\" " +
               $"data-ad-slot=\"{safeSlot}\"></ins>";
    }
}

/// <summary>
/// No-op ad service used in local / development mode or when ads are disabled.
/// </summary>
public sealed class DisabledAdService : IAdService
{
    /// <inheritdoc/>
    public bool ShouldShowAds(string subscriptionTier) => false;

    /// <inheritdoc/>
    public string? GetBannerAdSlot(string placementId) => null;
}
