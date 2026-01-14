# Unit Test Enhancement Checklist

## Current State Summary

### ? What's Working
- [x] FrameworkUsage.Tests (integration tests showing real patterns)
- [x] Sqlite.Tests (provider-specific tests)
- [x] SqlServer.Tests (provider-specific tests)
- [x] AdvancedCaching.Tests (component tests)
- [x] DataManagement.Tests (component tests)
- [x] PerformanceRegression.Tests (performance tests)

### ? What's Missing for True Unit Testing

#### Priority 1: Critical (Required for Database-Agnostic Unit Tests)

- [ ] **Create InMemoryDataStore class**
  - Location: `src/HighSpeedDAL.Core/Testing/InMemoryDataStore.cs`
  - Purpose: In-memory table storage for unit tests
  - Estimated effort: 2 hours
  - Impact: Enables all unit tests to run without database

- [ ] **Create MockDatabaseConnection class**
  - Location: `src/HighSpeedDAL.Core/Testing/MockDatabaseConnection.cs`
  - Purpose: Mock IDatabaseConnection implementation
  - Estimated effort: 3 hours
  - Impact: Enables testing generated DAL code without database
  - Key features:
    - Track executed SQL queries
    - Simulate INSERT/UPDATE/DELETE/SELECT
    - Return mock query results
    - Support parameter binding verification

- [ ] **Create MockDbConnectionFactory**
  - Location: `src/HighSpeedDAL.Core/Testing/MockDbConnectionFactory.cs`
  - Purpose: Factory for creating mock connections
  - Estimated effort: 1 hour
  - Impact: Simplifies test setup

- [ ] **Create DalUnitTestBase class**
  - Location: `tests/HighSpeedDAL.Core.Tests/DalUnitTestBase.cs`
  - Purpose: Base class for all DAL unit tests
  - Estimated effort: 2 hours
  - Impact: Standardizes test patterns across all unit tests
  - Key features:
    - Setup mock data store
    - Assertion helpers
    - Common entity configurations

#### Priority 2: High (Re-enable Existing Tests)

- [ ] **Restore HighSpeedDAL.Core.Tests project**
  - Remove empty directory
  - Recreate project file with proper references
  - Estimated effort: 1 hour

- [ ] **Move database-agnostic tests from Disabled**
  - [ ] DatabaseRetryPolicyTests.cs (? No changes needed)
  - [ ] RetryPolicyTests.cs (? No changes needed)
  - [ ] TableNamePluralizerTests.cs (? No changes needed)
  - [ ] PropertyAutoGeneratorTests.cs (? No changes needed)
  - [ ] AttributeTests.cs (? No changes needed)
  - [ ] CacheManagerTests.cs (?? May need mock updates)
  - Estimated effort: 1 hour
  - Impact: 6+ unit tests now passing without database

#### Priority 3: Medium (Update Remaining Tests)

- [ ] **Update CacheManagerTests.cs**
  - Remove SQL Server references
  - Use mock connection
  - Verify caching behavior with in-memory data
  - Estimated effort: 2 hours

- [ ] **Create GeneratedDalCodeTests.cs**
  - Test INSERT SQL generation
  - Test UPDATE SQL generation
  - Test DELETE SQL generation
  - Test SELECT SQL generation
  - Test parameter binding
  - Test auto-increment handling
  - Estimated effort: 4 hours
  - Impact: Validates generated code correctness without database

- [ ] **Create SqlGenerationTests.cs**
  - Test Sqlite SQL generation
  - Test SQL Server SQL generation
  - Test type mapping
  - Test null handling
  - Test default values
  - Estimated effort: 3 hours

- [ ] **Create CloningEdgeCaseTests.cs (if not exists)**
  - Test entity cloning with all property types
  - Test deep copy behavior
  - Test null values
  - Estimated effort: 2 hours

#### Priority 4: Lower (Restructure Tests)

- [ ] **Move database-coupled tests to provider projects**
  - [ ] EntityProcessingIntegrationTests.cs ? Sqlite.Tests/SqlServer.Tests
  - [ ] StagingTableManagerTests.cs ? DataManagement.Tests (already there?)
  - Estimated effort: 2 hours

- [ ] **Update FrameworkUsage.Tests documentation**
  - Mark as integration/example tests
  - Add note: "Use provider-specific tests for validation"
  - Add link to unit test patterns
  - Estimated effort: 1 hour

- [ ] **Create TestingGuidance.md**
  - Document difference between unit vs. integration tests
  - Show how to test without database
  - Show when database tests are needed
  - Provide test templates for custom entities
  - Estimated effort: 2 hours

## Specific Tests That Need Creation

### Unit Tests (No Database)

| Test Class | Location | Status | Effort |
|---|---|---|---|
| GeneratedDalCodeTests | Core.Tests | ? New | 4h |
| SqlGenerationTests | Core.Tests | ? New | 3h |
| ParameterBindingTests | Core.Tests | ? New | 2h |
| AttributeParsingTests | Core.Tests.Disabled | ? Move | 1h |
| PropertyAutoGeneratorTests | Core.Tests.Disabled | ? Move | 1h |
| RetryPolicyTests | Core.Tests.Disabled | ? Move | 1h |
| TableNamePluralizerTests | Core.Tests.Disabled | ? Move | 1h |
| CacheManagerTests (updated) | Core.Tests.Disabled | ? Update | 2h |

### Supporting Infrastructure

| Class | Location | Status | Effort |
|---|---|---|---|
| InMemoryDataStore | Core/Testing | ? New | 2h |
| MockDatabaseConnection | Core/Testing | ? New | 3h |
| MockDbConnectionFactory | Core/Testing | ? New | 1h |
| MockDataReader | Core/Testing | ? New | 1h |
| DalUnitTestBase | Core.Tests | ? New | 2h |
| TestDataBuilder | Core/Testing | ? New | 1h |

## Estimated Total Effort

- **Critical path** (minimum to get unit tests working): 12 hours
  1. InMemoryDataStore (2h)
  2. MockDatabaseConnection (3h)
  3. DalUnitTestBase (2h)
  4. Restore Core.Tests (1h)
  5. Move existing tests (1h)
  6. Update CacheManagerTests (2h)
  7. Create GeneratedDalCodeTests (4h) **OR** start with simpler tests

- **Full implementation** (complete unit test framework): 30 hours
  - All critical path items
  - All new test classes
  - All documentation
  - All test migrations

## Testing Strategy by Project

### HighSpeedDAL.Core.Tests (Database-AGNOSTIC Unit Tests) ?
**Goal**: Test framework logic without database
**Pattern**: Use mocks, InMemoryDataStore
**Examples**: 
- Test SQL generation (strings, not execution)
- Test attribute parsing
- Test caching logic
- Test entity cloning
- Test parameter binding
**Count**: 40-50 tests

### HighSpeedDAL.FrameworkUsage.Tests (Integration Examples) ?
**Goal**: Show developers how to use framework
**Pattern**: Use Sqlite in-memory, real database operations
**Examples**:
- Insert product and verify ID
- Update and select back
- Delete and verify gone
- Caching behavior
- Audit trail
**Count**: 10-15 tests

### HighSpeedDAL.Sqlite.Tests (Provider-Specific) ???
**Goal**: Validate Sqlite provider works correctly
**Pattern**: Real Sqlite database (file or in-memory)
**Examples**:
- Transaction handling
- Specific Sqlite features
- Performance with real data
- Concurrent access
**Count**: 20-30 tests

### HighSpeedDAL.SqlServer.Tests (Provider-Specific) ???
**Goal**: Validate SQL Server provider works correctly
**Pattern**: Real SQL Server database (local or Azure)
**Examples**:
- T-SQL specific features
- Transaction handling
- Performance with real data
- Security features
**Count**: 20-30 tests

## Success Criteria

? **Unit tests can run without any database**
? **All unit tests pass consistently**
? **Unit tests complete in < 30 seconds**
? **Clear separation: Unit vs. Integration tests**
? **Easy to add new entity tests using DalUnitTestBase**
? **Generated code correctness validated by unit tests**
? **SQL generation validated without execution**
? **Developer documentation for testing patterns**

## Questions to Consider

1. **Should we mock retry policies?**
   - Yes, for unit tests
   - No, test actual retry logic in Core.Tests

2. **Should we test caching with real cache implementations?**
   - Unit tests: Use mock data store + memory cache with IMemoryCache mock
   - Integration tests: Use real cache implementations

3. **Should we include async/await in unit tests?**
   - Yes, verify async patterns work correctly
   - Use `async Task` tests, not `void`

4. **Should we test error handling?**
   - Yes, extensively
   - Test null parameters, invalid IDs, duplicate inserts, etc.

5. **Should we have separate tests for each provider?**
   - Unit tests: One set (database-agnostic)
   - Integration tests: One per provider (Sqlite, SqlServer)
   - Example tests: Showcase with Sqlite

## Resources Needed

1. **Template for mock implementations** ? (provided in docs)
2. **Base class for tests** ? (provided in docs)
3. **Testing best practices guide** ? (needs creation)
4. **Example test for each pattern** ? (provided in docs)
5. **CI/CD configuration** ? (ensure unit tests run first)

## Risk Mitigation

| Risk | Mitigation |
|---|---|
| Mock doesn't accurately reflect real behavior | Include provider-specific integration tests |
| Tests become maintenance burden | Use code generation for test setup where possible |
| Tests are too slow | Keep mocks fast, separate long-running integration tests |
| Hard to debug failing tests | Use detailed assertion messages, track executed SQL |
| Tests couple to internal details | Test public API surface only, use abstraction layers |

## Next Steps (Prioritized)

1. **Review this checklist** with team (30 min)
2. **Create InMemoryDataStore** (2 hours)
3. **Create MockDatabaseConnection** (3 hours)
4. **Create DalUnitTestBase** (2 hours)
5. **Test with existing unit tests** (1 hour)
6. **Restore Core.Tests project** (1 hour)
7. **Move passing tests** (1 hour)
8. **Create GeneratedDalCodeTests** (4 hours)
9. **Update documentation** (2 hours)
10. **Review and iterate** (ongoing)

**Total: ~18 hours to achieve "database-agnostic unit tests working"**
