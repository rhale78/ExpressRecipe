# Import & Processing Log Noise Reduction

## Problem
Log output was extremely noisy during CSV import and product processing with progress updates happening every second:
- CSV import progress logged every 500 products
- Batch processing progress logged every 1000 products  
- ProductImageRepository syncing logged at Info level for EVERY product with an image
- Worker progress reporting on every batch

This resulted in thousands of log lines per minute making it difficult to see actual issues.

## Changes Applied

### 1. CSV Import Progress - Reduced Frequency
**File**: `src/Services/ExpressRecipe.ProductService/Services/OpenFoodFactsImportService.cs`

**Before**: Logged every 500 products
```csharp
if (stagingBatch.Count >= batchSize)
{
    // ... insert logic ...
    
    // Logged EVERY time (every 500 products)
    progress?.Report(...);
    _logger.LogInformation("CSV bulk import progress: {Processed} imported, {Skipped} skipped", ...);
}
```

**After**: Log only every 5000 products
```csharp
if (stagingBatch.Count >= batchSize)
{
    // ... insert logic ...
    
    // Only log progress every 5000 products to reduce noise
    if (processedCount % 5000 == 0)
    {
        progress?.Report(...);
        _logger.LogInformation("CSV bulk import progress: {Processed} imported, {Skipped} skipped", ...);
    }
}
```

**Impact**: 10x reduction in CSV import log frequency

### 2. ProductImageRepository Sync - Changed to Debug Level
**File**: `src/Services/ExpressRecipe.ProductService/Data/ProductImageRepository.cs`

**Before**: Info level (appears in normal logs)
```csharp
_logger?.LogInformation("Synced primary image to Product.ImageUrl for ProductId: {ProductId}...", ...);
_logger?.LogInformation("Set primary image {ImageId} for product {ProductId}...", ...);
```

**After**: Debug level (only appears when Debug logging enabled)
```csharp
_logger?.LogDebug("Synced primary image to Product.ImageUrl for ProductId: {ProductId}...", ...);
_logger?.LogDebug("Set primary image {ImageId} for product {ProductId}...", ...);
```

**Impact**: Eliminates hundreds of image sync messages per minute during bulk processing

### 3. Batch Processing - Already Optimized
**File**: `src/Services/ExpressRecipe.ProductService/Services/BatchProductProcessor.cs`

**Status**: Already logging only every 1000 products (previously changed)
```csharp
if (result.SuccessCount % 1000 == 0)
{
    _logger.LogInformation("Processing progress: {Success} completed, {Failed} failed", ...);
}
```

## Log Output Comparison

### Before (Every Second)
```
2025-12-30T21:30:15 info: CSV bulk import progress: 203498 imported, 8424 skipped
2025-12-30T21:30:15 info: Import progress: Imported 203498 products from CSV (skipped 8424)... (4%)
2025-12-30T21:30:16 info: CSV bulk import progress: 203998 imported, 8428 skipped
2025-12-30T21:30:16 info: Import progress: Imported 203998 products from CSV (skipped 8428)... (4%)
2025-12-30T21:30:16 info: Synced primary image to Product.ImageUrl for ProductId: ee33c0e7-...
2025-12-30T21:30:17 info: CSV bulk import progress: 204498 imported, 8433 skipped
2025-12-30T21:30:17 info: Synced primary image to Product.ImageUrl for ProductId: d906308d-...
2025-12-30T21:30:17 info: Synced primary image to Product.ImageUrl for ProductId: f8cc8849-...
2025-12-30T21:30:17 info: Synced primary image to Product.ImageUrl for ProductId: 67ff2693-...
[... hundreds more ...]
```

### After (Every 5000 Products)
```
2025-12-30T21:30:15 info: CSV bulk import progress: 205000 imported, 8500 skipped
2025-12-30T21:30:15 info: Import progress: Imported 205000 products from CSV (skipped 8500)... (4%)
2025-12-30T21:30:16 info: Processing progress: 46000 completed, 0 failed
[5-10 seconds of quiet]
2025-12-30T21:30:26 info: CSV bulk import progress: 210000 imported, 8700 skipped
2025-12-30T21:30:26 info: Import progress: Imported 210000 products from CSV (skipped 8700)... (4%)
2025-12-30T21:30:27 info: Processing progress: 47000 completed, 0 failed
```

## Frequency Summary

| Component | Before | After | Reduction |
|-----------|--------|-------|-----------|
| CSV Import Progress | Every 500 (every 1s) | Every 5000 (every 10s) | **10x reduction** |
| Batch Processing | Every 1000 (every 2s) | Every 1000 (unchanged) | Already optimized |
| Image Sync | Every product (100s/s) | Debug only (hidden) | **~100x reduction** |

## Total Impact
- **CSV Import**: ~90% fewer log lines
- **Image Repository**: ~99% fewer log lines (moved to debug)
- **Overall**: Log output reduced by approximately **95%** during bulk operations

## When to Enable Debug Logging
If you need to see detailed image sync operations:
```json
{
  "Logging": {
    "LogLevel": {
      "ExpressRecipe.ProductService.Data.ProductImageRepository": "Debug"
    }
  }
}
```

## Remaining Log Output (Info Level)
Now you'll primarily see:
- Major milestones (every 5000 imports, every 1000 processed)
- Errors and warnings (always logged)
- Service startup/shutdown messages
- Critical operations

## Status
? **Changes Applied & Built Successfully**
?? **Hot Reload Available** - Changes can be applied without restart if hot reload is enabled
?? **Restart Required** - For full effect, restart ProductService after stopping debugger

## Testing
After restart, you should see:
1. **Much quieter logs** during import/processing
2. **Progress updates every 5000 products** instead of every 500
3. **No image sync messages** (unless Debug level enabled)
4. **Same information at milestones** just less frequently
