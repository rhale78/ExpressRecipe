using ExpressRecipe.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
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
    /// MemoryCache + MemoryDistributedCache – suitable for unit tests
    /// without a live Redis connection.
    /// </summary>
    public static HybridCacheService CreateTestHybridCache()
    {
#pragma warning disable EXTEXP0018
        var services = new ServiceCollection();
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var distributedCache = new MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()));
        
        services.AddSingleton<IMemoryCache>(memoryCache);
        services.AddSingleton<IDistributedCache>(distributedCache);
        services.AddHybridCache();
        services.AddLogging();
        
        var sp = services.BuildServiceProvider();
        var hybridCache = sp.GetRequiredService<HybridCache>();
#pragma warning restore EXTEXP0018
        var logger = NullLogger<HybridCacheService>.Instance;
        return new HybridCacheService(hybridCache, logger);
    }
}
