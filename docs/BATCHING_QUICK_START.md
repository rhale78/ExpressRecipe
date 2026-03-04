# Price & Product Batching - Quick Start Guide

## What Was Added

### 1. Batched Product Lookups
- **Service**: `BatchProductLookupService`
- **Benefit**: Reduces HTTP calls to ProductService by ~99%
- **How**: Groups individual barcode lookups into batches of 100

### 2. Batched Price Inserts  
- **Service**: `BatchPriceInsertService`
- **Benefit**: Improves database throughput by ~10x
- **How**: Groups price inserts into batches of 1000

### 3. Dataflow Import Pipeline
- **Service**: `DataflowOpenPricesImportService`
- **Benefit**: Full end-to-end batching with parallel processing
- **How**: 7-stage TPL Dataflow pipeline

### 4. Bulk Product Endpoint
- **Endpoint**: `POST /api/products/barcode/bulk`
- **Benefit**: Single HTTP call can lookup 100+ products
- **Location**: ProductService

## How to Use

### Testing the Improvements

#### Test Bulk Product Lookup
```bash
curl -X POST https://localhost:7002/api/admin/test/batch-lookup \
  -H "Content-Type: application/json" \
  -d '{
    "Barcodes": ["012345678905", "012345678906", "012345678907"]
  }'
```

#### Trigger Dataflow Import
```bash
curl -X POST https://localhost:7002/api/admin/import/dataflow \
  -H "Authorization: Bearer YOUR_TOKEN"
```

#### Compare with Standard Import
```bash
curl -X POST https://localhost:7002/api/admin/import/standard \
  -H "Authorization: Bearer YOUR_TOKEN"
```

### Configuration

Edit `src/Services/ExpressRecipe.PriceService/appsettings.json`:

```json
{
  "PriceService": {
    "ProductLookup": {
      "BatchSize": 100,
      "BatchTimeoutMs": 500,
      "CacheMinutes": 30,
      "MaxParallelBatches": 4
    },
    "PriceBatch": {
      "BatchSize": 1000,
      "BatchTimeoutMs": 2000,
      "MaxParallelBatches": 2
    }
  },
  "PriceImport": {
    "ProductLookupBatchSize": 100,
    "PriceInsertBatchSize": 1000
  }
}
```

### Database Setup

Run the migration to create the table type:

```bash
# Navigate to ProductService
cd src/Services/ExpressRecipe.ProductService

# The migration will auto-run on startup via DatabaseMigrator
# Or run manually:
sqlcmd -S localhost -d ExpressRecipe_ProductService -i Data/Migrations/AddBarcodeListType.sql
```

## Monitoring

Watch the logs for batching metrics:

```
[DATAFLOW] Batch insert: 1000/1000 prices in 234ms (4273/sec). 
First: [012345678905] Organic Bananas $2.99. 
Last: [987654321098] Almond Milk $4.49

[DATAFLOW] Resolved 87/100 products from ProductService
```

## Performance Expectations

### For 100,000 price records:

**Before**:
- Product lookups: 100,000 HTTP calls
- Price inserts: 100 bulk inserts (1000 each)
- Time: ~2-4 hours

**After (with batching)**:
- Product lookups: ~1,000 HTTP calls (100 per batch)
- Price inserts: 100 bulk inserts (1000 each)
- Time: ~30-60 minutes

**After (with dataflow + bulk endpoint)**:
- Product lookups: ~1,000 bulk HTTP calls
- Price inserts: 100 bulk inserts
- Time: ~10-20 minutes

## Troubleshooting

### "BarcodeListType not found"
- Migration didn't run
- Check ProductService logs
- Run migration manually

### Slow product lookups
- Increase `ProductLookup:CacheMinutes`
- Increase `ProductLookup:BatchSize`
- Check ProductService health

### High memory usage
- Decrease `BoundedCapacity` values in dataflow blocks
- Decrease batch sizes
- Reduce `MaxParallelBatches`

### Timeouts
- Increase `ProductLookup:BatchTimeoutMs`
- Increase `PriceBatch:BatchTimeoutMs`
- Check network latency between services

## Next Steps

1. Deploy ProductService with bulk endpoint
2. Run database migration
3. Deploy PriceService with batch services
4. Monitor performance improvements
5. Tune batch sizes based on your workload

For detailed architecture, see `docs/BATCHING_IMPROVEMENTS.md`.
