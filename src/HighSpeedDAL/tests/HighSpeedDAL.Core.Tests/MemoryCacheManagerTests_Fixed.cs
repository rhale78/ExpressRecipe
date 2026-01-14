using FluentAssertions;
using HighSpeedDAL.Core.Caching;
using HighSpeedDAL.Core.Tests.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HighSpeedDAL.Core.Tests.Caching;

/// <summary>
/// Unit tests for MemoryCacheManager - Tests caching logic without a database
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
        // Arrange & Act
        bool contains = await _cache.ContainsAsync(999);

        // Assert
        contains.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabled_CanToggleCache()
    {
        // Arrange
        int key = 1;
        TestEntity entity = new TestEntity { Id = key, Name = "Test" };
        _cache.IsEnabled = true;

        // Act
        await _cache.SetAsync(key, entity);
        _cache.IsEnabled = false;
        TestEntity? disabledResult = await _cache.GetAsync(key);
        _cache.IsEnabled = true;
        TestEntity? enabledResult = await _cache.GetAsync(key);

        // Assert
        disabledResult.Should().BeNull("cache should return null when disabled");
        enabledResult.Should().NotBeNull("cache should return entity when re-enabled");
    }

    [Fact]
    public async Task ConcurrentOperations_HandledCorrectly()
    {
        // Arrange
        var tasks = new List<Task>();

        // Act - Add 100 items concurrently
        for (int i = 0; i < 100; i++)
        {
            int index = i;
            tasks.Add(_cache.SetAsync(index, new TestEntity { Id = index, Name = $"Test{index}" }));
        }
        await Task.WhenAll(tasks);

        // Assert - All items should be in cache
        for (int i = 0; i < 100; i++)
        {
            TestEntity? result = await _cache.GetAsync(i);
            result.Should().NotBeNull();
        }
    }
}
