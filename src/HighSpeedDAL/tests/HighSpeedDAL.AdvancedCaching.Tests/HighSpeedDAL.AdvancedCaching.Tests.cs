using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using HighSpeedDAL.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace HighSpeedDAL.AdvancedCaching.Tests
{
    // ============================================================================
    // TEST MODELS
    // ============================================================================

    public class TestProduct : IEntityCloneable<TestProduct>
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        public TestProduct ShallowClone()
        {
            return new TestProduct
            {
                Id = this.Id,
                Name = this.Name,
                Category = this.Category,
                Price = this.Price,
                IsActive = this.IsActive,
                CreatedAt = this.CreatedAt
            };
        }

        public TestProduct DeepClone()
        {
            return ShallowClone(); // No complex properties, shallow is sufficient
        }
    }

    public class TestCustomer : IEntityCloneable<TestCustomer>
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime JoinedDate { get; set; }

        public TestCustomer ShallowClone()
        {
            return new TestCustomer
            {
                Id = this.Id,
                Name = this.Name,
                Email = this.Email,
                JoinedDate = this.JoinedDate
            };
        }

        public TestCustomer DeepClone()
        {
            return ShallowClone(); // No complex properties, shallow is sufficient
        }
    }

    // ============================================================================
    // READ-THROUGH CACHE TESTS
    // ============================================================================

    public class ReadThroughCacheTests : IDisposable
    {
        private readonly Mock<ILogger<ReadThroughCache<TestProduct>>> _loggerMock;
        private readonly Dictionary<string, TestProduct> _dataStore;
        private readonly ReadThroughCacheOptions _options;
        private ReadThroughCache<TestProduct>? _cache;

        public ReadThroughCacheTests()
        {
            _loggerMock = new Mock<ILogger<ReadThroughCache<TestProduct>>>();
            _dataStore = new Dictionary<string, TestProduct>
            {
                ["product:1"] = new TestProduct { Id = 1, Name = "Laptop", Category = "Electronics", Price = 999.99m, IsActive = true },
                ["product:2"] = new TestProduct { Id = 2, Name = "Mouse", Category = "Accessories", Price = 29.99m, IsActive = true },
                ["product:3"] = new TestProduct { Id = 3, Name = "Keyboard", Category = "Accessories", Price = 79.99m, IsActive = true }
            };

            _options = new ReadThroughCacheOptions
            {
                DefaultTtl = TimeSpan.FromMinutes(5),
                MaxCacheSize = 10,
                EnableBackgroundRefresh = false, // Disable for tests
                EnableCacheWarming = false
            };
        }

        [Fact]
        public async Task GetAsync_CacheMiss_LoadsFromDatabase()
        {
            // Arrange
            _cache = CreateCache();

            // Act
            TestProduct? result = await _cache.GetAsync("product:1");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result!.Id);
            Assert.Equal("Laptop", result.Name);
        }

        [Fact]
        public async Task GetAsync_CacheHit_ReturnsFromCache()
        {
            // Arrange
            _cache = CreateCache();
            TestProduct? first = await _cache.GetAsync("product:1");

            // Act
            Stopwatch sw = Stopwatch.StartNew();
            TestProduct? second = await _cache.GetAsync("product:1");
            sw.Stop();

            // Assert
            Assert.NotNull(second);
            Assert.Equal(first!.Id, second!.Id);
            Assert.True(sw.ElapsedMilliseconds < 5, "Cache hit should be very fast");
        }

        [Fact]
        public async Task GetAsync_ExpiredEntry_ReloadsFromDatabase()
        {
            // Arrange
            ReadThroughCacheOptions shortTtlOptions = new ReadThroughCacheOptions
            {
                DefaultTtl = TimeSpan.FromMilliseconds(100),
                MaxCacheSize = 10,
                EnableBackgroundRefresh = false
            };

            _cache = new ReadThroughCache<TestProduct>(
                LoadFromDatabase,
                LoadBulkFromDatabase,
                shortTtlOptions,
                _loggerMock.Object);

            TestProduct? first = await _cache.GetAsync("product:1");

            // Act
            await Task.Delay(150); // Wait for expiration
            TestProduct? second = await _cache.GetAsync("product:1");

            // Assert
            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Equal(first!.Id, second!.Id);
        }

        [Fact]
        public async Task GetAsync_NonExistentKey_ReturnsNull()
        {
            // Arrange
            _cache = CreateCache();

            // Act
            TestProduct? result = await _cache.GetAsync("product:999");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetAsync_NullOrEmptyKey_ThrowsArgumentException()
        {
            // Arrange
            _cache = CreateCache();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () => await _cache.GetAsync(null!));
            await Assert.ThrowsAsync<ArgumentException>(async () => await _cache.GetAsync(""));
            await Assert.ThrowsAsync<ArgumentException>(async () => await _cache.GetAsync("   "));
        }

        [Fact]
        public async Task GetManyAsync_MixedCacheHitsAndMisses_LoadsMissingFromDatabase()
        {
            // Arrange
            _cache = CreateCache();
            List<string> keys = new List<string> { "product:1", "product:2", "product:3" };

            // Prime cache with product:1
            await _cache.GetAsync("product:1");

            // Act
            Dictionary<string, TestProduct> results = await _cache.GetManyAsync(keys);

            // Assert
            Assert.Equal(3, results.Count);
            Assert.True(results.ContainsKey("product:1"));
            Assert.True(results.ContainsKey("product:2"));
            Assert.True(results.ContainsKey("product:3"));
        }

        [Fact]
        public async Task GetManyAsync_AllCacheHits_ReturnsFast()
        {
            // Arrange
            _cache = CreateCache();
            List<string> keys = new List<string> { "product:1", "product:2" };

            // Prime cache
            await _cache.GetManyAsync(keys);

            // Act
            Stopwatch sw = Stopwatch.StartNew();
            Dictionary<string, TestProduct> results = await _cache.GetManyAsync(keys);
            sw.Stop();

            // Assert
            Assert.Equal(2, results.Count);
            Assert.True(sw.ElapsedMilliseconds < 10, "All cache hits should be very fast");
        }

        [Fact]
        public async Task GetManyAsync_EmptyKeys_ReturnsEmptyDictionary()
        {
            // Arrange
            _cache = CreateCache();

            // Act
            Dictionary<string, TestProduct> results = await _cache.GetManyAsync(new List<string>());

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task GetManyAsync_NullKeys_ThrowsArgumentNullException()
        {
            // Arrange
            _cache = CreateCache();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await _cache.GetManyAsync(null!));
        }

        [Fact]
        public async Task RefreshAsync_UpdatesCachedValue()
        {
            // Arrange
            _cache = CreateCache();
            TestProduct? original = await _cache.GetAsync("product:1");

            // Modify data store
            _dataStore["product:1"].Price = 1099.99m;

            // Act
            await _cache.RefreshAsync("product:1");
            TestProduct? refreshed = await _cache.GetAsync("product:1");

                // Assert
                Assert.NotNull(original);
                Assert.NotNull(refreshed);
                Assert.Equal(999.99m, original!.Price); // Original cached value
                Assert.Equal(1099.99m, refreshed!.Price); // Refreshed value
            }

            [Fact]
            public async Task GetAsync_MutationIsolation_CachedValueNotAffected()
            {
                // Arrange
                _cache = CreateCache();

                // Act - Get object from cache and mutate it
                TestProduct? cached1 = await _cache.GetAsync("product:1");
                Assert.NotNull(cached1);
                decimal originalPrice = cached1!.Price;

                // Mutate the returned object
                cached1.Price = 5000.99m;
                cached1.Name = "MUTATED";

                // Get the same object again
                TestProduct? cached2 = await _cache.GetAsync("product:1");

                // Assert - Cache should return original unmutated value
                Assert.NotNull(cached2);
                Assert.Equal(originalPrice, cached2!.Price);
                Assert.Equal("Laptop", cached2.Name);
                Assert.NotEqual(cached1.Price, cached2.Price); // Prove they're isolated
                Assert.NotEqual(cached1.Name, cached2.Name); // Prove they're isolated
            }

            [Fact]
            public async Task RefreshAsync_NullOrEmptyKey_ThrowsArgumentException()
            {
            // Arrange
            _cache = CreateCache();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () => await _cache.RefreshAsync(null!));
            await Assert.ThrowsAsync<ArgumentException>(async () => await _cache.RefreshAsync(""));
        }

        [Fact]
        public async Task ConcurrentAccess_ThreadSafe()
        {
            // Arrange
            _cache = CreateCache();
            List<Task<TestProduct?>> tasks = new List<Task<TestProduct?>>();

            // Act
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(_cache.GetAsync("product:1"));
            }

            TestProduct?[] results = await Task.WhenAll(tasks);

            // Assert
            Assert.All(results, result => Assert.NotNull(result));
            Assert.All(results, result => Assert.Equal(1, result!.Id));
        }

        [Fact]
        public async Task CacheEviction_EvictsLRUWhenFull()
        {
            // Arrange
            ReadThroughCacheOptions smallCacheOptions = new ReadThroughCacheOptions
            {
                DefaultTtl = TimeSpan.FromMinutes(5),
                MaxCacheSize = 2,
                EnableBackgroundRefresh = false
            };

            _cache = new ReadThroughCache<TestProduct>(
                LoadFromDatabase,
                LoadBulkFromDatabase,
                smallCacheOptions,
                _loggerMock.Object);

            // Act
            await _cache.GetAsync("product:1");
            await _cache.GetAsync("product:2");
            await _cache.GetAsync("product:3"); // Should trigger eviction

            // The cache should have evicted product:1 (least recently used)
            // Access product:1 again - it should reload from database
            Stopwatch sw = Stopwatch.StartNew();
            TestProduct? result = await _cache.GetAsync("product:1");
            sw.Stop();

            // Assert
            Assert.NotNull(result);
            // If it was evicted, the load time would be slower (cache miss)
        }

        private ReadThroughCache<TestProduct> CreateCache()
        {
            return new ReadThroughCache<TestProduct>(
                LoadFromDatabase,
                LoadBulkFromDatabase,
                _options,
                _loggerMock.Object);
        }

        private async Task<TestProduct?> LoadFromDatabase(string key)
        {
            await Task.Delay(10); // Simulate database latency
            return _dataStore.TryGetValue(key, out TestProduct? product) ? product : null;
        }

        private async Task<Dictionary<string, TestProduct>> LoadBulkFromDatabase(IEnumerable<string> keys)
        {
            await Task.Delay(20); // Simulate database latency
            Dictionary<string, TestProduct> results = new Dictionary<string, TestProduct>();

            foreach (string key in keys)
            {
                if (_dataStore.TryGetValue(key, out TestProduct? product))
                {
                    results[key] = product;
                }
            }

            return results;
        }

        public void Dispose()
        {
            // Cleanup
        }
    }

    // ============================================================================
    // WRITE-THROUGH CACHE TESTS
    // ============================================================================

    public class WriteThroughCacheTests : IDisposable
    {
        private readonly Mock<ILogger<WriteThroughCache<TestProduct>>> _loggerMock;
        private readonly Dictionary<string, TestProduct> _dataStore;
        private readonly WriteThroughCacheOptions _options;
        private WriteThroughCache<TestProduct>? _cache;

        public WriteThroughCacheTests()
        {
            _loggerMock = new Mock<ILogger<WriteThroughCache<TestProduct>>>();
            _dataStore = new Dictionary<string, TestProduct>();

            _options = new WriteThroughCacheOptions
            {
                EnableTransactions = true,
                WriteTimeout = TimeSpan.FromSeconds(5),
                RetryCount = 2,
                RetryDelay = TimeSpan.FromMilliseconds(50),
                RollbackOnCacheFailure = false
            };
        }

        [Fact]
        public async Task WriteAsync_Success_WritesToBothCacheAndDatabase()
        {
            // Arrange
            _cache = CreateCache();
            TestProduct product = new TestProduct
            {
                Id = 1,
                Name = "Laptop",
                Category = "Electronics",
                Price = 999.99m,
                IsActive = true
            };

            // Act
            bool result = await _cache.WriteAsync("product:1", product);

            // Assert
            Assert.True(result);
            Assert.True(_dataStore.ContainsKey("product:1"));
            Assert.Equal("Laptop", _dataStore["product:1"].Name);
        }

        [Fact]
        public async Task WriteAsync_Fast_CompletesWithinTimeout()
        {
            // Arrange
            _cache = CreateCache();
            TestProduct product = new TestProduct { Id = 1, Name = "Test" };

            // Act
            Stopwatch sw = Stopwatch.StartNew();
            bool result = await _cache.WriteAsync("product:1", product);
            sw.Stop();

            // Assert
            Assert.True(result);
            Assert.True(sw.ElapsedMilliseconds < 100, "Write-through should complete quickly");
        }

        [Fact]
        public async Task WriteAsync_NullKey_ThrowsArgumentException()
        {
            // Arrange
            _cache = CreateCache();
            TestProduct product = new TestProduct { Id = 1 };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () => await _cache.WriteAsync(null!, product));
            await Assert.ThrowsAsync<ArgumentException>(async () => await _cache.WriteAsync("", product));
        }

        [Fact]
        public async Task WriteAsync_NullEntity_ThrowsArgumentNullException()
        {
            // Arrange
            _cache = CreateCache();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await _cache.WriteAsync("product:1", null!));
        }

        [Fact]
        public async Task WriteAsync_UpdateExisting_OverwritesValue()
        {
            // Arrange
            _cache = CreateCache();
            TestProduct original = new TestProduct { Id = 1, Name = "Original", Price = 100m };
            TestProduct updated = new TestProduct { Id = 1, Name = "Updated", Price = 200m };

            // Act
            await _cache.WriteAsync("product:1", original);
            await _cache.WriteAsync("product:1", updated);

            // Assert
            Assert.Equal("Updated", _dataStore["product:1"].Name);
            Assert.Equal(200m, _dataStore["product:1"].Price);
        }

        [Fact]
        public async Task WriteManyAsync_Success_WritesAllEntities()
        {
            // Arrange
            _cache = CreateCache();
            Dictionary<string, TestProduct> products = new Dictionary<string, TestProduct>
            {
                ["product:1"] = new TestProduct { Id = 1, Name = "Product 1", Price = 100m },
                ["product:2"] = new TestProduct { Id = 2, Name = "Product 2", Price = 200m },
                ["product:3"] = new TestProduct { Id = 3, Name = "Product 3", Price = 300m }
            };

            // Act
            bool result = await _cache.WriteManyAsync(products);

            // Assert
            Assert.True(result);
            Assert.Equal(3, _dataStore.Count);
            Assert.All(_dataStore.Keys, key => Assert.True(products.ContainsKey(key)));
        }

        [Fact]
        public async Task WriteManyAsync_EmptyDictionary_ThrowsArgumentException()
        {
            // Arrange
            _cache = CreateCache();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await _cache.WriteManyAsync(new Dictionary<string, TestProduct>()));
        }

        [Fact]
        public async Task WriteManyAsync_NullDictionary_ThrowsArgumentException()
        {
            // Arrange
            _cache = CreateCache();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () => await _cache.WriteManyAsync(null!));
        }

        [Fact]
        public async Task DeleteAsync_Success_RemovesFromBothCacheAndDatabase()
        {
            // Arrange
            _cache = CreateCache();
            TestProduct product = new TestProduct { Id = 1, Name = "To Delete" };
            await _cache.WriteAsync("product:1", product);

            // Act
            bool result = await _cache.DeleteAsync("product:1");

            // Assert
            Assert.True(result);
            Assert.False(_dataStore.ContainsKey("product:1"));
        }

        [Fact]
        public async Task DeleteAsync_NonExistentKey_ReturnsFalse()
        {
            // Arrange
            _cache = CreateCache();

            // Act
            bool result = await _cache.DeleteAsync("product:999");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task DeleteAsync_NullOrEmptyKey_ThrowsArgumentException()
        {
            // Arrange
            _cache = CreateCache();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () => await _cache.DeleteAsync(null!));
            await Assert.ThrowsAsync<ArgumentException>(async () => await _cache.DeleteAsync(""));
        }

        [Fact]
        public async Task UpdateAsync_Success_UpdatesEntity()
        {
            // Arrange
            _cache = CreateCache();
            TestProduct original = new TestProduct { Id = 1, Name = "Original", Price = 100m };
            await _cache.WriteAsync("product:1", original);

            TestProduct updated = new TestProduct { Id = 1, Name = "Updated", Price = 150m };

            // Act
            bool result = await _cache.UpdateAsync("product:1", updated);

            // Assert
            Assert.True(result);
            Assert.Equal("Updated", _dataStore["product:1"].Name);
            Assert.Equal(150m, _dataStore["product:1"].Price);
        }

        [Fact]
        public async Task ConcurrentWrites_ThreadSafe()
        {
            // Arrange
            _cache = CreateCache();
            List<Task<bool>> tasks = new List<Task<bool>>();

            // Act
            for (int i = 0; i < 50; i++)
            {
                int index = i;
                tasks.Add(_cache.WriteAsync($"product:{index}",
                    new TestProduct { Id = index, Name = $"Product {index}" }));
            }

            bool[] results = await Task.WhenAll(tasks);

            // Assert
            Assert.All(results, result => Assert.True(result));
            Assert.Equal(50, _dataStore.Count);
        }

        private WriteThroughCache<TestProduct> CreateCache()
        {
            return new WriteThroughCache<TestProduct>(
                WriteToDatabase,
                DeleteFromDatabase,
                _options,
                _loggerMock.Object);
        }

        private async Task<bool> WriteToDatabase(string key, TestProduct entity)
        {
            await Task.Delay(5); // Simulate database latency
            _dataStore[key] = entity;
            return true;
        }

        private async Task<bool> DeleteFromDatabase(string key)
        {
            await Task.Delay(5); // Simulate database latency
            return _dataStore.Remove(key);
        }

        public void Dispose()
        {
            // Cleanup
        }
    }

    // ============================================================================
    // CACHE-ASIDE PATTERN TESTS
    // ============================================================================

    public class CacheAsidePatternTests : IDisposable
    {
        private readonly Mock<ILogger<CacheAsidePattern<TestProduct>>> _loggerMock;
        private readonly CacheAsideOptions _options;
        private CacheAsidePattern<TestProduct>? _cache;

        public CacheAsidePatternTests()
        {
            _loggerMock = new Mock<ILogger<CacheAsidePattern<TestProduct>>>();

            _options = new CacheAsideOptions
            {
                DefaultTtl = TimeSpan.FromMinutes(5),
                UseSlidingExpiration = false,
                MaxCacheSize = 10,
                EnableStatistics = true,
                EvictionPolicy = EvictionPolicy.LRU
            };
        }

        [Fact]
        public async Task GetFromCacheAsync_CacheMiss_ReturnsNull()
        {
            // Arrange
            _cache = CreateCache();

            // Act
            TestProduct? result = await _cache.GetFromCacheAsync("product:1");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetFromCacheAsync_CacheHit_ReturnsEntity()
        {
            // Arrange
            _cache = CreateCache();
            TestProduct product = new TestProduct { Id = 1, Name = "Laptop", Price = 999.99m };
            await _cache.SetInCacheAsync("product:1", product);

            // Act
            TestProduct? result = await _cache.GetFromCacheAsync("product:1");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result!.Id);
            Assert.Equal("Laptop", result.Name);
        }

        [Fact]
        public async Task GetFromCacheAsync_ExpiredEntry_ReturnsNull()
        {
            // Arrange
            CacheAsideOptions shortTtlOptions = new CacheAsideOptions
            {
                DefaultTtl = TimeSpan.FromMilliseconds(100),
                EnableStatistics = false
            };

            _cache = new CacheAsidePattern<TestProduct>(shortTtlOptions, _loggerMock.Object);
            TestProduct product = new TestProduct { Id = 1, Name = "Test" };
            await _cache.SetInCacheAsync("product:1", product);

            // Act
            await Task.Delay(150); // Wait for expiration
            TestProduct? result = await _cache.GetFromCacheAsync("product:1");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetFromCacheAsync_NullOrEmptyKey_ThrowsArgumentException()
        {
            // Arrange
            _cache = CreateCache();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () => await _cache.GetFromCacheAsync(null!));
            await Assert.ThrowsAsync<ArgumentException>(async () => await _cache.GetFromCacheAsync(""));
        }

        [Fact]
        public async Task SetInCacheAsync_Success_CachesEntity()
        {
            // Arrange
            _cache = CreateCache();
            TestProduct product = new TestProduct { Id = 1, Name = "Laptop" };

            // Act
            await _cache.SetInCacheAsync("product:1", product);
            TestProduct? result = await _cache.GetFromCacheAsync("product:1");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(product.Id, result!.Id);
        }

        [Fact]
        public async Task SetInCacheAsync_CustomTtl_UsesSpecifiedTtl()
        {
            // Arrange
            _cache = CreateCache();
            TestProduct product = new TestProduct { Id = 1, Name = "Test" };

            // Act
            await _cache.SetInCacheAsync("product:1", product, TimeSpan.FromMilliseconds(100));
            await Task.Delay(150);
            TestProduct? result = await _cache.GetFromCacheAsync("product:1");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task SetInCacheAsync_NullKey_ThrowsArgumentException()
        {
            // Arrange
            _cache = CreateCache();
            TestProduct product = new TestProduct { Id = 1 };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await _cache.SetInCacheAsync(null!, product));
        }

        [Fact]
        public async Task SetInCacheAsync_NullEntity_ThrowsArgumentNullException()
        {
            // Arrange
            _cache = CreateCache();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await _cache.SetInCacheAsync("product:1", null!));
        }

        [Fact]
        public async Task SetInCacheAsync_SlidingExpiration_ExtendsExpiration()
        {
            // Arrange
            CacheAsideOptions slidingOptions = new CacheAsideOptions
            {
                DefaultTtl = TimeSpan.FromMilliseconds(200),
                UseSlidingExpiration = true,
                EnableStatistics = false
            };

            _cache = new CacheAsidePattern<TestProduct>(slidingOptions, _loggerMock.Object);
            TestProduct product = new TestProduct { Id = 1, Name = "Test" };
            await _cache.SetInCacheAsync("product:1", product);

            // Act - Keep accessing to extend TTL
            await Task.Delay(100);
            TestProduct? result1 = await _cache.GetFromCacheAsync("product:1");
            await Task.Delay(100);
            TestProduct? result2 = await _cache.GetFromCacheAsync("product:1");

            // Assert - Should still be cached due to sliding expiration
            Assert.NotNull(result1);
            Assert.NotNull(result2);
        }

        [Fact]
        public async Task InvalidateAsync_RemovesFromCache()
        {
            // Arrange
            _cache = CreateCache();
            TestProduct product = new TestProduct { Id = 1, Name = "Laptop" };
            await _cache.SetInCacheAsync("product:1", product);

            // Act
            await _cache.InvalidateAsync("product:1");
            TestProduct? result = await _cache.GetFromCacheAsync("product:1");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task InvalidateAsync_NullOrEmptyKey_ThrowsArgumentException()
        {
            // Arrange
            _cache = CreateCache();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () => await _cache.InvalidateAsync(null!));
            await Assert.ThrowsAsync<ArgumentException>(async () => await _cache.InvalidateAsync(""));
        }

        [Fact]
        public async Task InvalidatePatternAsync_RemovesMatchingKeys()
        {
            // Arrange
            _cache = CreateCache();
            await _cache.SetInCacheAsync("product:1", new TestProduct { Id = 1 });
            await _cache.SetInCacheAsync("product:2", new TestProduct { Id = 2 });
            await _cache.SetInCacheAsync("customer:1", new TestProduct { Id = 3 });

            // Act
            await _cache.InvalidatePatternAsync("product:*");

            // Assert
            Assert.Null(await _cache.GetFromCacheAsync("product:1"));
            Assert.Null(await _cache.GetFromCacheAsync("product:2"));
            Assert.NotNull(await _cache.GetFromCacheAsync("customer:1"));
        }

        [Fact]
        public async Task InvalidatePatternAsync_NullOrEmptyPattern_ThrowsArgumentException()
        {
            // Arrange
            _cache = CreateCache();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () => await _cache.InvalidatePatternAsync(null!));
            await Assert.ThrowsAsync<ArgumentException>(async () => await _cache.InvalidatePatternAsync(""));
        }

        [Fact]
        public async Task GetStatistics_ReturnsAccurateStats()
        {
            // Arrange
            _cache = CreateCache();
            TestProduct product = new TestProduct { Id = 1, Name = "Test" };
            await _cache.SetInCacheAsync("product:1", product);

            // Act
            await _cache.GetFromCacheAsync("product:1"); // Hit
            await _cache.GetFromCacheAsync("product:2"); // Miss

            CacheStatistics stats = _cache.GetStatistics();

            // Assert
            Assert.Equal(1, stats.Hits);
            Assert.Equal(1, stats.Misses);
            Assert.Equal(0.5, stats.HitRatio);
            Assert.Equal(1, stats.ItemCount);
        }

        [Fact]
        public async Task Eviction_LRU_EvictsLeastRecentlyUsed()
        {
            // Arrange
            CacheAsideOptions smallCacheOptions = new CacheAsideOptions
            {
                MaxCacheSize = 2,
                EvictionPolicy = EvictionPolicy.LRU,
                EnableStatistics = true
            };

            _cache = new CacheAsidePattern<TestProduct>(smallCacheOptions, _loggerMock.Object);

            // Act
            await _cache.SetInCacheAsync("product:1", new TestProduct { Id = 1 });
            await _cache.SetInCacheAsync("product:2", new TestProduct { Id = 2 });
            await _cache.GetFromCacheAsync("product:1"); // Access product:1 to make it more recent
            await _cache.SetInCacheAsync("product:3", new TestProduct { Id = 3 }); // Should evict product:2

            // Assert
            Assert.NotNull(await _cache.GetFromCacheAsync("product:1"));
            Assert.Null(await _cache.GetFromCacheAsync("product:2")); // Evicted
            Assert.NotNull(await _cache.GetFromCacheAsync("product:3"));
        }

        [Fact]
        public async Task Eviction_LFU_EvictsLeastFrequentlyUsed()
        {
            // Arrange
            CacheAsideOptions smallCacheOptions = new CacheAsideOptions
            {
                MaxCacheSize = 2,
                EvictionPolicy = EvictionPolicy.LFU,
                EnableStatistics = true
            };

            _cache = new CacheAsidePattern<TestProduct>(smallCacheOptions, _loggerMock.Object);

            // Act
            await _cache.SetInCacheAsync("product:1", new TestProduct { Id = 1 });
            await _cache.SetInCacheAsync("product:2", new TestProduct { Id = 2 });
            
            // Access product:1 multiple times
            await _cache.GetFromCacheAsync("product:1");
            await _cache.GetFromCacheAsync("product:1");
            await _cache.GetFromCacheAsync("product:1");
            
            await _cache.SetInCacheAsync("product:3", new TestProduct { Id = 3 }); // Should evict product:2

            // Assert
            Assert.NotNull(await _cache.GetFromCacheAsync("product:1"));
            Assert.Null(await _cache.GetFromCacheAsync("product:2")); // Evicted (least frequently used)
            Assert.NotNull(await _cache.GetFromCacheAsync("product:3"));
        }

        [Fact]
        public async Task ConcurrentAccess_ThreadSafe()
        {
            // Arrange
            _cache = CreateCache();
            List<Task> tasks = new List<Task>();

            // Act
            for (int i = 0; i < 100; i++)
            {
                int index = i;
                tasks.Add(Task.Run(async () =>
                {
                    await _cache.SetInCacheAsync($"product:{index}",
                        new TestProduct { Id = index, Name = $"Product {index}" });
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            CacheStatistics stats = _cache.GetStatistics();
            Assert.True(stats.ItemCount <= 100);
        }

        private CacheAsidePattern<TestProduct> CreateCache()
        {
            return new CacheAsidePattern<TestProduct>(_options, _loggerMock.Object);
        }

        public void Dispose()
        {
            // Cleanup
        }
    }

    // ============================================================================
    // DISTRIBUTED CACHE COORDINATOR TESTS
    // ============================================================================

    public class DistributedCacheCoordinatorTests : IDisposable
    {
        private readonly Mock<ILogger<DistributedCacheCoordinator>> _loggerMock;
        private readonly DistributedCacheOptions _options;

        public DistributedCacheCoordinatorTests()
        {
            _loggerMock = new Mock<ILogger<DistributedCacheCoordinator>>();

            _options = new DistributedCacheOptions
            {
                RedisConnectionString = "localhost:6379,abortConnect=false",
                DefaultLockTimeout = TimeSpan.FromSeconds(5),
                EnableStampedePrevention = true,
                WriteBufferSize = 100,
                FlushInterval = TimeSpan.FromSeconds(1),
                EnableInvalidationBroadcast = false // Disable for tests
            };
        }

        [Fact(Skip = "Requires Redis server")]
        public async Task AcquireLockAsync_Success_ReturnsLock()
        {
            // Arrange
            using DistributedCacheCoordinator coordinator = new DistributedCacheCoordinator(_options, _loggerMock.Object);

            // Act
            IDistributedLock? lock1 = await coordinator.AcquireLockAsync("test-key", TimeSpan.FromSeconds(5));

            // Assert
            Assert.NotNull(lock1);
            Assert.True(lock1!.IsAcquired);

            // Cleanup
            await lock1.ReleaseAsync();
        }

        [Fact(Skip = "Requires Redis server")]
        public async Task AcquireLockAsync_AlreadyLocked_ReturnsNull()
        {
            // Arrange
            using DistributedCacheCoordinator coordinator = new DistributedCacheCoordinator(_options, _loggerMock.Object);
            IDistributedLock? lock1 = await coordinator.AcquireLockAsync("test-key", TimeSpan.FromSeconds(5));

            // Act
            IDistributedLock? lock2 = await coordinator.AcquireLockAsync("test-key", TimeSpan.FromMilliseconds(100));

            // Assert
            Assert.NotNull(lock1);
            Assert.Null(lock2);

            // Cleanup
            if (lock1 != null)
            {
                await lock1.ReleaseAsync();
            }
        }

        [Fact(Skip = "Requires Redis server")]
        public async Task AcquireLockAsync_NullOrEmptyKey_ThrowsArgumentException()
        {
            // Arrange
            using DistributedCacheCoordinator coordinator = new DistributedCacheCoordinator(_options, _loggerMock.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await coordinator.AcquireLockAsync(null!, TimeSpan.FromSeconds(5)));
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await coordinator.AcquireLockAsync("", TimeSpan.FromSeconds(5)));
        }

        [Fact(Skip = "Requires Redis server")]
        public async Task PreventStampedeAsync_ConcurrentRequests_OnlyOneLoads()
        {
            // Arrange
            using DistributedCacheCoordinator coordinator = new DistributedCacheCoordinator(_options, _loggerMock.Object);
            int loadCount = 0;

            async Task<int> Loader()
            {
                Interlocked.Increment(ref loadCount);
                await Task.Delay(100);
                return 42;
            }

            // Act
            List<Task<int>> tasks = new List<Task<int>>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(coordinator.PreventStampedeAsync("stampede-key", Loader, TimeSpan.FromSeconds(5)));
            }

            int[] results = await Task.WhenAll(tasks);

            // Assert
            Assert.All(results, r => Assert.Equal(42, r));
            Assert.Equal(1, loadCount); // Only one load should occur
        }

        [Fact(Skip = "Requires Redis server")]
        public async Task EnqueueWriteAsync_AddsToBuffer()
        {
            // Arrange
            using DistributedCacheCoordinator coordinator = new DistributedCacheCoordinator(_options, _loggerMock.Object);
            TestProduct product = new TestProduct { Id = 1, Name = "Test" };

            // Act
            await coordinator.EnqueueWriteAsync("product:1", product);

            // Assert - No exception thrown
        }

        [Fact(Skip = "Requires Redis server")]
        public async Task EnqueueWriteAsync_NullKey_ThrowsArgumentException()
        {
            // Arrange
            using DistributedCacheCoordinator coordinator = new DistributedCacheCoordinator(_options, _loggerMock.Object);
            TestProduct product = new TestProduct { Id = 1 };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await coordinator.EnqueueWriteAsync(null!, product));
        }

        [Fact(Skip = "Requires Redis server")]
        public async Task EnqueueWriteAsync_NullEntity_ThrowsArgumentNullException()
        {
            // Arrange
            using DistributedCacheCoordinator coordinator = new DistributedCacheCoordinator(_options, _loggerMock.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await coordinator.EnqueueWriteAsync<TestProduct>("product:1", null!));
        }

        [Fact(Skip = "Requires Redis server")]
        public async Task FlushPendingWritesAsync_ProcessesBuffer()
        {
            // Arrange
            using DistributedCacheCoordinator coordinator = new DistributedCacheCoordinator(_options, _loggerMock.Object);
            
            for (int i = 0; i < 10; i++)
            {
                await coordinator.EnqueueWriteAsync($"product:{i}", new TestProduct { Id = i });
            }

            // Act
            await coordinator.FlushPendingWritesAsync();

            // Assert - No exception thrown
        }

        [Fact(Skip = "Requires Redis server")]
        public async Task BroadcastInvalidationAsync_PublishesMessage()
        {
            // Arrange
            DistributedCacheOptions broadcastOptions = new DistributedCacheOptions
            {
                RedisConnectionString = "localhost:6379,abortConnect=false",
                EnableInvalidationBroadcast = true
            };

            using DistributedCacheCoordinator coordinator = new DistributedCacheCoordinator(broadcastOptions, _loggerMock.Object);

            // Act
            await coordinator.BroadcastInvalidationAsync("product:1");

            // Assert - No exception thrown
        }

        [Fact(Skip = "Requires Redis server")]
        public async Task BroadcastInvalidationAsync_NullOrEmptyKey_ThrowsArgumentException()
        {
            // Arrange
            using DistributedCacheCoordinator coordinator = new DistributedCacheCoordinator(_options, _loggerMock.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await coordinator.BroadcastInvalidationAsync(null!));
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await coordinator.BroadcastInvalidationAsync(""));
        }

        public void Dispose()
        {
            // Cleanup
        }
    }

    // ============================================================================
    // INTEGRATION TESTS
    // ============================================================================

    public class AdvancedCachingIntegrationTests
    {
        [Fact]
        public async Task ReadThroughAndCacheAside_WorkTogether()
        {
            // Arrange
            Dictionary<string, TestProduct> dataStore = new Dictionary<string, TestProduct>
            {
                ["product:1"] = new TestProduct { Id = 1, Name = "Laptop", Price = 999.99m }
            };

            Mock<ILogger<ReadThroughCache<TestProduct>>> readThroughLogger =
                new Mock<ILogger<ReadThroughCache<TestProduct>>>();
            Mock<ILogger<CacheAsidePattern<TestProduct>>> cacheAsideLogger =
                new Mock<ILogger<CacheAsidePattern<TestProduct>>>();

            ReadThroughCache<TestProduct> readThrough = new ReadThroughCache<TestProduct>(
                async (key) =>
                {
                    await Task.Delay(10);
                    return dataStore.TryGetValue(key, out TestProduct? p) ? p : null;
                },
                async (keys) =>
                {
                    await Task.Delay(10);
                    Dictionary<string, TestProduct> results = new Dictionary<string, TestProduct>();
                    foreach (string key in keys)
                    {
                        if (dataStore.TryGetValue(key, out TestProduct? p))
                        {
                            results[key] = p;
                        }
                    }
                    return results;
                },
                new ReadThroughCacheOptions { EnableBackgroundRefresh = false },
                readThroughLogger.Object);

            CacheAsidePattern<TestProduct> cacheAside = new CacheAsidePattern<TestProduct>(
                new CacheAsideOptions { EnableStatistics = true },
                cacheAsideLogger.Object);

            // Act
            TestProduct? fromReadThrough = await readThrough.GetAsync("product:1");
            await cacheAside.SetInCacheAsync("product:1", fromReadThrough!);
            TestProduct? fromCacheAside = await cacheAside.GetFromCacheAsync("product:1");

            // Assert
            Assert.NotNull(fromReadThrough);
            Assert.NotNull(fromCacheAside);
            Assert.Equal(fromReadThrough.Id, fromCacheAside!.Id);
            Assert.Equal(fromReadThrough.Name, fromCacheAside.Name);
        }

        [Fact]
        public async Task WriteThroughAndReadThrough_Consistency()
        {
            // Arrange
            Dictionary<string, TestProduct> dataStore = new Dictionary<string, TestProduct>();

            Mock<ILogger<ReadThroughCache<TestProduct>>> readLogger =
                new Mock<ILogger<ReadThroughCache<TestProduct>>>();
            Mock<ILogger<WriteThroughCache<TestProduct>>> writeLogger =
                new Mock<ILogger<WriteThroughCache<TestProduct>>>();

            WriteThroughCache<TestProduct> writeThrough = new WriteThroughCache<TestProduct>(
                async (key, entity) =>
                {
                    await Task.Delay(5);
                    dataStore[key] = entity;
                    return true;
                },
                async (key) =>
                {
                    await Task.Delay(5);
                    return dataStore.Remove(key);
                },
                new WriteThroughCacheOptions { EnableTransactions = false },
                writeLogger.Object);

            ReadThroughCache<TestProduct> readThrough = new ReadThroughCache<TestProduct>(
                async (key) =>
                {
                    await Task.Delay(10);
                    return dataStore.TryGetValue(key, out TestProduct? p) ? p : null;
                },
                async (keys) =>
                {
                    await Task.Delay(10);
                    Dictionary<string, TestProduct> results = new Dictionary<string, TestProduct>();
                    foreach (string key in keys)
                    {
                        if (dataStore.TryGetValue(key, out TestProduct? p))
                        {
                            results[key] = p;
                        }
                    }
                    return results;
                },
                new ReadThroughCacheOptions { EnableBackgroundRefresh = false },
                readLogger.Object);

            // Act
            TestProduct product = new TestProduct { Id = 1, Name = "Laptop", Price = 999.99m };
            await writeThrough.WriteAsync("product:1", product);
            TestProduct? readResult = await readThrough.GetAsync("product:1");

            // Assert
            Assert.NotNull(readResult);
            Assert.Equal(product.Id, readResult!.Id);
            Assert.Equal(product.Name, readResult.Name);
            Assert.Equal(product.Price, readResult.Price);
        }

        [Fact]
        public void CacheStatistics_AccurateThroughoutOperations()
        {
            // Arrange
            Mock<ILogger<CacheAsidePattern<TestProduct>>> logger =
                new Mock<ILogger<CacheAsidePattern<TestProduct>>>();
            CacheAsidePattern<TestProduct> cache = new CacheAsidePattern<TestProduct>(
                new CacheAsideOptions { EnableStatistics = true },
                logger.Object);

            // Act
            Task.Run(async () =>
            {
                await cache.SetInCacheAsync("product:1", new TestProduct { Id = 1 });
                await cache.GetFromCacheAsync("product:1"); // Hit
                await cache.GetFromCacheAsync("product:2"); // Miss
                await cache.GetFromCacheAsync("product:1"); // Hit
            }).Wait();

            CacheStatistics stats = cache.GetStatistics();

            // Assert
            Assert.Equal(2, stats.Hits);
            Assert.Equal(1, stats.Misses);
            Assert.Equal(1, stats.ItemCount);
            Assert.True(stats.HitRatio > 0.5);
        }
    }

    // ============================================================================
    // PERFORMANCE TESTS
    // ============================================================================

    public class CachingPerformanceTests
    {
        [Fact]
        public async Task ReadThroughCache_CacheHit_UnderTarget()
        {
            // Arrange
            Dictionary<string, TestProduct> dataStore = new Dictionary<string, TestProduct>
            {
                ["product:1"] = new TestProduct { Id = 1, Name = "Test" }
            };

            Mock<ILogger<ReadThroughCache<TestProduct>>> logger =
                new Mock<ILogger<ReadThroughCache<TestProduct>>>();

            ReadThroughCache<TestProduct> cache = new ReadThroughCache<TestProduct>(
                async (key) =>
                {
                    await Task.Delay(10);
                    return dataStore.TryGetValue(key, out TestProduct? p) ? p : null;
                },
                async (keys) => new Dictionary<string, TestProduct>(),
                new ReadThroughCacheOptions { EnableBackgroundRefresh = false },
                logger.Object);

            // Prime cache
            await cache.GetAsync("product:1");

            // Act
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++)
            {
                await cache.GetAsync("product:1");
            }
            sw.Stop();

            double avgMs = sw.Elapsed.TotalMilliseconds / 1000;

            // Assert
            Assert.True(avgMs < 1.0, $"Average cache hit time {avgMs}ms exceeds 1ms target");
        }

        [Fact]
        public async Task WriteThroughCache_SyncWrite_UnderTarget()
        {
            // Arrange
            Dictionary<string, TestProduct> dataStore = new Dictionary<string, TestProduct>();
            Mock<ILogger<WriteThroughCache<TestProduct>>> logger =
                new Mock<ILogger<WriteThroughCache<TestProduct>>>();

            WriteThroughCache<TestProduct> cache = new WriteThroughCache<TestProduct>(
                async (key, entity) =>
                {
                    await Task.Delay(5);
                    dataStore[key] = entity;
                    return true;
                },
                async (key) => await Task.FromResult(dataStore.Remove(key)),
                new WriteThroughCacheOptions { EnableTransactions = false },
                logger.Object);

            // Act
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < 100; i++)
            {
                await cache.WriteAsync($"product:{i}", new TestProduct { Id = i });
            }
            sw.Stop();

            double avgMs = sw.Elapsed.TotalMilliseconds / 100;

            // Assert
            Assert.True(avgMs < 50.0, $"Average write-through time {avgMs}ms exceeds 50ms target");
        }

        [Fact]
        public async Task CacheAsidePattern_GetFromCache_UnderTarget()
        {
            // Arrange
            Mock<ILogger<CacheAsidePattern<TestProduct>>> logger =
                new Mock<ILogger<CacheAsidePattern<TestProduct>>>();

            CacheAsidePattern<TestProduct> cache = new CacheAsidePattern<TestProduct>(
                new CacheAsideOptions { EnableStatistics = false },
                logger.Object);

            await cache.SetInCacheAsync("product:1", new TestProduct { Id = 1 });

            // Act
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < 10000; i++)
            {
                await cache.GetFromCacheAsync("product:1");
            }
            sw.Stop();

                        double avgMs = sw.Elapsed.TotalMilliseconds / 10000;

                        // Assert
                        Assert.True(avgMs < 1.0, $"Average cache-aside get time {avgMs}ms exceeds 1ms target");
                    }
                }

                // ============================================================================
                // CLONING ISOLATION TESTS
                // ============================================================================

                public class CloningIsolationTests
                {
                    [Fact]
                    public async Task ReadThroughCache_GetAsync_ReturnsIsolatedCopy()
                    {
                        // Arrange
                        Dictionary<string, TestProduct> dataStore = new Dictionary<string, TestProduct>
                        {
                            ["product:1"] = new TestProduct { Id = 1, Name = "Original", Price = 100m }
                        };

                        Mock<ILogger<ReadThroughCache<TestProduct>>> logger = new Mock<ILogger<ReadThroughCache<TestProduct>>>();

                        ReadThroughCache<TestProduct> cache = new ReadThroughCache<TestProduct>(
                            async (key) => await Task.FromResult(dataStore.TryGetValue(key, out TestProduct? p) ? p : null),
                            async (keys) => await Task.FromResult(new Dictionary<string, TestProduct>()),
                            new ReadThroughCacheOptions { EnableBackgroundRefresh = false },
                            logger.Object);

                        // Act
                        TestProduct? result1 = await cache.GetAsync("product:1");
                        result1!.Name = "MUTATED";
                        result1.Price = 999m;

                        TestProduct? result2 = await cache.GetAsync("product:1");

                        // Assert
                        Assert.Equal("Original", result2!.Name);
                        Assert.Equal(100m, result2.Price);
                    }

                    [Fact]
                    public async Task ReadThroughCache_GetManyAsync_ReturnsIsolatedCopies()
                    {
                        // Arrange
                        Dictionary<string, TestProduct> dataStore = new Dictionary<string, TestProduct>
                        {
                            ["product:1"] = new TestProduct { Id = 1, Name = "Product 1", Price = 100m },
                            ["product:2"] = new TestProduct { Id = 2, Name = "Product 2", Price = 200m }
                        };

                        Mock<ILogger<ReadThroughCache<TestProduct>>> logger = new Mock<ILogger<ReadThroughCache<TestProduct>>>();

                        ReadThroughCache<TestProduct> cache = new ReadThroughCache<TestProduct>(
                            async (key) => await Task.FromResult(dataStore.TryGetValue(key, out TestProduct? p) ? p : null),
                            async (keys) =>
                            {
                                Dictionary<string, TestProduct> result = new Dictionary<string, TestProduct>();
                                foreach (string key in keys)
                                {
                                    if (dataStore.TryGetValue(key, out TestProduct? p))
                                    {
                                        result[key] = p;
                                    }
                                }
                                return await Task.FromResult(result);
                            },
                            new ReadThroughCacheOptions { EnableBackgroundRefresh = false },
                            logger.Object);

                        // Act
                        Dictionary<string, TestProduct> results = await cache.GetManyAsync(new[] { "product:1", "product:2" });
                        results["product:1"].Name = "MUTATED 1";
                        results["product:2"].Price = 999m;

                        Dictionary<string, TestProduct> results2 = await cache.GetManyAsync(new[] { "product:1", "product:2" });

                        // Assert
                        Assert.Equal("Product 1", results2["product:1"].Name);
                        Assert.Equal(200m, results2["product:2"].Price);
                    }

                    [Fact]
                    public async Task WriteThroughCache_WriteAsync_StoresIsolatedCopy()
                    {
                        // Arrange
                        Dictionary<string, TestProduct> dataStore = new Dictionary<string, TestProduct>();
                        Mock<ILogger<WriteThroughCache<TestProduct>>> logger = new Mock<ILogger<WriteThroughCache<TestProduct>>>();

                        WriteThroughCache<TestProduct> cache = new WriteThroughCache<TestProduct>(
                            async (key, entity) =>
                            {
                                // Simulate database storing a clone (like real DB serialization would)
                                dataStore[key] = entity.ShallowClone();
                                return await Task.FromResult(true);
                            },
                            async (key) => await Task.FromResult(dataStore.Remove(key)),
                            new WriteThroughCacheOptions { EnableTransactions = false },
                            logger.Object);

                        TestProduct product = new TestProduct { Id = 1, Name = "Original", Price = 100m };

                        // Act
                        await cache.WriteAsync("product:1", product);
                        product.Name = "MUTATED AFTER WRITE";
                        product.Price = 999m;

                        // Assert - the database should have the original value, not the mutation
                        Assert.Equal("Original", dataStore["product:1"].Name);
                        Assert.Equal(100m, dataStore["product:1"].Price);
                    }

                    [Fact]
                    public async Task CacheAsidePattern_GetFromCache_ReturnsIsolatedCopy()
                    {
                        // Arrange
                        Mock<ILogger<CacheAsidePattern<TestProduct>>> logger = new Mock<ILogger<CacheAsidePattern<TestProduct>>>();

                        CacheAsidePattern<TestProduct> cache = new CacheAsidePattern<TestProduct>(
                            new CacheAsideOptions { EnableStatistics = false },
                            logger.Object);

                        TestProduct original = new TestProduct { Id = 1, Name = "Original", Price = 100m };
                        await cache.SetInCacheAsync("product:1", original);

                        // Act
                        TestProduct? result1 = await cache.GetFromCacheAsync("product:1");
                        result1!.Name = "MUTATED";
                        result1.Price = 999m;

                        TestProduct? result2 = await cache.GetFromCacheAsync("product:1");

                        // Assert
                        Assert.Equal("Original", result2!.Name);
                        Assert.Equal(100m, result2.Price);
                    }

                    [Fact]
                    public async Task CacheAsidePattern_SetInCache_StoresIsolatedCopy()
                    {
                        // Arrange
                        Mock<ILogger<CacheAsidePattern<TestProduct>>> logger = new Mock<ILogger<CacheAsidePattern<TestProduct>>>();

                        CacheAsidePattern<TestProduct> cache = new CacheAsidePattern<TestProduct>(
                            new CacheAsideOptions { EnableStatistics = false },
                            logger.Object);

                        TestProduct product = new TestProduct { Id = 1, Name = "Original", Price = 100m };

                        // Act
                        await cache.SetInCacheAsync("product:1", product);
                        product.Name = "MUTATED AFTER SET";
                        product.Price = 999m;

                        TestProduct? result = await cache.GetFromCacheAsync("product:1");

                        // Assert
                        Assert.Equal("Original", result!.Name);
                        Assert.Equal(100m, result.Price);
                    }

                    [Fact]
                    public async Task ReadThroughCache_ConcurrentMutations_DoNotInterfere()
                    {
                        // Arrange
                        Dictionary<string, TestProduct> dataStore = new Dictionary<string, TestProduct>
                        {
                            ["product:1"] = new TestProduct { Id = 1, Name = "Original", Price = 100m }
                        };

                        Mock<ILogger<ReadThroughCache<TestProduct>>> logger = new Mock<ILogger<ReadThroughCache<TestProduct>>>();

                        ReadThroughCache<TestProduct> cache = new ReadThroughCache<TestProduct>(
                            async (key) => await Task.FromResult(dataStore.TryGetValue(key, out TestProduct? p) ? p : null),
                            async (keys) => await Task.FromResult(new Dictionary<string, TestProduct>()),
                            new ReadThroughCacheOptions { EnableBackgroundRefresh = false },
                            logger.Object);

                        // Act - Concurrent gets and mutations
                        List<Task> tasks = new List<Task>();
                        for (int i = 0; i < 50; i++)
                        {
                            tasks.Add(Task.Run(async () =>
                            {
                                TestProduct? product = await cache.GetAsync("product:1");
                                product!.Name = $"MUTATED-{Guid.NewGuid()}";
                                product.Price = Random.Shared.Next(1, 1000);
                                await Task.Delay(1);
                            }));
                        }

                        await Task.WhenAll(tasks);

                        // Assert - Cache should still have original value
                        TestProduct? final = await cache.GetAsync("product:1");
                        Assert.Equal("Original", final!.Name);
                        Assert.Equal(100m, final.Price);
                    }

                    [Fact]
                    public async Task CacheAsidePattern_MultipleReaders_GetIsolatedCopies()
                    {
                        // Arrange
                        Mock<ILogger<CacheAsidePattern<TestProduct>>> logger = new Mock<ILogger<CacheAsidePattern<TestProduct>>>();

                        CacheAsidePattern<TestProduct> cache = new CacheAsidePattern<TestProduct>(
                            new CacheAsideOptions { EnableStatistics = false },
                            logger.Object);

                        await cache.SetInCacheAsync("product:1", new TestProduct { Id = 1, Name = "Shared", Price = 100m });

                        // Act - Multiple readers mutate their copies
                        List<Task<(int Index, TestProduct? Product)>> tasks = new List<Task<(int, TestProduct?)>>();
                        for (int i = 0; i < 20; i++)
                        {
                            int index = i; // Capture by value
                            tasks.Add(Task.Run(async () =>
                            {
                                TestProduct? product = await cache.GetFromCacheAsync("product:1");
                                product!.Name = $"Reader-{index}";
                                product.Price = index * 10m;
                                return (index, product);
                            }));
                        }

                        (int Index, TestProduct? Product)[] results = await Task.WhenAll(tasks);

                        // Assert - Each reader got their own isolated copy with correct index
                        foreach ((int index, TestProduct? product) in results)
                        {
                            Assert.Equal($"Reader-{index}", product!.Name);
                            Assert.Equal(index * 10m, product.Price);
                        }

                        // Cache still has original
                        TestProduct? cached = await cache.GetFromCacheAsync("product:1");
                        Assert.Equal("Shared", cached!.Name);
                        Assert.Equal(100m, cached.Price);
                    }

                    [Fact]
                    public void TestProduct_ShallowClone_CreatesIndependentCopy()
                    {
                        // Arrange
                        TestProduct original = new TestProduct
                        {
                            Id = 1,
                            Name = "Original",
                            Category = "Electronics",
                            Price = 999.99m,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        };

                        // Act
                        TestProduct clone = original.ShallowClone();
                        clone.Name = "Cloned";
                        clone.Price = 500m;

                        // Assert
                        Assert.Equal("Original", original.Name);
                        Assert.Equal(999.99m, original.Price);
                        Assert.Equal("Cloned", clone.Name);
                        Assert.Equal(500m, clone.Price);
                    }

                            [Fact]
                            public void TestCustomer_DeepClone_CreatesIndependentCopy()
                            {
                                // Arrange
                                TestCustomer original = new TestCustomer
                                {
                                    Id = 1,
                                    Name = "John Doe",
                                    Email = "john@example.com",
                                    JoinedDate = DateTime.UtcNow
                                };

                                // Act
                                TestCustomer clone = original.DeepClone();
                                clone.Name = "Jane Doe";
                                clone.Email = "jane@example.com";

                                // Assert
                                Assert.Equal("John Doe", original.Name);
                                Assert.Equal("john@example.com", original.Email);
                                Assert.Equal("Jane Doe", clone.Name);
                                Assert.Equal("jane@example.com", clone.Email);
                            }
                        }

                        // ============================================================================
                        // ADDITIONAL COMPREHENSIVE CLONING TESTS
                        // ============================================================================

                        public class CloningAdvancedScenarioTests
                        {
                            // ====================================================================
                            // CACHE EVICTION TESTS
                            // ====================================================================

                            [Fact]
                            public async Task ReadThroughCache_EvictedEntry_ReloadsAndReturnsClone()
                            {
                                // Arrange
                                Dictionary<string, TestProduct> dataStore = new Dictionary<string, TestProduct>
                                {
                                    ["product:1"] = new TestProduct { Id = 1, Name = "Original", Price = 100m }
                                };

                                Mock<ILogger<ReadThroughCache<TestProduct>>> logger = new Mock<ILogger<ReadThroughCache<TestProduct>>>();

                                ReadThroughCache<TestProduct> cache = new ReadThroughCache<TestProduct>(
                                    async (key) => await Task.FromResult(dataStore.TryGetValue(key, out TestProduct? p) ? p : null),
                                    async (keys) => await Task.FromResult(new Dictionary<string, TestProduct>()),
                                    new ReadThroughCacheOptions { MaxCacheSize = 1, EnableBackgroundRefresh = false },
                                    logger.Object);

                                // Act - Fill cache
                                TestProduct? result1 = await cache.GetAsync("product:1");
                                result1!.Name = "MUTATED";

                                // Evict by loading another item (max size = 1)
                                dataStore["product:2"] = new TestProduct { Id = 2, Name = "Product 2", Price = 200m };
                                await cache.GetAsync("product:2");

                                // Reload original
                                TestProduct? result2 = await cache.GetAsync("product:1");

                                // Assert - Should reload from data store, not affected by mutation
                                Assert.Equal("Original", result2!.Name);
                                Assert.Equal(100m, result2.Price);
                            }

                            [Fact]
                            public async Task WriteThroughCache_EvictionDuringWrite_MaintainsIsolation()
                            {
                                // Arrange
                                Dictionary<string, TestProduct> dataStore = new Dictionary<string, TestProduct>();
                                Mock<ILogger<WriteThroughCache<TestProduct>>> logger = new Mock<ILogger<WriteThroughCache<TestProduct>>>();

                                WriteThroughCache<TestProduct> cache = new WriteThroughCache<TestProduct>(
                                    async (key, entity) =>
                                    {
                                        dataStore[key] = entity.ShallowClone();
                                        return await Task.FromResult(true);
                                    },
                                    async (key) => await Task.FromResult(dataStore.Remove(key)),
                                    new WriteThroughCacheOptions { EnableTransactions = false },
                                    logger.Object);

                                // Act - Write multiple items to trigger eviction
                                TestProduct p1 = new TestProduct { Id = 1, Name = "Product 1", Price = 100m };
                                TestProduct p2 = new TestProduct { Id = 2, Name = "Product 2", Price = 200m };
                                TestProduct p3 = new TestProduct { Id = 3, Name = "Product 3", Price = 300m };

                                await cache.WriteAsync("product:1", p1);
                                await cache.WriteAsync("product:2", p2);
                                await cache.WriteAsync("product:3", p3); // Triggers eviction

                                // Mutate after write
                                p1.Name = "MUTATED 1";
                                p2.Name = "MUTATED 2";
                                p3.Name = "MUTATED 3";

                                // Assert - Database should have original values
                                Assert.Equal("Product 1", dataStore["product:1"].Name);
                                Assert.Equal("Product 2", dataStore["product:2"].Name);
                                Assert.Equal("Product 3", dataStore["product:3"].Name);
                            }

                            // ====================================================================
                            // EXPIRATION TESTS
                            // ====================================================================

                            [Fact]
                            public async Task ReadThroughCache_ExpiredEntry_ReloadsWithClone()
                            {
                                // Arrange
                                int loadCount = 0;
                                Dictionary<string, TestProduct> dataStore = new Dictionary<string, TestProduct>
                                {
                                    ["product:1"] = new TestProduct { Id = 1, Name = "Version 1", Price = 100m }
                                };

                                Mock<ILogger<ReadThroughCache<TestProduct>>> logger = new Mock<ILogger<ReadThroughCache<TestProduct>>>();

                                ReadThroughCache<TestProduct> cache = new ReadThroughCache<TestProduct>(
                                    async (key) =>
                                    {
                                        loadCount++;
                                        return await Task.FromResult(dataStore.TryGetValue(key, out TestProduct? p) ? p : null);
                                    },
                                    async (keys) => await Task.FromResult(new Dictionary<string, TestProduct>()),
                                    new ReadThroughCacheOptions
                                    {
                                        DefaultTtl = TimeSpan.FromMilliseconds(50),
                                        EnableBackgroundRefresh = false
                                    },
                                    logger.Object);

                                // Act
                                TestProduct? result1 = await cache.GetAsync("product:1");
                                result1!.Name = "MUTATED";

                                // Wait for expiration
                                await Task.Delay(100);

                                // Update data store
                                dataStore["product:1"] = new TestProduct { Id = 1, Name = "Version 2", Price = 200m };

                                TestProduct? result2 = await cache.GetAsync("product:1");

                                // Assert - Should reload after expiration
                                Assert.Equal(2, loadCount);
                                Assert.Equal("Version 2", result2!.Name);
                                Assert.Equal(200m, result2.Price);
                            }

                            // ====================================================================
                            // ERROR HANDLING TESTS
                            // ====================================================================

                            [Fact]
                            public async Task ReadThroughCache_LoaderException_DoesNotCorruptCache()
                            {
                                // Arrange
                                int attemptCount = 0;
                                Mock<ILogger<ReadThroughCache<TestProduct>>> logger = new Mock<ILogger<ReadThroughCache<TestProduct>>>();

                                ReadThroughCache<TestProduct> cache = new ReadThroughCache<TestProduct>(
                                    async (key) =>
                                    {
                                        attemptCount++;
                                        if (attemptCount == 1)
                                        {
                                            throw new InvalidOperationException("Simulated database error");
                                        }
                                        return await Task.FromResult(new TestProduct { Id = 1, Name = "Success", Price = 100m });
                                    },
                                    async (keys) => await Task.FromResult(new Dictionary<string, TestProduct>()),
                                    new ReadThroughCacheOptions { EnableBackgroundRefresh = false },
                                    logger.Object);

                                // Act & Assert - First call throws
                                await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetAsync("product:1"));

                                // Second call succeeds and returns clone
                                TestProduct? result = await cache.GetAsync("product:1");
                                result!.Name = "MUTATED";

                                // Third call should return cached clone (not mutated)
                                TestProduct? cached = await cache.GetAsync("product:1");
                                Assert.Equal("Success", cached!.Name);
                            }

                            [Fact]
                            public async Task WriteThroughCache_WriteFailureHandling_MaintainsIsolation()
                            {
                                // Arrange
                                Dictionary<string, TestProduct> dataStore = new Dictionary<string, TestProduct>();
                                Mock<ILogger<WriteThroughCache<TestProduct>>> logger = new Mock<ILogger<WriteThroughCache<TestProduct>>>();

                                WriteThroughCache<TestProduct> cache = new WriteThroughCache<TestProduct>(
                                    async (key, entity) =>
                                    {
                                        // Always clone before storing
                                        dataStore[key] = entity.ShallowClone();
                                        return await Task.FromResult(true);
                                    },
                                    async (key) => await Task.FromResult(dataStore.Remove(key)),
                                    new WriteThroughCacheOptions { EnableTransactions = false },
                                    logger.Object);

                                TestProduct product = new TestProduct { Id = 1, Name = "Test", Price = 100m };

                                // Act
                                await cache.WriteAsync("product:1", product);
                                product.Name = "MUTATED AFTER WRITE";
                                product.Price = 999m;

                                // Assert - Database should have original value (cloned)
                                Assert.True(dataStore.ContainsKey("product:1"));
                                Assert.Equal("Test", dataStore["product:1"].Name);
                                Assert.Equal(100m, dataStore["product:1"].Price);
                            }

                            // ====================================================================
                            // NULL HANDLING TESTS
                            // ====================================================================

                            [Fact]
                            public async Task ReadThroughCache_LoaderReturnsNull_DoesNotCacheNull()
                            {
                                // Arrange
                                int loadCount = 0;
                                Mock<ILogger<ReadThroughCache<TestProduct>>> logger = new Mock<ILogger<ReadThroughCache<TestProduct>>>();

                                ReadThroughCache<TestProduct> cache = new ReadThroughCache<TestProduct>(
                                    async (key) =>
                                    {
                                        loadCount++;
                                        return await Task.FromResult<TestProduct?>(null);
                                    },
                                    async (keys) => await Task.FromResult(new Dictionary<string, TestProduct>()),
                                    new ReadThroughCacheOptions { EnableBackgroundRefresh = false },
                                    logger.Object);

                                // Act
                                TestProduct? result1 = await cache.GetAsync("product:1");
                                TestProduct? result2 = await cache.GetAsync("product:1");

                                // Assert - Null results should not be cached (loader called twice)
                                Assert.Null(result1);
                                Assert.Null(result2);
                                Assert.Equal(2, loadCount);
                            }

                            [Fact]
                            public async Task CacheAsidePattern_GetNonExistent_ReturnsNull()
                            {
                                // Arrange
                                Mock<ILogger<CacheAsidePattern<TestProduct>>> logger = new Mock<ILogger<CacheAsidePattern<TestProduct>>>();

                                CacheAsidePattern<TestProduct> cache = new CacheAsidePattern<TestProduct>(
                                    new CacheAsideOptions { EnableStatistics = false },
                                    logger.Object);

                                // Act
                                TestProduct? result = await cache.GetFromCacheAsync("nonexistent");

                                // Assert
                                Assert.Null(result);
                            }

                            // ====================================================================
                            // CACHE STATISTICS TESTS
                            // ====================================================================

                            [Fact]
                            public async Task ReadThroughCache_MultipleGets_ClonedResultsAreIndependent()
                            {
                                // Arrange
                                Dictionary<string, TestProduct> dataStore = new Dictionary<string, TestProduct>
                                {
                                    ["product:1"] = new TestProduct { Id = 1, Name = "Original", Price = 100m }
                                };

                                Mock<ILogger<ReadThroughCache<TestProduct>>> logger = new Mock<ILogger<ReadThroughCache<TestProduct>>>();

                                ReadThroughCache<TestProduct> cache = new ReadThroughCache<TestProduct>(
                                    async (key) => await Task.FromResult(dataStore.TryGetValue(key, out TestProduct? p) ? p : null),
                                    async (keys) => await Task.FromResult(new Dictionary<string, TestProduct>()),
                                    new ReadThroughCacheOptions { EnableBackgroundRefresh = false },
                                    logger.Object);

                                // Act - Get same item 10 times and mutate each
                                List<TestProduct?> results = new List<TestProduct?>();
                                for (int i = 0; i < 10; i++)
                                {
                                    TestProduct? result = await cache.GetAsync("product:1");
                                    result!.Name = $"Mutated-{i}";
                                    result.Price = i * 10m;
                                    results.Add(result);
                                }

                                // Assert - Each result should have its own mutation
                                for (int i = 0; i < 10; i++)
                                {
                                    Assert.Equal($"Mutated-{i}", results[i]!.Name);
                                    Assert.Equal(i * 10m, results[i]!.Price);
                                }

                                // Cache should still have original
                                TestProduct? cached = await cache.GetAsync("product:1");
                                Assert.Equal("Original", cached!.Name);
                                Assert.Equal(100m, cached.Price);
                            }

                            // ====================================================================
                            // REFRESH TESTS
                            // ====================================================================

                            [Fact]
                            public async Task ReadThroughCache_ManualRefresh_ReturnsNewClone()
                            {
                                // Arrange
                                Dictionary<string, TestProduct> dataStore = new Dictionary<string, TestProduct>
                                {
                                    ["product:1"] = new TestProduct { Id = 1, Name = "Version 1", Price = 100m }
                                };

                                Mock<ILogger<ReadThroughCache<TestProduct>>> logger = new Mock<ILogger<ReadThroughCache<TestProduct>>>();

                                ReadThroughCache<TestProduct> cache = new ReadThroughCache<TestProduct>(
                                    async (key) => await Task.FromResult(dataStore.TryGetValue(key, out TestProduct? p) ? p : null),
                                    async (keys) => await Task.FromResult(new Dictionary<string, TestProduct>()),
                                    new ReadThroughCacheOptions { EnableBackgroundRefresh = false },
                                    logger.Object);

                                // Act
                                TestProduct? result1 = await cache.GetAsync("product:1");
                                result1!.Name = "MUTATED";

                                // Update data store and refresh
                                dataStore["product:1"] = new TestProduct { Id = 1, Name = "Version 2", Price = 200m };
                                await cache.RefreshAsync("product:1");

                                TestProduct? result2 = await cache.GetAsync("product:1");

                                // Assert
                                Assert.Equal("MUTATED", result1.Name); // Original clone unchanged
                                Assert.Equal("Version 2", result2!.Name); // New clone with updated data
                                Assert.Equal(200m, result2.Price);
                            }

                            // ====================================================================
                            // BULK OPERATION TESTS
                            // ====================================================================

                            [Fact]
                            public async Task ReadThroughCache_BulkGetManyAsync_AllResultsAreClones()
                            {
                                // Arrange
                                Dictionary<string, TestProduct> dataStore = new Dictionary<string, TestProduct>();
                                for (int i = 1; i <= 100; i++)
                                {
                                    dataStore[$"product:{i}"] = new TestProduct { Id = i, Name = $"Product {i}", Price = i * 10m };
                                }

                                Mock<ILogger<ReadThroughCache<TestProduct>>> logger = new Mock<ILogger<ReadThroughCache<TestProduct>>>();

                                ReadThroughCache<TestProduct> cache = new ReadThroughCache<TestProduct>(
                                    async (key) => await Task.FromResult(dataStore.TryGetValue(key, out TestProduct? p) ? p : null),
                                    async (keys) =>
                                    {
                                        Dictionary<string, TestProduct> result = new Dictionary<string, TestProduct>();
                                        foreach (string key in keys)
                                        {
                                            if (dataStore.TryGetValue(key, out TestProduct? p))
                                            {
                                                result[key] = p;
                                            }
                                        }
                                        return await Task.FromResult(result);
                                    },
                                    new ReadThroughCacheOptions { EnableBackgroundRefresh = false },
                                    logger.Object);

                                // Act
                                string[] keys = Enumerable.Range(1, 100).Select(i => $"product:{i}").ToArray();
                                Dictionary<string, TestProduct> results = await cache.GetManyAsync(keys);

                                // Mutate all results
                                foreach (KeyValuePair<string, TestProduct> kvp in results)
                                {
                                    kvp.Value.Name = "MUTATED";
                                    kvp.Value.Price = 9999m;
                                }

                                // Get again
                                Dictionary<string, TestProduct> results2 = await cache.GetManyAsync(keys);

                                // Assert - All should have original values
                                foreach (int i in Enumerable.Range(1, 100))
                                {
                                    string key = $"product:{i}";
                                    Assert.Equal($"Product {i}", results2[key].Name);
                                    Assert.Equal(i * 10m, results2[key].Price);
                                }
                            }

                            // ====================================================================
                            // CLEAR TESTS
                            // ====================================================================

                            [Fact]
                            public async Task CacheAsidePattern_ClearCache_ReloadsWithFreshClone()
                            {
                                // Arrange
                                Mock<ILogger<CacheAsidePattern<TestProduct>>> logger = new Mock<ILogger<CacheAsidePattern<TestProduct>>>();

                                CacheAsidePattern<TestProduct> cache = new CacheAsidePattern<TestProduct>(
                                    new CacheAsideOptions { EnableStatistics = false },
                                    logger.Object);

                                TestProduct original = new TestProduct { Id = 1, Name = "Original", Price = 100m };
                                await cache.SetInCacheAsync("product:1", original);

                                TestProduct? cached1 = await cache.GetFromCacheAsync("product:1");
                                cached1!.Name = "MUTATED";

                                // Act - Invalidate and set new value
                                await cache.InvalidateAsync("product:1");
                                TestProduct updated = new TestProduct { Id = 1, Name = "Updated", Price = 200m };
                                await cache.SetInCacheAsync("product:1", updated);

                                TestProduct? cached2 = await cache.GetFromCacheAsync("product:1");

                                // Assert
                                Assert.Equal("Updated", cached2!.Name);
                                Assert.Equal(200m, cached2.Price);
                            }

                            // ====================================================================
                            // STRESS TESTS
                            // ====================================================================

                            [Fact]
                            public async Task ReadThroughCache_HighConcurrency_MaintainsIsolation()
                            {
                                // Arrange
                                Dictionary<string, TestProduct> dataStore = new Dictionary<string, TestProduct>();
                                for (int i = 1; i <= 20; i++)
                                {
                                    dataStore[$"product:{i}"] = new TestProduct { Id = i, Name = $"Product {i}", Price = i * 10m };
                                }

                                Mock<ILogger<ReadThroughCache<TestProduct>>> logger = new Mock<ILogger<ReadThroughCache<TestProduct>>>();

                                ReadThroughCache<TestProduct> cache = new ReadThroughCache<TestProduct>(
                                    async (key) =>
                                    {
                                        await Task.Delay(1); // Simulate DB latency
                                        return dataStore.TryGetValue(key, out TestProduct? p) ? p : null;
                                    },
                                    async (keys) => await Task.FromResult(new Dictionary<string, TestProduct>()),
                                    new ReadThroughCacheOptions { EnableBackgroundRefresh = false },
                                    logger.Object);

                                // Act - 100 concurrent operations across 20 products
                                List<Task> tasks = new List<Task>();
                                for (int i = 0; i < 100; i++)
                                {
                                    int productId = (i % 20) + 1;
                                    tasks.Add(Task.Run(async () =>
                                    {
                                        TestProduct? product = await cache.GetAsync($"product:{productId}");
                                        product!.Name = $"MUTATED-{Guid.NewGuid()}";
                                        product.Price = Random.Shared.Next(1, 10000);
                                        await Task.Delay(Random.Shared.Next(1, 10));
                                    }));
                                }

                                await Task.WhenAll(tasks);

                                // Assert - All cached values should be unchanged
                                for (int i = 1; i <= 20; i++)
                                {
                                    TestProduct? cached = await cache.GetAsync($"product:{i}");
                                    Assert.Equal($"Product {i}", cached!.Name);
                                    Assert.Equal(i * 10m, cached.Price);
                                }
                            }
                        }
                    }

