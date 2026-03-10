using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using System.Text.Json;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace ExpressRecipe.Shared.Middleware;

/// <summary>
/// Distributed rate limiting middleware backed by HybridCache (L1 in-memory + L2 Redis/SQL).
/// Replaces the previous IMemoryCache-only implementation that was per-instance only and
/// would break in a multi-pod deployment. HybridCache coordinates across instances via
/// the configured distributed cache backend.
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly HybridCache _cache;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly RateLimitOptions _options;

    public RateLimitingMiddleware(
        RequestDelegate next,
        HybridCache cache,
        ILogger<RateLimitingMiddleware> logger,
        RateLimitOptions options)
    {
        _next = next;
        _cache = cache;
        _logger = logger;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.Enabled)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.ToString();
        var userId = GetUserId(context);
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var clientKey = userId ?? ipAddress;

        // Determine per-endpoint limit
        var limit = GetLimit(path, userId != null);
        if (limit <= 0)
        {
            // Endpoint is exempt (e.g., webhooks)
            await _next(context);
            return;
        }

        var cacheKey = $"ratelimit:{clientKey}:{path}";
        var windowTtl = TimeSpan.FromSeconds(_options.WindowSeconds);

        // Retrieve current counter; initialise to 0 if absent
        var counter = await _cache.GetOrCreateAsync(
            cacheKey,
            _ => ValueTask.FromResult(new RateLimitCounter()),
            new HybridCacheEntryOptions { Expiration = windowTtl, LocalCacheExpiration = windowTtl });

        if (counter.RequestCount >= limit)
        {
            _logger.LogWarning("Rate limit exceeded for {Client} on {Path}", clientKey, path);

            context.Response.StatusCode = 429;
            context.Response.Headers["Retry-After"] = _options.WindowSeconds.ToString();
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = "Rate limit exceeded",
                retryAfter = _options.WindowSeconds,
                limit
            }));

            return;
        }

        // Increment and persist the counter back into HybridCache
        counter.RequestCount++;
        await _cache.SetAsync(
            cacheKey,
            counter,
            new HybridCacheEntryOptions { Expiration = windowTtl, LocalCacheExpiration = windowTtl });

        // Informational response headers
        context.Response.Headers["X-RateLimit-Limit"] = limit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = Math.Max(0, limit - counter.RequestCount).ToString();

        await _next(context);
    }

    /// <summary>
    /// Returns the request limit for the given path.
    /// Returns 0 to indicate the path is exempt from rate limiting.
    /// </summary>
    private int GetLimit(string path, bool isAuthenticated)
    {
        // Webhook endpoints are exempt
        if (path.StartsWith("/webhooks", StringComparison.OrdinalIgnoreCase))
            return 0;

        // Auth endpoints: tighter per-IP limit (10/min)
        if (path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/auth", StringComparison.OrdinalIgnoreCase))
            return _options.AuthMaxRequestsPerWindow;

        // Scan endpoints (barcode scanning): 30/min per user
        if (path.StartsWith("/api/scan", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/scan/", StringComparison.OrdinalIgnoreCase))
            return _options.ScanMaxRequestsPerWindow;

        // Default API limit
        return _options.MaxRequestsPerWindow;
    }

    private static string? GetUserId(HttpContext context)
        => context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}

public class RateLimitCounter
{
    public int RequestCount { get; set; }
}

public class RateLimitOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>Default API limit: 100 req/min per user.</summary>
    public int MaxRequestsPerWindow { get; set; } = 100;

    /// <summary>Auth endpoint limit: 10 req/min per IP.</summary>
    public int AuthMaxRequestsPerWindow { get; set; } = 10;

    /// <summary>Scan endpoint limit: 30 req/min per user.</summary>
    public int ScanMaxRequestsPerWindow { get; set; } = 30;

    /// <summary>Duration of the rate-limit window in seconds (default: 60).</summary>
    public int WindowSeconds { get; set; } = 60;
}

// Extension method for easy registration
public static class RateLimitingExtensions
{
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app, RateLimitOptions? options = null)
    {
        options ??= new RateLimitOptions();
        return app.UseMiddleware<RateLimitingMiddleware>(options);
    }
}

