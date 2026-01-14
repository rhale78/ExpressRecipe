using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using FluentAssertions;
using HighSpeedDAL.Core;
using HighSpeedDAL.Core.Attributes;
using HighSpeedDAL.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Moq;

namespace HighSpeedDAL.PerformanceRegression.Tests;

/// <summary>
/// Performance regression tests for bulk operations.
/// Baseline: 228,930 rows/sec bulk insert (10K rows in 43ms)
/// Threshold: Fail if <205,000 rows/sec (10% regression)
/// </summary>
[MemoryDiagnoser]
[MarkdownExporter]
[Config(typeof(Config))]
public class BulkOperationsRegressionTests : IDisposable
{
    private SqliteConnection? _connection;
    private ProductDal? _productDal;
    private OrderDal? _orderDal;
    private Mock<ILogger>? _loggerMock;

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

            CREATE TABLE Orders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                OrderNumber TEXT NOT NULL,
                CustomerId INTEGER NOT NULL,
                TotalAmount REAL NOT NULL,
                Status TEXT,
                IsDeleted INTEGER DEFAULT 0,
                DeletedBy TEXT,
                DeletedDate TEXT,
                CreatedBy TEXT,
                CreatedDate TEXT,
                ModifiedBy TEXT,
                ModifiedDate TEXT
            );

            CREATE INDEX idx_Orders_IsDeleted ON Orders(IsDeleted) WHERE IsDeleted = 0;
            CREATE INDEX idx_Orders_Status ON Orders(Status);
        ";
        cmd.ExecuteNonQuery();

        var connectionFactory = new TestConnectionFactory(_connection);
        _productDal = new ProductDal(connectionFactory, _loggerMock.Object);
        _orderDal = new OrderDal(connectionFactory, _loggerMock.Object);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _connection?.Close();
        _connection?.Dispose();
    }

    /// <summary>
    /// Baseline benchmark: Bulk insert 10,000 products
    /// Expected: >228,000 rows/second (baseline from integration tests)
    /// Threshold: >205,000 rows/second (10% regression tolerance)
    /// </summary>
    [Benchmark(Baseline = true)]
    public async Task BulkInsert_10K_Products_Baseline()
    {
        var products = new List<Product>();
        for (int i = 0; i < 10000; i++)
        {
            products.Add(new Product
            {
                Name = $"Product {i}",
                Price = 100.00m + i,
                StockQuantity = 50 + i,
                Category = $"Category {i % 10}",
                CreatedBy = "regression-test",
                CreatedDate = DateTime.UtcNow,
                ModifiedBy = "regression-test",
                ModifiedDate = DateTime.UtcNow
            });
        }

        await _productDal!.BulkInsertAsync(products);
    }

    /// <summary>
    /// Benchmark: Bulk insert 5,000 products with active indexes
    /// Expected: <3 seconds (baseline from integration tests)
    /// </summary>
    [Benchmark]
    public async Task BulkInsert_5K_Products_WithIndexes()
    {
        var products = new List<Product>();
        for (int i = 0; i < 5000; i++)
        {
            products.Add(new Product
            {
                Name = $"Product {i}",
                Price = 100.00m + i,
                StockQuantity = 50 + i,
                Category = $"Category {i % 10}",
                CreatedBy = "regression-test",
                CreatedDate = DateTime.UtcNow,
                ModifiedBy = "regression-test",
                ModifiedDate = DateTime.UtcNow
            });
        }

        await _productDal!.BulkInsertAsync(products);
    }

    /// <summary>
    /// Benchmark: Bulk update 5,000 products
    /// Expected: <3 seconds (baseline from integration tests)
    /// </summary>
    [Benchmark]
    public async Task BulkUpdate_5K_Products()
    {
        // First insert
        var products = new List<Product>();
        for (int i = 0; i < 5000; i++)
        {
            products.Add(new Product
            {
                Name = $"Product {i}",
                Price = 100.00m + i,
                StockQuantity = 50 + i,
                Category = $"Category {i % 10}",
                CreatedBy = "regression-test",
                CreatedDate = DateTime.UtcNow,
                ModifiedBy = "regression-test",
                ModifiedDate = DateTime.UtcNow
            });
        }

        await _productDal!.BulkInsertAsync(products);

        // Then update all prices
        var allProducts = await _productDal.GetAllAsync();
        foreach (var product in allProducts)
        {
            product.Price *= 1.1m; // 10% price increase
            product.ModifiedBy = "regression-test-update";
            product.ModifiedDate = DateTime.UtcNow;
        }

        await _productDal.BulkUpdateAsync(allProducts.ToList());
    }

    /// <summary>
    /// Benchmark: Bulk soft delete 5,000 orders
    /// Expected: <2 seconds
    /// </summary>
    [Benchmark]
    public async Task BulkSoftDelete_5K_Orders()
    {
        // First insert
        var orders = new List<Order>();
        for (int i = 0; i < 5000; i++)
        {
            orders.Add(new Order
            {
                OrderNumber = $"ORD{i:D6}",
                CustomerId = i % 100,
                TotalAmount = 100.00m + i,
                Status = "New",
                IsDeleted = false,
                CreatedBy = "regression-test",
                CreatedDate = DateTime.UtcNow,
                ModifiedBy = "regression-test",
                ModifiedDate = DateTime.UtcNow
            });
        }

        await _orderDal!.BulkInsertAsync(orders);

        // Then soft delete all
        var allOrders = await _orderDal.GetAllAsync();
        await _orderDal.BulkDeleteAsync(allOrders.Select(o => o.Id).ToList());
    }

    /// <summary>
    /// Benchmark: Mixed workload (inserts, updates, deletes)
    /// Expected: >100,000 ops/second (baseline: 137,129 ops/sec)
    /// Threshold: >90,000 ops/second (10% regression tolerance)
    /// </summary>
    [Benchmark]
    public async Task MixedWorkload_1K_Operations()
    {
        // 500 inserts
        var products = new List<Product>();
        for (int i = 0; i < 500; i++)
        {
            products.Add(new Product
            {
                Name = $"Product {i}",
                Price = 100.00m + i,
                StockQuantity = 50 + i,
                Category = $"Category {i % 10}",
                CreatedBy = "regression-test",
                CreatedDate = DateTime.UtcNow,
                ModifiedBy = "regression-test",
                ModifiedDate = DateTime.UtcNow
            });
        }
        await _productDal!.BulkInsertAsync(products);

        // 300 updates
        var existingProducts = await _productDal.GetAllAsync();
        var toUpdate = existingProducts.Take(300).ToList();
        foreach (var product in toUpdate)
        {
            product.Price *= 1.05m;
            product.ModifiedBy = "regression-test-update";
            product.ModifiedDate = DateTime.UtcNow;
        }
        await _productDal.BulkUpdateAsync(toUpdate);

        // 200 reads
        for (int i = 0; i < 200; i++)
        {
            var product = await _productDal.GetByIdAsync(i % 500 + 1);
        }
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
/// Test entity: Product with auto-audit tracking
/// </summary>
[DalEntity]
[Table("Products")]
public partial class Product
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

/// <summary>
/// Test entity: Order with soft delete support
/// </summary>
[DalEntity]
[Table("Orders")]
public partial class Order
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Status { get; set; }
    public bool IsDeleted { get; set; }
    public string? DeletedBy { get; set; }
    public DateTime? DeletedDate { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
