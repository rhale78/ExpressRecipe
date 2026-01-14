using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using FluentAssertions;
using HighSpeedDAL.Core.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HighSpeedDAL.SqlServer.Tests
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
    public class SqlServerTestProduct : IEntityCloneable<SqlServerTestProduct>
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }

        public SqlServerTestProduct ShallowClone()
        {
            return new SqlServerTestProduct
            {
                Id = this.Id,
                Name = this.Name,
                Price = this.Price,
                Stock = this.Stock,
                CreatedDate = this.CreatedDate,
                ModifiedDate = this.ModifiedDate
            };
        }

            public SqlServerTestProduct DeepClone() => ShallowClone();
        }

        /// <summary>
        /// Manual test entity for isolated component testing.
        /// NOTE: This entity intentionally does NOT use framework attributes or source generation.
        /// For real-world usage examples, see HighSpeedDAL.FrameworkUsage.Tests project.
        /// </summary>
        public class SqlServerTestCustomer : IEntityCloneable<SqlServerTestCustomer>
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime JoinedDate { get; set; }
        public bool IsActive { get; set; }

        public SqlServerTestCustomer ShallowClone()
        {
            return new SqlServerTestCustomer
            {
                Id = this.Id,
                FirstName = this.FirstName,
                LastName = this.LastName,
                Email = this.Email,
                JoinedDate = this.JoinedDate,
                IsActive = this.IsActive
            };
        }

        public SqlServerTestCustomer DeepClone() => ShallowClone();
    }

    // ============================================================================
    // SQL SERVER CLONING UNIT TESTS (Mock-based)
    // ============================================================================

    /// <summary>
    /// Component tests for entity cloning behavior.
    /// 
    /// PURPOSE: These tests validate the defensive cloning mechanism in isolation
    /// using mock-based unit testing. They ensure that entities returned from cache
    /// are properly cloned to prevent mutation of cached objects.
    /// 
    /// APPROACH: Uses manual test entities (SqlServerTestProduct, SqlServerTestCustomer)
    /// without framework attributes to isolate cloning logic from source generator
    /// and database dependencies.
    /// 
    /// WHY NOT USE FRAMEWORK: Component isolation allows faster execution and
    /// focused testing of specific behaviors without the overhead of source
    /// generation and database operations.
    /// 
    /// FOR FRAMEWORK USAGE EXAMPLES: See HighSpeedDAL.FrameworkUsage.Tests project
    /// which demonstrates real-world usage with [Table] attributes, partial classes,
    /// and source-generated DAL classes.
    /// </summary>
    public class SqlServerCloningUnitTests
    {
        [Fact]
        public void EntityShallowClone_CreatesIndependentCopy()
        {
            // Arrange
            SqlServerTestProduct original = new SqlServerTestProduct
            {
                Id = 1,
                Name = "Original Product",
                Price = 99.99m,
                Stock = 100,
                CreatedDate = DateTime.UtcNow,
                ModifiedDate = null
            };

            // Act
            SqlServerTestProduct clone = original.ShallowClone();
            clone.Name = "Cloned Product";
            clone.Price = 199.99m;
            clone.Stock = 50;

            // Assert
            original.Name.Should().Be("Original Product");
            original.Price.Should().Be(99.99m);
            original.Stock.Should().Be(100);

            clone.Name.Should().Be("Cloned Product");
            clone.Price.Should().Be(199.99m);
            clone.Stock.Should().Be(50);
        }

        [Fact]
        public void EntityDeepClone_CreatesIndependentCopy()
        {
            // Arrange
            SqlServerTestCustomer original = new SqlServerTestCustomer
            {
                Id = 1,
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe@example.com",
                JoinedDate = DateTime.UtcNow,
                IsActive = true
            };

            // Act
            SqlServerTestCustomer clone = original.DeepClone();
            clone.FirstName = "Jane";
            clone.Email = "jane.doe@example.com";
            clone.IsActive = false;

            // Assert
            original.FirstName.Should().Be("John");
            original.Email.Should().Be("john.doe@example.com");
            original.IsActive.Should().BeTrue();

            clone.FirstName.Should().Be("Jane");
            clone.Email.Should().Be("jane.doe@example.com");
            clone.IsActive.Should().BeFalse();
        }

        [Fact]
        public void CloningPreservesNullableValues()
        {
            // Arrange
            SqlServerTestProduct original = new SqlServerTestProduct
            {
                Id = 1,
                Name = "Test",
                Price = 10m,
                Stock = 5,
                CreatedDate = DateTime.UtcNow,
                ModifiedDate = null // Nullable not set
            };

            // Act
            SqlServerTestProduct clone = original.ShallowClone();
            clone.ModifiedDate = DateTime.UtcNow;

            // Assert
            original.ModifiedDate.Should().BeNull();
            clone.ModifiedDate.Should().NotBeNull();
        }

        [Fact]
        public void MultipleClonesAreIndependent()
        {
            // Arrange
            SqlServerTestProduct original = new SqlServerTestProduct
            {
                Id = 1,
                Name = "Original",
                Price = 100m,
                Stock = 10,
                CreatedDate = DateTime.UtcNow
            };

            // Act
            SqlServerTestProduct clone1 = original.ShallowClone();
            SqlServerTestProduct clone2 = original.ShallowClone();
            SqlServerTestProduct clone3 = original.ShallowClone();

            clone1.Name = "Clone 1";
            clone2.Name = "Clone 2";
            clone3.Name = "Clone 3";

            // Assert
            original.Name.Should().Be("Original");
            clone1.Name.Should().Be("Clone 1");
            clone2.Name.Should().Be("Clone 2");
            clone3.Name.Should().Be("Clone 3");
        }

        [Fact]
        public async Task SimulatedDalOperation_ReturnsCopiedEntity()
        {
            // Arrange
            Dictionary<int, SqlServerTestProduct> mockDatabase = new Dictionary<int, SqlServerTestProduct>
            {
                [1] = new SqlServerTestProduct { Id = 1, Name = "Product 1", Price = 50m, Stock = 100, CreatedDate = DateTime.UtcNow }
            };

            // Simulate DAL GetById operation with defensive cloning
            Func<int, Task<SqlServerTestProduct?>> getById = async (id) =>
            {
                await Task.Delay(1); // Simulate async DB call
                if (mockDatabase.TryGetValue(id, out SqlServerTestProduct? product))
                {
                    return product.ShallowClone(); // Defensive copy
                }
                return null;
            };

            // Act
            SqlServerTestProduct? result = await getById(1);
            result!.Name = "MUTATED";
            result.Price = 999m;

            SqlServerTestProduct? result2 = await getById(1);

            // Assert
            result2.Should().NotBeNull();
            result2!.Name.Should().Be("Product 1");
            result2.Price.Should().Be(50m);
        }

        [Fact]
        public async Task SimulatedDalUpdate_AcceptsMutatedEntity()
        {
            // Arrange
            Dictionary<int, SqlServerTestProduct> mockDatabase = new Dictionary<int, SqlServerTestProduct>
            {
                [1] = new SqlServerTestProduct { Id = 1, Name = "Original", Price = 100m, Stock = 50, CreatedDate = DateTime.UtcNow }
            };

            // Simulate DAL Update operation with defensive cloning
            Func<SqlServerTestProduct, Task> update = async (product) =>
            {
                await Task.Delay(1); // Simulate async DB call
                // Store a clone to prevent external mutations
                mockDatabase[product.Id] = product.ShallowClone();
            };

            SqlServerTestProduct retrieved = mockDatabase[1].ShallowClone();

            // Act
            retrieved.Name = "Updated";
            retrieved.Price = 150m;
            await update(retrieved);

            // Mutate after update
            retrieved.Name = "MUTATED AFTER UPDATE";

            // Assert
            mockDatabase[1].Name.Should().Be("Updated");
            mockDatabase[1].Price.Should().Be(150m);
        }

        [Fact]
        public async Task ConcurrentDalOperations_MaintainIsolation()
        {
            // Arrange
            Dictionary<int, SqlServerTestProduct> mockDatabase = new Dictionary<int, SqlServerTestProduct>
            {
                [1] = new SqlServerTestProduct { Id = 1, Name = "Shared Product", Price = 100m, Stock = 50, CreatedDate = DateTime.UtcNow }
            };

            Func<int, Task<SqlServerTestProduct?>> getById = async (id) =>
            {
                await Task.Delay(Random.Shared.Next(1, 10));
                if (mockDatabase.TryGetValue(id, out SqlServerTestProduct? product))
                {
                    return product.ShallowClone();
                }
                return null;
            };

            // Act - Concurrent reads and mutations
            List<Task<SqlServerTestProduct?>> tasks = new List<Task<SqlServerTestProduct?>>();
            for (int i = 0; i < 50; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    SqlServerTestProduct? product = await getById(1);
                    product!.Name = $"Thread-{Guid.NewGuid()}";
                    product.Price = Random.Shared.Next(1, 1000);
                    return product;
                }));
            }

            SqlServerTestProduct?[] results = await Task.WhenAll(tasks);

            // Assert
            results.Should().OnlyContain(p => p != null);
            results.Should().OnlyContain(p => p!.Name.StartsWith("Thread-"));
            mockDatabase[1].Name.Should().Be("Shared Product"); // Original unchanged
            mockDatabase[1].Price.Should().Be(100m);
        }
    }

    // ============================================================================
    // SQL SERVER CONNECTION FACTORY TESTS
    // ============================================================================

    public class SqlServerConnectionFactoryCloningTests
    {
        private const string TestConnectionString = "Server=localhost;Database=TestDB;Integrated Security=true;TrustServerCertificate=true";

        [Fact]
        public void ConnectionFactory_PreservesConnectionString()
        {
            // Arrange & Act
            SqlServerConnectionFactory factory = new SqlServerConnectionFactory(TestConnectionString);
            string connectionString = factory.GetConnectionString();

            // Assert
            connectionString.Should().Be(TestConnectionString);
        }

        [Fact]
        public void ConnectionFactory_CreatesIndependentConnections()
        {
            // Arrange
            SqlServerConnectionFactory factory = new SqlServerConnectionFactory(TestConnectionString);

            // Act
            using SqlConnection conn1 = factory.CreateConnection();
            using SqlConnection conn2 = factory.CreateConnection();

            // Assert
            conn1.Should().NotBeSameAs(conn2);
            conn1.ConnectionString.Should().Be(conn2.ConnectionString);
        }

        [Fact]
        public void ConnectionFactory_ConnectionStringNotSharedReference()
        {
            // Arrange
            string originalConnectionString = TestConnectionString;
            SqlServerConnectionFactory factory = new SqlServerConnectionFactory(originalConnectionString);

            // Act
            string factoryConnectionString = factory.GetConnectionString();

            // Assert - String immutability ensures this, but verify pattern
            factoryConnectionString.Should().Be(originalConnectionString);
        }
    }

    // ============================================================================
    // SQL SERVER TRANSACTION ISOLATION TESTS
    // ============================================================================

    public class SqlServerTransactionCloningTests
    {
        [Fact]
        public async Task TransactionalUpdate_MaintainsCloningIsolation()
        {
            // Arrange
            Dictionary<int, SqlServerTestProduct> mockDatabase = new Dictionary<int, SqlServerTestProduct>
            {
                [1] = new SqlServerTestProduct { Id = 1, Name = "Original", Price = 100m, Stock = 50, CreatedDate = DateTime.UtcNow }
            };

            bool transactionCommitted = false;

            Func<SqlServerTestProduct, Task<bool>> transactionalUpdate = async (product) =>
            {
                await Task.Delay(10); // Simulate DB work
                try
                {
                    // Simulate transaction: clone before committing
                    SqlServerTestProduct toStore = product.ShallowClone();
                    mockDatabase[product.Id] = toStore;
                    transactionCommitted = true;
                    return true;
                }
                catch
                {
                    transactionCommitted = false;
                    return false;
                }
            };

            SqlServerTestProduct product = mockDatabase[1].ShallowClone();

            // Act
            product.Name = "Updated in Transaction";
            product.Price = 150m;
            bool success = await transactionalUpdate(product);

            // Mutate after transaction
            product.Name = "MUTATED AFTER COMMIT";
            product.Price = 999m;

            // Assert
            success.Should().BeTrue();
            transactionCommitted.Should().BeTrue();
            mockDatabase[1].Name.Should().Be("Updated in Transaction");
            mockDatabase[1].Price.Should().Be(150m);
        }

        [Fact]
        public async Task RollbackScenario_DoesNotPersistMutations()
        {
            // Arrange
            Dictionary<int, SqlServerTestProduct> mockDatabase = new Dictionary<int, SqlServerTestProduct>
                    {
                        [1] = new SqlServerTestProduct { Id = 1, Name = "Original", Price = 100m, Stock = 50, CreatedDate = DateTime.UtcNow }
                    };

                    SqlServerTestProduct snapshot = mockDatabase[1].ShallowClone();

                    Func<SqlServerTestProduct, Task<bool>> transactionalUpdateWithRollback = async (product) =>
                    {
                        await Task.Delay(10);
                        // Simulate transaction failure - don't update database
                        return false;
                    };

                    SqlServerTestProduct product = mockDatabase[1].ShallowClone();

                    // Act
                    product.Name = "Updated";
                    product.Price = 150m;
                    bool success = await transactionalUpdateWithRollback(product);

                    // Assert - Database should remain unchanged
                    success.Should().BeFalse();
                    mockDatabase[1].Name.Should().Be(snapshot.Name);
                    mockDatabase[1].Price.Should().Be(snapshot.Price);
                }
            }

            // ============================================================================
            // ADDITIONAL SQL SERVER CLONING TESTS
            // ============================================================================

            public class SqlServerAdvancedCloningTests
            {
                // ====================================================================
                // BULK OPERATION TESTS
                // ====================================================================

                [Fact]
                public async Task BulkInsert_MultipleEntities_AllClonedIndependently()
                {
                    // Arrange
                    Dictionary<int, SqlServerTestProduct> mockDatabase = new Dictionary<int, SqlServerTestProduct>();
                    int nextId = 1;

                    Func<List<SqlServerTestProduct>, Task<bool>> bulkInsert = async (products) =>
                    {
                        await Task.Delay(10);
                        foreach (SqlServerTestProduct product in products)
                        {
                            mockDatabase[nextId] = product.ShallowClone();
                            product.Id = nextId++; // Simulate auto-increment
                        }
                        return true;
                    };

                    List<SqlServerTestProduct> products = new List<SqlServerTestProduct>();
                    for (int i = 0; i < 100; i++)
                    {
                        products.Add(new SqlServerTestProduct
                        {
                            Name = $"Bulk Product {i}",
                            Price = i * 10m,
                            Stock = i * 5,
                            CreatedDate = DateTime.UtcNow
                        });
                    }

                    // Act
                    await bulkInsert(products);

                    // Mutate source products
                    foreach (SqlServerTestProduct product in products)
                    {
                        product.Name = "MUTATED";
                        product.Price = 9999m;
                    }

                    // Assert - Database should have original values
                    mockDatabase.Should().HaveCount(100);
                    for (int i = 1; i <= 100; i++)
                    {
                        mockDatabase[i].Name.Should().Be($"Bulk Product {i - 1}");
                        mockDatabase[i].Price.Should().Be((i - 1) * 10m);
                    }
                }

                [Fact]
                public async Task BulkUpdate_MultipleEntities_MaintainsIsolation()
                {
                    // Arrange
                    Dictionary<int, SqlServerTestProduct> mockDatabase = new Dictionary<int, SqlServerTestProduct>();
                    for (int i = 1; i <= 50; i++)
                    {
                        mockDatabase[i] = new SqlServerTestProduct
                        {
                            Id = i,
                            Name = $"Original {i}",
                            Price = i * 10m,
                            Stock = i * 5,
                            CreatedDate = DateTime.UtcNow
                        };
                    }

                    Func<List<SqlServerTestProduct>, Task<int>> bulkUpdate = async (products) =>
                    {
                        await Task.Delay(10);
                        int updatedCount = 0;
                        foreach (SqlServerTestProduct product in products)
                        {
                            if (mockDatabase.ContainsKey(product.Id))
                            {
                                mockDatabase[product.Id] = product.ShallowClone();
                                updatedCount++;
                            }
                        }
                        return updatedCount;
                    };

                    List<SqlServerTestProduct> updates = new List<SqlServerTestProduct>();
                    for (int i = 1; i <= 50; i++)
                    {
                        updates.Add(new SqlServerTestProduct
                        {
                            Id = i,
                            Name = $"Updated {i}",
                            Price = i * 20m,
                            Stock = i * 10,
                            CreatedDate = DateTime.UtcNow,
                            ModifiedDate = DateTime.UtcNow
                        });
                    }

                    // Act
                    int count = await bulkUpdate(updates);

                    // Mutate source updates
                    foreach (SqlServerTestProduct update in updates)
                    {
                        update.Name = "MUTATED";
                    }

                    // Assert
                    count.Should().Be(50);
                    for (int i = 1; i <= 50; i++)
                    {
                        mockDatabase[i].Name.Should().Be($"Updated {i}");
                        mockDatabase[i].Price.Should().Be(i * 20m);
                        mockDatabase[i].ModifiedDate.Should().NotBeNull();
                    }
                }

                [Fact]
                public async Task BulkDelete_ByIds_MaintainsReferenceIsolation()
                {
                    // Arrange
                    Dictionary<int, SqlServerTestProduct> mockDatabase = new Dictionary<int, SqlServerTestProduct>();
                    for (int i = 1; i <= 100; i++)
                    {
                        mockDatabase[i] = new SqlServerTestProduct { Id = i, Name = $"Product {i}", Price = i * 10m };
                    }

                    Func<List<int>, Task<int>> bulkDelete = async (ids) =>
                    {
                        await Task.Delay(10);
                        int deletedCount = 0;
                        foreach (int id in ids)
                        {
                            if (mockDatabase.Remove(id))
                            {
                                deletedCount++;
                            }
                        }
                        return deletedCount;
                    };

                    List<int> idsToDelete = new List<int> { 1, 5, 10, 15, 20, 25, 30 };

                    // Act
                    int deletedCount = await bulkDelete(idsToDelete);

                    // Assert
                    deletedCount.Should().Be(7);
                    mockDatabase.Should().HaveCount(93);
                    foreach (int id in idsToDelete)
                    {
                        mockDatabase.Should().NotContainKey(id);
                    }
                }

                // ====================================================================
                // STORED PROCEDURE SIMULATION TESTS
                // ====================================================================

                [Fact]
                public async Task StoredProcedure_OutputParameters_CloningPreservesValues()
                {
                    // Arrange
                    Func<SqlServerTestProduct, Task<(SqlServerTestProduct Product, int GeneratedId)>> insertWithOutputId = async (product) =>
                    {
                        await Task.Delay(5);
                        int generatedId = 42;
                        SqlServerTestProduct storedProduct = product.ShallowClone();
                        storedProduct.Id = generatedId;
                        return (storedProduct, generatedId);
                    };

                    SqlServerTestProduct product = new SqlServerTestProduct
                    {
                        Name = "New Product",
                        Price = 100m,
                        Stock = 50,
                        CreatedDate = DateTime.UtcNow
                    };

                    // Act
                    (SqlServerTestProduct stored, int id) = await insertWithOutputId(product);
                    product.Name = "MUTATED";

                    // Assert
                    stored.Name.Should().Be("New Product");
                    stored.Id.Should().Be(42);
                    id.Should().Be(42);
                }

                [Fact]
                public async Task StoredProcedure_MultipleResultSets_AllCloned()
                {
                    // Arrange - Simulates stored procedure returning multiple result sets
                    Func<Task<(List<SqlServerTestProduct> Products, List<SqlServerTestCustomer> Customers)>> getMultipleResultSets = async () =>
                    {
                        await Task.Delay(10);

                        List<SqlServerTestProduct> products = new List<SqlServerTestProduct>
                        {
                            new SqlServerTestProduct { Id = 1, Name = "Product 1", Price = 100m },
                            new SqlServerTestProduct { Id = 2, Name = "Product 2", Price = 200m }
                        };

                        List<SqlServerTestCustomer> customers = new List<SqlServerTestCustomer>
                        {
                            new SqlServerTestCustomer { Id = 1, FirstName = "John", LastName = "Doe", Email = "john@example.com" },
                            new SqlServerTestCustomer { Id = 2, FirstName = "Jane", LastName = "Smith", Email = "jane@example.com" }
                        };

                        // Clone all results
                        return (
                            products.ConvertAll(p => p.ShallowClone()),
                            customers.ConvertAll(c => c.ShallowClone())
                        );
                    };

                    // Act
                    (List<SqlServerTestProduct> products, List<SqlServerTestCustomer> customers) = await getMultipleResultSets();

                    // Mutate
                    products[0].Name = "MUTATED";
                    customers[0].FirstName = "MUTATED";

                    // Get again
                    (List<SqlServerTestProduct> products2, List<SqlServerTestCustomer> customers2) = await getMultipleResultSets();

                    // Assert
                    products2[0].Name.Should().Be("Product 1");
                    customers2[0].FirstName.Should().Be("John");
                }

                // ====================================================================
                // ERROR SCENARIO TESTS
                // ====================================================================

                [Fact]
                public async Task DalOperation_DatabaseTimeout_NoCorruption()
                {
                    // Arrange
                    Dictionary<int, SqlServerTestProduct> mockDatabase = new Dictionary<int, SqlServerTestProduct>
                    {
                        [1] = new SqlServerTestProduct { Id = 1, Name = "Original", Price = 100m }
                    };

                    int attemptCount = 0;
                    Func<int, Task<SqlServerTestProduct?>> getByIdWithRetry = async (id) =>
                    {
                        attemptCount++;
                        if (attemptCount <= 2)
                        {
                            await Task.Delay(5);
                            throw new InvalidOperationException("Simulated timeout");
                        }

                        return mockDatabase.TryGetValue(id, out SqlServerTestProduct? p) ? p.ShallowClone() : null;
                    };

                    // Act & Assert - First two attempts timeout
                    await Assert.ThrowsAsync<InvalidOperationException>(() => getByIdWithRetry(1));
                    await Assert.ThrowsAsync<InvalidOperationException>(() => getByIdWithRetry(1));

                    // Third attempt succeeds with clone
                    SqlServerTestProduct? result = await getByIdWithRetry(1);
                    result!.Name = "MUTATED";

                    // Original database unchanged
                    mockDatabase[1].Name.Should().Be("Original");
                }

                [Fact]
                public async Task DalOperation_ConnectionFailure_StatePreserved()
                {
                    // Arrange
                    Dictionary<int, SqlServerTestProduct> mockDatabase = new Dictionary<int, SqlServerTestProduct>();

                    Func<SqlServerTestProduct, Task<bool>> saveWithConnectionCheck = async (product) =>
                    {
                        // Simulate connection failure
                        await Task.Delay(5);
                        throw new InvalidOperationException("Connection is closed");
                    };

                    SqlServerTestProduct product = new SqlServerTestProduct
                    {
                        Name = "Test Product",
                        Price = 100m,
                        Stock = 50,
                        CreatedDate = DateTime.UtcNow
                    };

                    // Act & Assert
                    await Assert.ThrowsAsync<InvalidOperationException>(() => saveWithConnectionCheck(product));

                    // Product state preserved
                    product.Name.Should().Be("Test Product");
                    product.Price.Should().Be(100m);
                    mockDatabase.Should().BeEmpty();
                }

                // ====================================================================
                // COMPLEX QUERY TESTS
                // ====================================================================

                [Fact]
                public async Task ComplexQuery_JoinWithAggregates_ResultsCloned()
                {
                    // Arrange - Simulates complex query with JOINs and aggregates
                    Func<Task<List<(SqlServerTestProduct Product, int TotalOrders, decimal TotalRevenue)>>> getProductSummary = async () =>
                    {
                        await Task.Delay(10);

                        List<(SqlServerTestProduct, int, decimal)> results = new List<(SqlServerTestProduct, int, decimal)>
                        {
                            (new SqlServerTestProduct { Id = 1, Name = "Product 1", Price = 100m }, 50, 5000m),
                            (new SqlServerTestProduct { Id = 2, Name = "Product 2", Price = 200m }, 30, 6000m),
                            (new SqlServerTestProduct { Id = 3, Name = "Product 3", Price = 150m }, 40, 6000m)
                        };

                        // Clone products in results
                        return results.ConvertAll(r => (r.Item1.ShallowClone(), r.Item2, r.Item3));
                    };

                    // Act
                    List<(SqlServerTestProduct Product, int Orders, decimal Revenue)> results = await getProductSummary();

                    // Mutate
                    results[0].Product.Name = "MUTATED";

                    // Get again
                    List<(SqlServerTestProduct Product, int Orders, decimal Revenue)> results2 = await getProductSummary();

                    // Assert
                    results2[0].Product.Name.Should().Be("Product 1");
                    results2.Should().HaveCount(3);
                }

                [Fact]
                public async Task ParameterizedQuery_DifferentParameters_IndependentResults()
                {
                    // Arrange
                    Dictionary<int, SqlServerTestProduct> mockDatabase = new Dictionary<int, SqlServerTestProduct>();
                    for (int i = 1; i <= 10; i++)
                    {
                        mockDatabase[i] = new SqlServerTestProduct
                        {
                            Id = i,
                            Name = $"Product {i}",
                            Price = i * 100m,
                            Stock = i * 10
                        };
                    }

                    Func<decimal, Task<List<SqlServerTestProduct>>> getByMinPrice = async (minPrice) =>
                    {
                        await Task.Delay(5);
                        return mockDatabase.Values
                            .Where(p => p.Price >= minPrice)
                            .Select(p => p.ShallowClone())
                            .ToList();
                    };

                    // Act
                    List<SqlServerTestProduct> expensive = await getByMinPrice(500m);
                    List<SqlServerTestProduct> midRange = await getByMinPrice(300m);
                    List<SqlServerTestProduct> cheap = await getByMinPrice(100m);

                    // Mutate
                    foreach (SqlServerTestProduct p in expensive)
                    {
                        p.Name = "EXPENSIVE";
                    }

                    // Assert - Database and other results unchanged
                    mockDatabase[5].Name.Should().Be("Product 5");
                    midRange.Should().HaveCountGreaterThan(expensive.Count);
                    cheap.Should().HaveCountGreaterThan(midRange.Count);
                    midRange.Should().OnlyContain(p => p.Name.StartsWith("Product"));
                }

                // ====================================================================
                // PERFORMANCE TESTS
                // ====================================================================

                [Fact]
                public async Task HighVolume_ThousandsConcurrent_MaintainsIsolation()
                {
                    // Arrange
                    Dictionary<int, SqlServerTestProduct> mockDatabase = new Dictionary<int, SqlServerTestProduct>();
                    for (int i = 1; i <= 100; i++)
                    {
                        mockDatabase[i] = new SqlServerTestProduct { Id = i, Name = $"Product {i}", Price = i * 10m };
                    }

                    Func<int, Task<SqlServerTestProduct?>> getById = async (id) =>
                    {
                        await Task.Delay(Random.Shared.Next(1, 5));
                        return mockDatabase.TryGetValue(id, out SqlServerTestProduct? p) ? p.ShallowClone() : null;
                    };

                    // Act - 1000 concurrent reads
                    List<Task> tasks = new List<Task>();
                    for (int i = 0; i < 1000; i++)
                    {
                        int productId = (i % 100) + 1;
                        tasks.Add(Task.Run(async () =>
                        {
                            SqlServerTestProduct? product = await getById(productId);
                            product!.Name = $"MUTATED-{Guid.NewGuid()}";
                            product.Price = Random.Shared.Next(1, 10000);
                        }));
                    }

                    await Task.WhenAll(tasks);

                    // Assert - All database values unchanged
                    for (int i = 1; i <= 100; i++)
                    {
                        mockDatabase[i].Name.Should().Be($"Product {i}");
                        mockDatabase[i].Price.Should().Be(i * 10m);
                    }
                }

                [Fact]
                public void Clone_LargeEntity_CompletesEfficiently()
                {
                    // Arrange
                    SqlServerTestProduct large = new SqlServerTestProduct
                    {
                        Id = 1,
                        Name = new string('X', 10000),
                        Price = decimal.MaxValue,
                        Stock = int.MaxValue,
                        CreatedDate = DateTime.UtcNow,
                        ModifiedDate = DateTime.UtcNow
                    };

                    // Act
                    System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                    for (int i = 0; i < 1000; i++)
                    {
                        SqlServerTestProduct clone = large.ShallowClone();
                    }
                    sw.Stop();

                    // Assert - 1000 clones should complete in < 50ms
                    sw.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(50));
                }
            }

            // Simple connection factory for tests
            public class SqlServerConnectionFactory
    {
        private readonly string _connectionString;

        public SqlServerConnectionFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        public SqlConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }

        public string GetConnectionString()
        {
            return _connectionString;
        }
    }
}
