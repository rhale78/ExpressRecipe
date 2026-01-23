# Data Source Logging Guide

## Overview

Track whether data is coming from **Cache**, **Memory**, or **Database** with centralized logging that's easy to parse and monitor.

**File**: `DataSourceLogger.cs` in `ExpressRecipe.Data.Common`

---

## Quick Start

### Import the Logger

```csharp
using ExpressRecipe.Data.Common;
```

### Log Each Data Operation

```csharp
public async Task<ProductDto?> GetByIdAsync(Guid id)
{
    // Try cache
    var cached = await _cache.GetAsync(id);
    if (cached != null)
    {
        DataSourceLogger.LogCacheHit(_logger, "Product", "L1", id);
        return cached;
    }

    // Database fallback
    DataSourceLogger.LogCacheMiss(_logger, "Product", id);
    DataSourceLogger.LogDatabaseRead(_logger, "Product", "GetByIdAsync", id);

    var product = await FetchFromDatabase(id);

    // Cache result
    if (product != null)
    {
        await _cache.SetAsync(id, product);
    }

    return product;
}
```

---

## Log Methods

### Cache Operations

#### Cache Hit
```csharp
DataSourceLogger.LogCacheHit(logger, tableName, cacheLevel, identifier);

// Output:
// [INF] DATA_SOURCE=CACHE | CacheLevel=L1 | Table=Product | ID: 550e8400...
```

**Parameters**:
- `logger`: ILogger instance
- `tableName`: "Product", "Ingredient", etc.
- `cacheLevel`: "L1", "L2", "Memory"
- `identifier`: (optional) ID or key

#### Cache Miss
```csharp
DataSourceLogger.LogCacheMiss(logger, tableName, identifier);

// Output:
// [DBG] DATA_SOURCE=CACHE | Status=MISS | Table=Product | Fallback=DATABASE | ID: 550e8400...
```

**Parameters**:
- `logger`: ILogger instance
- `tableName`: "Product", "Ingredient", etc.
- `identifier`: (optional) ID or key

### Database Operations

#### Read from Database
```csharp
DataSourceLogger.LogDatabaseRead(logger, tableName, operation, identifier, rowCount);

// Output:
// [INF] DATA_SOURCE=DATABASE | Operation=READ | Table=Product | Method=GetByIdAsync | ID: 550e8400... | Rows: 1
```

**Parameters**:
- `logger`: ILogger instance
- `tableName`: "Product", "Ingredient", etc.
- `operation`: "GetByIdAsync", "GetAllAsync", "GetByCategoryAsync"
- `identifier`: (optional) ID or query parameter
- `rowCount`: (optional) Number of rows returned

#### Write to Database
```csharp
DataSourceLogger.LogDatabaseWrite(logger, tableName, operation, identifier, rowsAffected);

// Output:
// [INF] DATA_SOURCE=DATABASE | Operation=WRITE | Table=Product | Method=InsertAsync | ID: 550e8400... | RowsAffected: 1
```

**Parameters**:
- `logger`: ILogger instance
- `tableName`: "Product", "Ingredient", etc.
- `operation`: "InsertAsync", "UpdateAsync", "DeleteAsync", "BulkInsertAsync"
- `identifier`: (optional) ID being modified
- `rowsAffected`: Number of rows affected (default: 1)

### Memory Operations (Future)

#### Read from In-Memory Table
```csharp
DataSourceLogger.LogMemoryRead(logger, tableName, operation, identifier, rowCount);

// Output:
// [INF] DATA_SOURCE=MEMORY | Operation=READ | Table=Ingredient | Method=GetByIdAsync | ID: 550e8400... | Rows: 1
```

#### Write to In-Memory Table
```csharp
DataSourceLogger.LogMemoryWrite(logger, tableName, operation, identifier, rowsAffected);

// Output:
// [INF] DATA_SOURCE=MEMORY | Operation=WRITE | Table=Ingredient | Method=InsertAsync | ID: 550e8400... | RowsAffected: 1
```

### Configuration Logging

#### Log Table Configuration at Startup
```csharp
DataSourceLogger.LogTableConfiguration(logger, tableName, hasMemory, hasCache, cacheStrategy);

// Output:
// [INF] TABLE_CONFIG | Table=Ingredient | DataSources=Cache[TwoLayer] => InMemory => Database[SqlServer] | Fallback=>Database
```

**Parameters**:
- `logger`: ILogger instance
- `tableName`: Table being configured
- `hasMemory`: Whether in-memory table is enabled
- `hasCache`: Whether cache is enabled
- `cacheStrategy`: "TwoLayer", "Memory", "Distributed", "None"

### Diagnostics

#### Log Summary Statistics
```csharp
DataSourceLogger.LogDataSourceSummary(logger, tableName, cacheHits, cacheMisses, dbReads, dbWrites);

// Output:
// [INF] DATA_SOURCE_SUMMARY | Table=Product | CacheHits=1500 | CacheMisses=25 | CacheHitRate=98.4% | DbReads=25 | DbWrites=100
```

**Parameters**:
- `logger`: ILogger instance
- `tableName`: Table name
- `cacheHits`: Number of cache hits
- `cacheMisses`: Number of cache misses
- `databaseReads`: Number of DB reads
- `databaseWrites`: Number of DB writes

---

## Log Format

All logs follow this structured format:
```
[LEVEL] DATA_SOURCE={SOURCE} | [KEY1]={VALUE1} | [KEY2]={VALUE2} | ...
```

### Examples

**Cache Hit**:
```
[INF] DATA_SOURCE=CACHE | CacheLevel=L1 | Table=Product | ID: 550e8400-e29b-41d4-a716-446655440000
```

**Cache Miss, then Database Read**:
```
[DBG] DATA_SOURCE=CACHE | Status=MISS | Table=Product | Fallback=DATABASE | ID: 550e8400...
[INF] DATA_SOURCE=DATABASE | Operation=READ | Table=Product | Method=GetByIdAsync | ID: 550e8400... | Rows: 1
```

**Batch Write**:
```
[INF] DATA_SOURCE=DATABASE | Operation=WRITE | Table=Product | Method=BulkInsertAsync | RowsAffected: 1000
```

**Summary**:
```
[INF] DATA_SOURCE_SUMMARY | Table=Product | CacheHits=1500 | CacheMisses=25 | CacheHitRate=98.4% | DbReads=25 | DbWrites=100
```

---

## Implementation Patterns

### Pattern 1: Repository with Cache

```csharp
public class ProductRepository
{
    private readonly ILogger<ProductRepository> _logger;
    private readonly IProductCache _cache;
    private readonly ProductDal _dal;

    public async Task<Product?> GetByIdAsync(Guid id)
    {
        // Try cache first
        var cached = await _cache.GetAsync(id);
        if (cached != null)
        {
            DataSourceLogger.LogCacheHit(_logger, "Product", "L1", id);
            return cached;
        }

        // Log cache miss and database attempt
        DataSourceLogger.LogCacheMiss(_logger, "Product", id);
        DataSourceLogger.LogDatabaseRead(_logger, "Product", "GetByIdAsync", id);

        // Fetch from database
        var product = await _dal.GetByIdAsync(id);

        // Cache result
        if (product != null)
        {
            await _cache.SetAsync(id, product);
        }

        return product;
    }

    public async Task<int> BulkInsertAsync(List<Product> products)
    {
        DataSourceLogger.LogDatabaseWrite(_logger, "Product", "BulkInsertAsync",
            rowsAffected: products.Count);

        return await _dal.BulkInsertAsync(products);
    }
}
```

### Pattern 2: Batch Operation with Tracking

```csharp
public async Task<BatchUpdateSummary> UpdateProductsAsync(List<Product> updates)
{
    var tracker = new BatchUpdateTracker("ProductUpdate", _logger);
    long dbReads = 0;
    long cacheHits = 0;

    foreach (var product in updates)
    {
        // Try cache
        var original = await _cache.GetAsync(product.Id);
        if (original != null)
        {
            DataSourceLogger.LogCacheHit(_logger, "Product", "L1", product.Id);
            cacheHits++;
        }
        else
        {
            // Database
            DataSourceLogger.LogCacheMiss(_logger, "Product", product.Id);
            DataSourceLogger.LogDatabaseRead(_logger, "Product", "GetByIdAsync", product.Id);
            original = await _dal.GetByIdAsync(product.Id);
            dbReads++;
        }

        // Update if changed
        if (original != null && HasChanges(original, product))
        {
            DataSourceLogger.LogDatabaseWrite(_logger, "Product", "UpdateAsync", product.Id);
            await _dal.UpdateAsync(product);
            tracker.RecordSuccess();
        }
        else
        {
            tracker.RecordSkipped();
        }
    }

    // Log summary
    DataSourceLogger.LogDataSourceSummary(_logger, "Product", cacheHits, updates.Count - cacheHits, dbReads, tracker.SuccessfulUpdates);

    return tracker.Report();
}
```

### Pattern 3: Multi-source Lookup

```csharp
public async Task<List<Ingredient>> SearchAsync(string searchTerm)
{
    long cacheHits = 0;
    long cacheEmptyResults = 0;

    // Try to get all from cache
    var allCached = await _cache.GetAllAsync();
    if (allCached != null && allCached.Count > 0)
    {
        DataSourceLogger.LogCacheHit(_logger, "Ingredient", "L1_FULL");
        cacheHits++;

        var results = allCached.Where(i => i.Name.Contains(searchTerm)).ToList();
        if (results.Any())
        {
            return results;
        }

        cacheEmptyResults++;
    }

    // Cache miss or empty - go to database
    DataSourceLogger.LogCacheMiss(_logger, "Ingredient", null);
    DataSourceLogger.LogDatabaseRead(_logger, "Ingredient", "SearchAsync", null, null);

    var dbResults = await _dal.GetByNamesAsync(searchTerm);

    // Update cache
    if (dbResults.Any())
    {
        await _cache.SetAllAsync(dbResults);
    }

    DataSourceLogger.LogDataSourceSummary(_logger, "Ingredient", cacheHits, cacheEmptyResults, 1, 0);

    return dbResults;
}
```

---

## Parsing Logs for Analysis

### Query Cache Hit Rate
```bash
# Count cache hits and misses
grep "DATA_SOURCE=CACHE" logs.txt | wc -l      # Total cache operations
grep "CacheLevel=" logs.txt | wc -l             # Cache hits
grep "Status=MISS" logs.txt | wc -l             # Cache misses

# Calculate hit rate
awk 'BEGIN { hits=0; total=0 }
  /CacheLevel=L1/ { hits++ }
  /DATA_SOURCE=CACHE/ { total++ }
  END { printf "Hit Rate: %.1f%%\n", (hits/total)*100 }' logs.txt
```

### Find Most Accessed Tables
```bash
grep "DATA_SOURCE=" logs.txt | sed 's/.*Table=//;s/ .*//' | sort | uniq -c | sort -rn
```

### Monitor Database Load
```bash
grep "DATA_SOURCE=DATABASE.*Operation=READ" logs.txt | wc -l   # Read count
grep "DATA_SOURCE=DATABASE.*Operation=WRITE" logs.txt | wc -l  # Write count
```

### Track Specific Entity
```bash
grep "Table=Product.*ID: 550e8400-e29b" logs.txt    # All operations for entity
grep "Table=Product.*METHOD=BulkInsertAsync" logs.txt # Bulk operations
```

---

## Configuration Recommendations

### Log Levels

- **Information (INF)**: Cache hits, database operations, configuration
- **Debug (DBG)**: Cache misses, detailed operation info
- **Warning (WRN)**: Unexpected data sources, slow operations
- **Error (ERR)**: Failed operations, missing data

### Example appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "ExpressRecipe.Data.Common.DataSourceLogger": "Information",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  }
}
```

---

## Troubleshooting

### Problem: Everything going to database, no cache hits
**Solution**:
1. Check cache initialization
2. Verify cache isn't disabled
3. Check logs: grep "CACHE.*MISS" logs.txt

### Problem: In-memory operations not showing up
**Solution**:
1. In-memory features are NOT YET ENABLED (per verification report)
2. When enabled, will see "DATA_SOURCE=MEMORY" logs
3. Currently only Cache + Database are active

### Problem: Too many logs
**Solution**:
1. Set log level to "Warning" or higher
2. Filter logs by table: grep "Table=Product" logs.txt
3. Use DEBUG level only for troubleshooting

---

## Best Practices

1. ✅ **Always log cache misses** - Helps identify performance issues
2. ✅ **Log batch operation counts** - Understand volume
3. ✅ **Use LogDataSourceSummary** - Get aggregate statistics
4. ✅ **Include entity IDs** - Debug specific records
5. ❌ **Don't log sensitive data** - Filter personally identifiable info

---

## Example Log Session

```
[INF] TABLE_CONFIG | Table=Product | DataSources=Cache[TwoLayer] => Database[SqlServer] | Fallback=>Database
[INF] TABLE_CONFIG | Table=Ingredient | DataSources=Cache[TwoLayer] => Database[SqlServer] | Fallback=>Database

--- User requests product ---
[DBG] DATA_SOURCE=CACHE | Status=MISS | Table=Product | Fallback=DATABASE | ID: 550e8400-e29b-41d4-a716-446655440000
[INF] DATA_SOURCE=DATABASE | Operation=READ | Table=Product | Method=GetByIdAsync | ID: 550e8400-e29b-41d4-a716-446655440000 | Rows: 1

--- User requests same product again (cache hit) ---
[INF] DATA_SOURCE=CACHE | CacheLevel=L1 | Table=Product | ID: 550e8400-e29b-41d4-a716-446655440000

--- Batch update operation ---
[INF] DATA_SOURCE=DATABASE | Operation=WRITE | Table=Product | Method=BulkUpdateAsync | RowsAffected: 50

--- End of session summary ---
[INF] DATA_SOURCE_SUMMARY | Table=Product | CacheHits=150 | CacheMisses=5 | CacheHitRate=96.8% | DbReads=5 | DbWrites=50
[INF] DATA_SOURCE_SUMMARY | Table=Ingredient | CacheHits=2000 | CacheMisses=1 | CacheHitRate=99.9% | DbReads=1 | DbWrites=0
```

---

## Integration Steps

1. **Add DataSourceLogger to your repository**:
   ```csharp
   using ExpressRecipe.Data.Common;
   ```

2. **Log cache operations**:
   ```csharp
   DataSourceLogger.LogCacheHit(_logger, "TableName", "L1", id);
   ```

3. **Log database operations**:
   ```csharp
   DataSourceLogger.LogDatabaseRead(_logger, "TableName", "MethodName", id);
   ```

4. **Log configuration at startup**:
   ```csharp
   DataSourceLogger.LogTableConfiguration(_logger, "TableName", hasMemory: false, hasCache: true, "TwoLayer");
   ```

5. **Monitor logs and analyze**:
   ```bash
   grep "DATA_SOURCE=" logs.txt | grep "Table=Product" | head -20
   ```

---

**Result**: Clear visibility into data flow (Cache vs Database) with structured, parseable logs that help identify performance bottlenecks and cache effectiveness.
