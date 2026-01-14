using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using FluentAssertions;
using HighSpeedDAL.Core;
using HighSpeedDAL.Core.Attributes;
using HighSpeedDAL.AdvancedCaching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace HighSpeedDAL.PerformanceRegression.Tests;

/// <summary>
/// Performance regression tests for caching operations.
/// Tests memory cache, defensive cloning, cache hit/miss patterns.
/// </summary>
[MemoryDiagnoser]
[MarkdownExporter]
[Config(typeof(Config))]
public class CacheRegressionTests : IDisposable
{
    private IMemoryCache? _memoryCache;
    private MemoryCacheManager? _cacheManager;
    private Mock<ILogger>? _loggerMock;
    private const int CacheSize = 10000;

    private class Config : ManualConfig
    {
        public Config()
        {
            AddColumn(StatisticColumn.Mean);
            AddColumn(StatisticColumn.StdDev);
            AddColumn(StatisticColumn.Median);
            AddColumn(StatisticColumn.Min);
            AddColumn(StatisticColumn.Max);
        }
    }

    [GlobalSetup]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = CacheSize * 2 // Allow plenty of space
        });
        _cacheManager = new MemoryCacheManager(_memoryCache, _loggerMock.Object);

        // Pre-populate cache with test data
        for (int i = 0; i < CacheSize; i++)
        {
            var customer = new Customer
            {
                Id = i,
                FirstName = $"First{i}",
                LastName = $"Last{i}",
                Email = $"test{i}@example.com",
                CreatedBy = "cache-test",
                CreatedDate = DateTime.UtcNow,
                ModifiedBy = "cache-test",
                ModifiedDate = DateTime.UtcNow
            };
            _cacheManager.Set($"customer:{i}", customer, TimeSpan.FromMinutes(10));
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _memoryCache?.Dispose();
    }

    /// <summary>
    /// Baseline: Cache hits (sequential reads)
    /// Expected: <1ms per operation
    /// </summary>
    [Benchmark(Baseline = true)]
    public void CacheHit_Sequential_10K_Reads()
    {
        for (int i = 0; i < CacheSize; i++)
        {
            var customer = _cacheManager!.Get<Customer>($"customer:{i}");
            customer.Should().NotBeNull();
        }
    }

    /// <summary>
    /// Benchmark: Cache hits with random access pattern
    /// Expected: <1ms per operation (similar to sequential)
    /// </summary>
    [Benchmark]
    public void CacheHit_Random_10K_Reads()
    {
        var random = new Random(42); // Fixed seed for reproducibility
        for (int i = 0; i < CacheSize; i++)
        {
            var id = random.Next(CacheSize);
            var customer = _cacheManager!.Get<Customer>($"customer:{id}");
            customer.Should().NotBeNull();
        }
    }

    /// <summary>
    /// Benchmark: Cache misses (no hits)
    /// Expected: <1ms per operation (just null checks)
    /// </summary>
    [Benchmark]
    public void CacheMiss_10K_Reads()
    {
        for (int i = CacheSize; i < CacheSize * 2; i++)
        {
            var customer = _cacheManager!.Get<Customer>($"customer:{i}");
            customer.Should().BeNull();
        }
    }

    /// <summary>
    /// Benchmark: Cache writes (inserts)
    /// Expected: <1ms per operation
    /// </summary>
    [Benchmark]
    public void CacheWrite_10K_Inserts()
    {
        for (int i = 0; i < CacheSize; i++)
        {
            var customer = new Customer
            {
                Id = i + CacheSize,
                FirstName = $"First{i}",
                LastName = $"Last{i}",
                Email = $"test{i}@example.com",
                CreatedBy = "cache-write-test",
                CreatedDate = DateTime.UtcNow,
                ModifiedBy = "cache-write-test",
                ModifiedDate = DateTime.UtcNow
            };
            _cacheManager!.Set($"customer:new:{i}", customer, TimeSpan.FromMinutes(10));
        }
    }

    /// <summary>
    /// Benchmark: Cache updates (overwrite existing)
    /// Expected: <1ms per operation (same as inserts)
    /// </summary>
    [Benchmark]
    public void CacheUpdate_10K_Overwrites()
    {
        for (int i = 0; i < CacheSize; i++)
        {
            var customer = new Customer
            {
                Id = i,
                FirstName = $"Updated{i}",
                LastName = $"Updated{i}",
                Email = $"updated{i}@example.com",
                CreatedBy = "cache-update-test",
                CreatedDate = DateTime.UtcNow,
                ModifiedBy = "cache-update-test",
                ModifiedDate = DateTime.UtcNow
            };
            _cacheManager!.Set($"customer:{i}", customer, TimeSpan.FromMinutes(10));
        }
    }

    /// <summary>
    /// Benchmark: Cache removes
    /// Expected: <1ms per operation
    /// </summary>
    [Benchmark]
    public void CacheRemove_10K_Deletes()
    {
        for (int i = 0; i < CacheSize; i++)
        {
            _cacheManager!.Remove($"customer:{i}");
        }
    }

    /// <summary>
    /// Benchmark: Defensive cloning - shallow clone 10K objects
    /// Expected: <10ms total (defensive cloning overhead)
    /// </summary>
    [Benchmark]
    public void DefensiveCloning_ShallowClone_10K_Objects()
    {
        var customer = new Customer
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            CreatedBy = "clone-test",
            CreatedDate = DateTime.UtcNow,
            ModifiedBy = "clone-test",
            ModifiedDate = DateTime.UtcNow
        };

        for (int i = 0; i < CacheSize; i++)
        {
            var clone = customer.ShallowClone();
            clone.Should().NotBeNull();
            clone.Should().NotBeSameAs(customer);
        }
    }

    /// <summary>
    /// Benchmark: Defensive cloning - deep clone 10K objects
    /// Expected: <20ms total (more overhead than shallow)
    /// </summary>
    [Benchmark]
    public void DefensiveCloning_DeepClone_10K_Objects()
    {
        var customer = new Customer
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            CreatedBy = "clone-test",
            CreatedDate = DateTime.UtcNow,
            ModifiedBy = "clone-test",
            ModifiedDate = DateTime.UtcNow
        };

        for (int i = 0; i < CacheSize; i++)
        {
            var clone = customer.DeepClone();
            clone.Should().NotBeNull();
            clone.Should().NotBeSameAs(customer);
        }
    }

    /// <summary>
    /// Benchmark: Mixed cache operations (80% reads, 15% writes, 5% removes)
    /// Expected: <1ms per operation average
    /// </summary>
    [Benchmark]
    public void MixedCacheOperations_10K_Operations()
    {
        var random = new Random(42);
        for (int i = 0; i < CacheSize; i++)
        {
            var operation = random.Next(100);
            if (operation < 80) // 80% reads
            {
                var customer = _cacheManager!.Get<Customer>($"customer:{random.Next(CacheSize)}");
            }
            else if (operation < 95) // 15% writes
            {
                var customer = new Customer
                {
                    Id = i,
                    FirstName = $"First{i}",
                    LastName = $"Last{i}",
                    Email = $"test{i}@example.com",
                    CreatedBy = "mixed-test",
                    CreatedDate = DateTime.UtcNow,
                    ModifiedBy = "mixed-test",
                    ModifiedDate = DateTime.UtcNow
                };
                _cacheManager!.Set($"customer:mixed:{i}", customer, TimeSpan.FromMinutes(10));
            }
            else // 5% removes
            {
                _cacheManager!.Remove($"customer:{random.Next(CacheSize)}");
            }
        }
    }

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Test entity: Customer for cache testing
/// </summary>
[DalEntity]
[Table("Customers")]
public partial class Customer : IEntityCloneable<Customer>
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? CreatedBy { get; set; }
    public DateTime? CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }

    public Customer ShallowClone()
    {
        return (Customer)MemberwiseClone();
    }

    public Customer DeepClone()
    {
        return new Customer
        {
            Id = Id,
            FirstName = FirstName,
            LastName = LastName,
            Email = Email,
            CreatedBy = CreatedBy,
            CreatedDate = CreatedDate,
            ModifiedBy = ModifiedBy,
            ModifiedDate = ModifiedDate
        };
    }
}
