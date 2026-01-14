# Quick Start: Unit Testing with HighSpeedDAL Mocks

## Overview
The HighSpeedDAL framework now includes complete mock infrastructure for database-agnostic unit testing.

## Running Tests

### Run all unit tests (no database required)
```bash
dotnet test tests/HighSpeedDAL.Core.Tests/
```

### Run specific test class
```bash
dotnet test tests/HighSpeedDAL.Core.Tests/ --filter "DatabaseRetryPolicyTests"
```

### Run with verbose output
```bash
dotnet test tests/HighSpeedDAL.Core.Tests/ --verbosity detailed
```

## Writing Unit Tests

### Basic Test Class
```csharp
using Xunit;
using FluentAssertions;
using HighSpeedDAL.Core.Testing;

public class MyTests : UnitTestBase
{
    [Fact]
    public async Task MyTest_Scenario_Expected()
    {
        // Arrange
        DataStore.CreateTable("Products", new Dictionary<string, Type>
        {
            { "Id", typeof(int) },
            { "Name", typeof(string) }
        });

        // Act
        DataStore.Insert("Products", new Dictionary<string, object?>
        {
            { "Id", 1 },
            { "Name", "Widget" }
        });

        // Assert
        AssertTableRowCount("Products", 1);
    }
}
```

## Available Classes

### UnitTestBase
Base class for all unit tests providing:
- `DataStore` - InMemoryDataStore instance
- `Connection` - MockDatabaseConnection instance
- `ConnectionFactory` - MockDbConnectionFactory instance
- `AssertQueryExecuted(pattern)` - Verify SQL execution
- `AssertTableRowCount(table, count)` - Verify row count
- `AssertTableExists(table)` - Verify table exists
- `AssertRowExists(table, predicate)` - Verify row exists

### InMemoryDataStore
Simulates database tables:
```csharp
DataStore.CreateTable("Products", new() { { "Id", typeof(int) } });
DataStore.Insert("Products", new() { { "Id", 1 } });
DataStore.Update("Products", row => row["Id"].Equals(1), new() { { "Name", "New" } });
DataStore.Delete("Products", row => row["Id"].Equals(1));
var rows = DataStore.Select("Products");
var count = DataStore.Count("Products");
```

### MockDatabaseConnection
Simulates IDbConnection:
```csharp
await Connection.ExecuteNonQueryAsync(
    "INSERT INTO Products VALUES (@id, @name)",
    new() { { "id", 1 }, { "name", "Widget" } }
);
var count = await Connection.ExecuteScalarAsync("SELECT COUNT(*) FROM Products");
var queries = Connection.GetExecutedQueries();
```

### SqlExceptionHelper
Creates mock SQL exceptions:
```csharp
var ex = SqlExceptionHelper.CreateSqlException(1205, "Deadlock victim");
var ex2 = SqlExceptionHelper.CreateSqlException(40197, "Service error");
```

## Common Test Patterns

### Testing Retry Logic
```csharp
[Fact]
public async Task ExecuteWithRetry_TransientError_Retries()
{
    int attempts = 0;
    var policy = new DatabaseRetryPolicy(_logger, maxRetryAttempts: 3);
    
    var result = await policy.ExecuteAsync(async () =>
    {
        attempts++;
        if (attempts < 3)
            throw SqlExceptionHelper.CreateSqlException(1205);
        return 42;
    });

    Assert.Equal(42, result);
    Assert.Equal(3, attempts);
}
```

### Testing Cache Operations
```csharp
[Fact]
public async Task Cache_SetGet_ReturnsEntity()
{
    var cache = new MemoryCacheManager<TestEntity, int>(
        _logger, maxSize: 100, expirationSeconds: 60);
    
    var entity = new TestEntity { Id = 1, Name = "Test" };
    await cache.SetAsync(1, entity);
    
    var result = await cache.GetAsync(1);
    result.Should().NotBeNull();
    result.Id.Should().Be(1);
}
```

### Testing Data Operations
```csharp
[Fact]
public async Task Insert_ValidData_StoresInDataStore()
{
    // Setup
    DataStore.CreateTable("Products", new()
    {
        { "Id", typeof(int) },
        { "Name", typeof(string) },
        { "Price", typeof(decimal) }
    });

    // Act
    DataStore.Insert("Products", new()
    {
        { "Id", 1 },
        { "Name", "Widget" },
        { "Price", 9.99m }
    });

    // Assert
    AssertTableRowCount("Products", 1);
    var rows = DataStore.Select("Products");
    rows[0]["Name"].Should().Be("Widget");
}
```

### Testing SQL Execution
```csharp
[Fact]
public async Task ExecuteQuery_TracksSql()
{
    await Connection.ExecuteNonQueryAsync(
        "INSERT INTO Products (Id, Name) VALUES (@id, @name)",
        new() { { "id", 1 }, { "name", "Widget" } }
    );

    AssertQueryExecuted("INSERT INTO Products");
    AssertQueryCount(1);
    
    var executed = GetExecutedQueries();
    executed[0].sql.Should().Contain("INSERT");
    executed[0].parameters["id"].Should().Be(1);
}
```

## Tips & Tricks

### Clearing Data Between Tests
```csharp
public override void Dispose()
{
    DataStore.ClearTable("Products");
    Connection.ClearExecutedQueries();
    base.Dispose();
}
```

### Testing with Predicates
```csharp
var filtered = DataStore.Select("Products", 
    row => (decimal?)row["Price"] > 10m);
```

### Verifying Query Parameters
```csharp
var queries = Connection.GetExecutedQueries();
var lastQuery = queries[queries.Count - 1];
Assert.Equal("@id", lastQuery.parameters.Keys.First());
```

### Testing Error Scenarios
```csharp
[Theory]
[InlineData(1205)]   // Deadlock
[InlineData(40197)]  // Service error
[InlineData(40501)]  // Service busy
public async Task Retry_TransientErrors_Handled(int errorCode)
{
    var ex = SqlExceptionHelper.CreateSqlException(errorCode);
    // Test error handling...
}
```

## Project Structure

```
src/HighSpeedDAL.Core/Testing/
??? InMemoryDataStore.cs
??? MockDatabaseConnection.cs
??? MockDbConnectionFactory.cs
??? UnitTestBase.cs
??? SqlExceptionHelper.cs

tests/HighSpeedDAL.Core.Tests/
??? DatabaseRetryPolicyTests_Fixed.cs
??? MemoryCacheManagerTests_Fixed.cs
??? RetryPolicyTransientErrorTests_Fixed.cs
??? TableNamePluralizerTests.cs
??? AttributeTests.cs
??? PropertyAutoGeneratorTests.cs
??? TestEntities.cs
??? HighSpeedDAL.Core.Tests.csproj
```

## No Database Required!

All unit tests run **in-memory** without:
- ? SQL Server
- ? Sqlite
- ? MySQL
- ? PostgreSQL
- ? Any external database

Perfect for:
- ? Local development
- ? CI/CD pipelines
- ? Fast test execution
- ? Parallel test runs

## Documentation

- See `UNIT_TEST_FIXES_COMPLETE.md` for comprehensive guide
- See `UNIT_TEST_FIXES_PROGRESS.md` for implementation details
- See `UNIT_TEST_FIX_SUMMARY.md` for executive summary

## Support

For questions about unit testing:
1. Check existing test examples in Core.Tests/
2. Review mock infrastructure in Core/Testing/
3. Refer to IDataConnection interface for mock capabilities
4. Check FluentAssertions documentation for assertions

---

**Happy testing! ??**
