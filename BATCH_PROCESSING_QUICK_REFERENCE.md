# Batch Processing Quick Reference

## Overview
Optimized product and ingredient processing using TPL Dataflow with bulk database operations.

## Key Components

### 1. BatchProductProcessor
**Location:** `src/Services/ExpressRecipe.ProductService/Services/BatchProductProcessor.cs`

**Constructor:**
```csharp
new BatchProductProcessor(
    logger,
    maxDegreeOfParallelism: 4,  // Parallel processors
    batchSize: 100,              // Items per batch
    bufferSize: 500              // Buffer capacity
)
```

**Usage:**
```csharp
var result = await batchProcessor.ProcessStagedProductsAsync(
    stagingRepo,
    productRepo,
    ingredientRepo,
    ingredientParser,
    cancellationToken);

Console.WriteLine($"Success: {result.SuccessCount}, Failed: {result.FailureCount}");
```

### 2. BulkOperationsHelper
**Location:** `src/ExpressRecipe.Data.Common/BulkOperationsHelper.cs`

**Bulk Upsert:**
```csharp
await BulkOperationsHelper.BulkUpsertAsync(
    connectionString,
    items,
    "TargetTable",
    "#TempTable",
    new[] { "KeyColumn1", "KeyColumn2" },
    mapToDataRowFunc,
    dataTableStructure,
    cancellationToken);
```

**Bulk Insert:**
```csharp
await BulkOperationsHelper.BulkInsertWithDeduplicationAsync(
    connectionString,
    items,
    "TargetTable",
    new[] { "UniqueColumn" },
    mapToDataRowFunc,
    dataTableStructure,
    cancellationToken);
```

### 3. IngredientRepositoryExtensions
**Location:** `src/Services/ExpressRecipe.ProductService/Data/IngredientRepositoryExtensions.cs`

**Bulk Upsert Ingredients:**
```csharp
var createRequests = ingredientNames.Select(name => new CreateIngredientRequest
{
    Name = name,
    Category = "General"
});

await ingredientRepo.BulkUpsertIngredientsAsync(
    createRequests,
    connectionString,
    cancellationToken);
```

**Bulk Get Ingredient IDs:**
```csharp
var idMap = await ingredientRepo.BulkGetIngredientIdsByNamesAsync(
    ingredientNames,
    connectionString,
    cancellationToken);

var ingredientId = idMap["Flour"]; // Guid
```

**Bulk Insert Product-Ingredient Links:**
```csharp
var links = new List<ProductIngredientBulkInsert>
{
    new() { ProductId = productId, IngredientId = ingredientId, OrderIndex = 0 }
};

await ingredientRepo.BulkInsertProductIngredientsAsync(
    links,
    connectionString,
    cancellationToken);
```

## Configuration

**appsettings.json:**
```json
{
  "ProductImport": {
    "AutoImport": true,
    "AutoProcessing": true,
    "MaxParallelism": 4,
    "BatchSize": 100,
    "BufferSize": 500
  }
}
```

## Performance Tuning

### Conservative (Default)
```json
{
  "MaxParallelism": 4,
  "BatchSize": 100,
  "BufferSize": 500
}
```
- Good for: 2-4 core servers, 4-8GB RAM
- Throughput: ~500-1000 products/minute

### Balanced
```json
{
  "MaxParallelism": 6,
  "BatchSize": 150,
  "BufferSize": 750
}
```
- Good for: 4-8 core servers, 8-16GB RAM
- Throughput: ~1000-2000 products/minute

### Aggressive
```json
{
  "MaxParallelism": 8,
  "BatchSize": 200,
  "BufferSize": 1000
}
```
- Good for: 8+ core servers, 16GB+ RAM
- Throughput: ~2000-5000 products/minute

### Resource-Constrained
```json
{
  "MaxParallelism": 2,
  "BatchSize": 50,
  "BufferSize": 200
}
```
- Good for: Limited resources, shared hosting
- Throughput: ~200-500 products/minute

## Pipeline Stages

1. **Fetch** ? Retrieves pending products from staging
2. **Batch** ? Groups items into configurable batches
3. **Pre-Create** ? Bulk creates missing ingredients
4. **Unbatch** ? Distributes to parallel processors
5. **Process** ? Creates products and links ingredients

## Error Handling

### Handled Automatically:
- Duplicate ingredient creation (SQL 2627/2601)
- Existing products (skipped via barcode check)
- Individual product failures (logged, processing continues)
- Pre-creation failures (falls back to individual creation)

### Requires Attention:
- Database connection failures
- Transaction deadlocks (retry logic recommended)
- Memory exhaustion (reduce batch/buffer sizes)

## Monitoring

**Key Metrics to Track:**
- Processing throughput (products/minute)
- Success vs. failure rate
- Memory usage during processing
- Database CPU and I/O
- Pipeline stage latencies

**Logging:**
```csharp
// Progress updates every 10 items
_logger.LogInformation("Processing progress: {Success} completed, {Failed} failed");

// Batch completion
_logger.LogInformation("Batch processing complete: {Success} succeeded, {Failed} failed");
```

## Common Issues

### Issue: High Memory Usage
**Solution:** Reduce `BatchSize` and `BufferSize`

### Issue: Slow Processing
**Solution:** Increase `MaxParallelism` (if CPU available)

### Issue: Database Deadlocks
**Solution:** Reduce `MaxParallelism` or implement retry logic

### Issue: Duplicate Ingredients
**Solution:** Pre-creation handles this automatically via deduplication

## Best Practices

1. **Start Conservative:** Use default settings, then tune based on metrics
2. **Monitor Resources:** Watch CPU, memory, and database load
3. **Batch Size vs. Parallelism:** Balance between throughput and resource usage
4. **Buffer Capacity:** Should be 5-10x batch size
5. **Cancellation:** Always pass cancellation token for graceful shutdown

## Examples

### Example 1: Process 1000 Products
```csharp
// Will automatically batch and process with optimal settings
var result = await batchProcessor.ProcessStagedProductsAsync(...);
// Expected time: 2-4 minutes (vs. 20-30 minutes sequential)
```

### Example 2: Custom Batch Processing
```csharp
var processor = new BatchProductProcessor(
    logger,
    maxDegreeOfParallelism: 8,  // High throughput
    batchSize: 200,              // Large batches
    bufferSize: 1000             // Deep buffer
);
```

### Example 3: Bulk Ingredient Creation
```csharp
var ingredients = new[] { "Flour", "Sugar", "Eggs", "Butter" };
var requests = ingredients.Select(name => new CreateIngredientRequest { Name = name });
await ingredientRepo.BulkUpsertIngredientsAsync(requests, connectionString);
// Inserts all at once, updates if already exist
```

## Related Documentation
- Full details: `BATCH_PROCESSING_OPTIMIZATION_COMPLETE.md`
- Migration runner: `src/ExpressRecipe.Data.Common/MigrationRunner.cs`
- Worker implementation: `src/Services/ExpressRecipe.ProductService/Workers/ProductProcessingWorker.cs`
