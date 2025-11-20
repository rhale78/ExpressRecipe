using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace ExpressRecipe.Shared.Middleware;

/// <summary>
/// Rate limiting middleware with per-user and per-endpoint limits
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly RateLimitOptions _options;

    public RateLimitingMiddleware(
        RequestDelegate next,
        IMemoryCache cache,
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

        var endpoint = context.Request.Path.ToString();
        var userId = GetUserId(context);
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var cacheKey = $"ratelimit:{userId ?? ipAddress}:{endpoint}";

        if (_cache.TryGetValue(cacheKey, out RateLimitCounter? counter))
        {
            if (counter!.RequestCount >= _options.MaxRequestsPerWindow)
            {
                _logger.LogWarning("Rate limit exceeded for {UserId} on {Endpoint}", userId ?? ipAddress, endpoint);

                context.Response.StatusCode = 429; // Too Many Requests
                context.Response.Headers["Retry-After"] = _options.WindowSeconds.ToString();

                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Rate limit exceeded",
                    retryAfter = _options.WindowSeconds,
                    limit = _options.MaxRequestsPerWindow
                });

                return;
            }

            counter.RequestCount++;
        }
        else
        {
            _cache.Set(cacheKey, new RateLimitCounter { RequestCount = 1 },
                TimeSpan.FromSeconds(_options.WindowSeconds));
        }

        // Add rate limit headers
        context.Response.Headers["X-RateLimit-Limit"] = _options.MaxRequestsPerWindow.ToString();
        if (_cache.TryGetValue(cacheKey, out counter))
        {
            context.Response.Headers["X-RateLimit-Remaining"] =
                (_options.MaxRequestsPerWindow - counter!.RequestCount).ToString();
        }

        await _next(context);
    }

    private string? GetUserId(HttpContext context)
    {
        return context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}

public class RateLimitCounter
{
    public int RequestCount { get; set; }
}

public class RateLimitOptions
{
    public bool Enabled { get; set; } = true;
    public int MaxRequestsPerWindow { get; set; } = 100; // requests
    public int WindowSeconds { get; set; } = 60; // 1 minute window
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
