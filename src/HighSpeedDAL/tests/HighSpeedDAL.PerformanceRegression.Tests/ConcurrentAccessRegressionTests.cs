using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using FluentAssertions;
using HighSpeedDAL.Core;
using HighSpeedDAL.Core.Attributes;
using HighSpeedDAL.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Moq;

namespace HighSpeedDAL.PerformanceRegression.Tests;

/// <summary>
/// Performance regression tests for concurrent access scenarios.
/// Tests parallel reads, writes, and mixed workloads.
/// </summary>
[MemoryDiagnoser]
[MarkdownExporter]
[Config(typeof(Config))]
public class ConcurrentAccessRegressionTests : IDisposable
{
    private SqliteConnection? _connection;
    private ProductDal? _productDal;
    private Mock<ILogger>? _loggerMock;
    private const int DataSize = 1000;

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
    public async Task Setup()
    {
        _loggerMock = new Mock<ILogger>();
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        // Create tables
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE Products (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Price REAL NOT NULL,
                StockQuantity INTEGER NOT NULL,
                Category TEXT,
                CreatedBy TEXT,
                CreatedDate TEXT,
                ModifiedBy TEXT,
                ModifiedDate TEXT
            );

            CREATE INDEX idx_Products_Category ON Products(Category);
            CREATE INDEX idx_Products_Price ON Products(Price);
        ";
        cmd.ExecuteNonQuery();

        var connectionFactory = new TestConnectionFactory(_connection);
        _productDal = new ProductDal(connectionFactory, _loggerMock.Object);

        // Pre-populate with test data
        var products = new List<Product>();
        for (int i = 0; i < DataSize; i++)
        {
            products.Add(new Product
            {
                Name = $"Product {i}",
                Price = 100.00m + i,
                StockQuantity = 50 + i,
                Category = $"Category {i % 10}",
                CreatedBy = "concurrent-test",
                CreatedDate = DateTime.UtcNow,
                ModifiedBy = "concurrent-test",
                ModifiedDate = DateTime.UtcNow
            });
        }
        await _productDal.BulkInsertAsync(products);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _connection?.Close();
        _connection?.Dispose();
    }

    /// <summary>
    /// Baseline: 10 parallel read queries
    /// Expected: All queries complete successfully with correct data
    /// </summary>
    [Benchmark(Baseline = true)]
    public async Task ConcurrentReads_10_Queries()
    {
        var tasks = new List<Task<IEnumerable<Product>>>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_productDal!.GetAllAsync());
        }

        var results = await Task.WhenAll(tasks);
        results.Should().HaveCount(10);
        results.Should().AllSatisfy(r => r.Should().HaveCount(DataSize));
    }

    /// <summary>
    /// Benchmark: 50 parallel read queries
    /// Expected: All queries complete successfully
    /// </summary>
    [Benchmark]
    public async Task ConcurrentReads_50_Queries()
    {
        var tasks = new List<Task<IEnumerable<Product>>>();
        for (int i = 0; i < 50; i++)
        {
            tasks.Add(_productDal!.GetAllAsync());
        }

        var results = await Task.WhenAll(tasks);
        results.Should().HaveCount(50);
    }

    /// <summary>
    /// Benchmark: 100 parallel read queries
    /// Expected: All queries complete successfully
    /// </summary>
    [Benchmark]
    public async Task ConcurrentReads_100_Queries()
    {
        var tasks = new List<Task<IEnumerable<Product>>>();
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(_productDal!.GetAllAsync());
        }

        var results = await Task.WhenAll(tasks);
        results.Should().HaveCount(100);
    }

    /// <summary>
    /// Benchmark: Sequential inserts (SQLite limitation)
    /// Expected: 1000 inserts complete successfully
    /// </summary>
    [Benchmark]
    public async Task SequentialInserts_1K_Products()
    {
        for (int i = 0; i < 1000; i++)
        {
            var product = new Product
            {
                Name = $"Product {i + DataSize}",
                Price = 100.00m + i,
                StockQuantity = 50 + i,
                Category = $"Category {i % 10}",
                CreatedBy = "sequential-test",
                CreatedDate = DateTime.UtcNow,
                ModifiedBy = "sequential-test",
                ModifiedDate = DateTime.UtcNow
            };
            await _productDal!.InsertAsync(product);
        }
    }

    /// <summary>
    /// Benchmark: Sequential updates
    /// Expected: 1000 updates complete successfully
    /// </summary>
    [Benchmark]
    public async Task SequentialUpdates_1K_Products()
    {
        var products = await _productDal!.GetAllAsync();
        var toUpdate = products.Take(1000).ToList();

        for (int i = 0; i < toUpdate.Count; i++)
        {
            toUpdate[i].Price *= 1.05m;
            toUpdate[i].ModifiedBy = "sequential-update-test";
            toUpdate[i].ModifiedDate = DateTime.UtcNow;
            await _productDal.UpdateAsync(toUpdate[i]);
        }
    }

    /// <summary>
    /// Benchmark: Mixed workload with concurrent reads
    /// 50 reads + 100 sequential writes
    /// Expected: <2 seconds total
    /// </summary>
    [Benchmark]
    public async Task MixedWorkload_Concurrent_50Reads_100Writes()
    {
        // Start 50 read queries
        var readTasks = new List<Task<IEnumerable<Product>>>();
        for (int i = 0; i < 50; i++)
        {
            readTasks.Add(_productDal!.GetAllAsync());
        }

        // Perform 100 sequential writes
        for (int i = 0; i < 100; i++)
        {
            var product = new Product
            {
                Name = $"MixedProduct {i}",
                Price = 100.00m + i,
                StockQuantity = 50 + i,
                Category = $"Category {i % 10}",
                CreatedBy = "mixed-test",
                CreatedDate = DateTime.UtcNow,
                ModifiedBy = "mixed-test",
                ModifiedDate = DateTime.UtcNow
            };
            await _productDal!.InsertAsync(product);
        }

        // Wait for all reads to complete
        var results = await Task.WhenAll(readTasks);
        results.Should().HaveCount(50);
    }

    /// <summary>
    /// Benchmark: Concurrent updates with shared connection
    /// Expected: All updates complete successfully (sequential in SQLite)
    /// </summary>
    [Benchmark]
    public async Task ConcurrentUpdates_100_Products()
    {
        var products = await _productDal!.GetAllAsync();
        var toUpdate = products.Take(100).ToList();

        for (int i = 0; i < toUpdate.Count; i++)
        {
            toUpdate[i].Price *= 1.10m;
            toUpdate[i].ModifiedBy = "concurrent-update-test";
            toUpdate[i].ModifiedDate = DateTime.UtcNow;
        }

        // SQLite will serialize these, but test the pattern
        await _productDal.BulkUpdateAsync(toUpdate);
    }

    /// <summary>
    /// Benchmark: High-frequency reads (stress test)
    /// 1000 reads as fast as possible
    /// Expected: <500ms total
    /// </summary>
    [Benchmark]
    public async Task HighFrequencyReads_1K_Queries()
    {
        var tasks = new List<Task<Product?>>();
        for (int i = 0; i < 1000; i++)
        {
            int id = (i % DataSize) + 1;
            tasks.Add(_productDal!.GetByIdAsync(id));
        }

        var results = await Task.WhenAll(tasks);
        results.Should().HaveCount(1000);
        results.Should().AllSatisfy(r => r.Should().NotBeNull());
    }

    /// <summary>
    /// Benchmark: Read-heavy mixed workload
    /// 90% reads, 10% writes
    /// Expected: <1 second total
    /// </summary>
    [Benchmark]
    public async Task ReadHeavyWorkload_90Reads_10Writes()
    {
        var tasks = new List<Task>();

        // 90 read queries
        for (int i = 0; i < 90; i++)
        {
            tasks.Add(_productDal!.GetAllAsync());
        }

        // 10 write operations (sequential)
        for (int i = 0; i < 10; i++)
        {
            var product = new Product
            {
                Name = $"ReadHeavyProduct {i}",
                Price = 100.00m + i,
                StockQuantity = 50 + i,
                Category = $"Category {i % 10}",
                CreatedBy = "read-heavy-test",
                CreatedDate = DateTime.UtcNow,
                ModifiedBy = "read-heavy-test",
                ModifiedDate = DateTime.UtcNow
            };
            tasks.Add(_productDal!.InsertAsync(product));
        }

        await Task.WhenAll(tasks);
    }

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }

    private class TestConnectionFactory : IDatabaseConnectionFactory
    {
        private readonly SqliteConnection _connection;

        public TestConnectionFactory(SqliteConnection connection)
        {
            _connection = connection;
        }

        public async Task<IDbConnectionWrapper> CreateConnectionAsync()
        {
            return new SqliteConnectionWrapper(_connection);
        }
    }
}

/// <summary>
/// Test entity: Product for concurrent access testing
/// </summary>
[DalEntity]
[Table("Products")]
public partial class ConcurrentProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public string? Category { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
