# Feature Request: Expression-Based Query Support

**Created:** 2026-01-19
**Updated:** 2026-01-20
**Status:** Phase 1 Complete, Phase 2 Proposed
**Priority:** High

## Problem Statement

Currently, the generated DAL classes only provide:
- `GetByIdAsync(id)` - single entity by primary key
- `GetByIdsAsync(ids)` - multiple entities by primary keys
- `GetAllAsync()` - all entities
- `GetByNameAsync(name)` - only for `[ReferenceTable]` entities (unique lookup)

There is no way to query by indexed properties (e.g., `GetByCategoryAsync`) without:
1. Writing raw SQL (bypasses cache, doesn't work with in-memory tables)
2. Using `GetAllAsync()` + in-memory LINQ filter (inefficient for large tables)

## Requirements

The solution must:
1. **Work across all storage modes** - Database, cache, and in-memory tables
2. **Leverage indexes** - Use database indexes when querying DB, use indexed lookups for in-memory
3. **Support caching** - Integrate with the existing cache layer
4. **Handle compound conditions** - Support multiple WHERE conditions (e.g., `Category = X AND IsActive = true`)
5. **Be type-safe** - Compile-time checking of property names and types

## Proposed API Options

### Option 1: Expression-Based (Recommended)

```csharp
// Single condition
var results = await _dal.GetWhereAsync(x => x.Category == "Dairy");

// Multiple conditions
var results = await _dal.GetWhereAsync(x => x.Category == "Dairy" && x.IsCommonAllergen == true);

// With ordering
var results = await _dal.Query()
    .Where(x => x.Category == category)
    .OrderBy(x => x.Name)
    .Take(100)
    .ToListAsync();
```

**Implementation approach:**
- Expression visitor translates `Expression<Func<T, bool>>` to:
  - SQL WHERE clause for database queries
  - `Func<T, bool>` predicate for in-memory/cache filtering
- Detect indexed properties and optimize query path
- For `[ReferenceTable]` + `[Cache]` entities, always filter from cache

### Option 2: Named/Indexed Queries (Generated)

Automatically generate query methods for all `[Index]` properties:

```csharp
// Entity definition
[Index]
public string? Category { get; set; }

// Generated method
public async Task<List<T>> GetByCategoryAsync(string? category, CancellationToken ct = default)
```

**Pros:** Simple, explicit, optimized per-index
**Cons:** Doesn't handle compound queries, combinatorial explosion with multiple indexes

### Option 3: Named Query Attribute

Define queries at the entity level:

```csharp
[NamedQuery("ActiveByCategory", "Category=@Category AND IsDeleted=0")]
[NamedQuery("ByBrandAndCategory", "Brand=@Brand AND Category=@Category")]
public partial class ProductEntity { }

// Generated methods
public async Task<List<ProductEntity>> GetActiveByCategoryAsync(string category, CancellationToken ct = default);
public async Task<List<ProductEntity>> GetByBrandAndCategoryAsync(string brand, string category, CancellationToken ct = default);
```

**Pros:** Explicit about what queries are supported, can optimize each
**Cons:** Verbose, requires anticipating all query patterns

## Recommended Approach

**Phase 1: Named Index Queries (Quick Win)**
- Generate `GetBy{PropertyName}Async` for all `[Index]` properties
- Returns `List<T>` for non-unique indexes, `T?` for unique indexes
- Implementation checks storage mode:
  - In-memory: Filter L0 cache or call `GetAllAsync()` + filter
  - Database: Execute indexed SQL query
  - Cache: Check cache first, fall back to DB

**Phase 2: Expression-Based Queries (Full Solution)**
- Add `GetWhereAsync(Expression<Func<T, bool>> predicate)` to base DAL
- Expression visitor handles translation to SQL or predicate
- Optimize for indexed properties when expression matches index pattern

## Technical Considerations

### Soft Delete Integration
All generated queries must include `AND [IsDeleted] = 0` for entities with `[SoftDelete]`.

### Cache Integration
For cached entities:
1. Check if query can be satisfied from cache
2. If yes, filter cached data with compiled predicate
3. If no (e.g., partial cache), query database and update cache

### In-Memory Table Integration
For `[InMemoryTable]` entities:
1. Query the L0 cache (ConcurrentDictionary) directly
2. Use indexed lookups if available
3. Never hit database for pure in-memory tables

### Compound Index Support
For queries spanning multiple indexed columns, consider:
- Detecting common patterns and generating compound index DDL
- Class-level attribute: `[CompositeIndex("Category", "IsActive")]`

## Files to Modify

1. `src/HighSpeedDAL.SourceGenerators/Generation/DalClassGenerator.Part1.cs` - Make class `partial`
2. `src/HighSpeedDAL.SourceGenerators/Generation/DalClassGenerator.Part2.cs` or new `Part3.cs` - Generate index query methods
3. `src/HighSpeedDAL.Core/Base/DalOperationsBase.cs` - Add `GetWhereAsync` base implementation
4. `src/HighSpeedDAL.Core/Expressions/` (new) - Expression visitor for SQL translation

## Phase 1: Named Query Attribute (Immediate Implementation)

### Design

Add a `[NamedQuery]` attribute that generates optimized query methods:

```csharp
// Entity definition
[NamedQuery("ByCategory", nameof(Category))]
[NamedQuery("ByCategoryActive", nameof(Category), nameof(IsDeleted))]
[NamedQuery("ByProductIdAndIngredientId", nameof(ProductId), nameof(IngredientId))]
public partial class ProductIngredientEntity { }
```

### Generated Code

```csharp
// SQL constants
private const string SQL_GET_BY_CATEGORY = @"SELECT * FROM [ProductIngredient]
WHERE [Category] = @Category AND [IsDeleted] = 0;";

private const string SQL_GET_BY_CATEGORY_ACTIVE = @"SELECT * FROM [ProductIngredient]
WHERE [Category] = @Category AND [IsDeleted] = @IsDeleted;";

// Generated methods
public async Task<List<ProductIngredientEntity>> GetByCategoryAsync(
    string? category,
    CancellationToken cancellationToken = default)
{
    // Check cache first (if [Cache] is enabled)
    var cacheKey = $"ProductIngredient:ByCategory:{category}";
    if (_cacheManager != null)
    {
        var cached = await _cacheManager.GetAsync(cacheKey, cancellationToken);
        if (cached != null) return cached;
    }

    // Execute query
    var parameters = new Dictionary<string, object>
    {
        { "Category", category ?? (object)DBNull.Value }
    };
    var results = await ExecuteQueryAsync(SQL_GET_BY_CATEGORY, MapFromReader, parameters, null, cancellationToken);

    // Cache results
    if (_cacheManager != null)
    {
        await _cacheManager.SetAsync(cacheKey, results, cancellationToken);
    }

    return results;
}
```

### Attribute Definition

```csharp
namespace HighSpeedDAL.Core.Attributes;

/// <summary>
/// Generates a query method based on the specified property names.
/// The method will be named Get{Name}Async and will query by the specified properties.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class NamedQueryAttribute : Attribute
{
    /// <summary>
    /// The name of the query (used in method name: Get{Name}Async)
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The property names to include in the WHERE clause
    /// </summary>
    public string[] Properties { get; }

    /// <summary>
    /// If true, the query returns a single result (FirstOrDefault)
    /// If false (default), returns a List
    /// </summary>
    public bool IsSingle { get; set; }

    /// <summary>
    /// If true, automatically adds IsDeleted = 0 filter for [SoftDelete] entities
    /// Default: true
    /// </summary>
    public bool AutoFilterDeleted { get; set; } = true;

    /// <summary>
    /// If true, results are cached using the entity's cache strategy
    /// Default: true (if entity has [Cache])
    /// </summary>
    public bool EnableCache { get; set; } = true;

    public NamedQueryAttribute(string name, params string[] properties)
    {
        Name = name;
        Properties = properties;
    }
}
```

### Usage Examples

```csharp
// Single property query
[NamedQuery("ByCategory", nameof(Category))]
// Generates: GetByCategoryAsync(string? category)
// SQL: WHERE [Category] = @Category AND [IsDeleted] = 0

// Multi-property query
[NamedQuery("ByProductIdAndStatus", nameof(ProductId), nameof(Status))]
// Generates: GetByProductIdAndStatusAsync(Guid productId, string? status)
// SQL: WHERE [ProductId] = @ProductId AND [Status] = @Status AND [IsDeleted] = 0

// Single result query
[NamedQuery("ByBarcode", nameof(Barcode), IsSingle = true)]
// Generates: GetByBarcodeAsync(string? barcode) -> returns T?
// SQL: SELECT TOP 1 ... WHERE [Barcode] = @Barcode AND [IsDeleted] = 0

// Without auto-delete filter
[NamedQuery("AllByProductId", nameof(ProductId), AutoFilterDeleted = false)]
// Generates: GetAllByProductIdAsync(Guid productId)
// SQL: WHERE [ProductId] = @ProductId (no IsDeleted filter)

// With explicit IsDeleted parameter
[NamedQuery("ByProductIdWithDeleted", nameof(ProductId), nameof(IsDeleted))]
// Generates: GetByProductIdWithDeletedAsync(Guid productId, bool isDeleted)
// SQL: WHERE [ProductId] = @ProductId AND [IsDeleted] = @IsDeleted
```

### Implementation Files

1. **New file**: `src/HighSpeedDAL.Core/Attributes/NamedQueryAttribute.cs`
2. **Modify**: `src/HighSpeedDAL.SourceGenerators/Parsing/EntityParser.cs`
   - Parse `[NamedQuery]` attributes
   - Add `NamedQueries` list to `EntityMetadata`
3. **Modify**: `src/HighSpeedDAL.SourceGenerators/Generation/SqlGenerator.cs`
   - Generate SQL constants for named queries
4. **Modify**: `src/HighSpeedDAL.SourceGenerators/Generation/DalClassGenerator.Part2.cs` or new `Part3.cs`
   - Generate query methods with cache integration

### In-Memory Support

For entities with `[InMemoryTable]` or `[Cache]`, the generated methods should:
1. Check L0 cache (ConcurrentDictionary) first
2. Filter in memory using compiled predicates
3. Fall back to database only if cache miss

```csharp
public async Task<List<ProductIngredientEntity>> GetByCategoryAsync(
    string? category,
    CancellationToken cancellationToken = default)
{
    // For [InMemoryTable] entities, filter L0 cache
    if (_inMemoryTable != null && _inMemoryTable.IsLoaded)
    {
        return _inMemoryTable.GetAll()
            .Where(e => e.Category == category && !e.IsDeleted)
            .ToList();
    }

    // Fall back to database query
    // ...
}
```

## References

- Current `[Index]` attribute: `src/HighSpeedDAL.Core/Attributes/IndexAttribute.cs`
- Entity metadata parsing: `src/HighSpeedDAL.SourceGenerators/Parsing/EntityParser.cs`
- SQL generation: `src/HighSpeedDAL.SourceGenerators/Generation/SqlGenerator.cs`

---

## Phase 1 Implementation Summary (Completed 2026-01-20)

### Files Created/Modified

1. **`src/HighSpeedDAL.Core/Attributes/NamedQueryAttribute.cs`** (NEW)
   - Attribute class for defining named queries on entities
   - Properties: `Name`, `Properties[]`, `IsSingle`, `AutoFilterDeleted`, `EnableCache`

2. **`src/HighSpeedDAL.SourceGenerators/Models/EntityMetadata.cs`** (MODIFIED)
   - Added `NamedQueryMetadata` class
   - Added `NamedQueries` list property to `EntityMetadata`

3. **`src/HighSpeedDAL.SourceGenerators/Parsing/EntityParser.cs`** (MODIFIED)
   - Added `ParseNamedQueryAttribute` method
   - Added NamedQuery case to attribute switch statement
   - Updated `CloneMetadata` to copy NamedQueries

4. **`src/HighSpeedDAL.SourceGenerators/Generation/SqlGenerator.cs`** (MODIFIED)
   - Added `GenerateNamedQuerySql` method
   - Added `GenerateNamedQueryActiveSql` helper method

5. **`src/HighSpeedDAL.SourceGenerators/Generation/DalClassGenerator.Part1.cs`** (MODIFIED)
   - Added call to `GenerateNamedQuerySqlConstants`
   - Added call to `GenerateNamedQueryMethods`

6. **`src/HighSpeedDAL.SourceGenerators/Generation/DalClassGenerator.Part4.cs`** (NEW)
   - `GenerateNamedQuerySqlConstants` - generates SQL constant strings
   - `GenerateNamedQueryMethods` - generates query methods
   - `GenerateNamedQueryMethod` - generates a single query method
   - `GenerateNamedQueryActiveHelperMethod` - generates `GetActive*` helper for soft-delete
   - Helper methods for type normalization and naming conventions

### Generated Output Example

For entity:
```csharp
[NamedQuery("ByCategory", nameof(Category))]
[NamedQuery("ByIsCommonAllergen", nameof(IsCommonAllergen))]
[NamedQuery("ByCategoryAndAllergen", nameof(Category), nameof(IsCommonAllergen))]
public partial class IngredientEntity { }
```

Generated SQL:
```sql
-- SQL_GET_BY_CATEGORY
SELECT * FROM [Ingredient] WHERE [Category] = @Category AND [IsDeleted] = 0;

-- SQL_GET_BY_IS_COMMON_ALLERGEN
SELECT * FROM [Ingredient] WHERE [IsCommonAllergen] = @IsCommonAllergen AND [IsDeleted] = 0;

-- SQL_GET_BY_CATEGORY_AND_ALLERGEN
SELECT * FROM [Ingredient] WHERE [Category] = @Category AND [IsCommonAllergen] = @IsCommonAllergen AND [IsDeleted] = 0;
```

Generated Methods:
```csharp
// Query methods
public Task<List<IngredientEntity>> GetByCategoryAsync(string? category, CancellationToken ct = default);
public Task<List<IngredientEntity>> GetByIsCommonAllergenAsync(bool isCommonAllergen, CancellationToken ct = default);
public Task<List<IngredientEntity>> GetByCategoryAndAllergenAsync(string? category, bool isCommonAllergen, CancellationToken ct = default);

// Active helpers (alias for soft-delete clarity)
public Task<List<IngredientEntity>> GetActiveByCategoryAsync(string? category, CancellationToken ct = default);
public Task<List<IngredientEntity>> GetActiveByIsCommonAllergenAsync(bool isCommonAllergen, CancellationToken ct = default);
public Task<List<IngredientEntity>> GetActiveByCategoryAndAllergenAsync(string? category, bool isCommonAllergen, CancellationToken ct = default);
```

### Features Implemented
- Multi-property WHERE clauses
- Automatic soft-delete filtering (`AutoFilterDeleted = true` by default)
- Single result queries (`IsSingle = true` generates `SELECT TOP 1`)
- Type-safe parameter handling
- SQLite and SQL Server support
- `GetActive*` helper methods for clarity on soft-delete entities

### Next Steps (Phase 2)
- Expression-based queries: `GetWhereAsync(Expression<Func<T, bool>> predicate)`
- In-memory/cache-first query execution for cached entities
- Indexed property detection and optimization
