using ExpressRecipe.Shared.Services.FeatureGates;
using ExpressRecipe.UserService.Data;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.UserService.Services;

/// <summary>
/// DB-backed implementation of <see cref="IFeatureFlagService"/> for UserService.
/// Checks the three-layer gate:
/// <list type="number">
///   <item>Global admin toggle (FeatureFlag.IsEnabled)</item>
///   <item>Per-user override (UserFeatureFlagOverride)</item>
///   <item>Subscription tier requirement (FeatureFlag.RequiredTier)</item>
/// </list>
/// Results are cached in-process for 30 seconds to reduce DB load.
/// </summary>
public sealed class FeatureFlagService : IFeatureFlagService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private static readonly Dictionary<string, int> TierRank =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "Free",    0 },
            { "AdFree",  1 },
            { "Plus",    2 },
            { "Premium", 3 }
        };

    private readonly IFeatureFlagRepository _repo;
    private readonly IMemoryCache _cache;
    private readonly ILogger<FeatureFlagService> _logger;

    public FeatureFlagService(
        IFeatureFlagRepository repo,
        IMemoryCache cache,
        ILogger<FeatureFlagService> logger)
    {
        _repo   = repo;
        _cache  = cache;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> IsEnabledAsync(string featureKey, Guid userId, string userTier,
        CancellationToken ct = default)
    {
        string cacheKey = $"ff:enabled:{featureKey}:{userId}:{userTier}";
        if (_cache.TryGetValue(cacheKey, out bool cached)) return cached;

        try
        {
            var flag = await _repo.GetFlagAsync(featureKey, ct);

            // Unknown feature keys are treated as globally disabled
            if (flag == null)
            {
                _cache.Set(cacheKey, false, CacheTtl);
                return false;
            }

            // Layer 2 — user override (checked first so it can re-enable a globally-off flag)
            var userOverride = await _repo.GetActiveUserOverrideAsync(userId, featureKey, ct);
            if (userOverride != null)
            {
                bool overrideResult = userOverride.IsEnabled;
                _cache.Set(cacheKey, overrideResult, CacheTtl);
                return overrideResult;
            }

            // Layer 1 — global admin toggle
            if (!flag.IsEnabled)
            {
                _cache.Set(cacheKey, false, CacheTtl);
                return false;
            }

            // Layer 3 — tier requirement
            if (!string.IsNullOrEmpty(flag.RequiredTier))
            {
                int required = TierRank.GetValueOrDefault(flag.RequiredTier, 99);
                int actual   = TierRank.GetValueOrDefault(userTier, 0);
                if (actual < required)
                {
                    _cache.Set(cacheKey, false, CacheTtl);
                    return false;
                }
            }

            _cache.Set(cacheKey, true, CacheTtl);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error evaluating feature flag {FeatureKey} for user {UserId}", featureKey, userId);
            return true; // fail-open
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsGloballyEnabledAsync(string featureKey,
        CancellationToken ct = default)
    {
        string cacheKey = $"ff:global:{featureKey}";
        if (_cache.TryGetValue(cacheKey, out bool cached)) return cached;

        try
        {
            var flag = await _repo.GetFlagAsync(featureKey, ct);
            bool enabled = flag?.IsEnabled ?? false;
            _cache.Set(cacheKey, enabled, CacheTtl);
            return enabled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error evaluating global feature flag {FeatureKey}", featureKey);
            return true; // fail-open
        }
    }

    /// <summary>
    /// Removes the global cached entry for <paramref name="featureKey"/> so the next request
    /// reads fresh data from the DB. Call this after an admin toggle.
    /// <para>
    /// <b>Note:</b> Only the global cache key is removed explicitly. Per-user entries
    /// (<c>ff:enabled:{key}:{userId}:{tier}</c>) will expire naturally within the 30-second
    /// cache TTL. For immediate enforcement, consider shortening <see cref="CacheTtl"/> or
    /// restarting the service.
    /// </para>
    /// </summary>
    public void InvalidateCache(string featureKey)
    {
        _cache.Remove($"ff:global:{featureKey}");
        _logger.LogInformation("Feature flag cache invalidated for {FeatureKey}", featureKey);
    }
}
