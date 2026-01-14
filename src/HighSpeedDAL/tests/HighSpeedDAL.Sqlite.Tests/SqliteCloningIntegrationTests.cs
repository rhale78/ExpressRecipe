using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using HighSpeedDAL.Core.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HighSpeedDAL.Sqlite.Tests
{
    // ============================================================================
    // TEST ENTITIES WITH CLONING SUPPORT
    // ============================================================================

    /// <summary>
    /// Manual test entity for isolated component testing.
    /// NOTE: This entity intentionally does NOT use framework attributes ([Table], [Cache])
    /// or source generation. It manually implements IEntityCloneable to test the cloning
    /// behavior in isolation without framework dependencies.
    /// 
    /// For examples of real-world framework usage with [Table] attributes and
    /// source-generated DAL classes, see HighSpeedDAL.FrameworkUsage.Tests project.
    /// </summary>
    public class SqliteTestProduct : IEntityCloneable<SqliteTestProduct>
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }

        public SqliteTestProduct ShallowClone()
        {
            return new SqliteTestProduct
            {
                Id = this.Id,
                Name = this.Name,
                Price = this.Price,
                IsActive = this.IsActive,
                CreatedDate = this.CreatedDate
            };
        }

            public SqliteTestProduct DeepClone() => ShallowClone();
        }

        /// <summary>
        /// Manual test entity for isolated component testing.
        /// NOTE: This entity intentionally does NOT use framework attributes or source generation.
        /// For real-world usage examples, see HighSpeedDAL.FrameworkUsage.Tests project.
        /// </summary>
        public class SqliteTestOrder : IEntityCloneable<SqliteTestOrder>
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }

        public SqliteTestOrder ShallowClone()
        {
            return new SqliteTestOrder
            {
                Id = this.Id,
                CustomerId = this.CustomerId,
                TotalAmount = this.TotalAmount,
                Status = this.Status,
                OrderDate = this.OrderDate
            };
        }

        public SqliteTestOrder DeepClone() => ShallowClone();
    }

    // ============================================================================
    // SQLITE CLONING INTEGRATION TESTS
    // ============================================================================

    /// <summary>
    /// Component tests for entity cloning behavior with SQLite.
    /// 
    /// PURPOSE: These tests validate the defensive cloning mechanism in isolation
    /// using SQLite in-memory database for fast, isolated testing.
    /// 
    /// APPROACH: Uses manual test entities (SqliteTestProduct, SqliteTestOrder)
    /// without framework attributes to isolate cloning logic from source generator
    /// and production database dependencies.
    /// 
    /// WHY NOT USE FRAMEWORK: Component isolation allows faster execution and
    /// focused testing of specific behaviors. SQLite in-memory provides fast,
    /// isolated database operations without external dependencies.
    /// 
    /// FOR FRAMEWORK USAGE EXAMPLES: See HighSpeedDAL.FrameworkUsage.Tests project
    /// which demonstrates real-world usage with [Table] attributes, partial classes,
    /// and source-generated DAL classes.
    /// </summary>
    public class SqliteCloningIntegrationTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly string _connectionString = "Data Source=:memory:";
        private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);

        public SqliteCloningIntegrationTests()
        {
            _connection = new SqliteConnection(_connectionString);
            _connection.Open();

            // Create test tables
            using SqliteCommand createProducts = _connection.CreateCommand();
            createProducts.CommandText = @"
                CREATE TABLE Products (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Price REAL NOT NULL,
                    IsActive INTEGER NOT NULL,
                    CreatedDate TEXT NOT NULL
                )";
            createProducts.ExecuteNonQuery();

            using SqliteCommand createOrders = _connection.CreateCommand();
            createOrders.CommandText = @"
                CREATE TABLE Orders (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CustomerId INTEGER NOT NULL,
                    TotalAmount REAL NOT NULL,
                    Status TEXT NOT NULL,
                    OrderDate TEXT NOT NULL
                )";
            createOrders.ExecuteNonQuery();

            // Insert test data
            InsertTestData();
        }

        private void InsertTestData()
        {
            using SqliteCommand insertProduct = _connection.CreateCommand();
            insertProduct.CommandText = @"
                INSERT INTO Products (Name, Price, IsActive, CreatedDate)
                VALUES ('Test Product 1', 99.99, 1, @createdDate)";
            insertProduct.Parameters.AddWithValue("@createdDate", DateTime.UtcNow.ToString("o"));
            insertProduct.ExecuteNonQuery();

            using SqliteCommand insertOrder = _connection.CreateCommand();
            insertOrder.CommandText = @"
                INSERT INTO Orders (CustomerId, TotalAmount, Status, OrderDate)
                VALUES (1, 199.99, 'Pending', @orderDate)";
            insertOrder.Parameters.AddWithValue("@orderDate", DateTime.UtcNow.ToString("o"));
            insertOrder.ExecuteNonQuery();
        }

        [Fact]
        public async Task GetByIdAsync_ReturnsCopiedEntity_MutationDoesNotAffectDatabase()
        {
            // Arrange
            SqliteTestProduct? product = await GetProductByIdAsync(1);
            decimal originalPrice = product!.Price;

            // Act - Mutate the returned entity
            product.Price = 9999.99m;
            product.Name = "MUTATED";

            // Get the same entity again
            SqliteTestProduct? productAgain = await GetProductByIdAsync(1);

            // Assert - Database should have original value
            productAgain.Should().NotBeNull();
            productAgain!.Price.Should().Be(originalPrice);
            productAgain.Name.Should().Be("Test Product 1");
        }

        [Fact]
        public async Task GetAllAsync_ReturnsCopiedEntities_MutationsIsolated()
        {
            // Arrange
            List<SqliteTestProduct> products = await GetAllProductsAsync();

            // Act - Mutate all returned entities
            foreach (SqliteTestProduct product in products)
            {
                product.Name = "MUTATED";
                product.Price = 0m;
            }

            // Get entities again
            List<SqliteTestProduct> productsAgain = await GetAllProductsAsync();

            // Assert - Database should have original values
            productsAgain.Should().HaveCount(products.Count);
            productsAgain.Should().OnlyContain(p => p.Name != "MUTATED" && p.Price > 0);
        }

        [Fact]
        public async Task UpdateAsync_AcceptsMutatedEntity_PersistsChanges()
        {
            // Arrange
            SqliteTestProduct? product = await GetProductByIdAsync(1);
            product.Should().NotBeNull();

            // Act - Mutate and update
            product!.Name = "Updated Product";
            product.Price = 149.99m;
            await UpdateProductAsync(product);

            // Get updated entity
            SqliteTestProduct? updated = await GetProductByIdAsync(1);

            // Assert
            updated.Should().NotBeNull();
            updated!.Name.Should().Be("Updated Product");
            updated.Price.Should().Be(149.99m);
        }

        [Fact]
        public async Task ConcurrentReads_ReturnIsolatedCopies()
        {
            // Arrange & Act
            List<Task<SqliteTestProduct?>> tasks = new List<Task<SqliteTestProduct?>>();
            for (int i = 0; i < 20; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    SqliteTestProduct? product = await GetProductByIdAsync(1);
                    product!.Name = $"Mutated-{Guid.NewGuid()}";
                    product.Price = Random.Shared.Next(1, 1000);
                    return product;
                }));
            }

            SqliteTestProduct?[] results = await Task.WhenAll(tasks);

            // Assert - Each task got its own isolated copy
            results.Should().OnlyContain(p => p != null);
            results.Should().OnlyContain(p => p!.Name.StartsWith("Mutated-"));

            // Database should still have original value
            SqliteTestProduct? original = await GetProductByIdAsync(1);
            original!.Name.Should().Be("Test Product 1");
        }

        [Fact]
        public async Task BulkOperations_MaintainIsolation()
        {
            // Arrange
            List<SqliteTestProduct> newProducts = new List<SqliteTestProduct>
            {
                new SqliteTestProduct { Name = "Bulk 1", Price = 10m, IsActive = true, CreatedDate = DateTime.UtcNow },
                new SqliteTestProduct { Name = "Bulk 2", Price = 20m, IsActive = true, CreatedDate = DateTime.UtcNow },
                new SqliteTestProduct { Name = "Bulk 3", Price = 30m, IsActive = true, CreatedDate = DateTime.UtcNow }
            };

            // Act - Insert bulk
            await BulkInsertProductsAsync(newProducts);

            // Mutate the original list
            foreach (SqliteTestProduct product in newProducts)
            {
                product.Name = "MUTATED";
                product.Price = 0m;
            }

            // Get inserted products
            List<SqliteTestProduct> inserted = await GetAllProductsAsync();

            // Assert - Database should have original values
            inserted.Should().Contain(p => p.Name == "Bulk 1" && p.Price == 10m);
            inserted.Should().Contain(p => p.Name == "Bulk 2" && p.Price == 20m);
            inserted.Should().Contain(p => p.Name == "Bulk 3" && p.Price == 30m);
        }

        [Fact]
        public void EntityClone_CreatesIndependentCopy()
        {
            // Arrange
            SqliteTestProduct original = new SqliteTestProduct
            {
                Id = 1,
                Name = "Original",
                Price = 100m,
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            };

            // Act
            SqliteTestProduct clone = original.ShallowClone();
            clone.Name = "Cloned";
            clone.Price = 200m;

            // Assert
            original.Name.Should().Be("Original");
            original.Price.Should().Be(100m);
            clone.Name.Should().Be("Cloned");
            clone.Price.Should().Be(200m);
        }

        [Fact]
        public void EntityDeepClone_CreatesIndependentCopy()
        {
            // Arrange
            SqliteTestOrder original = new SqliteTestOrder
            {
                Id = 1,
                CustomerId = 100,
                TotalAmount = 500m,
                Status = "Pending",
                OrderDate = DateTime.UtcNow
            };

            // Act
            SqliteTestOrder clone = original.DeepClone();
            clone.Status = "Completed";
            clone.TotalAmount = 600m;

            // Assert
            original.Status.Should().Be("Pending");
            original.TotalAmount.Should().Be(500m);
            clone.Status.Should().Be("Completed");
            clone.TotalAmount.Should().Be(600m);
        }

        // ============================================================================
        // HELPER METHODS (Simulating DAL operations with cloning)
        // ============================================================================

        private async Task<SqliteTestProduct?> GetProductByIdAsync(int id)
        {
            await _connectionLock.WaitAsync();
            try
            {
                using SqliteCommand cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT * FROM Products WHERE Id = @id";
                cmd.Parameters.AddWithValue("@id", id);

                using SqliteDataReader reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    SqliteTestProduct product = new SqliteTestProduct
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        Price = (decimal)reader.GetDouble(2),
                        IsActive = reader.GetInt32(3) == 1,
                        CreatedDate = DateTime.Parse(reader.GetString(4))
                    };
                    // Simulate defensive cloning (as DAL would do)
                    return product.ShallowClone();
                }

                return null;
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        private async Task<List<SqliteTestProduct>> GetAllProductsAsync()
        {
            List<SqliteTestProduct> products = new List<SqliteTestProduct>();

            using SqliteCommand cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM Products";

            using SqliteDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                SqliteTestProduct product = new SqliteTestProduct
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Price = (decimal)reader.GetDouble(2),
                    IsActive = reader.GetInt32(3) == 1,
                    CreatedDate = DateTime.Parse(reader.GetString(4))
                };
                // Simulate defensive cloning (as DAL would do)
                products.Add(product.ShallowClone());
            }

            return products;
        }

        private async Task UpdateProductAsync(SqliteTestProduct product)
        {
            // Simulate defensive cloning before persisting (as DAL would do)
            SqliteTestProduct toStore = product.ShallowClone();

            using SqliteCommand cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE Products 
                SET Name = @name, Price = @price, IsActive = @isActive
                WHERE Id = @id";

            cmd.Parameters.AddWithValue("@id", toStore.Id);
            cmd.Parameters.AddWithValue("@name", toStore.Name);
            cmd.Parameters.AddWithValue("@price", (double)toStore.Price);
            cmd.Parameters.AddWithValue("@isActive", toStore.IsActive ? 1 : 0);

            await cmd.ExecuteNonQueryAsync();
        }

        private async Task BulkInsertProductsAsync(List<SqliteTestProduct> products)
        {
            foreach (SqliteTestProduct product in products)
            {
                // Simulate defensive cloning before persisting
                SqliteTestProduct toStore = product.ShallowClone();

                using SqliteCommand cmd = _connection.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO Products (Name, Price, IsActive, CreatedDate)
                    VALUES (@name, @price, @isActive, @createdDate)";

                cmd.Parameters.AddWithValue("@name", toStore.Name);
                cmd.Parameters.AddWithValue("@price", (double)toStore.Price);
                cmd.Parameters.AddWithValue("@isActive", toStore.IsActive ? 1 : 0);
                cmd.Parameters.AddWithValue("@createdDate", toStore.CreatedDate.ToString("o"));

                await cmd.ExecuteNonQueryAsync();
            }
        }

            public void Dispose()
            {
                _connectionLock?.Dispose();
                _connection?.Dispose();
            }
        }

    // ============================================================================
    // SQLITE CONNECTION FACTORY TESTS WITH CLONING
    // ============================================================================

    public class SqliteAdditionalIntegrationTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);

        public SqliteAdditionalIntegrationTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            using SqliteCommand createProducts = _connection.CreateCommand();
            createProducts.CommandText = @"
                CREATE TABLE Products (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Price REAL NOT NULL,
                    IsActive INTEGER NOT NULL,
                    CreatedDate TEXT NOT NULL
                )";
            createProducts.ExecuteNonQuery();

            // Insert test data
            InsertTestData();
        }

        private void InsertTestData()
        {
            for (int i = 1; i <= 10; i++)
            {
                using SqliteCommand cmd = _connection.CreateCommand();
                cmd.CommandText = "INSERT INTO Products (Name, Price, IsActive, CreatedDate) VALUES (@name, @price, @active, @date)";
                cmd.Parameters.AddWithValue("@name", $"Product {i}");
                cmd.Parameters.AddWithValue("@price", i * 10.0);
                cmd.Parameters.AddWithValue("@active", i % 2 == 0 ? 1 : 0);
                cmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("O"));
                cmd.ExecuteNonQuery();
            }
        }

        // ====================================================================
        // TRANSACTION TESTS
        // ====================================================================

        [Fact]
        public async Task Transaction_Commit_DataPersistsWithCloning()
        {
            await _connectionLock.WaitAsync();
            try
            {
                // Arrange
                using SqliteTransaction transaction = _connection.BeginTransaction();

                // Act - Insert within transaction
                using SqliteCommand cmd = _connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "INSERT INTO Products (Name, Price, IsActive, CreatedDate) VALUES (@name, @price, @active, @date)";
                cmd.Parameters.AddWithValue("@name", "Transaction Product");
                cmd.Parameters.AddWithValue("@price", 999.99);
                cmd.Parameters.AddWithValue("@active", 1);
                cmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("O"));
                await cmd.ExecuteNonQueryAsync();

                transaction.Commit();

                // Assert - Data should be persisted
                SqliteTestProduct? product = await GetProductByNameAsync("Transaction Product");
                product.Should().NotBeNull();
                product!.Name.Should().Be("Transaction Product");
                product.Price.Should().Be(999.99m);
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        [Fact]
        public async Task Transaction_Rollback_DataNotPersisted()
        {
            await _connectionLock.WaitAsync();
            try
            {
                // Arrange
                using SqliteTransaction transaction = _connection.BeginTransaction();

                // Act - Insert within transaction
                using SqliteCommand cmd = _connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "INSERT INTO Products (Name, Price, IsActive, CreatedDate) VALUES (@name, @price, @active, @date)";
                cmd.Parameters.AddWithValue("@name", "Rollback Product");
                cmd.Parameters.AddWithValue("@price", 888.88);
                cmd.Parameters.AddWithValue("@active", 1);
                cmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("O"));
                await cmd.ExecuteNonQueryAsync();

                transaction.Rollback();

                // Assert - Data should not be persisted
                SqliteTestProduct? product = await GetProductByNameAsync("Rollback Product");
                product.Should().BeNull();
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        // ====================================================================
        // NULL HANDLING TESTS
        // ====================================================================

        [Fact]
        public async Task GetByIdAsync_NonExistentId_ReturnsNull()
        {
            await _connectionLock.WaitAsync();
            try
            {
                // Act
                SqliteTestProduct? product = await GetProductByIdAsync(99999);

                // Assert
                product.Should().BeNull();
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        [Fact]
        public async Task GetAllAsync_EmptyTable_ReturnsEmptyList()
        {
            await _connectionLock.WaitAsync();
            try
            {
                // Arrange - Delete all products
                using SqliteCommand deleteCmd = _connection.CreateCommand();
                deleteCmd.CommandText = "DELETE FROM Products";
                await deleteCmd.ExecuteNonQueryAsync();

                // Act
                List<SqliteTestProduct> products = await GetAllProductsAsync();

                // Assert
                products.Should().BeEmpty();
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        // ====================================================================
        // ERROR HANDLING TESTS
        // ====================================================================

        [Fact]
        public async Task UpdateAsync_NonExistentId_NoErrorThrown()
        {            await _connectionLock.WaitAsync();
            try
            {
                // Act & Assert - Should not throw
                SqliteTestProduct nonExistent = new SqliteTestProduct
                {
                    Id = 99999,
                    Name = "Non-existent",
                    Price = 100m,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };

                Func<Task> act = async () => await UpdateProductAsync(nonExistent);
                await act.Should().NotThrowAsync();
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        // ====================================================================
        // FILTERING TESTS
        // ====================================================================

        [Fact]
        public async Task GetFilteredAsync_ActiveOnly_ReturnsCorrectResults()
        {
            await _connectionLock.WaitAsync();
            try
            {
                // Act
                List<SqliteTestProduct> activeProducts = await GetProductsByActiveStatusAsync(true);

                // Assert - Only even-numbered products are active (from InsertTestData)
                activeProducts.Should().HaveCountGreaterThan(0);
                activeProducts.Should().OnlyContain(p => p.IsActive);
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        [Fact]
        public async Task GetFilteredAsync_InactiveOnly_ReturnsCorrectResults()
        {
            await _connectionLock.WaitAsync();
            try
            {
                // Act
                List<SqliteTestProduct> inactiveProducts = await GetProductsByActiveStatusAsync(false);

                // Assert - Only odd-numbered products are inactive
                inactiveProducts.Should().HaveCountGreaterThan(0);
                inactiveProducts.Should().OnlyContain(p => !p.IsActive);
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        // ====================================================================
        // ORDERING TESTS
        // ====================================================================

        [Fact]
        public async Task GetAllAsync_OrderedByPrice_ReturnsCorrectOrder()
        {
            await _connectionLock.WaitAsync();
            try
            {
                // Act
                List<SqliteTestProduct> products = await GetAllProductsOrderedByPriceAsync();

                // Assert
                products.Should().HaveCountGreaterThan(0);
                for (int i = 1; i < products.Count; i++)
                {
                    products[i].Price.Should().BeGreaterThanOrEqualTo(products[i - 1].Price);
                }
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        // ====================================================================
        // PERFORMANCE TESTS
        // ====================================================================

        [Fact]
        public async Task BulkRead_LargeResultSet_CompletesEfficiently()
        {
            await _connectionLock.WaitAsync();
            try
            {
                // Arrange - Insert 1000 products
                for (int i = 11; i <= 1000; i++)
                {
                    using SqliteCommand cmd = _connection.CreateCommand();
                    cmd.CommandText = "INSERT INTO Products (Name, Price, IsActive, CreatedDate) VALUES (@name, @price, @active, @date)";
                    cmd.Parameters.AddWithValue("@name", $"Bulk Product {i}");
                    cmd.Parameters.AddWithValue("@price", i * 5.0);
                    cmd.Parameters.AddWithValue("@active", 1);
                    cmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("O"));
                    await cmd.ExecuteNonQueryAsync();
                }

                // Act
                System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                List<SqliteTestProduct> products = await GetAllProductsAsync();
                sw.Stop();

                // Assert
                products.Should().HaveCount(1000);
                sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        // ====================================================================
        // HELPER METHODS
        // ====================================================================

        private async Task<SqliteTestProduct?> GetProductByIdAsync(int id)
        {
            using SqliteCommand cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM Products WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);

            using SqliteDataReader reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                SqliteTestProduct product = new SqliteTestProduct
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Price = (decimal)reader.GetDouble(2),
                    IsActive = reader.GetInt32(3) == 1,
                    CreatedDate = DateTime.Parse(reader.GetString(4))
                };
                return product.ShallowClone();
            }
            return null;
        }

        private async Task<SqliteTestProduct?> GetProductByNameAsync(string name)
        {
            using SqliteCommand cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM Products WHERE Name = @name";
            cmd.Parameters.AddWithValue("@name", name);

            using SqliteDataReader reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                SqliteTestProduct product = new SqliteTestProduct
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Price = (decimal)reader.GetDouble(2),
                    IsActive = reader.GetInt32(3) == 1,
                    CreatedDate = DateTime.Parse(reader.GetString(4))
                };
                return product.ShallowClone();
            }
            return null;
        }

        private async Task<List<SqliteTestProduct>> GetAllProductsAsync()
        {
            List<SqliteTestProduct> products = new List<SqliteTestProduct>();
            using SqliteCommand cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM Products";

            using SqliteDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                SqliteTestProduct product = new SqliteTestProduct
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Price = (decimal)reader.GetDouble(2),
                    IsActive = reader.GetInt32(3) == 1,
                    CreatedDate = DateTime.Parse(reader.GetString(4))
                };
                products.Add(product.ShallowClone());
            }
            return products;
        }

        private async Task<List<SqliteTestProduct>> GetAllProductsOrderedByPriceAsync()
        {
            List<SqliteTestProduct> products = new List<SqliteTestProduct>();
            using SqliteCommand cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM Products ORDER BY Price ASC";

            using SqliteDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                SqliteTestProduct product = new SqliteTestProduct
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Price = (decimal)reader.GetDouble(2),
                    IsActive = reader.GetInt32(3) == 1,
                    CreatedDate = DateTime.Parse(reader.GetString(4))
                };
                products.Add(product.ShallowClone());
            }
            return products;
        }

        private async Task<List<SqliteTestProduct>> GetProductsByActiveStatusAsync(bool isActive)
        {
            List<SqliteTestProduct> products = new List<SqliteTestProduct>();
            using SqliteCommand cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM Products WHERE IsActive = @active";
            cmd.Parameters.AddWithValue("@active", isActive ? 1 : 0);

            using SqliteDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                SqliteTestProduct product = new SqliteTestProduct
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Price = (decimal)reader.GetDouble(2),
                    IsActive = reader.GetInt32(3) == 1,
                    CreatedDate = DateTime.Parse(reader.GetString(4))
                };
                products.Add(product.ShallowClone());
            }
            return products;
        }

        private async Task UpdateProductAsync(SqliteTestProduct product)
        {
            using SqliteCommand cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE Products 
                SET Name = @name, Price = @price, IsActive = @active, CreatedDate = @date 
                WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", product.Id);
            cmd.Parameters.AddWithValue("@name", product.Name);
            cmd.Parameters.AddWithValue("@price", (double)product.Price);
            cmd.Parameters.AddWithValue("@active", product.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@date", product.CreatedDate.ToString("O"));
            await cmd.ExecuteNonQueryAsync();
        }

        public void Dispose()
        {
            _connectionLock?.Dispose();
            _connection?.Dispose();
        }
    }

    // ============================================================================
    // SQLITE CONNECTION FACTORY TESTS WITH CLONING
    // ============================================================================

    public class SqliteConnectionFactoryCloningTests
    {
        [Fact]
        public void ConnectionFactory_CreatesIndependentConnections()
        {
            // Arrange
            SqliteConnectionFactory factory = new SqliteConnectionFactory("Data Source=:memory:");

            // Act
            using SqliteConnection conn1 = factory.CreateConnection();
            using SqliteConnection conn2 = factory.CreateConnection();

            conn1.Open();
            conn2.Open();

            // Assert
            conn1.Should().NotBeSameAs(conn2);
            conn1.State.Should().Be(System.Data.ConnectionState.Open);
            conn2.State.Should().Be(System.Data.ConnectionState.Open);
        }

            [Fact]
            public void ConnectionFactory_ConnectionStringPreserved()
            {
                // Arrange
                string originalConnectionString = "Data Source=:memory:;Cache=Shared";
                SqliteConnectionFactory factory = new SqliteConnectionFactory(originalConnectionString);

                // Act
                string factoryConnectionString = factory.GetConnectionString();

                // Assert - Connection string is preserved correctly
                factoryConnectionString.Should().Be(originalConnectionString);
            }
        }

    // Simple connection factory for tests
    public class SqliteConnectionFactory
    {
        private readonly string _connectionString;

        public SqliteConnectionFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        public SqliteConnection CreateConnection()
        {
            return new SqliteConnection(_connectionString);
        }

        public string GetConnectionString()
        {
            return _connectionString;
        }
    }
}
