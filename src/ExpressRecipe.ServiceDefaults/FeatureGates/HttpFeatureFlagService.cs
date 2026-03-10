using ExpressRecipe.Shared.Services.FeatureGates;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace ExpressRecipe.ServiceDefaults.FeatureGates;

/// <summary>
/// <see cref="IFeatureFlagService"/> implementation for services other than UserService.
/// Delegates to UserService's feature-flag check endpoint with in-process caching.
/// Falls back to <c>true</c> (fail-open) if UserService is unreachable, so that a
/// temporary UserService outage does not block all gated actions.
/// </summary>
public sealed class HttpFeatureFlagService : IFeatureFlagService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
    private const string HttpClientName = "UserService";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<HttpFeatureFlagService> _logger;

    public HttpFeatureFlagService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<HttpFeatureFlagService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache             = cache;
        _logger            = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> IsEnabledAsync(string featureKey, Guid userId, string userTier,
        CancellationToken ct = default)
    {
        string cacheKey = $"ff:enabled:{featureKey}:{userId}:{userTier}";
        if (_cache.TryGetValue(cacheKey, out bool cached)) return cached;

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var response = await client.GetAsync(
                $"/api/featureflags/check?featureKey={Uri.EscapeDataString(featureKey)}" +
                $"&userId={userId}&userTier={Uri.EscapeDataString(userTier)}",
                ct);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<FeatureFlagCheckResult>(
                    cancellationToken: ct);
                bool enabled = result?.IsEnabled ?? true;
                _cache.Set(cacheKey, enabled, CacheTtl);
                return enabled;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Feature flag check failed for {FeatureKey}; defaulting to enabled (fail-open)",
                featureKey);
        }

        return true; // fail-open
    }

    /// <inheritdoc/>
    public async Task<bool> IsGloballyEnabledAsync(string featureKey,
        CancellationToken ct = default)
    {
        string cacheKey = $"ff:global:{featureKey}";
        if (_cache.TryGetValue(cacheKey, out bool cached)) return cached;

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var response = await client.GetAsync(
                $"/api/featureflags/{Uri.EscapeDataString(featureKey)}/isglobal",
                ct);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<FeatureFlagCheckResult>(
                    cancellationToken: ct);
                bool enabled = result?.IsEnabled ?? true;
                _cache.Set(cacheKey, enabled, CacheTtl);
                return enabled;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Global feature flag check failed for {FeatureKey}; defaulting to enabled (fail-open)",
                featureKey);
        }

        return true; // fail-open
    }

    private sealed class FeatureFlagCheckResult
    {
        public bool IsEnabled { get; init; }
    }
}
