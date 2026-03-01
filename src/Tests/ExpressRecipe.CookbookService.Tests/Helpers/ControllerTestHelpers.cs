using ExpressRecipe.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
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
    /// MemoryCache + MemoryDistributedCache – suitable for unit tests
    /// without a live Redis connection.
    /// </summary>
    public static HybridCacheService CreateTestHybridCache()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var distributedCache = new MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()));
        var logger = NullLogger<HybridCacheService>.Instance;
        return new HybridCacheService(memoryCache, distributedCache, logger);
    }
}
