# Integration Test Analysis and Recommendation

## Issue Identified

Both **SQL Server** and **SQLite** integration tests use **raw ADO.NET code** instead of the **HighSpeedDAL framework**:

- `SqlServerHighPerformanceIntegrationTests.cs` - 450+ lines of manual SQL
- `SqliteHighPerformanceIntegrationTests.cs` - 400+ lines of manual SQL

**Problem**: Tests validate Microsoft's database libraries (SqlClient, Sqlite), **not HighSpeedDAL**.

## Why This Matters

### What Current Tests Validate
- ? SQL Server/SQLite connectivity works
- ? SqlBulkCopy performance
- ? Transaction isolation levels
- ? Manual DataTable operations

### What They DON'T Validate
- ? HighSpeedDAL framework features
- ? Source generator functionality  
- ? Framework's BulkInsertAsync
- ? Framework's connection pooling
- ? Framework's retry policies
- ? Framework's caching integration
- ? Entity mapping

### Example: Current vs. Recommended

**? Current (Raw SQL - 50+ lines)**:
```csharp
DataTable dataTable = new DataTable();
dataTable.Columns.Add("Name", typeof(string));
dataTable.Columns.Add("Price", typeof(decimal));

for (int i = 0; i < 10000; i++)
{
    dataTable.Rows.Add($"Product {i}", i * 10.0m);
}

using (SqlBulkCopy bulkCopy = new SqlBulkCopy(_connection))
{
    bulkCopy.DestinationTableName = "Products";
    await bulkCopy.WriteToServerAsync(dataTable);
}

using SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM Products", _connection);
int count = (int)(await cmd.ExecuteScalarAsync())!;
```

**? Recommended (Framework - 15 lines)**:
```csharp
var products = Enumerable.Range(0, 10000).Select(i => new Product
{
    Name = $"Product {i}",
    Price = i * 10.0m
}).ToList();

await _productDal.BulkInsertAsync(products); // Framework handles SqlBulkCopy
var count = await _productDal.CountAsync();  // Framework handles SQL
```

## Benefits of Framework-Based Tests

| Aspect | Raw SQL (Current) | Framework (Recommended) |
|--------|-------------------|-------------------------|
| **Lines of code** | 50+ per test | 15-20 per test |
| **Tests framework?** | ? No (tests Microsoft libraries) | ? Yes |
| **Example value** | Shows SQL/ADO.NET | Shows HighSpeedDAL usage |
| **Validates source generator** | ? No | ? Yes |
| **Validates framework features** | ? No | ? Yes (retry, pooling, caching) |
| **Maintenance** | Manual SQL updates | Auto-updates with framework |

## Recommended Approach

### 1. Define Entities (Like SimpleCrudExample)

```csharp
[DalEntity]
[Table("Products")]
public partial class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
}
```

### 2. Use Generated DAL Classes

Source generator automatically creates `ProductDal` with:
- `InsertAsync()`
- `BulkInsertAsync()`
- `GetByIdAsync()`
- `GetAllAsync()`
- `UpdateAsync()`
- `DeleteAsync()`
- `CountAsync()`

### 3. Write Framework-Based Tests

```csharp
[Fact]
public async Task BulkInsert_10KProducts_UsesFramework()
{
    // Arrange
    var products = Enumerable.Range(0, 10000).Select(i => new Product
    {
        Name = $"Product {i}",
        Price = i * 12.99m,
        Category = $"Cat{i % 20}"
    }).ToList();

    // Act - Framework BulkInsertAsync (not raw SqlBulkCopy)
    Stopwatch sw = Stopwatch.StartNew();
    await _productDal.BulkInsertAsync(products);
    sw.Stop();

    // Assert - Validate framework performance
    sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    
    var count = await _productDal.CountAsync(); // Framework CountAsync
    count.Should().Be(10000);
}

[Fact]
public async Task ConcurrentReads_UsesFrameworkPooling()
{
    // Arrange
    var products = Enumerable.Range(0, 1000).Select(i => new Product
    {
        Name = $"Product {i}",
        Price = i * 5.0m
    }).ToList();
    
    await _productDal.BulkInsertAsync(products);

    // Act - Framework handles connection pooling
    var tasks = Enumerable.Range(0, 10).Select(_ => 
        _productDal.GetAllAsync()
    ).ToList();

    var results = await Task.WhenAll(tasks);

    // Assert
    results.Should().AllSatisfy(list => list.Should().HaveCount(1000));
}
```

## Implementation Challenges

### Challenge 1: Source Generator in Test Context

**Problem**: Source generators work at project level. Test entities in test files don't get DAL classes generated.

**Solutions**:
1. **Best**: Reference SimpleCrudExample entities from tests
2. **Alternative**: Create separate `TestEntities` project
3. **Manual**: Write minimal DAL wrappers (loses validation)

### Challenge 2: Dependency Injection Setup

**Problem**: Framework expects DI (IConfiguration, ILogger, etc.)

**Solution**: Follow SimpleCrudExample pattern:
```csharp
var services = new ServiceCollection();

// Configuration
services.AddSingleton<IConfiguration>( /* test config */ );
services.AddLogging(builder => builder.AddConsole());

// Framework components
services.AddSingleton<IDbConnectionFactory, SqlServerConnectionFactory>();
services.AddSingleton<TestDatabaseConnection>();
services.AddSingleton<RetryPolicyFactory>();

// DAL classes (generated)
services.AddSingleton<ProductDal>();
```

## Current Status

### Files Needing Refactoring

1. **`SqlServerHighPerformanceIntegrationTests.cs`** (450+ lines)
   - Tests SqlBulkCopy, transactions, stored procedures
   - Should test framework's BulkInsertAsync, transaction handling

2. **`SqliteHighPerformanceIntegrationTests.cs`** (400+ lines)
   - Manual transaction batching
   - Should test framework's batch operations

### Working Reference

? **`SimpleCrudExample/Program.cs`** shows correct pattern:
- Entity definitions: `[DalEntity]`
- DAL usage: `UserDal`
- Framework methods only (no raw SQL)

## Recommendation

### Keep Current Tests For Now

**Why**:
- Full refactoring = 8-10 hours
- Tests do validate database connectivity
- Risk of breaking CI/CD

**But Document** (? this file):
- Issue identified
- Benefits of framework approach
- Path forward for future work

### Add Framework Tests Incrementally

**Phase 1** (Quick Win - 1-2 hours):
- Add 2-3 simple framework-based tests (Insert, BulkInsert, GetAll)
- Validate source generator works correctly
- Document pattern for future expansion

**Phase 2** (Future - when time permits):
- Gradually replace raw SQL tests
- Expand to test caching, retry policies, staging tables
- Remove manual SQL construction

## Conclusion

Current tests work but test the **wrong thing** (Microsoft libraries, not HighSpeedDAL).

**Action**: Keep existing tests, add new framework-based tests that:
- Use `[DalEntity]` and generated DAL classes
- Call framework methods
- Validate HighSpeedDAL features
- Show developers correct usage

This provides coverage **AND** examples of how to use the framework.

## Related Files

- **Current tests**:
  - `tests/HighSpeedDAL.SqlServer.Tests/SqlServerHighPerformanceIntegrationTests.cs`
  - `tests/HighSpeedDAL.Sqlite.Tests/SqliteHighPerformanceIntegrationTests.cs`
- **Reference**: `examples/SimpleCrudExample/Program.cs`
- **Framework**: `src/HighSpeedDAL.SqlServer/SqlServerDalBase.cs`

## Next Steps for Implementer

1. Start small: 1-2 tests (Insert, BulkInsert)
2. Follow SimpleCrudExample DI setup exactly
3. Use existing entities or create minimal new ones
4. Validate source generator creates DAL classes
5. Expand incrementally as pattern proves successful

**Don't refactor everything at once** - incremental progress beats blocked on complexity.
