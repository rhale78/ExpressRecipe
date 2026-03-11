using Microsoft.Extensions.Caching.Hybrid;

namespace ExpressRecipe.Shared.Services;

/// <summary>
/// Feature flag service — evaluates whether a feature is enabled for a given user.
/// Checks (in order): local mode → user override → global flag → tier requirement → rollout %.
/// </summary>
public interface IFeatureFlagService
{
    /// <summary>
    /// Is this feature available to this specific user?
    /// Checks: local mode → user override → global flag → tier requirement → rollout %
    /// </summary>
    Task<bool> IsEnabledAsync(string featureKey, Guid userId, string userTier,
        CancellationToken ct = default);

    /// <summary>
    /// Is this feature enabled globally (ignores user overrides)?
    /// </summary>
    Task<bool> IsGloballyEnabledAsync(string featureKey, CancellationToken ct = default);

    /// <summary>
    /// For admin UI — get all flags.
    /// </summary>
    Task<List<FeatureFlagDto>> GetAllFlagsAsync(CancellationToken ct = default);
}

/// <summary>
/// HybridCache-backed implementation of IFeatureFlagService.
/// 5-minute TTL on both L1 (in-memory) and L2 (distributed) cache layers.
/// </summary>
public sealed class FeatureFlagService : IFeatureFlagService
{
    private readonly IFeatureFlagRepository _repo;
    private readonly HybridCache _cache;
    private readonly ILocalModeConfig _localMode;

    // Tier hierarchy for "RequiresTier" checks
    private static readonly Dictionary<string, int> TierRank =
        new(StringComparer.OrdinalIgnoreCase)
        { { "Free", 0 }, { "AdFree", 1 }, { "Plus", 2 }, { "Premium", 3 } };

    public FeatureFlagService(IFeatureFlagRepository repo, HybridCache cache,
        ILocalModeConfig localMode)
    {
        _repo = repo;
        _cache = cache;
        _localMode = localMode;
    }

    public async Task<bool> IsEnabledAsync(string featureKey, Guid userId, string userTier,
        CancellationToken ct = default)
    {
        // ── Local mode: everything on ─────────────────────────────────────
        if (_localMode.IsLocalMode) { return true; }

        // ── User override (checked first, takes priority over global flag) ─
        UserFeatureOverrideDto? userOverride = await _cache.GetOrCreateAsync(
            $"feat-override:{userId}:{featureKey}",
            async innerCt => await _repo.GetUserOverrideAsync(userId, featureKey, innerCt),
            new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(5) },
            cancellationToken: ct);

        if (userOverride is not null) { return userOverride.IsEnabled; }

        // ── Global flag ───────────────────────────────────────────────────
        FeatureFlagDto? flag = await _cache.GetOrCreateAsync(
            $"feat-flag:{featureKey}",
            async innerCt => await _repo.GetFlagAsync(featureKey, innerCt),
            new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(5) },
            cancellationToken: ct);

        if (flag is null || !flag.IsEnabled) { return false; }

        // ── Tier requirement ──────────────────────────────────────────────
        if (flag.RequiresTier is not null)
        {
            int required = TierRank.GetValueOrDefault(flag.RequiresTier, 99);
            int actual   = TierRank.GetValueOrDefault(userTier, 0);
            if (actual < required) { return false; }
        }

        // ── Rollout percent — deterministic per user via hash ─────────────
        if (flag.RolloutPercent < 100)
        {
            uint bucket = HashUserId(userId, featureKey) % 100;
            return bucket < (uint)flag.RolloutPercent;
        }

        return true;
    }

    public async Task<bool> IsGloballyEnabledAsync(string featureKey,
        CancellationToken ct = default)
    {
        if (_localMode.IsLocalMode) { return true; }
        FeatureFlagDto? flag = await _cache.GetOrCreateAsync(
            $"feat-flag:{featureKey}",
            async innerCt => await _repo.GetFlagAsync(featureKey, innerCt),
            new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(5) },
            cancellationToken: ct);
        return flag?.IsEnabled ?? false;
    }

    public async Task<List<FeatureFlagDto>> GetAllFlagsAsync(CancellationToken ct = default)
    {
        return await _repo.GetAllFlagsAsync(ct);
    }

    /// <summary>
    /// Deterministic hash — same user always gets same bucket for a given feature.
    /// This prevents users from flickering in/out of a rollout on each request.
    /// Uses explicit little-endian byte ordering for consistent bucketing across architectures.
    /// </summary>
    private static uint HashUserId(Guid userId, string featureKey)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes($"{userId}:{featureKey}");
        byte[] hash  = System.Security.Cryptography.SHA256.HashData(bytes);
        // Read first 4 bytes as little-endian uint32 regardless of host architecture
        return (uint)(hash[0] | (hash[1] << 8) | (hash[2] << 16) | (hash[3] << 24));
    }
}
