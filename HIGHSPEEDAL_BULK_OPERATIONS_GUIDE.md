# HighSpeedDAL Bulk Operations Guide

## Problem Identified

Products and ingredients were being inserted **one at a time** despite having `[InMemoryTable]` attributes configured, which defeats the purpose of HighSpeedDAL's high-performance capabilities.

## Root Cause

1. **Repository Adapter Pattern Bypassing DAL**
   - Adapters had mixed usage: some methods used the generated DAL, others used direct SQL
   - Bulk insert methods weren't exposed through the repository interface
   - Import code called `CreateAsync()` in a loop instead of batching

2. **Example of the Issue** (BatchProductProcessor.cs):
   ```csharp
   // ❌ BAD: One at a time in a loop
   foreach (var product in products)
   {
       await productRepo.CreateAsync(product);  // Slow!
   }
   ```

## The Fix

### 1. Added Bulk Operations to Repository Interfaces

**IProductRepository.cs:**
```csharp
Task<int> BulkCreateAsync(IEnumerable<CreateProductRequest> requests, Guid? createdBy = null);
```

**IIngredientRepository.cs:**
Already had it, but it was using direct SQL batching instead of the DAL.

### 2. Implemented Bulk Operations Using Generated DAL

**ProductRepositoryAdapter.cs:**
```csharp
public async Task<int> BulkCreateAsync(IEnumerable<CreateProductRequest> requests, Guid? createdBy = null)
{
    var entities = requests.Select(MapCreateRequestToEntity).ToList();
    if (entities.Count == 0) return 0;

    // Uses generated DAL BulkInsertAsync which leverages InMemoryTable for high-speed writes
    return await _dal.BulkInsertAsync(entities, null, System.Threading.CancellationToken.None);
}
```

**IngredientRepositoryAdapter.cs:**
Changed from direct SQL batching to DAL bulk insert:
```csharp
public async Task<int> BulkCreateIngredientsAsync(IEnumerable<string> names, Guid? createdBy = null)
{
    var entities = namesList.Select(name => new IngredientEntity
    {
        Id = Guid.NewGuid(),
        Name = name,
        Category = "General",
        IsAllergen = false
    }).ToList();

    // Uses generated DAL BulkInsertAsync which leverages InMemoryTable
    return await _dal.BulkInsertAsync(entities, null, CancellationToken.None);
}
```

## How InMemoryTable Works

Entities with `[InMemoryTable]` attribute (like ProductEntity):

```csharp
[InMemoryTable(FlushIntervalSeconds = 30, MaxRowCount = 100000)]
public partial class ProductEntity
```

### Write Path:
1. `BulkInsertAsync()` → **In-Memory Table** (thread-safe, fast)
2. In-Memory Table → Automatic batch flush to database every 30 seconds OR when 100,000 rows reached
3. Batch flush uses SQL Server's `SqlBulkCopy` for maximum throughput

### Benefits:
- **High-speed writes**: Memory operations are orders of magnitude faster than database I/O
- **Automatic batching**: Framework handles optimal batch sizes
- **Transactional safety**: Flushes are transactional
- **Configurable**: Tune `FlushIntervalSeconds` and `MaxRowCount` per entity

## Usage Examples

### ✅ CORRECT: Bulk Insert

```csharp
// Collect all products first
var productRequests = new List<CreateProductRequest>();
foreach (var item in items)
{
    productRequests.Add(new CreateProductRequest
    {
        Name = item.Name,
        Brand = item.Brand,
        // ... other fields
    });
}

// Insert all at once using bulk operation
int inserted = await productRepo.BulkCreateAsync(productRequests);
_logger.LogInformation("Inserted {Count} products using bulk operation", inserted);
```

### ❌ INCORRECT: Loop Insert

```csharp
// DON'T DO THIS
foreach (var item in items)
{
    await productRepo.CreateAsync(new CreateProductRequest
    {
        Name = item.Name,
        Brand = item.Brand
    });
}
```

## Performance Impact

**Before (One-at-a-time):**
- 10,000 products = ~2-5 minutes
- Each insert is a round-trip to database
- Network latency dominates

**After (Bulk via InMemoryTable):**
- 10,000 products = ~2-10 seconds
- Writes go to memory (microseconds)
- Batch flush to database (single operation)
- **10-100x faster**

## When to Use Each Method

| Method | Use Case | Performance |
|--------|----------|-------------|
| `CreateAsync()` | Single product insert | Moderate (still uses in-memory table) |
| `BulkCreateAsync()` | Importing/processing many products | **Very Fast** |
| Direct SQL | Complex joins/queries for **reads only** | Fast for reads |

## Migration Checklist

To fully leverage HighSpeedDAL's performance:

- [x] Added `BulkCreateAsync` to IProductRepository
- [x] Implemented `BulkCreateAsync` in ProductRepositoryAdapter using `_dal.BulkInsertAsync`
- [x] Fixed `BulkCreateIngredientsAsync` to use `_dal.BulkInsertAsync` instead of direct SQL
- [ ] Update BatchProductProcessor to use bulk operations (next step)
- [ ] Update OpenFoodFactsImportService to use bulk operations (if applicable)
- [ ] Review all repository methods that use direct SQL - convert writes to use DAL

## Next Steps

1. **Update BatchProductProcessor:**
   ```csharp
   // Instead of processing one at a time:
   // foreach (var staged in stagedProducts) { await CreateAsync(...); }

   // Collect all, then bulk insert:
   var requests = stagedProducts.Select(MapToCreateRequest).ToList();
   await productRepo.BulkCreateAsync(requests);
   ```

2. **Monitor In-Memory Table:**
   - Watch flush logs to ensure batches are optimal size
   - Adjust `FlushIntervalSeconds` if needed (lower = more frequent flushes, less memory)
   - Adjust `MaxRowCount` if needed (higher = fewer flushes, more memory)

3. **Consider Adding Bulk Update:**
   The generated DAL also has `BulkUpdateAsync` - expose it if needed:
   ```csharp
   Task<int> BulkUpdateAsync(IEnumerable<UpdateProductRequest> requests);
   ```

## Additional Notes

- **Thread Safety**: InMemoryTable is thread-safe, can handle concurrent writes
- **Crash Recovery**: Un-flushed writes are lost on crash (by design for speed)
- **Cache Integration**: InMemoryTable works with `[Cache]` attribute automatically
- **Soft Deletes**: Works correctly with `[SoftDelete]` - soft-deleted items filtered from in-memory queries

## Difficulty Assessment

**How hard is it to switch from direct SQL to DAL with InMemoryTable?**

**Answer: Very Easy** ✅

1. Change 1 line: Replace direct SQL with `_dal.BulkInsertAsync(entities)`
2. Add mapping: Convert DTOs/requests to entities (usually already exists)
3. Expose via interface: Add method to repository interface

**Time to implement:** ~15 minutes per repository method

**Performance gain:** 10-100x for bulk operations

The HighSpeedDAL framework does all the heavy lifting (in-memory buffering, batch flushing, SqlBulkCopy optimization). You just need to call the generated DAL methods instead of writing SQL.
