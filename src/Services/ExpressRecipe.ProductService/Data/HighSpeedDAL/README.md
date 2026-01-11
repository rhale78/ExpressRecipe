# HighSpeed DAL for Products and Ingredients

This directory contains high-performance Data Access Layer (DAL) implementations for Products and Ingredients, following patterns from the [HighSpeedDAL framework](https://github.com/rhale78/HighSpeedDAL).

## Philosophy

The HighSpeed DAL follows these principles:
- **Minimal Manual SQL**: Delegate to base class operations rather than writing SQL directly
- **Base Class Reuse**: Leverage `DalOperationsBase` for all common operations
- **Simple Mapping**: Keep entity-to-database mappings straightforward
- **Smart Caching**: Cache-first reads with automatic invalidation

## Architecture

```
┌──────────────────────────────────────┐
│         Application Layer            │
└────────────────┬─────────────────────┘
                 │
┌────────────────┴─────────────────────┐
│    ProductDal / IngredientDal        │
│    (Minimal ~150 lines each)         │
│    - Maps entities                   │
│    - Handles caching                 │
│    - Delegates to base               │
└────────────────┬─────────────────────┘
                 │
┌────────────────┴─────────────────────┐
│      DalOperationsBase               │
│    - ExecuteQueryAsync               │
│    - ExecuteNonQueryAsync            │
│    - BulkInsertAsync                 │
│    - Retry logic                     │
└──────────────────────────────────────┘
```

## Components

### ProductDal (~156 lines)
Minimal DAL for Product entities.

**Operations:**
- `GetByIdAsync` - Single product with caching
- `GetAllAsync` - All products
- `CreateAsync` - Insert single product
- `UpdateAsync` - Update with cache invalidation
- `DeleteAsync` - Soft delete with cache invalidation
- `BulkInsertAsync` - Bulk insert using SqlBulkCopy

**Usage:**
```csharp
// Single operations
var product = await productDal.GetByIdAsync(id);
var newId = await productDal.CreateAsync(productDto);

// Bulk operations
var products = GetProductsToImport();
int count = await productDal.BulkInsertAsync(products);
```

### IngredientDal (~212 lines)
Minimal DAL for Ingredient entities.

**Operations:**
- `GetByIdAsync` - Single ingredient with caching
- `GetAllAsync` - All ingredients
- `CreateAsync` - Insert single ingredient
- `UpdateAsync` - Update with cache invalidation
- `DeleteAsync` - Soft delete with cache invalidation
- `BulkInsertAsync` - Bulk insert using SqlBulkCopy
- `GetIdsByNamesAsync` - Batch name-to-ID lookup with caching

**Usage:**
```csharp
// Single operations
var ingredient = await ingredientDal.GetByIdAsync(id);

// Name lookup (cached)
var names = new[] { "flour", "sugar", "eggs" };
var idsByName = await ingredientDal.GetIdsByNamesAsync(names);

// Bulk insert
int count = await ingredientDal.BulkInsertAsync(ingredients);
```

## Base Classes

### DalOperationsBase<TEntity, TConnection>
Provides core DAL operations that derived classes use.

**Key Methods:**
- `ExecuteQueryAsync<T>` - Execute SELECT queries with mapping
- `ExecuteNonQueryAsync` - Execute INSERT/UPDATE/DELETE
- `BulkInsertAsync` - SqlBulkCopy for bulk inserts
- `ExecuteWithRetryAsync` - Automatic retry for transient errors

### DatabaseConnectionBase
Connection lifecycle management.

**Features:**
- Configuration-based connection strings
- Transaction support
- Async connection creation
- Structured logging

## Performance

### Bulk Operations
- **Batch Size**: 1000 rows per batch
- **Timeout**: 300 seconds
- **Performance**: 5-20x faster than individual inserts

### Caching
- **Products**: 15min memory / 1hr distributed
- **Ingredients**: 30min memory / 2hr distributed
- **Name Lookups**: 12hr memory / 24hr distributed
- **Typical Hit Rate**: >95% after warmup

### Retry Logic
- **Max Retries**: 3 attempts
- **Backoff**: Exponential (100ms, 200ms, 400ms)
- **Transient Errors**: Deadlocks, timeouts, service unavailable

## Integration

### Service Registration
```csharp
// Register connections
builder.Services.AddSingleton<ProductConnection>();

// Register DALs
builder.Services.AddScoped<IProductDal, ProductDal>();
builder.Services.AddScoped<IIngredientDal, IngredientDal>();
```

### Usage in Services
```csharp
public class ProductImportService
{
    private readonly IProductDal _productDal;
    
    public ProductImportService(IProductDal productDal)
    {
        _productDal = productDal;
    }
    
    public async Task ImportAsync(List<ProductDto> products)
    {
        // Bulk insert for optimal performance
        int count = await _productDal.BulkInsertAsync(products);
        _logger.LogInformation("Imported {Count} products", count);
    }
}
```

## Key Differences from Manual SQL

**Before (Manual SQL):**
- 500+ lines of code per DAL
- Explicit SQL for every operation
- Manual reader mapping for each query
- Complex batch operations

**After (HighSpeed DAL Pattern):**
- ~150-200 lines per DAL
- Delegate to base class methods
- Simple mapping functions
- Base class handles complexity

## See Also

- [HIGHSPEED_DAL_INTEGRATION_PLAN.md](../../../../HIGHSPEED_DAL_INTEGRATION_PLAN.md) - Complete architecture guide
- [HighSpeedDAL Framework](https://github.com/rhale78/HighSpeedDAL) - Original framework
