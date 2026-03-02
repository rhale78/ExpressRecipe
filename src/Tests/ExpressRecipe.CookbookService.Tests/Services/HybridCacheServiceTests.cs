using ExpressRecipe.CookbookService.Tests.Helpers;
using ExpressRecipe.Shared.Services;
using Xunit;

namespace ExpressRecipe.CookbookService.Tests.Services;

/// <summary>
/// Unit tests for <see cref="HybridCacheService"/> using in-process
/// MemoryCache + MemoryDistributedCache (no Redis required).
/// </summary>
public class HybridCacheServiceTests
{
    private readonly HybridCacheService _cache = ControllerTestHelpers.CreateTestHybridCache();

    // ── GetOrSetAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrSetAsync_CacheMiss_CallsFactory()
    {
        var key = $"test:{Guid.NewGuid()}";
        var factoryCalled = 0;

        var result = await _cache.GetOrSetAsync<string?>(key, _ =>
        {
            factoryCalled++;
            return ValueTask.FromResult<string?>("hello");
        });

        Assert.Equal("hello", result);
        Assert.Equal(1, factoryCalled);
    }

    [Fact]
    public async Task GetOrSetAsync_CacheHit_DoesNotCallFactory()
    {
        var key = $"test:{Guid.NewGuid()}";
        var factoryCalled = 0;

        // First call – populate the cache
        await _cache.GetOrSetAsync<string?>(key, _ =>
        {
            factoryCalled++;
            return ValueTask.FromResult<string?>("value");
        });
        // Second call – should hit the cache
        var result = await _cache.GetOrSetAsync<string?>(key, _ =>
        {
            factoryCalled++;
            return ValueTask.FromResult<string?>("different");
        });

        Assert.Equal("value", result);
        Assert.Equal(1, factoryCalled); // factory called only once
    }

    [Fact]
    public async Task GetOrSetAsync_NullFactory_StillReturnsNull()
    {
        var key = $"test:{Guid.NewGuid()}";

        var result = await _cache.GetOrSetAsync<string?>(key, _ => ValueTask.FromResult<string?>(null));

        Assert.Null(result);
    }

    // ── SetAsync / GetAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task SetAsync_ThenGetAsync_ReturnsCachedValue()
    {
        var key = $"test:{Guid.NewGuid()}";
        await _cache.SetAsync(key, 42);

        var result = await _cache.GetAsync<int>(key);

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task GetAsync_UnknownKey_ReturnsDefault()
    {
        var result = await _cache.GetAsync<string>("no-such-key:" + Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_ComplexObject_RoundTripsCorrectly()
    {
        var key = $"test:{Guid.NewGuid()}";
        var obj = new TestCookbookData { Id = Guid.NewGuid(), Name = "Test Cookbook" };

        await _cache.SetAsync(key, obj);
        var result = await _cache.GetAsync<TestCookbookData>(key);

        Assert.NotNull(result);
        Assert.Equal(obj.Id, result!.Id);
        Assert.Equal(obj.Name, result.Name);
    }

    [Fact]
    public async Task SetAsync_OverwritesExistingEntry()
    {
        var key = $"test:{Guid.NewGuid()}";
        await _cache.SetAsync(key, "first");
        await _cache.SetAsync(key, "second");

        var result = await _cache.GetAsync<string>(key);

        Assert.Equal("second", result);
    }

    // ── RemoveAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveAsync_ExistingKey_SubsequentGetReturnsDefault()
    {
        var key = $"test:{Guid.NewGuid()}";
        await _cache.SetAsync(key, "to-delete");

        await _cache.RemoveAsync(key);
        var result = await _cache.GetAsync<string>(key);

        Assert.Null(result);
    }

    [Fact]
    public async Task RemoveAsync_NonExistentKey_DoesNotThrow()
    {
        // Should not throw even if key never existed
        var exception = await Record.ExceptionAsync(() =>
            _cache.RemoveAsync("no-such-key:" + Guid.NewGuid()));

        Assert.Null(exception);
    }

    // ── Cookbook-specific cache key patterns ──────────────────────────────────

    [Fact]
    public async Task CookbookById_KeyPattern_SetAndGet()
    {
        var id = Guid.NewGuid();
        var key = string.Format(CacheKeys.CookbookById, id);
        var value = $"cookbook-data-{id}";

        await _cache.SetAsync(key, value);
        var result = await _cache.GetAsync<string>(key);

        Assert.Equal(value, result);
    }

    [Fact]
    public async Task CookbookBySlug_KeyPattern_SetAndGet()
    {
        var slug = "my-great-cookbook";
        var key = string.Format(CacheKeys.CookbookBySlug, slug);
        var value = "slug-cookbook-data";

        await _cache.SetAsync(key, value);
        var result = await _cache.GetAsync<string>(key);

        Assert.Equal(value, result);
    }

    [Fact]
    public async Task DifferentCookbookIds_HaveIndependentCacheEntries()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var key1 = string.Format(CacheKeys.CookbookById, id1);
        var key2 = string.Format(CacheKeys.CookbookById, id2);

        await _cache.SetAsync(key1, "book-one");
        await _cache.SetAsync(key2, "book-two");

        Assert.Equal("book-one", await _cache.GetAsync<string>(key1));
        Assert.Equal("book-two", await _cache.GetAsync<string>(key2));
    }

    [Fact]
    public async Task RemoveCookbookById_DoesNotAffectOtherCookbook()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var key1 = string.Format(CacheKeys.CookbookById, id1);
        var key2 = string.Format(CacheKeys.CookbookById, id2);

        await _cache.SetAsync(key1, "book-one");
        await _cache.SetAsync(key2, "book-two");
        await _cache.RemoveAsync(key1);

        Assert.Null(await _cache.GetAsync<string>(key1));
        Assert.Equal("book-two", await _cache.GetAsync<string>(key2));
    }

    // ── Custom expiry ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SetAsync_WithExplicitExpiry_ValueRetrievable()
    {
        var key = $"test:{Guid.NewGuid()}";
        // Use a generous expiry – the test should pass well within this window
        await _cache.SetAsync(key, "expiring-value",
            expiration: TimeSpan.FromMinutes(5));

        var result = await _cache.GetAsync<string>(key);

        Assert.Equal("expiring-value", result);
    }

    // ── CacheKeys constants ───────────────────────────────────────────────────

    [Fact]
    public void CacheKeys_CookbookById_FormatProducesExpectedKey()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var key = string.Format(CacheKeys.CookbookById, id);

        Assert.Equal($"cookbook:id:{id}", key);
    }

    [Fact]
    public void CacheKeys_CookbookBySlug_FormatProducesExpectedKey()
    {
        var key = string.Format(CacheKeys.CookbookBySlug, "my-slug");

        Assert.Equal("cookbook:slug:my-slug", key);
    }

    [Fact]
    public void CacheKeys_FormatKey_HelperProducesSameResult()
    {
        var id = Guid.NewGuid();
        var direct = string.Format(CacheKeys.CookbookById, id);
        var helper = CacheKeys.FormatKey(CacheKeys.CookbookById, id);

        Assert.Equal(direct, helper);
    }

    // Helper DTO for round-trip serialization tests
    private class TestCookbookData
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
