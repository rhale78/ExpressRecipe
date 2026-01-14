# Test Framework Usage Analysis

## Executive Summary

**Issue Identified**: Most unit and integration tests in the HighSpeedDAL repository **do not use the framework's source generator** or generated DAL classes. Instead, they:
- Define manual test entities without `[Table]` attributes
- Implement manual cloning via `IEntityCloneable<T>` 
- Use raw ADO.NET (SqlCommand, DataTable, SqlBulkCopy)
- Don't demonstrate how users would actually use the framework

**Impact**: Tests validate underlying components but **don't serve as usage examples** for developers learning the framework.

**Recommendation**: Add a new category of tests that use source-generated DAL classes, demonstrating real-world usage patterns.

---

## Current Test Architecture

### 1. Unit Tests (Core, DataManagement, AdvancedCaching)

**Pattern**: Manual test entities without framework attributes

**Example** (`VersionManagerTests.cs`):
```csharp
// Manual test entity - NO source generation
[Versioned(Strategy = VersionStrategy.Integer, PropertyName = "Version")]
public class VersionedProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
}

// Tests manually create tables and use raw SQL
private async Task InitializeDatabaseAsync()
{
    using (SqliteConnection connection = new SqliteConnection(_connectionString))
    {
        await connection.OpenAsync();
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = @"
                CREATE TABLE VersionedProducts (
                    Id INTEGER PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Version INTEGER NOT NULL DEFAULT 1
                )";
            await command.ExecuteNonQueryAsync();
        }
    }
}
```

**Why This Approach**:
- ? Isolates component testing (VersionManager logic independent of source generator)
- ? Fast test execution (no source generator overhead)
- ? Tests specific behaviors without framework dependencies
- ? **Doesn't show users how to use the framework**

### 2. Integration Tests (SqlServer, Sqlite)

**Pattern**: Manual entities with manual cloning, raw ADO.NET

**Example** (`SqlServerCloningIntegrationTests.cs`):
```csharp
// Manual test entity with manual cloning implementation
public class SqlServerTestProduct : IEntityCloneable<SqlServerTestProduct>
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    
    // Manual cloning (framework generates this automatically)
    public SqlServerTestProduct ShallowClone()
    {
        return new SqlServerTestProduct
        {
            Id = this.Id,
            Name = this.Name,
            Price = this.Price
        };
    }
    
    public SqlServerTestProduct DeepClone() => ShallowClone();
}

// Tests use mocked dictionaries, not actual DAL classes
[Fact]
public async Task SimulatedDalOperation_ReturnsCopiedEntity()
{
    Dictionary<int, SqlServerTestProduct> mockDatabase = new();
    // ... manual simulation, no generated DAL
}
```

**Example** (`SqlServerHighPerformanceIntegrationTests.cs`):
```csharp
// Raw SQL table creation - NO [Table] attribute
private void InitializeDatabase()
{
    string[] createTableSql = new[]
    {
        @"CREATE TABLE Products (
            Id INT PRIMARY KEY IDENTITY(1,1),
            Name NVARCHAR(200) NOT NULL,
            Price DECIMAL(18,2) NOT NULL
        )"
    };
}

// Raw SqlBulkCopy - NOT framework's BulkInsertAsync
[Fact]
public async Task BulkInsert_SqlBulkCopy_50KProducts()
{
    DataTable dataTable = new DataTable();
    dataTable.Columns.Add("Name", typeof(string));
    // ... manual DataTable setup
    
    using (SqlBulkCopy bulkCopy = new SqlBulkCopy(_connection!))
    {
        bulkCopy.DestinationTableName = "Products";
        await bulkCopy.WriteToServerAsync(dataTable);
    }
}
```

**Why This Approach**:
- ? Tests actual database connectivity (SQL Server, SQLite)
- ? Validates low-level components (connection pooling, transactions)
- ? Performance benchmarks use real database operations
- ? **Tests Microsoft libraries (SqlBulkCopy), not framework methods**
- ? **Doesn't demonstrate source generator usage**
- ? **Developers can't copy-paste test code for their own entities**

### 3. Example Projects (SimpleCrudExample, ShowcaseExample)

**Pattern**: **CORRECTLY** uses framework with source generation

**Example** (`SimpleCrudExample/Entities/User.cs`):
```csharp
// CORRECT: Framework-driven entity definition
[Table("Users")]
[Cache(CacheStrategy.Memory, MaxSize = 1000, ExpirationSeconds = 300)]
[StagingTable(SyncIntervalSeconds = 30)]
public partial class User  // ? partial keyword for source generator
{
    // Framework auto-generates: public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    // ... other properties
}

// CORRECT: Uses generated DAL class
public class FeatureShowcase
{
    private readonly UserDal _userDal;  // ? Generated by source generator
    
    public async Task DemonstrateBasicCrudOperationsAsync()
    {
        // ? Framework method - this is how users should write code
        var user = await _userDal.InsertAsync(new User { Username = "john" });
        var retrieved = await _userDal.GetByIdAsync(user.Id);
        await _userDal.UpdateAsync(user);
        await _userDal.DeleteAsync(user.Id);
    }
}
```

**Why This Is Correct**:
- ? Shows real framework usage (source generator + DAL classes)
- ? Demonstrates attribute configuration
- ? Uses generated methods (InsertAsync, GetByIdAsync, etc.)
- ? Developers can copy patterns for their own projects
- ? Tests the **full framework stack** (attributes ? generator ? DAL ? database)

---

## Problem Statement

### Gap Between Tests and Usage

**What Tests Do**:
```csharp
// Unit test approach - isolated component
public class SqlServerTestProduct  // NO [Table] attribute
{
    public int Id { get; set; }
    
    // Manual cloning - user shouldn't write this
    public SqlServerTestProduct ShallowClone() { /* ... */ }
}

// Manual SQL - user shouldn't do this
DataTable dataTable = new DataTable();
using (SqlBulkCopy bulkCopy = new SqlBulkCopy(connection))
{
    await bulkCopy.WriteToServerAsync(dataTable);
}
```

**What Users Should Do** (from documentation):
```csharp
// User-facing approach - framework-driven
[Table("Products")]
[Cache(CacheStrategy.Memory)]
public partial class Product  // ? partial for auto-generation
{
    // Id auto-generated
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

// Generated DAL class (users don't write this, source generator does)
// ProductDal is created automatically

// Usage
var dal = serviceProvider.GetRequiredService<ProductDal>();
var products = new List<Product> { /* ... */ };
await dal.BulkInsertAsync(products);  // ? Framework handles SqlBulkCopy
```

### Why This Matters

1. **Learning Curve**: Developers studying tests see manual cloning and raw SQL, then read docs saying "use attributes and framework methods" - confusing disconnect

2. **Example Code**: Tests should serve as working examples. Current tests can't be copied to real projects without major rewrites

3. **Framework Validation**: Integration tests validate SqlBulkCopy (Microsoft) not BulkInsertAsync (HighSpeedDAL)

4. **Source Generator Coverage**: No tests validate end-to-end source generator workflow with actual databases

---

## Proposed Solution

### Add New Test Category: "Framework Integration Tests"

Create tests that use the framework **exactly as users would**, alongside existing isolated component tests.

### Test Structure

```
tests/
??? HighSpeedDAL.Core.Tests/              # Keep existing - component isolation
??? HighSpeedDAL.SqlServer.Tests/         # Keep existing - connectivity validation
??? HighSpeedDAL.Sqlite.Tests/            # Keep existing - SQLite-specific
??? HighSpeedDAL.FrameworkUsage.Tests/    # NEW - end-to-end framework usage
?   ??? Entities/
?   ?   ??? Product.cs                    # [Table] with source generation
?   ?   ??? Order.cs                      # [Table] [Cache] [SoftDelete]
?   ?   ??? Customer.cs                   # [Table] [AutoAudit]
?   ??? Data/
?   ?   ??? TestDatabaseConnection.cs     # Connection class
?   ??? SqlServerFrameworkTests.cs        # Uses ProductDal, OrderDal (generated)
?   ??? SqliteFrameworkTests.cs           # SQLite variant
?   ??? CachingFrameworkTests.cs          # Cache via generated DAL
?   ??? BulkOperationsFrameworkTests.cs   # BulkInsertAsync via DAL
?   ??? README.md                         # Explains "these are usage examples"
```

### Example Test Implementation

**Entity Definition** (`HighSpeedDAL.FrameworkUsage.Tests/Entities/Product.cs`):
```csharp
using HighSpeedDAL.Core;

namespace HighSpeedDAL.FrameworkUsage.Tests.Entities
{
    /// <summary>
    /// Example entity demonstrating framework usage with source generation.
    /// This is how developers should define entities in their projects.
    /// </summary>
    [Table("Products")]
    [Cache(CacheStrategy.Memory, MaxSize = 1000, ExpirationSeconds = 300)]
    public partial class Product  // partial enables source generator
    {
        // Id property is auto-generated by framework
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public string Category { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
    }
}
```

**Test Implementation** (`HighSpeedDAL.FrameworkUsage.Tests/SqlServerFrameworkTests.cs`):
```csharp
using HighSpeedDAL.FrameworkUsage.Tests.Entities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HighSpeedDAL.FrameworkUsage.Tests
{
    /// <summary>
    /// Integration tests demonstrating real-world framework usage.
    /// These tests use source-generated DAL classes exactly as users would.
    /// 
    /// NOTE: Unlike isolated component tests, these validate the full stack:
    /// Entity attributes ? Source generator ? Generated DAL ? Database
    /// </summary>
    public class SqlServerFrameworkTests : IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ProductDal _productDal;  // Source-generated
        
        public SqlServerFrameworkTests()
        {
            // Setup DI exactly as users would
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(/* ... */);
            services.AddSingleton<IDbConnectionFactory, SqlServerConnectionFactory>();
            services.AddSingleton<ProductDal>();  // Register generated DAL
            
            _serviceProvider = services.BuildServiceProvider();
            _productDal = _serviceProvider.GetRequiredService<ProductDal>();
            
            // Create test database (one-time setup)
            InitializeDatabaseAsync().GetAwaiter().GetResult();
        }
        
        /// <summary>
        /// Demonstrates basic CRUD using generated DAL methods.
        /// Users can copy this pattern for their own entities.
        /// </summary>
        [Fact]
        public async Task BasicCrud_UsesGeneratedDalMethods()
        {
            // Arrange - Create entity (no manual cloning needed)
            var product = new Product
            {
                Name = "Test Product",
                Price = 99.99m,
                StockQuantity = 50,
                Category = "Electronics",
                CreatedDate = DateTime.UtcNow
            };
            
            // Act - Use generated DAL methods
            var inserted = await _productDal.InsertAsync(product);  // ? Generated method
            var retrieved = await _productDal.GetByIdAsync(inserted.Id);  // ? Generated method
            
            retrieved.Name = "Updated Product";
            await _productDal.UpdateAsync(retrieved);  // ? Generated method
            
            var updated = await _productDal.GetByIdAsync(inserted.Id);
            await _productDal.DeleteAsync(inserted.Id);  // ? Generated method
            
            // Assert
            inserted.Id.Should().BeGreaterThan(0);
            retrieved.Should().NotBeNull();
            retrieved.Name.Should().Be("Test Product");
            updated.Name.Should().Be("Updated Product");
            
            var deleted = await _productDal.GetByIdAsync(inserted.Id);
            deleted.Should().BeNull();
        }
        
        /// <summary>
        /// Demonstrates bulk operations using generated DAL.
        /// Shows how framework handles SqlBulkCopy internally.
        /// </summary>
        [Fact]
        public async Task BulkInsert_UsesGeneratedDalMethod()
        {
            // Arrange
            var products = new List<Product>();
            for (int i = 0; i < 1000; i++)
            {
                products.Add(new Product
                {
                    Name = $"Bulk Product {i}",
                    Price = i * 10m,
                    StockQuantity = i,
                    Category = $"Category{i % 5}",
                    CreatedDate = DateTime.UtcNow
                });
            }
            
            // Act - Framework method (not manual SqlBulkCopy)
            await _productDal.BulkInsertAsync(products);  // ? Generated method
            
            // Assert
            var count = await _productDal.CountAsync();
            count.Should().BeGreaterOrEqualTo(1000);
        }
        
        /// <summary>
        /// Demonstrates caching behavior with generated DAL.
        /// Shows defensive cloning happens automatically.
        /// </summary>
        [Fact]
        public async Task Cache_DefensiveCloning_AutomaticViaDal()
        {
            // Arrange
            var product = await _productDal.InsertAsync(new Product
            {
                Name = "Cached Product",
                Price = 50m,
                StockQuantity = 100,
                Category = "Test",
                CreatedDate = DateTime.UtcNow
            });
            
            // Act - First read (cache miss, stores copy)
            var first = await _productDal.GetByIdAsync(product.Id);
            
            // Mutate returned object
            first.Name = "MUTATED";
            first.Price = 999m;
            
            // Second read (cache hit, returns fresh copy)
            var second = await _productDal.GetByIdAsync(product.Id);
            
            // Assert - Cache protected from mutation
            second.Name.Should().Be("Cached Product");
            second.Price.Should().Be(50m);
            // ? Defensive cloning worked automatically via generated DAL
        }
        
        public void Dispose()
        {
            // Cleanup test database
        }
        
        private async Task InitializeDatabaseAsync()
        {
            // Framework creates table based on [Table] attribute
            // No manual CREATE TABLE needed in production
            // For tests, we manually create for isolation
        }
    }
}
```

### Key Features of New Tests

1. **Use `[Table]` Attribute**: Entities defined with framework attributes
2. **`partial` Class**: Enables source generator
3. **Generated DAL**: Tests use `ProductDal`, `OrderDal` (source-generated classes)
4. **Framework Methods**: `InsertAsync`, `BulkInsertAsync`, `GetByIdAsync` (not raw SQL)
5. **DI Setup**: Shows proper dependency injection configuration
6. **Copy-Paste Ready**: Developers can copy test code to their projects

---

## Implementation Plan

### Phase 1: Create Framework Usage Test Project

```bash
dotnet new xunit -n HighSpeedDAL.FrameworkUsage.Tests -o tests/HighSpeedDAL.FrameworkUsage.Tests
cd tests/HighSpeedDAL.FrameworkUsage.Tests
dotnet add reference ../../src/HighSpeedDAL.Core/HighSpeedDAL.Core.csproj
dotnet add reference ../../src/HighSpeedDAL.SqlServer/HighSpeedDAL.SqlServer.csproj
dotnet add reference ../../src/HighSpeedDAL.Sqlite/HighSpeedDAL.Sqlite.csproj
dotnet add package Microsoft.Extensions.DependencyInjection
dotnet add package FluentAssertions
```

### Phase 2: Define Test Entities

Create entities that demonstrate common patterns:
- **Product**: Basic entity with caching
- **Order**: Entity with `[SoftDelete]` and `[AutoAudit]`
- **Customer**: Entity with staging table
- **LogEntry**: Entity with memory-mapped files

All entities use `partial` and `[Table]` attributes.

### Phase 3: Write Framework Integration Tests

Create test classes for:
1. **BasicCrudFrameworkTests**: INSERT, SELECT, UPDATE, DELETE
2. **BulkOperationsFrameworkTests**: BulkInsertAsync, BulkUpdateAsync
3. **CachingFrameworkTests**: Cache hits, defensive cloning, invalidation
4. **SoftDeleteFrameworkTests**: Soft delete via generated DAL
5. **AuditFrameworkTests**: Auto-audit field population
6. **StagingTableFrameworkTests**: High-write scenarios

### Phase 4: Document Test Purpose

Add README in test project:
```markdown
# HighSpeedDAL Framework Usage Tests

## Purpose

These tests demonstrate **real-world framework usage** with source-generated
DAL classes. Unlike isolated component tests, these validate the full stack:

Entity attributes ? Source generator ? Generated DAL ? Database

## For Developers

**Use these tests as examples** for your own projects. The entity definitions
and DAL usage patterns shown here are exactly how you should structure your
code.

## Test Categories

- **BasicCrudFrameworkTests**: Copy-paste CRUD patterns
- **BulkOperationsFrameworkTests**: High-throughput examples
- **CachingFrameworkTests**: Cache configuration and usage
- **SoftDeleteFrameworkTests**: Soft delete implementation
- **AuditFrameworkTests**: Auto-audit setup
- **StagingTableFrameworkTests**: High-write scenarios

## Why Two Test Suites?

**Component Tests** (HighSpeedDAL.Core.Tests, SqlServer.Tests):
- Test isolated components (VersionManager, ConnectionFactory)
- Fast execution, no source generator overhead
- Validate low-level functionality

**Framework Usage Tests** (THIS PROJECT):
- Test full framework stack (attributes ? DAL ? database)
- Demonstrate real-world usage patterns
- Validate source generator end-to-end
- Serve as working examples for developers
```

### Phase 5: Add Comments to Existing Tests

Add XML comments to existing test classes explaining their purpose:

**Example** (`SqlServerCloningIntegrationTests.cs`):
```csharp
/// <summary>
/// SQL Server cloning integration tests.
/// 
/// NOTE: These tests use manual test entities (SqlServerTestProduct) without
/// source generation to isolate and validate the cloning behavior of the
/// cache manager component. This approach allows focused testing of defensive
/// cloning logic independent of the source generator.
/// 
/// For examples of real-world framework usage with source-generated DAL classes,
/// see HighSpeedDAL.FrameworkUsage.Tests project.
/// </summary>
public class SqlServerCloningIntegrationTests
{
    /// <summary>
    /// Manual test entity used to test cloning in isolation.
    /// In production code, users would define entities with [Table] attribute
    /// and use source-generated DAL classes (see FrameworkUsage.Tests).
    /// </summary>
    public class SqlServerTestProduct : IEntityCloneable<SqlServerTestProduct>
    {
        // ...
    }
}
```

**Example** (`SqlServerHighPerformanceIntegrationTests.cs`):
```csharp
/// <summary>
/// SQL Server high-performance integration tests.
/// 
/// NOTE: These tests use raw ADO.NET (SqlBulkCopy, DataTable) to validate
/// database connectivity and performance characteristics. They test Microsoft's
/// database libraries, not HighSpeedDAL framework methods.
/// 
/// For examples showing how users should use the framework (BulkInsertAsync,
/// GetByIdAsync, etc. via generated DAL classes), see HighSpeedDAL.FrameworkUsage.Tests.
/// 
/// REASON: These tests require SQL Server environment variable. They serve as
/// smoke tests for deployment validation, not as usage examples.
/// </summary>
public class SqlServerHighPerformanceIntegrationTests
{
    // Raw SQL approach for connectivity validation
    [Fact]
    public async Task BulkInsert_SqlBulkCopy_50KProducts()
    {
        // NOTE: Users should NOT do this. Use dal.BulkInsertAsync() instead.
        // See HighSpeedDAL.FrameworkUsage.Tests for correct usage.
        DataTable dataTable = new DataTable();
        using (SqlBulkCopy bulkCopy = new SqlBulkCopy(_connection!))
        {
            await bulkCopy.WriteToServerAsync(dataTable);
        }
    }
}
```

---

## Benefits of This Approach

### 1. Clear Separation of Concerns

| Test Type | Purpose | Entities | Methods | Source Generator |
|-----------|---------|----------|---------|-----------------|
| **Component Tests** | Validate isolated components | Manual, no attributes | Direct component APIs | Not used |
| **Framework Usage Tests** | Demonstrate real-world usage | `[Table]`, `partial` | Generated DAL methods | ? Used |
| **Example Projects** | Educational showcase | `[Table]`, `partial` | Generated DAL methods | ? Used |

### 2. Developer Experience

**Before** (current state):
- Developer studies tests ? sees manual entities and raw SQL
- Developer reads docs ? says "use [Table] and generated DAL"
- Developer confused: "Why don't tests use the framework?"

**After** (with new tests):
- Developer studies FrameworkUsage.Tests ? sees `[Table]` and `ProductDal`
- Developer reads docs ? says "use [Table] and generated DAL"
- Developer copies test code to project ? **IT JUST WORKS**

### 3. Framework Validation

**Current gaps**:
- ? No tests validate source generator with actual database
- ? No tests show DI setup for generated DAL classes
- ? No tests demonstrate end-to-end workflow (attribute ? generator ? DAL ? DB)

**With new tests**:
- ? Validates source generator output is correct
- ? Validates generated DAL methods work with real databases
- ? Validates framework features (caching, soft delete, audit) via DAL
- ? Catches regressions in source generator

### 4. Documentation Alignment

Tests and documentation say the same thing:

**Documentation**: "Define entities with `[Table]` attribute, use generated DAL"
**Framework Usage Tests**: ? Show exactly that
**Component Tests**: Clearly labeled as "internal component validation"

---

## Migration Strategy

### Immediate (Low Effort)

1. **Add Comments to Existing Tests** (~1-2 hours)
   - Add XML documentation explaining test purpose
   - Note that tests use manual entities for isolation
   - Reference FrameworkUsage.Tests for usage examples

2. **Update Test README Files** (~1 hour)
   - Explain why tests don't use source generator
   - Link to example projects for real usage patterns

### Short Term (Medium Effort)

3. **Create FrameworkUsage.Tests Project** (~8-12 hours)
   - Define 3-4 test entities (`[Table]`, `partial`)
   - Write 10-15 framework usage tests
   - Validate source generator end-to-end
   - Document as usage examples

4. **Add to CI/CD** (~2 hours)
   - Run framework usage tests in CI
   - Catch source generator regressions
   - Validate DAL generation

### Long Term (High Effort)

5. **Refactor Integration Tests** (documented in INTEGRATION_TEST_REFACTORING.md)
   - Convert raw SQL tests to framework methods
   - Use generated DAL classes
   - 8-10 hours estimated

---

## Conclusion

**Current State**:
- Tests validate components in isolation (good for unit testing)
- Tests use manual entities and raw SQL (bad for usage examples)
- No end-to-end validation of source generator workflow

**Proposed State**:
- Keep existing tests for component isolation
- Add new FrameworkUsage.Tests showing real-world usage
- All tests clearly documented with their purpose
- Developers have working examples to copy

**Next Steps**:
1. Add comments to existing tests explaining manual approach
2. Create FrameworkUsage.Tests project with source-generated DAL usage
3. Update documentation linking to usage test examples
4. Consider long-term integration test refactoring (separate effort)

---

## Related Documentation

- **Integration Test Refactoring**: `docs/INTEGRATION_TEST_REFACTORING.md`
- **SimpleCrud Refactoring**: `docs/SIMPLECRUD_REFACTORING_SUMMARY.md`
- **Example Projects**: `examples/SimpleCrudExample/README.md`
