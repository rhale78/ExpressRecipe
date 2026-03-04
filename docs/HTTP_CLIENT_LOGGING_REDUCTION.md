# HTTP Client Logging Reduction - Summary

## Problem
Services were flooding logs with verbose HTTP client messages for every single call:
- "Calling ProductService for barcode..."
- "Calling IngredientService for..."
- "Added authorization header to GET /api/..."
- Redundant start/stop messages
- Individual call logging instead of batch summaries

## Solution Implemented

### 1. **Reduced Individual Call Logging**

#### ProductServiceClient
**Before**:
```
[INFO] Calling ProductService for barcode 012345678905. BaseAddress: http://productservice
[DEBUG] Product with barcode 012345678905 not found in ProductService
[INFO] Calling ProductService for barcode 012345678906. BaseAddress: http://productservice
... (repeated 100,000 times)
```

**After**:
```
[INFO] [ProductService] Bulk lookup: 100 barcodes -> 87 products in 234ms
```

#### Changes:
- ✅ Removed "Calling ProductService" log from single lookups
- ✅ Changed 404 logs from Debug to no log (expected case)
- ✅ Added timing and summary to bulk operations
- ✅ Consolidated error messages

### 2. **AuthenticationDelegatingHandler**

**Before**:
```
[DEBUG] Added authorization header to GET /api/products/barcode/012345678905
[DEBUG] Added authorization header to GET /api/products/barcode/012345678906
... (repeated for every HTTP call)
```

**After**:
```
(Only warns if token is missing)
```

#### Changes:
- ✅ Changed "Auth added" from Debug → **Trace** (disabled by default)
- ✅ Kept "No token" as Warning (actual issue)

### 3. **API Client Logs (Source-Generated)**

**Before**:
```
[DEBUG] [ApiClient] GET /api/ingredient/name/salt -> 200 in 45ms
[DEBUG] [ApiClient] GET /api/ingredient/name/pepper -> 200 in 38ms
... (repeated for every API call)
```

**After**:
```
[INFO] [ApiClient] Bulk: POST /api/ingredient/bulk/lookup | 100 items -> 200 in 234ms
(Individual calls moved to Trace level)
```

#### Changes:
- ✅ Individual API calls: Debug → **Trace**
- ✅ Bulk operations: stays at **Information** with improved format
- ✅ Auth logs: Debug → **Trace**
- ✅ Errors/retries: stay at **Warning/Error**

### 4. **Batch Summary Logging**

Added consolidated batch logs with timing:

#### IngredientServiceClient
```csharp
_logger?.LogInformation("[IngredientService] Bulk lookup: {Requested} names -> {Found} ingredients in {Ms}ms",
    names.Count, result.Count, sw.ElapsedMilliseconds);
```

#### ProductServiceClient
```csharp
_logger.LogInformation("[ProductService] Bulk lookup: {Requested} barcodes -> {Found} products in {Ms}ms",
    barcodeList.Count, result.Count, sw.ElapsedMilliseconds);
```

#### ProductRepository
```csharp
_logger?.LogDebug("[Products] DB bulk lookup: {Requested} barcodes -> {Found} products in {Ms}ms",
    barcodeList.Count, result.Count, sw.ElapsedMilliseconds);
```

### 5. **Configuration Changes**

Updated appsettings to suppress noisy loggers:

#### Global Config (`Config/appsettings.Global.json`)
```json
{
  "Logging": {
    "LogLevel": {
      "System.Net.Http.HttpClient": "Warning",
      "ExpressRecipe.Client.Shared": "Information",
      "ExpressRecipe.Shared.Services.AuthenticationDelegatingHandler": "Warning"
    }
  }
}
```

#### Service-Specific (`appsettings.json` in each service)
- PriceService ✅
- ProductService ✅
- IngredientService ✅
- RecipeService ✅
- GroceryStoreLocationService ✅

## Log Level Strategy

| Logger | Level | Rationale |
|--------|-------|-----------|
| **System.Net.Http.HttpClient** | Warning | Too verbose, only log errors |
| **AuthenticationDelegatingHandler** | Warning | Only log missing tokens |
| **Individual API calls** | Trace | Available for deep debugging |
| **Bulk operations** | Information | Important for monitoring |
| **Errors** | Error/Warning | Always visible |
| **Repository DB calls** | Debug | Internal implementation detail |

## Example Log Output

### Before (100 product lookups)
```
[INFO] Calling ProductService for barcode 012345678905. BaseAddress: http://productservice
[DEBUG] Added authorization header to GET /api/products/barcode/012345678905
[DEBUG] Product with barcode 012345678905 not found in ProductService
[INFO] Calling ProductService for barcode 012345678906. BaseAddress: http://productservice
[DEBUG] Added authorization header to GET /api/products/barcode/012345678906
... (300+ log lines)
```

### After (100 product lookups in 2 batches)
```
[INFO] [ProductService] Bulk lookup: 50 barcodes -> 42 products in 187ms
[INFO] [ProductService] Bulk lookup: 50 barcodes -> 38 products in 165ms
[INFO] [Products] Bulk barcode lookup: 100 barcodes -> 80 products in 352ms
```

**Result**: 300+ lines → 3 lines (99% reduction)

## Files Modified

### Services
- `src/Services/ExpressRecipe.PriceService/Services/ProductServiceClient.cs`
- `src/Services/ExpressRecipe.ProductService/Controllers/ProductsController.cs`
- `src/Services/ExpressRecipe.ProductService/Data/ProductRepository.cs`
- `src/ExpressRecipe.Client.Shared/Services/IngredientServiceClient.cs`
- `src/ExpressRecipe.Shared/Services/AuthenticationDelegatingHandler.cs`

### Logging
- `src/ExpressRecipe.Client.Shared/Logging/ApiClientLogs.cs`

### Configuration
- `Config/appsettings.Global.json`
- `src/Services/ExpressRecipe.PriceService/appsettings.json`
- `src/Services/ExpressRecipe.ProductService/appsettings.json`
- `src/Services/ExpressRecipe.IngredientService/appsettings.json`
- `src/Services/ExpressRecipe.RecipeService/appsettings.json`
- `src/Services/ExpressRecipe.GroceryStoreLocationService/appsettings.json`

## Testing

Run services and check logs - should see:
- ✅ Batch summaries with timing
- ✅ First/last item samples
- ✅ Error/warning messages
- ❌ No "Calling..." for every request
- ❌ No "Added authorization header" spam
- ❌ No individual 404 messages for expected cases

## Enabling Detailed Logging (Debugging)

If you need verbose logs for troubleshooting:

```json
{
  "Logging": {
    "LogLevel": {
      "ExpressRecipe.Client.Shared": "Trace",
      "ExpressRecipe.Shared.Services.AuthenticationDelegatingHandler": "Trace",
      "System.Net.Http.HttpClient": "Information"
    }
  }
}
```

## Benefits

1. **Reduced log volume** by ~99% for bulk operations
2. **Easier monitoring** - batch summaries instead of individual calls
3. **Better performance metrics** - timing included in logs
4. **Cleaner logs** - focus on important events
5. **Still debuggable** - can enable Trace level when needed

## Build Status

✅ All changes compile successfully
