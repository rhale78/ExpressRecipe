using ExpressRecipe.Shared.Services.FeatureGates;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace ExpressRecipe.ServiceDefaults.FeatureGates;

/// <summary>
/// <see cref="IFeatureFlagService"/> implementation for services other than UserService.
/// Delegates to UserService's feature-flag check endpoint with in-process caching.
/// Falls back to <see cref="FeatureCheckReason.Enabled"/> (fail-open) if UserService is
/// unreachable, so a temporary UserService outage does not block all gated actions.
/// </summary>
public sealed class HttpFeatureFlagService : IFeatureFlagService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
    private const string HttpClientName = "UserService";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HybridCache _cache;
    private readonly ILogger<HttpFeatureFlagService> _logger;

    public HttpFeatureFlagService(
        IHttpClientFactory httpClientFactory,
        HybridCache cache,
        ILogger<HttpFeatureFlagService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache             = cache;
        _logger            = logger;
    }

    /// <inheritdoc/>
    public async Task<FeatureCheckResult> IsEnabledAsync(string featureKey, Guid userId,
        string userTier, CancellationToken ct = default)
    {
        string cacheKey = $"ff:enabled:{featureKey}:{userId}:{userTier}";
        return await _cache.GetOrCreateAsync(
            cacheKey,
            async innerCt =>
            {
                try
                {
                    var client = _httpClientFactory.CreateClient(HttpClientName);
                    var response = await client.GetAsync(
                        $"/api/featureflags/check?featureKey={Uri.EscapeDataString(featureKey)}" +
                        $"&userId={userId}&userTier={Uri.EscapeDataString(userTier)}",
                        innerCt);

                    if (response.IsSuccessStatusCode)
                    {
                        var dto = await response.Content.ReadFromJsonAsync<FeatureFlagCheckDto>(
                            cancellationToken: innerCt);
                        if (dto != null)
                        {
                            return new FeatureCheckResult(
                                dto.IsEnabled,
                                Enum.TryParse<FeatureCheckReason>(dto.Reason, out var reason)
                                    ? reason
                                    : (dto.IsEnabled ? FeatureCheckReason.Enabled : FeatureCheckReason.GloballyDisabled));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Feature flag check failed for {FeatureKey}; defaulting to enabled (fail-open)",
                        featureKey);
                }

                return new FeatureCheckResult(true, FeatureCheckReason.Enabled); // fail-open
            },
            new HybridCacheEntryOptions { Expiration = CacheTtl, LocalCacheExpiration = CacheTtl },
            cancellationToken: ct);
    }

    /// <inheritdoc/>
    public async Task<bool> IsGloballyEnabledAsync(string featureKey,
        CancellationToken ct = default)
    {
        string cacheKey = $"ff:global:{featureKey}";
        return await _cache.GetOrCreateAsync(
            cacheKey,
            async innerCt =>
            {
                try
                {
                    var client = _httpClientFactory.CreateClient(HttpClientName);
                    var response = await client.GetAsync(
                        $"/api/featureflags/{Uri.EscapeDataString(featureKey)}/isglobal",
                        innerCt);

                    if (response.IsSuccessStatusCode)
                    {
                        var dto = await response.Content.ReadFromJsonAsync<FeatureFlagCheckDto>(
                            cancellationToken: innerCt);
                        return dto?.IsEnabled ?? true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Global feature flag check failed for {FeatureKey}; defaulting to enabled (fail-open)",
                        featureKey);
                }

                return true; // fail-open
            },
            new HybridCacheEntryOptions { Expiration = CacheTtl, LocalCacheExpiration = CacheTtl },
            cancellationToken: ct);
    }

    private sealed class FeatureFlagCheckDto
    {
        public bool   IsEnabled { get; init; }
        public string Reason    { get; init; } = nameof(FeatureCheckReason.Enabled);
    }
}
