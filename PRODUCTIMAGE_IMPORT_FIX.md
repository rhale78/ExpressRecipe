# ProductImage Import Fix

## Issue
No images showing up in the ProductImages table despite image import code being present.

## Root Cause
The `SaveProductImagesAsync` method in `OpenFoodFactsImportService` was **silently swallowing exceptions**. The original code had:

```csharp
catch (Exception ex)
{
    _logger.LogWarning(ex, "Failed to save product images for {ProductId}", productId);
    // ERROR SWALLOWED - method returns normally
}
```

This meant that if the ProductImage table didn't exist or any database error occurred, the error was logged but never surfaced, making it appear as if the import succeeded.

## Most Likely Cause
**Migration 011_CreateProductImageTable.sql has not been applied yet**, so the ProductImage table doesn't exist in the database.

## Fixes Applied

### 1. Enhanced Error Handling in OpenFoodFactsImportService
**File**: `src/Services/ExpressRecipe.ProductService/Services/OpenFoodFactsImportService.cs`

Changed `SaveProductImagesAsync` to:
- **Re-throw exceptions** from the primary (front) image insertion to surface critical errors
- **Log and continue** for secondary images (nutrition, ingredients, back)
- **Add detailed error messages** indicating likely causes (table doesn't exist, connection issues)
- **Individual try-catch blocks** for each image type to prevent one failure from blocking others

### 2. Diagnostic Tools Created

#### A. SQL Diagnostic Script
**File**: `src/Services/ExpressRecipe.ProductService/Data/Migrations/DIAGNOSTIC_CheckProductImages.sql`

Run this script to check:
- ? Does ProductImage table exist?
- ? Which migrations have been applied?
- ? How many images are in the database?
- ? Sample image records

#### B. PowerShell Diagnostic Script
**File**: `scripts/check-productimage-migration.ps1`

Run this to:
- Check if ProductImage table exists
- Verify migration 011 status
- List all applied migrations
- Auto-fix if migration is marked as applied but table missing

## How to Fix

### Option 1: Restart ProductService (Recommended)
The service auto-applies migrations on startup:

```powershell
# Stop the service
# Start the service - it will apply migration 011 automatically
```

### Option 2: Run Diagnostic Script
```powershell
cd scripts
.\check-productimage-migration.ps1
```

This will tell you exactly what's wrong and how to fix it.

### Option 3: Manual SQL Execution
If needed, manually apply the migration:

```powershell
sqlcmd -S localhost,1433 -U sa -P "YourStrong@Passw0rd" -d productdb -i "src\Services\ExpressRecipe.ProductService\Data\Migrations\011_CreateProductImageTable.sql"
```

## Verification

After applying the fix:

1. **Check logs** - You should now see detailed error messages if image import fails:
   ```
   ? CRITICAL: Failed to save product images for {ProductId} from OpenFoodFacts. 
   This likely means the ProductImage table doesn't exist or there's a database connection issue.
   ```

2. **Import a product** - Try importing a product with a barcode:
   ```
   POST /api/products/import/openfoodfacts/{barcode}
   ```

3. **Check the database**:
   ```sql
   SELECT COUNT(*) FROM ProductImage WHERE IsDeleted = 0
   ```

4. **View sample images**:
   ```sql
   SELECT TOP 10 * FROM ProductImage ORDER BY CreatedAt DESC
   ```

## Expected Behavior After Fix

When importing from OpenFoodFacts, you should see logs like:
```
Starting image import for product {ProductId} from OpenFoodFacts barcode {Barcode}
Found front image URL: {Url}
? Imported front image (primary) for product {ProductId}
? Imported nutrition image for product {ProductId}
? Imported ingredients image for product {ProductId}
? Successfully imported 3 image(s) for product {ProductId} from OpenFoodFacts
```

## Related Files Changed
- ? `src/Services/ExpressRecipe.ProductService/Services/OpenFoodFactsImportService.cs` - Enhanced error handling
- ? `src/Services/ExpressRecipe.ProductService/Data/ProductImageRepository.cs` - Already has Product.ImageUrl syncing (from previous fix)
- ? `src/Services/ExpressRecipe.ProductService/Program.cs` - Already has ILogger injection (from previous fix)

## Migration 011 Contents
The migration creates:
- **ProductImage table** with support for multiple images per product
- **Indexes** for fast lookups
- **Foreign key** to Product table
- **Data migration** from old Product.ImageUrl to new ProductImage table

## Next Steps
1. Run the diagnostic script to verify the issue
2. Restart ProductService to apply migration 011
3. Test image import with a known barcode (e.g., "078742370972")
4. Check logs for success/error messages
5. Query ProductImage table to verify images are being saved

## Prevention
Going forward, all database-related operations should:
- ? Throw exceptions for critical failures (don't swallow)
- ? Log detailed error context
- ? Use try-catch only for graceful degradation, not to hide errors
