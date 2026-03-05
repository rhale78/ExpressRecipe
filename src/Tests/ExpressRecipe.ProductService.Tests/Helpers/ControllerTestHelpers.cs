using ExpressRecipe.Shared.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;

namespace ExpressRecipe.ProductService.Tests.Helpers;

public static class ControllerTestHelpers
{
    public static ControllerContext CreateAuthenticatedContext(Guid userId)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, "test@example.com")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext
        {
            User = claimsPrincipal
        };

        return new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    public static ControllerContext CreateUnauthenticatedContext()
    {
        var identity = new ClaimsIdentity();
        var claimsPrincipal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext
        {
            User = claimsPrincipal
        };

        return new ControllerContext
        {
            HttpContext = httpContext
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
        services.AddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions()));
        services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions())));
        services.AddHybridCache();
        services.AddLogging();
        var sp = services.BuildServiceProvider();
        var hybridCache = sp.GetRequiredService<HybridCache>();
#pragma warning restore EXTEXP0018
        return new HybridCacheService(hybridCache, NullLogger<HybridCacheService>.Instance);
    }
}
