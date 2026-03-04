# Price Service Batching Improvements

## Overview

This implementation adds batched dataflow processing to improve performance and reduce strain on dependent services during price data imports.

## Components

### 1. **BatchProductLookupService** (`Services/BatchProductLookupService.cs`)

**Purpose**: Batches product lookups to ProductService to reduce HTTP overhead and service strain.

**Features**:
- Uses TPL Dataflow `BatchBlock` to group barcode lookups
- Configurable batch size (default: 100)
- Configurable batch timeout (default: 500ms)
- In-memory cache with TTL (default: 30 minutes)
- Parallel batch processing (default: 4 concurrent batches)

**Configuration** (appsettings.json):
```json
{
  "PriceService": {
    "ProductLookup": {
      "BatchSize": 100,
      "BatchTimeoutMs": 500,
      "CacheMinutes": 30,
      "MaxParallelBatches": 4
    }
  }
}
```

**Usage**:
```csharp
// Single lookup (automatically batched)
var product = await batchProductLookup.GetProductByBarcodeAsync("012345678905");

// Bulk lookup
var products = await batchProductLookup.GetProductsByBarcodesAsync(barcodes);
```

### 2. **BatchPriceInsertService** (`Services/BatchPriceInsertService.cs`)

**Purpose**: Batches price insert operations to improve database throughput.

**Features**:
- Uses TPL Dataflow `BatchBlock` to group price inserts
- Configurable batch size (default: 1000)
- Configurable batch timeout (default: 2 seconds)
- Parallel batch processing (default: 2 concurrent batches)
- Queue statistics and monitoring

**Configuration** (appsettings.json):
```json
{
  "PriceService": {
    "PriceBatch": {
      "BatchSize": 1000,
      "BatchTimeoutMs": 2000,
      "MaxParallelBatches": 2
    }
  }
}
```

**Usage**:
```csharp
// Queue a single price (automatically batched)
await batchPriceInsert.QueuePriceAsync(priceRequest);

// Flush pending batches
await batchPriceInsert.FlushAsync();

// Graceful shutdown
await batchPriceInsert.ShutdownAsync();
```

### 3. **DataflowOpenPricesImportService** (`Services/DataflowOpenPricesImportService.cs`)

**Purpose**: Complete dataflow pipeline for price imports with batched product lookups.

**Pipeline Stages**:
1. **Parse** - Read and parse Parquet/CSV/JSONL records
2. **Filter** - Filter by country (US only)
3. **Batch Barcodes** - Group barcodes for bulk lookup
4. **Resolve Products** - Bulk lookup products from ProductService
5. **Map** - Transform to price insert requests
6. **Batch Prices** - Group prices for bulk insert
7. **Insert** - Bulk insert to database

**Performance Benefits**:
- Parallel processing at each stage
- Backpressure management to prevent memory overflow
- Batched product lookups reduce HTTP calls by ~100x
- Batched inserts improve database throughput by ~10x

### 4. **Bulk Barcode Lookup Endpoint** (ProductService)

**New Endpoint**: `POST /api/products/barcode/bulk`

**Request**:
```json
{
  "Barcodes": ["012345678905", "012345678906", ...]
}
```

**Response**:
```json
{
  "012345678905": {
    "Id": "guid",
    "Name": "Product Name",
    "Brand": "Brand Name",
    "Barcode": "012345678905",
    ...
  },
  ...
}
```

**Implementation**: Uses SQL Server table-valued parameters for efficient bulk lookups.

### 5. **Database Migration** (`ProductService/Data/Migrations/AddBarcodeListType.sql`)

Creates the `BarcodeListType` user-defined table type for bulk lookups.

**To apply**:
```sql
-- Run this migration in your ProductService database
sqlcmd -S localhost -d ProductService -i AddBarcodeListType.sql
```

## Performance Comparison

### Before (Individual Lookups)
- **Product Lookups**: 100,000 prices × 1 HTTP call = 100,000 HTTP requests
- **Price Inserts**: 100,000 prices × 1 DB call = 100,000 INSERT statements
- **Estimated Time**: ~2-4 hours for 100k records

### After (Batched Dataflow)
- **Product Lookups**: 100,000 prices ÷ 100 batch size = 1,000 HTTP requests (99% reduction)
- **Price Inserts**: 100,000 prices ÷ 1,000 batch size = 100 bulk INSERTs (99% reduction)
- **Estimated Time**: ~10-20 minutes for 100k records (10-20x faster)

## Usage Examples

### Option 1: Use Dataflow Import Service Directly

```csharp
// In PriceDataImportWorker or controller
var dataflowService = scope.ServiceProvider.GetRequiredService<DataflowOpenPricesImportService>();
var result = await dataflowService.ImportFromUrlAsync(url, "parquet", cancellationToken);
```

### Option 2: Use Batch Services Independently

```csharp
// Batch product lookups
var batchLookup = serviceProvider.GetRequiredService<IBatchProductLookupService>();
var products = await batchLookup.GetProductsByBarcodesAsync(barcodes);

// Batch price inserts
var batchInsert = serviceProvider.GetRequiredService<IBatchPriceInsertService>();
foreach (var price in prices)
{
    await batchInsert.QueuePriceAsync(price);
}
await batchInsert.FlushAsync();
```

## Configuration

### Tuning Guidelines

**For Low-Memory Environments**:
```json
{
  "ProductLookup": { "BatchSize": 50, "MaxParallelBatches": 2 },
  "PriceBatch": { "BatchSize": 500, "MaxParallelBatches": 1 }
}
```

**For High-Throughput Environments**:
```json
{
  "ProductLookup": { "BatchSize": 200, "MaxParallelBatches": 8 },
  "PriceBatch": { "BatchSize": 2000, "MaxParallelBatches": 4 }
}
```

**For Slow ProductService**:
```json
{
  "ProductLookup": {
    "BatchSize": 200,
    "BatchTimeoutMs": 2000,
    "CacheMinutes": 60
  }
}
```

## Monitoring

Both batch services log detailed metrics:

- Batch size and processing time
- Success/failure counts
- Average queue time
- Processing rates (items/second)
- Sample data (first/last items in batch)

Example log output:
```
[DATAFLOW] Batch insert: 1000/1000 prices in 234ms (4273/sec). 
First: [012345678905] Organic Bananas $2.99. 
Last: [987654321098] Almond Milk $4.49
```

## Testing

See `ExpressRecipe.PriceService.Tests` for unit tests covering:
- Batch aggregation logic
- Product lookup caching
- Error handling and retries
- Pipeline backpressure

## Migration Path

1. **Deploy ProductService** with new bulk endpoint
2. **Run SQL migration** to create BarcodeListType
3. **Deploy PriceService** with new batch services
4. **Monitor logs** for performance improvements
5. **Tune configuration** based on observed metrics

## Fallback Behavior

If the bulk endpoint is unavailable:
- `GetByBarcodesAsync` falls back to individual queries
- No functionality is lost, only performance
- Logs warning about fallback usage

## Future Improvements

1. Add Redis caching layer for product lookups
2. Implement circuit breaker for ProductService calls
3. Add metrics/telemetry for batch efficiency
4. Create dedicated bulk price insert stored procedure
5. Implement smart batching based on available memory
