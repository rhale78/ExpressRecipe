# CSV Import Quick Reference

## What Was Added
? CSV import support for OpenFoodFacts data
? CSV is now the **DEFAULT** data source
? Configuration option to switch between CSV and JSON
? Enhanced logging for first 100 products showing all image URLs
? All original JSON import functionality preserved

## Key Files Changed
1. **ExpressRecipe.ProductService.csproj** - Added CsvHelper package
2. **OpenFoodFactsImportService.cs** - Added `ImportFromCsvDataAsync()` method
3. **ProductDataImportWorker.cs** - Updated to support CSV/JSON selection
4. **appsettings.json** - Added `DataSource: "CSV"` setting

## Configuration

### Current Setting (CSV - Default)
```json
{
  "ProductImport": {
    "DataSource": "CSV"
  }
}
```

### Switch to JSON
```json
{
  "ProductImport": {
    "DataSource": "JSON"
  }
}
```

## What to Look For in Logs

### CSV Import Starting
```
Using CSV data source for import
Starting CSV bulk import from OpenFoodFacts: https://...csv.gz
```

### Image URL Debugging (First 100 Products)
```
CSV Record 1 - Barcode: 3017620422003, Product: Nutella
  ImageUrl: https://images.openfoodfacts.org/images/products/301/762/042/2003/front_en.jpg
  image_front_url: https://images.openfoodfacts.org/images/products/301/762/042/2003/front_en.jpg
  image_nutrition_url: https://images.openfoodfacts.org/images/products/301/762/042/2003/nutrition_en.jpg
  image_ingredients_url: https://images.openfoodfacts.org/images/products/301/762/042/2003/ingredients_en.jpg
```

### Progress Updates
```
CSV bulk import progress: 500 imported, 20 skipped
Imported 1000 products from CSV (skipped 50)...
```

## Testing Steps

1. **Start ProductService** (CSV is already default)
2. **Watch logs** for first 100 products with image URLs
3. **Compare** CSV image availability vs JSON (if needed)
4. **Verify** images in ProductImage table after import completes

## Troubleshooting

### No images in CSV either?
- Check the logs for first 100 products
- See if `image_front_url`, `image_nutrition_url` etc. are `(null)`
- If CSV also lacks images, the issue is upstream in OpenFoodFacts data

### Want to test JSON format?
- Change `DataSource` to `"JSON"` in appsettings.json
- Restart ProductService
- Drop products table or set count < 1000 to trigger import
- Compare log output

### CSV parsing errors?
- Check delimiter (should be tab `\t`)
- Check CsvHelper version (should be 33.0.1)
- Check compression (should be GZip)

## Why CSV?

CSV format may have better image data because:
- Explicit columns for each image type
- Used for data analysis and exports
- Simpler structure without nested JSON
- May be updated more frequently

## Original Functionality Preserved

? JSON/JSONL import still works
? API-based imports unchanged
? Individual product import by barcode works
? Search and import works
? Delta updates work
? All image extraction logic preserved
