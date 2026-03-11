using ExpressRecipe.Shared.Services.FeatureGates;
using ExpressRecipe.UserService.Data;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace ExpressRecipe.UserService.Services;

/// <summary>
/// DB-backed implementation of <see cref="IFeatureFlagService"/> for UserService.
/// Evaluates the three-layer gate and returns a <see cref="FeatureCheckResult"/>
/// with the precise reason so callers can map to the correct HTTP status code.
/// <list type="number">
///   <item>Layer 1 — Global admin toggle (FeatureFlag.IsEnabled)</item>
///   <item>Layer 2 — Per-user override (UserFeatureFlagOverride) — can override Layer 1</item>
///   <item>Layer 3a — Rollout percentage (deterministic hash of featureKey:userId)</item>
///   <item>Layer 3b — Subscription tier requirement (FeatureFlag.RequiredTier)</item>
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
    public async Task<FeatureCheckResult> IsEnabledAsync(string featureKey, Guid userId,
        string userTier, CancellationToken ct = default)
    {
        string cacheKey = $"ff:enabled:{featureKey}:{userId}:{userTier}";
        if (_cache.TryGetValue(cacheKey, out FeatureCheckResult? cached) && cached != null)
            return cached;

        FeatureCheckResult result;
        try
        {
            result = await EvaluateCoreAsync(featureKey, userId, userTier, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error evaluating feature flag {FeatureKey} for user {UserId}; defaulting to enabled (fail-open)",
                featureKey, userId);
            result = new FeatureCheckResult(true, FeatureCheckReason.Enabled);
        }

        _cache.Set(cacheKey, result, CacheTtl);
        return result;
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

    // ── evaluation core ──────────────────────────────────────────────────────

    private async Task<FeatureCheckResult> EvaluateCoreAsync(string featureKey, Guid userId,
        string userTier, CancellationToken ct)
    {
        var flag = await _repo.GetFlagAsync(featureKey, ct);

        // Unknown feature keys are treated as globally disabled
        if (flag == null)
            return new FeatureCheckResult(false, FeatureCheckReason.GloballyDisabled);

        // Layer 2 — per-user override (wins over global toggle in both directions,
        // allowing beta access or explicit revocations per the product spec).
        var userOverride = await _repo.GetActiveUserOverrideAsync(userId, featureKey, ct);
        if (userOverride != null)
        {
            return userOverride.IsEnabled
                ? new FeatureCheckResult(true,  FeatureCheckReason.Enabled)
                : new FeatureCheckResult(false, FeatureCheckReason.UserDisabled);
        }

        // Layer 1 — global admin toggle
        if (!flag.IsEnabled)
            return new FeatureCheckResult(false, FeatureCheckReason.GloballyDisabled);

        // Layer 3a — rollout percentage (deterministic per-user bucket)
        if (flag.RolloutPercentage < 100)
        {
            int bucket = ComputeRolloutBucket(featureKey, userId);
            if (bucket >= flag.RolloutPercentage)
                return new FeatureCheckResult(false, FeatureCheckReason.NotInRollout);
        }

        // Layer 3b — subscription tier requirement
        if (!string.IsNullOrEmpty(flag.RequiredTier))
        {
            int required = TierRank.GetValueOrDefault(flag.RequiredTier, 99);
            int actual   = TierRank.GetValueOrDefault(userTier, 0);
            if (actual < required)
                return new FeatureCheckResult(false, FeatureCheckReason.TierInsufficient);
        }

        return new FeatureCheckResult(true, FeatureCheckReason.Enabled);
    }

    /// <summary>
    /// Computes a deterministic 0-99 bucket for gradual rollouts using
    /// SHA-256 of <c>"{featureKey}:{userId}"</c>.  Stable across service restarts.
    /// </summary>
    private static int ComputeRolloutBucket(string featureKey, Guid userId)
    {
        var input     = $"{featureKey}:{userId:N}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return (int)(BitConverter.ToUInt32(hashBytes, 0) % 100);
    }
}
