# OpenFoodFacts CSV Import Support

## Overview
Added CSV import capability to OpenFoodFacts import service as an alternative to JSONL format. CSV format is now the **default** data source as it may contain more complete image URL data.

## Changes Made

### 1. Package Addition
**File**: `src/Services/ExpressRecipe.ProductService/ExpressRecipe.ProductService.csproj`
- Added `CsvHelper` v33.0.1 package for CSV parsing

### 2. CSV Import Method
**File**: `src/Services/ExpressRecipe.ProductService/Services/OpenFoodFactsImportService.cs`

Added new method `ImportFromCsvDataAsync`:
- Downloads OpenFoodFacts CSV export (`.csv.gz` format)
- Uses tab-delimited parsing (OpenFoodFacts CSV standard)
- Extracts all image URL columns:
  - `image_url`
  - `image_front_url`
  - `image_small_url`
  - `image_nutrition_url`
  - `image_ingredients_url`
  - `image_thumb_url`
- Logs first 100 records with detailed image URL information for debugging
- Same filtering as JSONL import:
  - English products only
  - Food products only (excludes beauty, pet food, cosmetics)
  - Requires barcode and product name
- Bulk insert staging products in batches of 500
- Progress reporting
- Error handling per record

### 3. Configuration
**File**: `src/Services/ExpressRecipe.ProductService/appsettings.json`

Added `DataSource` setting:
```json
{
  "ProductImport": {
    "AutoImport": true,
    "AutoProcessing": true,
    "DataSource": "CSV",  // ? NEW: "CSV" or "JSON"
    "MaxParallelism": 16,
    "BatchSize": 1000,
    "BufferSize": 5000
  }
}
```

### 4. Worker Update
**File**: `src/Services/ExpressRecipe.ProductService/Workers/ProductDataImportWorker.cs`

Updated `PerformInitialImportIfNeededAsync` to:
- Read `DataSource` from configuration (defaults to "CSV")
- Choose import method based on configuration:
  - `"CSV"` ? calls `ImportFromCsvDataAsync()`
  - `"JSON"` or other ? calls `ImportFromBulkDataAsync()` (JSONL)
- Log which data source is being used

## Data Sources

### CSV Format (Default)
- **URL**: `https://static.openfoodfacts.org/data/en.openfoodfacts.org.products.csv.gz`
- **Format**: Tab-delimited CSV
- **Compression**: GZip
- **Columns**: 180+ columns including explicit image URL fields
- **Size**: ~2-3 GB compressed
- **Advantages**:
  - May have more complete image data in dedicated columns
  - Easier to inspect/debug with tools
  - Explicit column names for all image types

### JSONL Format (Original)
- **URL**: `https://static.openfoodfacts.org/data/openfoodfacts-products.jsonl.gz`
- **Format**: Line-delimited JSON
- **Compression**: GZip
- **Size**: ~5-6 GB compressed
- **Advantages**:
  - More structured nested data
  - Full JSON object per product
  - `selected_images` nested structure

## Testing the CSV Import

### 1. Check Configuration
```json
{
  "ProductImport": {
    "DataSource": "CSV"  // Use CSV format
  }
}
```

### 2. Monitor Logs
Look for these log messages:
```
Using CSV data source for import
CSV Headers found: code, url, creator, created_t, ...
CSV Record 1 - Barcode: 0000000000000, Product: Product Name
  ImageUrl: https://...
  image_front_url: https://...
  image_nutrition_url: https://...
CSV bulk import progress: 500 imported, 20 skipped
```

### 3. Compare Image Data
The CSV import logs the first 100 products with detailed image URL information to verify which format has better image data.

## Switching Between Formats

### Use CSV (Default - Recommended)
```json
"DataSource": "CSV"
```

### Use JSONL (Original)
```json
"DataSource": "JSON"
```

### Switch at Runtime
Restart the ProductService after changing the configuration. The worker will:
1. Check product count
2. If < 1000 products, start import using configured data source
3. Log which format is being used

## Why CSV Might Have Better Image Data

OpenFoodFacts maintains both formats, but CSV has:
- **Explicit columns** for each image type (`image_front_url`, `image_nutrition_url`, etc.)
- **Simpler structure** - no nested JSON to traverse
- **Better for analytics** - CSV is the format data analysts use
- **May be more complete** - Updated more frequently for data exports

## Backward Compatibility

All existing functionality preserved:
- ? JSON/JSONL import still available
- ? API-based import unchanged (`ImportProductByBarcodeAsync`, `SearchAndImportAsync`)
- ? Image extraction from JSON nested structures still works
- ? All image logging and debugging features retained
- ? Delta updates still use JSONL format (as designed)

## Next Steps

1. **Run ProductService** with CSV as default
2. **Monitor first 100 products** in logs to see image URL availability
3. **Compare with JSONL** if needed by changing `DataSource` to "JSON"
4. **Verify images** appear in ProductImage table after import
5. **Document findings** about which format has better image coverage

## Image URL Fields in CSV

The CSV format includes these image-related columns:
- `image_url` - Primary product image
- `image_front_url` - Front label (display size ~400px)
- `image_small_url` - Small thumbnail
- `image_thumb_url` - Tiny thumbnail
- `image_nutrition_url` - Nutrition facts label
- `image_ingredients_url` - Ingredients list
- `image_packaging_url` - Packaging/back label

All of these are extracted and logged for the first 100 products to help diagnose the image availability issue.
