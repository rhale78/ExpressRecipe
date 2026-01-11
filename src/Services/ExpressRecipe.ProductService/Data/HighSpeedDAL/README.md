# HighSpeed DAL for Products and Ingredients

This directory contains high-performance Data Access Layer (DAL) implementations for Products and Ingredients, following patterns from the [HighSpeedDAL framework](https://github.com/rhale78/HighSpeedDAL).

## Architecture

The HighSpeed DAL provides:
- **Bulk Operations**: SqlBulkCopy for maximum insert/update performance
- **Intelligent Caching**: Cache-first lookups with automatic cache population
- **Retry Logic**: Automatic retry with exponential backoff for transient database errors
- **Batch Reads**: Optimized queries for fetching multiple records

## Components

### ProductDal
High-speed data access for Product entities.

**Key Features:**
- Bulk insert up to 1000 products per batch
- Cache-aware batch reads by IDs or barcodes
- Automatic image loading and mapping
- Smart cache invalidation

**Usage:**
```csharp
// Dependency injection setup
services.AddScoped<ProductConnection>();
services.AddScoped<IProductDal, ProductDal>();

// Bulk insert
var products = new List<CreateProductRequest> { /* ... */ };
int inserted = await productDal.BulkInsertAsync(products);

// Batch read with caching
var ids = new[] { id1, id2, id3 };
var products = await productDal.GetByIdsAsync(ids);
```

### IngredientDal
High-speed data access for Ingredient entities.

**Key Features:**
- Bulk insert ingredients with automatic deduplication
- Batch lookup by names with 12-hour cache
- Cache-aware batch reads by IDs
- Smart cache invalidation

**Usage:**
```csharp
// Dependency injection setup
services.AddScoped<IIngredientDal, IngredientDal>();

// Bulk insert
var ingredients = new List<CreateIngredientRequest> { /* ... */ };
int inserted = await ingredientDal.BulkInsertAsync(ingredients);

// Batch name-to-ID lookup (cached)
var names = new[] { "flour", "sugar", "salt" };
var idsByName = await ingredientDal.GetIdsByNamesAsync(names);
```

## Base Classes

### DalOperationsBase<TEntity, TConnection>
Base class providing core DAL operations.

**Features:**
- Transient error retry (deadlocks, timeouts)
- Exponential backoff (100ms, 200ms, 400ms)
- Bulk insert via SqlBulkCopy
- Query execution with parameter binding

### DatabaseConnectionBase
Base class for database connection management.

**Features:**
- Configuration-based connection strings
- Transaction support
- Async connection lifecycle
- Structured logging

## Performance Characteristics

### Bulk Insert
- **Batch Size**: 1000 rows
- **Timeout**: 300 seconds
- **Expected Performance**: 5-20x faster than individual inserts

### Batch Reads
- **Cache Hit Latency**: <1ms (memory cache)
- **Cache Miss Latency**: 10-50ms (database query)
- **Typical Cache Hit Rate**: >95% after warmup

### Caching Strategy
- **Products**: 15min memory / 1hr distributed cache
- **Ingredients**: 30min memory / 2hr distributed cache
- **Name Lookups**: 12hr memory / 24hr distributed cache

## Integration

To integrate HighSpeed DAL into your services:

1. **Register connections**:
```csharp
builder.Services.AddSingleton<ProductConnection>();
```

2. **Register DAL implementations**:
```csharp
builder.Services.AddScoped<IProductDal, ProductDal>();
builder.Services.AddScoped<IIngredientDal, IngredientDal>();
```

3. **Use in services**:
```csharp
public class ProductImportService
{
    private readonly IProductDal _productDal;
    
    public ProductImportService(IProductDal productDal)
    {
        _productDal = productDal;
    }
    
    public async Task ImportProductsAsync(List<CreateProductRequest> products)
    {
        // Use bulk insert for optimal performance
        int count = await _productDal.BulkInsertAsync(products);
        _logger.LogInformation("Bulk inserted {Count} products", count);
    }
}
```

## Error Handling

The HighSpeed DAL automatically retries transient errors:
- **Deadlocks** (SQL error 1205)
- **Timeouts** (SQL error -2)
- **Service unavailable** (SQL errors 40197, 40501, 40613, 49918)

Non-transient errors are thrown immediately for proper error handling at the application layer.

## See Also

- [HIGHSPEED_DAL_INTEGRATION_PLAN.md](../../../../HIGHSPEED_DAL_INTEGRATION_PLAN.md) - Complete architecture and migration strategy
- [HighSpeedDAL Framework](https://github.com/rhale78/HighSpeedDAL) - Original framework inspiration
