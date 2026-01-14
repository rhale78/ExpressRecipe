using FluentAssertions;
using HighSpeedDAL.Core.Caching;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HighSpeedDAL.Core.Tests.Caching;

/// <summary>
/// Unit tests for MemoryCacheManager
/// </summary>
public class MemoryCacheManagerTests
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly MemoryCacheManager<TestEntity, int> _cache;

    public MemoryCacheManagerTests()
    {
        _mockLogger = new Mock<ILogger>();
        _cache = new MemoryCacheManager<TestEntity, int>(_mockLogger.Object, maxSize: 100, expirationSeconds: 60);
    }

    [Fact]
    public async Task GetAsync_NonExistentKey_ReturnsNull()
    {
        // Arrange
        int key = 1;

        // Act
        TestEntity? result = await _cache.GetAsync(key);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_ValidEntity_StoresInCache()
    {
        // Arrange
        int key = 1;
        TestEntity entity = new TestEntity { Id = key, Name = "Test" };

        // Act
        await _cache.SetAsync(key, entity);
        TestEntity? result = await _cache.GetAsync(key);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(key);
        result.Name.Should().Be("Test");
    }

    [Fact]
    public async Task RemoveAsync_ExistingKey_RemovesFromCache()
    {
        // Arrange
        int key = 1;
        TestEntity entity = new TestEntity { Id = key, Name = "Test" };
        await _cache.SetAsync(key, entity);

        // Act
        await _cache.RemoveAsync(key);
        TestEntity? result = await _cache.GetAsync(key);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ClearAsync_MultipleEntries_RemovesAll()
    {
        // Arrange
        for (int i = 1; i <= 10; i++)
        {
            await _cache.SetAsync(i, new TestEntity { Id = i, Name = $"Test{i}" });
        }

        // Act
        await _cache.ClearAsync();

        // Assert
        for (int i = 1; i <= 10; i++)
        {
            TestEntity? result = await _cache.GetAsync(i);
            result.Should().BeNull();
        }
    }

    [Fact]
    public async Task ContainsAsync_ExistingKey_ReturnsTrue()
    {
        // Arrange
        int key = 1;
        TestEntity entity = new TestEntity { Id = key, Name = "Test" };
        await _cache.SetAsync(key, entity);

        // Act
        bool contains = await _cache.ContainsAsync(key);

        // Assert
        contains.Should().BeTrue();
    }

    [Fact]
    public async Task ContainsAsync_NonExistentKey_ReturnsFalse()
    {
        // Arrange
        int key = 999;

        // Act
        bool contains = await _cache.ContainsAsync(key);

        // Assert
        contains.Should().BeFalse();
    }

    [Fact]
    public async Task SetAsync_ExceedsMaxSize_EvictsOldestEntry()
    {
        // Arrange
        MemoryCacheManager<TestEntity, int> smallCache = new MemoryCacheManager<TestEntity, int>(
            _mockLogger.Object,
            maxSize: 5,
            expirationSeconds: 0);

        // Add 5 entries
        for (int i = 1; i <= 5; i++)
        {
            await smallCache.SetAsync(i, new TestEntity { Id = i, Name = $"Test{i}" });
            await Task.Delay(10); // Ensure different timestamps
        }

        // Act - Add 6th entry, should evict oldest (1)
        await smallCache.SetAsync(6, new TestEntity { Id = 6, Name = "Test6" });

        // Assert
        TestEntity? evicted = await smallCache.GetAsync(1);
        TestEntity? newest = await smallCache.GetAsync(6);

        evicted.Should().BeNull("oldest entry should be evicted");
        newest.Should().NotBeNull("newest entry should be present");
    }

    [Fact]
    public async Task GetAsync_ExpiredEntry_ReturnsNull()
    {
        // Arrange
        MemoryCacheManager<TestEntity, int> shortLivedCache = new MemoryCacheManager<TestEntity, int>(
            _mockLogger.Object,
            maxSize: 100,
            expirationSeconds: 1); // 1 second expiration

        int key = 1;
        TestEntity entity = new TestEntity { Id = key, Name = "Test" };
        await shortLivedCache.SetAsync(key, entity);

        // Act - Wait for expiration
        await Task.Delay(1100);
        TestEntity? result = await shortLivedCache.GetAsync(key);

        // Assert
        result.Should().BeNull("entry should be expired");
    }

    [Fact]
    public async Task SetAsync_UpdateExistingKey_OverwritesValue()
    {
        // Arrange
        int key = 1;
        TestEntity entity1 = new TestEntity { Id = key, Name = "Original" };
        TestEntity entity2 = new TestEntity { Id = key, Name = "Updated" };

        await _cache.SetAsync(key, entity1);

        // Act
        await _cache.SetAsync(key, entity2);
        TestEntity? result = await _cache.GetAsync(key);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Updated");
    }
}

/// <summary>
/// Unit tests for TwoLayerCacheManager
/// </summary>
public class TwoLayerCacheManagerTests
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly TwoLayerCacheManager<TestEntity, int> _cache;

    public TwoLayerCacheManagerTests()
    {
        _mockLogger = new Mock<ILogger>();
        _cache = new TwoLayerCacheManager<TestEntity, int>(
            _mockLogger.Object,
            maxSize: 100,
            expirationSeconds: 60,
            promotionIntervalSeconds: 1); // Fast promotion for testing
    }

    [Fact]
    public async Task SetAsync_NewEntry_StoresInL2Cache()
    {
        // Arrange
        int key = 1;
        TestEntity entity = new TestEntity { Id = key, Name = "Test" };

        // Act
        await _cache.SetAsync(key, entity);
        TestEntity? result = await _cache.GetAsync(key);

        // Assert
        result.Should().NotBeNull("entry should be in L2 cache");
        result!.Id.Should().Be(key);
    }

    [Fact]
    public async Task GetAsync_AfterPromotion_RetrievesFromL1Cache()
    {
        // Arrange
        int key = 1;
        TestEntity entity = new TestEntity { Id = key, Name = "Test" };
        await _cache.SetAsync(key, entity);

        // Act - Wait for promotion to L1 (1 second timer + buffer)
        await Task.Delay(1500);
        TestEntity? result = await _cache.GetAsync(key);

        // Assert
        result.Should().NotBeNull("entry should be promoted to L1 cache");
        result!.Name.Should().Be("Test");
    }

    [Fact]
    public async Task GetAsync_L1Hit_ShouldBeFasterThanL2()
    {
        // Arrange
        List<int> keys = Enumerable.Range(1, 100).ToList();
        foreach (int key in keys)
        {
            await _cache.SetAsync(key, new TestEntity { Id = key, Name = $"Test{key}" });
        }

        // Measure L2 performance
        DateTime l2Start = DateTime.UtcNow;
        for (int i = 0; i < 1000; i++)
        {
            TestEntity? result = await _cache.GetAsync(50);
        }
        TimeSpan l2Time = DateTime.UtcNow - l2Start;

        // Wait for promotion to L1
        await Task.Delay(1500);

        // Measure L1 performance
        DateTime l1Start = DateTime.UtcNow;
        for (int i = 0; i < 1000; i++)
        {
            TestEntity? result = await _cache.GetAsync(50);
        }
        TimeSpan l1Time = DateTime.UtcNow - l1Start;

        // Assert - L1 should be faster (lock-free read vs ConcurrentDictionary)
        l1Time.Should().BeLessThan(l2Time, "L1 cache should be faster than L2 cache");
    }

    [Fact]
    public async Task RemoveAsync_ExistingEntry_RemovesFromL2()
    {
        // Arrange
        int key = 1;
        TestEntity entity = new TestEntity { Id = key, Name = "Test" };
        await _cache.SetAsync(key, entity);

        // Act
        await _cache.RemoveAsync(key);

        // Wait for promotion (should not promote removed item)
        await Task.Delay(1500);

        TestEntity? result = await _cache.GetAsync(key);

        // Assert
        result.Should().BeNull("removed entry should not be in cache");
    }

    [Fact]
    public async Task ClearAsync_MultipleEntries_ClearsBothLayers()
    {
        // Arrange
        for (int i = 1; i <= 10; i++)
        {
            await _cache.SetAsync(i, new TestEntity { Id = i, Name = $"Test{i}" });
        }

        // Wait for promotion to L1
        await Task.Delay(1500);

        // Act
        await _cache.ClearAsync();

        // Assert
        for (int i = 1; i <= 10; i++)
        {
            TestEntity? result = await _cache.GetAsync(i);
            result.Should().BeNull($"entry {i} should be cleared from both layers");
        }
    }
}

/// <summary>
/// Test entity for cache testing
/// </summary>
public class TestEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
