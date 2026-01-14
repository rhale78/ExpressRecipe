# Implementation Guide: Database-Agnostic Unit Testing

## Quick Start: What Needs to Be Done

### 1. Create Mock Database Implementation

**File: `src/HighSpeedDAL.Core/Testing/InMemoryDataStore.cs`**
```csharp
public class InMemoryDataStore
{
    private readonly Dictionary<string, List<Dictionary<string, object>>> _tables = new();
    private readonly object _lock = new object();

    public void CreateTable(string tableName, List<(string columnName, Type columnType)> columns)
    {
        lock (_lock)
        {
            if (!_tables.ContainsKey(tableName))
            {
                _tables[tableName] = new List<Dictionary<string, object>>();
            }
        }
    }

    public int Insert(string tableName, Dictionary<string, object> row)
    {
        lock (_lock)
        {
            if (!_tables.TryGetValue(tableName, out var table))
                throw new InvalidOperationException($"Table {tableName} not found");
            
            table.Add(row);
            return table.Count; // Simulate auto-increment
        }
    }

    public int Update(string tableName, Dictionary<string, object> row, int id)
    {
        lock (_lock)
        {
            if (!_tables.TryGetValue(tableName, out var table))
                throw new InvalidOperationException($"Table {tableName} not found");
            
            var index = table.FindIndex(r => (int)r["Id"] == id);
            if (index >= 0)
            {
                table[index] = row;
                return 1;
            }
            return 0;
        }
    }

    public int Delete(string tableName, int id)
    {
        lock (_lock)
        {
            if (!_tables.TryGetValue(tableName, out var table))
                throw new InvalidOperationException($"Table {tableName} not found");
            
            var removed = table.RemoveAll(r => (int)r["Id"] == id);
            return removed;
        }
    }

    public List<Dictionary<string, object>> Select(string tableName)
    {
        lock (_lock)
        {
            if (!_tables.TryGetValue(tableName, out var table))
                throw new InvalidOperationException($"Table {tableName} not found");
            
            return new List<Dictionary<string, object>>(table);
        }
    }

    public Dictionary<string, object>? SelectById(string tableName, int id)
    {
        lock (_lock)
        {
            if (!_tables.TryGetValue(tableName, out var table))
                throw new InvalidOperationException($"Table {tableName} not found");
            
            return table.FirstOrDefault(r => (int)r["Id"] == id);
        }
    }

    public int Count(string tableName)
    {
        lock (_lock)
        {
            return _tables.TryGetValue(tableName, out var table) ? table.Count : 0;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _tables.Clear();
        }
    }
}
```

### 2. Create Mock Database Connection

**File: `src/HighSpeedDAL.Core/Testing/MockDatabaseConnection.cs`**
```csharp
using HighSpeedDAL.Core.Interfaces;

public class MockDatabaseConnection : IDatabaseConnection
{
    private readonly InMemoryDataStore _dataStore;
    private List<(string sql, Dictionary<string, object> parameters)> _executedQueries = new();

    public MockDatabaseConnection(InMemoryDataStore dataStore)
    {
        _dataStore = dataStore;
    }

    public async Task<int> ExecuteNonQueryAsync(
        string sql,
        object? parameters = null,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        _executedQueries.Add((sql, (Dictionary<string, object>?)parameters ?? new()));
        
        // Parse basic SQL and execute against in-memory store
        if (sql.StartsWith("INSERT"))
            return ExecuteInsert(sql, parameters);
        if (sql.StartsWith("UPDATE"))
            return ExecuteUpdate(sql, parameters);
        if (sql.StartsWith("DELETE"))
            return ExecuteDelete(sql, parameters);
        
        return 0;
    }

    public async Task<T?> ExecuteScalarAsync<T>(
        string sql,
        object? parameters = null,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        _executedQueries.Add((sql, (Dictionary<string, object>?)parameters ?? new()));
        
        // For SELECT SCOPE_IDENTITY() / last_insert_rowid()
        if (sql.Contains("SCOPE_IDENTITY") || sql.Contains("last_insert_rowid"))
            return (T?)(object)_executedQueries.Count;
        
        return default;
    }

    public async Task<IDataReader> ExecuteReaderAsync(
        string sql,
        object? parameters = null,
        CommandBehavior behavior = CommandBehavior.Default,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        _executedQueries.Add((sql, (Dictionary<string, object>?)parameters ?? new()));
        return new MockDataReader(_dataStore.Select("Products")); // Simplified
    }

    public IList<(string sql, Dictionary<string, object> parameters)> GetExecutedQueries()
        => _executedQueries;

    public void ClearExecutedQueries()
        => _executedQueries.Clear();

    private int ExecuteInsert(string sql, object? parameters)
    {
        var row = ConvertParametersToRow(parameters);
        return _dataStore.Insert("Products", row); // Simplified
    }

    private int ExecuteUpdate(string sql, object? parameters)
    {
        var row = ConvertParametersToRow(parameters);
        var id = (int)row["Id"];
        return _dataStore.Update("Products", row, id);
    }

    private int ExecuteDelete(string sql, object? parameters)
    {
        // Parse WHERE clause to get ID
        var id = ExtractIdFromWhere(sql);
        return _dataStore.Delete("Products", id);
    }

    private Dictionary<string, object> ConvertParametersToRow(object? parameters)
    {
        if (parameters is Dictionary<string, object> dict)
            return dict;
        
        var row = new Dictionary<string, object>();
        if (parameters != null)
        {
            var props = parameters.GetType().GetProperties();
            foreach (var prop in props)
            {
                row[prop.Name] = prop.GetValue(parameters)!;
            }
        }
        return row;
    }

    private int ExtractIdFromWhere(string sql)
    {
        // Simple parsing: "WHERE [Id] = @Id" or similar
        var match = System.Text.RegularExpressions.Regex.Match(sql, @"WHERE.*\[?Id\]?\s*=\s*(@Id|\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }
}
```

### 3. Create Test Base Class

**File: `tests/HighSpeedDAL.Core.Tests/DalUnitTestBase.cs`**
```csharp
using HighSpeedDAL.Core.Testing;
using Microsoft.Extensions.Logging;
using Moq;

public abstract class DalUnitTestBase : IDisposable
{
    protected InMemoryDataStore DataStore { get; }
    protected MockDatabaseConnection MockConnection { get; }
    protected Mock<ILogger> MockLogger { get; }

    protected DalUnitTestBase()
    {
        DataStore = new InMemoryDataStore();
        MockConnection = new MockDatabaseConnection(DataStore);
        MockLogger = new Mock<ILogger>();
        
        SetupDefaultTables();
    }

    protected virtual void SetupDefaultTables()
    {
        DataStore.CreateTable("Products", new()
        {
            ("Id", typeof(int)),
            ("Name", typeof(string)),
            ("Price", typeof(decimal)),
        });
    }

    protected void AssertQueryExecuted(string sqlPattern)
    {
        var executed = MockConnection.GetExecutedQueries();
        Assert.True(
            executed.Any(q => q.sql.Contains(sqlPattern)),
            $"Expected query containing '{sqlPattern}' was not executed. Executed: {string.Join("; ", executed.Select(q => q.sql))}");
    }

    protected void AssertQueryCount(int expectedCount)
    {
        var count = MockConnection.GetExecutedQueries().Count;
        Assert.Equal(expectedCount, count);
    }

    public void Dispose()
    {
        DataStore.Clear();
    }
}
```

### 4. Example Unit Test (No Database)

**File: `tests/HighSpeedDAL.Core.Tests/GeneratedDalCodeTests.cs`**
```csharp
public class GeneratedDalCodeTests : DalUnitTestBase
{
    [Fact]
    public void InsertAsync_GeneratesSqlWithAllParameters()
    {
        // Arrange
        var entity = new Product 
        { 
            Name = "Test Product",
            Price = 99.99m
        };

        // Act
        // (In real test, would call generated ProductDal.InsertAsync with mock)
        MockConnection.ExecuteNonQueryAsync(
            "INSERT INTO [Products] ([Name], [Price]) VALUES (@Name, @Price)",
            new Dictionary<string, object>
            {
                ["Name"] = "Test Product",
                ["Price"] = 99.99m
            }
        );

        // Assert - Verify SQL was generated correctly
        AssertQueryExecuted("INSERT INTO [Products]");
        AssertQueryCount(1);
    }

    [Fact]
    public void UpdateAsync_GeneratesSqlWithWhereClause()
    {
        // Arrange - Verify WHERE clause is present
        var updateSql = "UPDATE [Products] SET [Name] = @Name, [Price] = @Price WHERE [Id] = @Id";

        // Act
        MockConnection.ExecuteNonQueryAsync(
            updateSql,
            new Dictionary<string, object>
            {
                ["Id"] = 1,
                ["Name"] = "Updated",
                ["Price"] = 49.99m
            }
        );

        // Assert
        AssertQueryExecuted("UPDATE [Products]");
        AssertQueryExecuted("WHERE [Id]");
        AssertQueryCount(1);
    }

    [Fact]
    public void DeleteAsync_GeneratesSqlWithIdParameter()
    {
        // Arrange
        var deleteSql = "DELETE FROM [Products] WHERE [Id] = @Id";

        // Act
        MockConnection.ExecuteNonQueryAsync(deleteSql, new { Id = 1 });

        // Assert
        AssertQueryExecuted("DELETE FROM [Products]");
        AssertQueryExecuted("WHERE [Id]");
    }
}
```

## Next Steps

1. **Create the mock classes** (InMemoryDataStore, MockDatabaseConnection)
2. **Move unit tests** from Core.Tests.Disabled to Core.Tests
3. **Update unit tests** to remove Sqlite/SqlServer references
4. **Add mock-based tests** for generated code
5. **Keep integration tests** in provider-specific projects
6. **Keep example tests** in FrameworkUsage.Tests

## Benefits

? **Fast** - No database startup overhead  
? **Reliable** - No external dependencies  
? **Isolated** - Tests don't affect each other  
? **Debuggable** - Easy to inspect mock calls  
? **CI/CD Friendly** - No database containers needed  
? **Clear** - Separation of unit vs. integration tests  

## Files to Create

```
src/HighSpeedDAL.Core/Testing/
??? InMemoryDataStore.cs
??? MockDatabaseConnection.cs
??? MockDataReader.cs
??? TestDataBuilder.cs (helper)

tests/HighSpeedDAL.Core.Tests/
??? DalUnitTestBase.cs
??? GeneratedDalCodeTests.cs
??? AttributeParsingTests.cs (moved from Disabled)
??? PropertyGenerationTests.cs (moved from Disabled)
??? RetryPolicyTests.cs (moved from Disabled)
??? SqlGenerationTests.cs (new)
```
