using ExpressRecipe.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;

namespace ExpressRecipe.CookbookService.Tests.Helpers;

public static class ControllerTestHelpers
{
    public static ControllerContext CreateAuthenticatedContext(Guid userId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, "testuser")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    public static ControllerContext CreateUnauthenticatedContext()
    {
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity())
            }
        };
    }

    /// <summary>
    /// Creates a real <see cref="HybridCacheService"/> backed by in-process
    /// HybridCache – suitable for unit tests without a live Redis connection.
    /// </summary>
    public static HybridCacheService CreateTestHybridCache()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        var provider = services.BuildServiceProvider();
        var hybridCache = provider.GetRequiredService<HybridCache>();
        var logger = NullLogger<HybridCacheService>.Instance;
        return new HybridCacheService(hybridCache, logger);
    }
}
