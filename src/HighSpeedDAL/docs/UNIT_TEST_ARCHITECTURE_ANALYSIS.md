# Unit Test Architecture Analysis: Database-Agnostic Testing

## Current State

### Test Projects Structure
```
? HighSpeedDAL.FrameworkUsage.Tests       - Integration tests with Sqlite
??  HighSpeedDAL.Core.Tests               - Empty (should contain unit tests)
? HighSpeedDAL.Core.Tests.Disabled       - Contains unit tests but disabled
? HighSpeedDAL.AdvancedCaching.Tests     - Component tests
? HighSpeedDAL.DataManagement.Tests      - Component tests
? HighSpeedDAL.Sqlite.Tests              - Database-specific tests
? HighSpeedDAL.SqlServer.Tests           - Database-specific tests
? HighSpeedDAL.PerformanceRegression.Tests - Performance tests
```

## Issues Identified

### 1. **Test Project Organization Problem**
- `HighSpeedDAL.Core.Tests` directory exists but is empty
- `HighSpeedDAL.Core.Tests.Disabled` has valuable unit tests that should be active
- **Impact**: Unit tests are not being run; they're hidden in the "Disabled" folder

### 2. **Unit Tests Currently Disabled**
Found in `HighSpeedDAL.Core.Tests.Disabled`:
- `DatabaseRetryPolicyTests.cs` - Tests retry logic (DATABASE-AGNOSTIC ?)
- `RetryPolicyTests.cs` - Tests retry policies (DATABASE-AGNOSTIC ?)
- `TableNamePluralizerTests.cs` - Tests name pluralization (DATABASE-AGNOSTIC ?)
- `PropertyAutoGeneratorTests.cs` - Tests property generation (DATABASE-AGNOSTIC ?)
- `AttributeTests.cs` - Tests attribute parsing (DATABASE-AGNOSTIC ?)
- `CacheManagerTests.cs` - Tests caching (PARTIALLY DATABASE-AGNOSTIC ??)
- `EntityProcessingIntegrationTests.cs` - Integration test (DATABASE-COUPLED ?)
- `StagingTableManagerTests.cs` - Staging tests (DATABASE-COUPLED ?)

### 3. **Framework Usage Tests Tight Coupling**
`BasicCrudFrameworkTests.cs` dependencies:
- Direct Sqlite reference: `using HighSpeedDAL.Sqlite;`
- Direct Sqlite types: `SqliteConnection`, `SqliteConnectionFactory`
- Direct table creation: Manual SQL in test setup
- **Impact**: Tests are integration tests, not unit tests; they test Sqlite, not the framework

### 4. **Missing Database-Agnostic Test Abstractions**
There are no:
- Mock `IDatabaseConnection` implementations
- Mock `IDbConnectionFactory` implementations
- Mock DAL base classes
- Fake/stub database operations
- **Impact**: Cannot unit test generated code without a real database

### 5. **No Separation of Unit vs. Integration Tests**
Current structure mixes:
- Unit tests (retry policy, attribute parsing)
- Integration tests (full CRUD with database)
- Component tests (caching, data management)
- **Impact**: Difficult to identify what should be tested without database

## What's Needed

### Phase 1: Restore Core Unit Tests
```
1. Rename HighSpeedDAL.Core.Tests.Disabled ? HighSpeedDAL.Core.Tests.Disabled.OLD
2. Recreate HighSpeedDAL.Core.Tests project
3. Move database-agnostic unit tests from Disabled folder
4. Fix database-coupled tests (remove Sqlite/SqlServer references)
5. Add mocks for database operations where needed
```

### Phase 2: Create Mock/Stub Framework
```
Needed Mock Classes:
??? MockDatabaseConnection : IDatabaseConnection
?   ??? MockInsert()
?   ??? MockUpdate()
?   ??? MockDelete()
?   ??? MockSelect()
?   ??? MockSelectScalar()
?
??? MockDbConnectionFactory : IDbConnectionFactory
?   ??? CreateConnection()
?
??? MockRetryPolicy : IAsyncPolicy
?   ??? ExecuteAsync()
?
??? InMemoryDataStore
    ??? Tables (Dictionary<string, List<Dictionary<string, object>>>)
    ??? Insert(table, row)
    ??? Update(table, row)
    ??? Delete(table, id)
    ??? Query(table, predicate)
```

### Phase 3: Create Database-Agnostic Test Base Classes
```csharp
public abstract class DalUnitTestBase
{
    protected IInMemoryDataStore DataStore { get; }
    protected MockDatabaseConnection MockConnection { get; }
    protected TDal CreateDal<TEntity, TDal>() 
        where TDal : DalBase<TEntity>
    {
        // Create DAL with mocks, no database needed
    }
}

public class DalGeneratedCodeTests : DalUnitTestBase
{
    // Test generated code without Sqlite/SqlServer
    [Fact]
    public async Task InsertAsync_GeneratesCorrectSql()
    {
        var entity = new Product { Name = "Test" };
        var dal = CreateDal<Product, ProductDal>();
        
        // DAL writes to mock data store
        await dal.InsertAsync(entity);
        
        // Assert written to in-memory data
        DataStore.GetTable("Products").Should().HaveCount(1);
    }
}
```

### Phase 4: Separate Integration Tests
Move Sqlite/SqlServer specific tests to their projects:
- `HighSpeedDAL.Sqlite.Tests` - All tests with Sqlite
- `HighSpeedDAL.SqlServer.Tests` - All tests with SQL Server
- Keep `HighSpeedDAL.FrameworkUsage.Tests` for example usage only

## Recommended Test Hierarchy

```
HighSpeedDAL.Core.Tests (Database-AGNOSTIC Unit Tests)
??? Attribute Parsing Tests
??? Property Generation Tests
??? Retry Policy Tests
??? Cache Manager Tests (with mocks)
??? Table Name Pluralization Tests
??? SQL Type Inference Tests
??? Generated DAL Code Tests (with mock database)
    ??? INSERT tests
    ??? UPDATE tests
    ??? DELETE tests
    ??? SELECT tests
    ??? Bulk operation tests

HighSpeedDAL.FrameworkUsage.Tests (Integration Examples)
??? BasicCrudFrameworkTests (shows real usage patterns)
??? CachingExampleTests
??? AuditingExampleTests

HighSpeedDAL.Sqlite.Tests (Provider-Specific Integration)
??? Sqlite-specific CRUD tests
??? Transaction tests
??? Performance tests

HighSpeedDAL.SqlServer.Tests (Provider-Specific Integration)
??? SQL Server-specific CRUD tests
??? T-SQL specific features
??? Performance tests
```

## Key Principles

1. **Unit Tests** = No database, no I/O, pure logic
   - Test attribute parsing
   - Test code generation logic
   - Test caching logic with mocks
   - Test SQL generation (strings, no execution)

2. **Integration Tests** = With database, real operations
   - Test actual Sqlite behavior
   - Test actual SQL Server behavior
   - Test transaction handling
   - Test connection pooling

3. **Example/Framework Tests** = Show patterns
   - Demonstrate framework usage
   - Copy-paste examples for developers
   - Show best practices

## What Should Be Database-Agnostic ?

- ? Attribute parsing
- ? Property auto-generation
- ? Retry policy logic
- ? Cache management
- ? SQL string generation (not execution)
- ? Entity cloning
- ? Data validation
- ? In-memory table operations

## What Requires Database ?

- ? Actual INSERT/UPDATE/DELETE execution
- ? Connection pooling
- ? Transaction handling
- ? Database-specific SQL features
- ? Performance testing
- ? Concurrent access patterns

## Action Items

### Immediate (Critical)
1. [ ] Re-enable unit tests from Core.Tests.Disabled
2. [ ] Create mock database implementation
3. [ ] Remove Sqlite/SqlServer references from unit tests
4. [ ] Get unit tests passing without database

### Short-term (Important)
5. [ ] Create DalUnitTestBase for generated code testing
6. [ ] Add tests for SQL generation logic
7. [ ] Add tests for parameter binding
8. [ ] Verify caching works with mocks

### Medium-term (Nice-to-have)
9. [ ] Create test helper factories
10. [ ] Document testing patterns for users
11. [ ] Create test templates for custom entities
12. [ ] Add performance benchmarks with mocks

### Long-term (Polish)
13. [ ] Create test result publishing
14. [ ] Set up CI/CD with coverage tracking
15. [ ] Create testing guide in docs
