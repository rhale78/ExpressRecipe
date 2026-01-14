using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using HighSpeedDAL.Core.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HighSpeedDAL.Sqlite.Tests
{
    /// <summary>
    /// Comprehensive integration tests for SQLite with high-performance scenarios.
    /// 
    /// PURPOSE: Validates SQLite connectivity, database operations, and performance
    /// characteristics. These tests ensure the framework works correctly with SQLite
    /// and can handle high-throughput scenarios with in-memory databases.
    /// 
    /// APPROACH: Uses raw ADO.NET (SqliteConnection, SqliteCommand, transactions)
    /// to test underlying database connectivity and Microsoft's SQLite libraries.
    /// Intentionally does NOT use framework methods (BulkInsertAsync, GetByIdAsync, etc.)
    /// to isolate database connectivity testing.
    /// 
    /// WHY NOT USE FRAMEWORK: These are infrastructure validation tests. They verify
    /// that SQLite connectivity works correctly, transactions function properly,
    /// and database performance meets expectations. They test Microsoft's SQLite
    /// libraries, not HighSpeedDAL framework methods.
    /// 
    /// BENEFITS: SQLite in-memory provides fast, isolated testing without external
    /// database dependencies. Perfect for CI/CD pipelines and developer workstations.
    /// 
    /// FOR FRAMEWORK USAGE EXAMPLES: See HighSpeedDAL.FrameworkUsage.Tests project
    /// which demonstrates how developers should use the framework with [Table] attributes,
    /// source-generated DAL classes, and framework methods like BulkInsertAsync,
    /// GetByIdAsync, UpdateAsync, etc.
    /// 
    /// USAGE NOTE: Users should NOT copy these raw SQL patterns. Instead, use framework
    /// methods demonstrated in FrameworkUsage.Tests which provide better performance,
    /// automatic caching, retry logic, and defensive cloning.
    /// </summary>
    public class SqliteHighPerformanceIntegrationTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly Mock<ILogger> _loggerMock;

        public SqliteHighPerformanceIntegrationTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            _loggerMock = new Mock<ILogger>();
            InitializeDatabase();
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }

        private void InitializeDatabase()
        {
            string[] createTableSql = new[]
            {
                @"CREATE TABLE Products (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Price REAL NOT NULL,
                    StockQuantity INTEGER NOT NULL,
                    Category TEXT NOT NULL,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    CreatedDate TEXT NOT NULL
                )",
                @"CREATE TABLE Orders (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    OrderNumber TEXT NOT NULL,
                    CustomerId INTEGER NOT NULL,
                    TotalAmount REAL NOT NULL,
                    Status TEXT NOT NULL,
                    OrderDate TEXT NOT NULL,
                    IsDeleted INTEGER NOT NULL DEFAULT 0
                )",
                @"CREATE TABLE Customers (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FirstName TEXT NOT NULL,
                    LastName TEXT NOT NULL,
                    Email TEXT NOT NULL,
                    CreatedDate TEXT NOT NULL
                )",
                @"CREATE INDEX idx_products_category ON Products(Category)",
                @"CREATE INDEX idx_orders_customer ON Orders(CustomerId)",
                @"CREATE INDEX idx_orders_date ON Orders(OrderDate)"
            };

            foreach (string sql in createTableSql)
            {
                using SqliteCommand cmd = new SqliteCommand(sql, _connection);
                cmd.ExecuteNonQuery();
            }
        }

        // ====================================================================
        // BULK INSERT TESTS
        // ====================================================================

        [Fact]
        public async Task BulkInsert_10KProducts_CompletesUnder5Seconds()
        {
            // Arrange
            const int productCount = 10000;
            Stopwatch sw = Stopwatch.StartNew();

            // Act - Bulk insert with transaction batching
            using (SqliteTransaction transaction = _connection.BeginTransaction())
            {
                for (int i = 0; i < productCount; i++)
                {
                    using SqliteCommand cmd = _connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"INSERT INTO Products (Name, Price, StockQuantity, Category, CreatedDate) 
                                      VALUES (@name, @price, @stock, @category, @date)";
                    cmd.Parameters.AddWithValue("@name", $"Product {i}");
                    cmd.Parameters.AddWithValue("@price", (i % 1000) * 10.99m);
                    cmd.Parameters.AddWithValue("@stock", i % 500);
                    cmd.Parameters.AddWithValue("@category", $"Category{i % 10}");
                    cmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("o"));
                    await cmd.ExecuteNonQueryAsync();
                }
                transaction.Commit();
            }
            sw.Stop();

            // Assert - Performance target
            sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));

            // Verify count
            using SqliteCommand countCmd = new SqliteCommand("SELECT COUNT(*) FROM Products", _connection);
            long count = (long)(await countCmd.ExecuteScalarAsync())!;
            count.Should().Be(productCount);

            Console.WriteLine($"Bulk insert performance: {productCount} rows in {sw.ElapsedMilliseconds}ms ({productCount / sw.Elapsed.TotalSeconds:N0} rows/sec)");
        }

        [Fact]
        public async Task BulkInsert_WithIndexes_MaintainsPerformance()
        {
            // Arrange
            const int rowCount = 5000;

            // Act - Insert with indexes
            Stopwatch sw = Stopwatch.StartNew();
            using (SqliteTransaction transaction = _connection.BeginTransaction())
            {
                for (int i = 0; i < rowCount; i++)
                {
                    using SqliteCommand cmd = _connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"INSERT INTO Products (Name, Price, StockQuantity, Category, CreatedDate) 
                                      VALUES (@name, @price, @stock, @category, @date)";
                    cmd.Parameters.AddWithValue("@name", $"Indexed Product {i}");
                    cmd.Parameters.AddWithValue("@price", i * 5.99m);
                    cmd.Parameters.AddWithValue("@stock", i);
                    cmd.Parameters.AddWithValue("@category", $"Cat{i % 5}");
                    cmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("o"));
                    await cmd.ExecuteNonQueryAsync();
                }
                transaction.Commit();
            }
            sw.Stop();

            // Assert - Performance acceptable with indexes
            sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(3));

            // Verify index usage with query
            using SqliteCommand queryCmd = new SqliteCommand("SELECT COUNT(*) FROM Products WHERE Category = 'Cat2'", _connection);
            long count = (long)(await queryCmd.ExecuteScalarAsync())!;
            count.Should().Be(1000); // 5000 / 5 categories
        }

        // ====================================================================
        // BULK UPDATE TESTS
        // ====================================================================

        [Fact]
        public async Task BulkUpdate_5KProducts_CompletesUnder3Seconds()
        {
            // Arrange - Seed data
            const int productCount = 5000;
            using (SqliteTransaction transaction = _connection.BeginTransaction())
            {
                for (int i = 0; i < productCount; i++)
                {
                    using SqliteCommand cmd = _connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"INSERT INTO Products (Name, Price, StockQuantity, Category, CreatedDate) 
                                      VALUES (@name, @price, @stock, @category, @date)";
                    cmd.Parameters.AddWithValue("@name", $"Product {i}");
                    cmd.Parameters.AddWithValue("@price", i * 10.0m);
                    cmd.Parameters.AddWithValue("@stock", i);
                    cmd.Parameters.AddWithValue("@category", "Electronics");
                    cmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("o"));
                    await cmd.ExecuteNonQueryAsync();
                }
                transaction.Commit();
            }

            // Act - Bulk update with transaction
            Stopwatch sw = Stopwatch.StartNew();
            using (SqliteTransaction transaction = _connection.BeginTransaction())
            {
                using SqliteCommand cmd = new SqliteCommand(
                    "UPDATE Products SET Price = Price * 1.1 WHERE Category = 'Electronics'", 
                    _connection, 
                    transaction);
                int updated = await cmd.ExecuteNonQueryAsync();
                transaction.Commit();
                updated.Should().Be(productCount);
            }
            sw.Stop();

            // Assert - Performance target
            sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(3));

            // Verify updates applied
            using SqliteCommand verifyCmd = new SqliteCommand("SELECT Price FROM Products WHERE Id = 1", _connection);
            double price = (double)(await verifyCmd.ExecuteScalarAsync())!;
            price.Should().BeApproximately(0.0 * 1.1, 0.01); // First product (i=0) price * 1.1
        }

        // ====================================================================
        // CONCURRENT READ TESTS
        // ====================================================================

        [Fact]
        public async Task ConcurrentReads_10Queries_AllReturnCorrectData()
        {
            // Arrange - Seed data
            const int productCount = 1000;
            using (SqliteTransaction transaction = _connection.BeginTransaction())
            {
                for (int i = 0; i < productCount; i++)
                {
                    using SqliteCommand cmd = _connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"INSERT INTO Products (Name, Price, StockQuantity, Category, CreatedDate) 
                                      VALUES (@name, @price, @stock, @category, @date)";
                    cmd.Parameters.AddWithValue("@name", $"Concurrent Product {i}");
                    cmd.Parameters.AddWithValue("@price", i * 2.50m);
                    cmd.Parameters.AddWithValue("@stock", i);
                    cmd.Parameters.AddWithValue("@category", $"Category{i % 5}");
                    cmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("o"));
                    await cmd.ExecuteNonQueryAsync();
                }
                transaction.Commit();
            }

            // Act - 10 concurrent read queries
            List<Task<long>> tasks = new List<Task<long>>();
            for (int i = 0; i < 10; i++)
            {
                int categoryId = i % 5;
                tasks.Add(Task.Run(async () =>
                {
                    using SqliteCommand cmd = new SqliteCommand(
                        $"SELECT COUNT(*) FROM Products WHERE Category = 'Category{categoryId}'", 
                        _connection);
                    return (long)(await cmd.ExecuteScalarAsync())!;
                }));
            }

            long[] results = await Task.WhenAll(tasks);

            // Assert - All queries return correct counts
            results.Should().AllSatisfy(count => count.Should().Be(200)); // 1000 / 5 categories
        }

        // ====================================================================
        // TRANSACTION TESTS
        // ====================================================================

        [Fact]
        public async Task Transaction_Commit_DataPersists()
        {
            // Arrange & Act
            int orderId;
            using (SqliteTransaction transaction = _connection.BeginTransaction())
            {
                using SqliteCommand cmd = _connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"INSERT INTO Orders (OrderNumber, CustomerId, TotalAmount, Status, OrderDate) 
                                  VALUES (@number, @custId, @amount, @status, @date);
                                  SELECT last_insert_rowid()";
                cmd.Parameters.AddWithValue("@number", "ORD-001");
                cmd.Parameters.AddWithValue("@custId", 1);
                cmd.Parameters.AddWithValue("@amount", 299.99);
                cmd.Parameters.AddWithValue("@status", "Pending");
                cmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("o"));
                orderId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                transaction.Commit();
            }

            // Assert - Data persists after commit
            using SqliteCommand verifyCmd = new SqliteCommand("SELECT OrderNumber FROM Orders WHERE Id = @id", _connection);
            verifyCmd.Parameters.AddWithValue("@id", orderId);
            string? orderNumber = (string?)(await verifyCmd.ExecuteScalarAsync());
            orderNumber.Should().Be("ORD-001");
        }

        [Fact]
        public async Task Transaction_Rollback_DataNotPersisted()
        {
            // Arrange - Get initial count
            using SqliteCommand countBeforeCmd = new SqliteCommand("SELECT COUNT(*) FROM Orders", _connection);
            long countBefore = (long)(await countBeforeCmd.ExecuteScalarAsync())!;

            // Act - Insert with rollback
            try
            {
                using SqliteTransaction transaction = _connection.BeginTransaction();
                using SqliteCommand cmd = _connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"INSERT INTO Orders (OrderNumber, CustomerId, TotalAmount, Status, OrderDate) 
                                  VALUES (@number, @custId, @amount, @status, @date)";
                cmd.Parameters.AddWithValue("@number", "ORD-ROLLBACK");
                cmd.Parameters.AddWithValue("@custId", 1);
                cmd.Parameters.AddWithValue("@amount", 499.99);
                cmd.Parameters.AddWithValue("@status", "Pending");
                cmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("o"));
                await cmd.ExecuteNonQueryAsync();
                transaction.Rollback();
            }
            catch { /* Rollback already called */ }

            // Assert - Data not persisted
            using SqliteCommand countAfterCmd = new SqliteCommand("SELECT COUNT(*) FROM Orders", _connection);
            long countAfter = (long)(await countAfterCmd.ExecuteScalarAsync())!;
            countAfter.Should().Be(countBefore);
        }

        // ====================================================================
        // COMPLEX QUERY TESTS
        // ====================================================================

        [Fact]
        public async Task ComplexQuery_JoinWithAggregates_ReturnsCorrectResults()
        {
            // Arrange - Seed customers and orders
            using (SqliteTransaction transaction = _connection.BeginTransaction())
            {
                // Insert customers
                for (int i = 1; i <= 10; i++)
                {
                    using SqliteCommand custCmd = _connection.CreateCommand();
                    custCmd.Transaction = transaction;
                    custCmd.CommandText = @"INSERT INTO Customers (FirstName, LastName, Email, CreatedDate) 
                                          VALUES (@first, @last, @email, @date)";
                    custCmd.Parameters.AddWithValue("@first", $"Customer{i}");
                    custCmd.Parameters.AddWithValue("@last", $"Last{i}");
                    custCmd.Parameters.AddWithValue("@email", $"customer{i}@test.com");
                    custCmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("o"));
                    await custCmd.ExecuteNonQueryAsync();
                }

                // Insert orders (5 orders per customer)
                for (int custId = 1; custId <= 10; custId++)
                {
                    for (int ordNum = 1; ordNum <= 5; ordNum++)
                    {
                        using SqliteCommand ordCmd = _connection.CreateCommand();
                        ordCmd.Transaction = transaction;
                        ordCmd.CommandText = @"INSERT INTO Orders (OrderNumber, CustomerId, TotalAmount, Status, OrderDate) 
                                             VALUES (@number, @custId, @amount, @status, @date)";
                        ordCmd.Parameters.AddWithValue("@number", $"ORD-{custId}-{ordNum}");
                        ordCmd.Parameters.AddWithValue("@custId", custId);
                        ordCmd.Parameters.AddWithValue("@amount", ordNum * 100.0);
                        ordCmd.Parameters.AddWithValue("@status", "Completed");
                        ordCmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("o"));
                        await ordCmd.ExecuteNonQueryAsync();
                    }
                }
                transaction.Commit();
            }

            // Act - Complex query with join and aggregates
            string sql = @"
                SELECT c.Id, c.FirstName, c.LastName, 
                       COUNT(o.Id) as OrderCount, 
                       SUM(o.TotalAmount) as TotalSpent
                FROM Customers c
                LEFT JOIN Orders o ON c.Id = o.CustomerId
                WHERE o.IsDeleted = 0
                GROUP BY c.Id
                HAVING COUNT(o.Id) >= 5
                ORDER BY TotalSpent DESC";

            using SqliteCommand cmd = new SqliteCommand(sql, _connection);
            using SqliteDataReader reader = await cmd.ExecuteReaderAsync();

            // Assert - Verify results
            int customerCount = 0;
            while (await reader.ReadAsync())
            {
                customerCount++;
                long orderCount = reader.GetInt64(3);
                double totalSpent = reader.GetDouble(4);

                orderCount.Should().Be(5);
                totalSpent.Should().Be(1500.0); // 100 + 200 + 300 + 400 + 500
            }
            customerCount.Should().Be(10);
        }

        // ====================================================================
        // SOFT DELETE TESTS
        // ====================================================================

        [Fact]
        public async Task SoftDelete_MarksRecordDeleted_DataPreserved()
        {
            // Arrange - Insert order
            int orderId;
            using (SqliteTransaction transaction = _connection.BeginTransaction())
            {
                using SqliteCommand cmd = _connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"INSERT INTO Orders (OrderNumber, CustomerId, TotalAmount, Status, OrderDate) 
                                  VALUES (@number, @custId, @amount, @status, @date);
                                  SELECT last_insert_rowid()";
                cmd.Parameters.AddWithValue("@number", "ORD-SOFTDEL");
                cmd.Parameters.AddWithValue("@custId", 1);
                cmd.Parameters.AddWithValue("@amount", 199.99);
                cmd.Parameters.AddWithValue("@status", "Pending");
                cmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("o"));
                orderId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                transaction.Commit();
            }

            // Act - Soft delete
            using (SqliteCommand deleteCmd = new SqliteCommand(
                "UPDATE Orders SET IsDeleted = 1 WHERE Id = @id", _connection))
            {
                deleteCmd.Parameters.AddWithValue("@id", orderId);
                await deleteCmd.ExecuteNonQueryAsync();
            }

            // Assert - Record marked deleted but data preserved
            using SqliteCommand verifyCmd = new SqliteCommand(
                "SELECT OrderNumber, IsDeleted FROM Orders WHERE Id = @id", _connection);
            verifyCmd.Parameters.AddWithValue("@id", orderId);
            using SqliteDataReader reader = await verifyCmd.ExecuteReaderAsync();
            
            reader.Read().Should().BeTrue();
            reader.GetString(0).Should().Be("ORD-SOFTDEL");
            reader.GetInt32(1).Should().Be(1); // IsDeleted = true
        }

        [Fact]
        public async Task SoftDelete_FilteredQueries_ExcludeDeletedRecords()
        {
            // Arrange - Insert 10 orders, delete 3
            List<int> orderIds = new List<int>();
            using (SqliteTransaction transaction = _connection.BeginTransaction())
            {
                for (int i = 0; i < 10; i++)
                {
                    using SqliteCommand cmd = _connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"INSERT INTO Orders (OrderNumber, CustomerId, TotalAmount, Status, OrderDate) 
                                      VALUES (@number, @custId, @amount, @status, @date);
                                      SELECT last_insert_rowid()";
                    cmd.Parameters.AddWithValue("@number", $"ORD-{i}");
                    cmd.Parameters.AddWithValue("@custId", 1);
                    cmd.Parameters.AddWithValue("@amount", i * 50.0);
                    cmd.Parameters.AddWithValue("@status", "Completed");
                    cmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("o"));
                    orderIds.Add(Convert.ToInt32(await cmd.ExecuteScalarAsync()));
                }
                transaction.Commit();
            }

            // Soft delete first 3 orders
            using (SqliteCommand deleteCmd = new SqliteCommand(
                "UPDATE Orders SET IsDeleted = 1 WHERE Id IN (@id1, @id2, @id3)", _connection))
            {
                deleteCmd.Parameters.AddWithValue("@id1", orderIds[0]);
                deleteCmd.Parameters.AddWithValue("@id2", orderIds[1]);
                deleteCmd.Parameters.AddWithValue("@id3", orderIds[2]);
                await deleteCmd.ExecuteNonQueryAsync();
            }

            // Act - Query active orders only
            using SqliteCommand countCmd = new SqliteCommand(
                "SELECT COUNT(*) FROM Orders WHERE IsDeleted = 0", _connection);
            long activeCount = (long)(await countCmd.ExecuteScalarAsync())!;

            // Assert - Only 7 active orders
            activeCount.Should().Be(7);
        }

        // ====================================================================
        // PERFORMANCE BENCHMARK TESTS
        // ====================================================================

        [Fact]
        public async Task PerformanceBenchmark_MixedWorkload_MeetsTargets()
        {
            // Arrange
            const int iterations = 1000;
            Stopwatch sw = Stopwatch.StartNew();

            // Act - Mixed workload: inserts, updates, reads
            using (SqliteTransaction transaction = _connection.BeginTransaction())
            {
                for (int i = 0; i < iterations; i++)
                {
                    // Insert
                    using (SqliteCommand insertCmd = _connection.CreateCommand())
                    {
                        insertCmd.Transaction = transaction;
                        insertCmd.CommandText = @"INSERT INTO Products (Name, Price, StockQuantity, Category, CreatedDate) 
                                                VALUES (@name, @price, @stock, @category, @date)";
                        insertCmd.Parameters.AddWithValue("@name", $"Benchmark Product {i}");
                        insertCmd.Parameters.AddWithValue("@price", i * 5.0m);
                        insertCmd.Parameters.AddWithValue("@stock", i);
                        insertCmd.Parameters.AddWithValue("@category", "Benchmark");
                        insertCmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("o"));
                        await insertCmd.ExecuteNonQueryAsync();
                    }

                    // Update (every 10th)
                    if (i % 10 == 0)
                    {
                        using SqliteCommand updateCmd = _connection.CreateCommand();
                        updateCmd.Transaction = transaction;
                        updateCmd.CommandText = "UPDATE Products SET IsActive = 0 WHERE Name = @name";
                        updateCmd.Parameters.AddWithValue("@name", $"Benchmark Product {i}");
                        await updateCmd.ExecuteNonQueryAsync();
                    }

                    // Read (every 5th)
                    if (i % 5 == 0)
                    {
                        using SqliteCommand readCmd = _connection.CreateCommand();
                        readCmd.Transaction = transaction;
                        readCmd.CommandText = "SELECT COUNT(*) FROM Products WHERE Category = 'Benchmark'";
                        await readCmd.ExecuteScalarAsync();
                    }
                }
                transaction.Commit();
            }
            sw.Stop();

            // Assert - Performance targets
            sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
            Console.WriteLine($"Mixed workload: {iterations} iterations in {sw.ElapsedMilliseconds}ms ({iterations / sw.Elapsed.TotalSeconds:N0} ops/sec)");
        }

                // ====================================================================
                // DATA INTEGRITY TESTS
                // ====================================================================

                [Fact]
                public async Task DataIntegrity_SequentialInserts_AllDataPreserved()
                {
                    // Arrange
                    const int taskCount = 10;
                    const int insertsPerTask = 100;

                    // Act - Sequential inserts (SQLite in-memory doesn't handle true concurrent writes well)
                    for (int taskId = 0; taskId < taskCount; taskId++)
                    {
                        for (int i = 0; i < insertsPerTask; i++)
                        {
                            using SqliteCommand cmd = _connection.CreateCommand();
                            cmd.CommandText = @"INSERT INTO Products (Name, Price, StockQuantity, Category, CreatedDate) 
                                              VALUES (@name, @price, @stock, @category, @date)";
                            cmd.Parameters.AddWithValue("@name", $"Task{taskId}-Product{i}");
                            cmd.Parameters.AddWithValue("@price", i * 1.0m);
                            cmd.Parameters.AddWithValue("@stock", i);
                            cmd.Parameters.AddWithValue("@category", $"Task{taskId}");
                            cmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("o"));
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    // Assert - All data inserted
                    using SqliteCommand countCmd = new SqliteCommand("SELECT COUNT(*) FROM Products", _connection);
                    long totalCount = (long)(await countCmd.ExecuteScalarAsync())!;
                    totalCount.Should().Be(taskCount * insertsPerTask);

                                // Verify each task's data
                                for (int taskId = 0; taskId < taskCount; taskId++)
                                {
                                    using SqliteCommand taskCountCmd = new SqliteCommand(
                                        $"SELECT COUNT(*) FROM Products WHERE Category = 'Task{taskId}'", _connection);
                                    long taskRowCount = (long)(await taskCountCmd.ExecuteScalarAsync())!;
                                    taskRowCount.Should().Be(insertsPerTask);
                                }
                            }
                        }
                    }
