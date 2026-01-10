# Product Image Import - Debug Guide

## Current Configuration Status

### ✅ Database Management (Config/appsettings.DatabaseManagement.json)
```json
{
  "DropDatabasesOnStartup": false,     // ← Change to TRUE for complete wipe
  "DropTablesOnStartup": true,         // ✓ Enabled - drops all tables
  "RunMigrationsOnStartup": true,      // ✓ Enabled - runs migrations
  "ProductService": {
    "EnableManagement": true           // ✓ Enabled
  }
}
```

### ✅ Logging (Config/appsettings.Development.json)
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"               // ✓ Debug level enabled
    }
  }
}
```

### ✅ Migration Files
- `011_CreateProductImageTable.sql` - ✓ Exists
- Will create ProductImage table with all needed columns

### ✅ Comprehensive Logging Added
All import operations now log extensively.

---

## For COMPLETE Database Wipe

**Edit:** `Config/appsettings.DatabaseManagement.json`

Change line 5 from:
```json
"DropDatabasesOnStartup": false,
```

To:
```json
"DropDatabasesOnStartup": true,
```

This will:
1. Drop the ENTIRE ExpressRecipe.Products database
2. Recreate it fresh
3. Run all migrations (including 011_CreateProductImageTable.sql)
4. Start with completely clean slate

---

## What You Should See in Logs

### On Startup:
```
[DatabaseManager] Dropping database ExpressRecipe.Products for ProductService
[DatabaseManager] Dropped database: ExpressRecipe.Products
[DatabaseManager] Ensured database exists: ExpressRecipe.Products
[DatabaseManager] Database ExpressRecipe.Products ready for ProductService
[MigrationRunner] Running migration: 001_CreateProductTable.sql
[MigrationRunner] Running migration: 011_CreateProductImageTable.sql
```

### When Importing Products:
```
========== IMPORTING OpenFoodFacts product: 5449000000996 ==========
>>> Extracting image URLs for barcode 5449000000996:
  image_url: https://...
  image_front_url: https://...
  selected_images structure found:
    front image found in selected_images
      display property found with keys: en, fr
        en: https://...
>>> Processing product: Coca-Cola (Barcode: 5449000000996, Brand: Coca-Cola)
Checking if product with barcode 5449000000996 already exists in database...
>>> Product NOT found in database. Creating NEW product for barcode 5449000000996
    Creating product: Name=Coca-Cola, Brand=Coca-Cola, Category=Beverages, ImageUrl=https://...
    ✓ Product created with ID: {guid}
Starting image import for product {guid} from OpenFoodFacts barcode 5449000000996
Found front image URL: https://...
🖼️ Imported front image (primary) for product {guid}
Found nutrition image URL: https://...
🖼️ Imported nutrition image for product {guid}
✅ Successfully imported 2 image(s) for product {guid} from OpenFoodFacts
========== Successfully imported product 5449000000996: Coca-Cola ==========
```

### If Product Already Exists (should NOT happen after wipe):
```
!!! Product ALREADY EXISTS in database: Coca-Cola (ID: {guid}, Barcode: 5449000000996)
    Will update existing product and save images
```

### If No Images Found:
```
  image_url: (null)
  image_front_url: (null)
  selected_images structure NOT found in response
⚠️ No front image URL found for product {guid} barcode {barcode}
⚠️⚠️ No images found for product {guid} barcode {barcode} from OpenFoodFacts
```

---

## Troubleshooting Steps

### 1. Complete Database Wipe
```powershell
# Option A: Set DropDatabasesOnStartup to true (recommended)
# Edit Config/appsettings.DatabaseManagement.json

# Option B: Manual SQL wipe
sqlcmd -S localhost -Q "DROP DATABASE [ExpressRecipe.Products]"
```

### 2. Restart AppHost
Stop and start the AppHost to trigger database management.

### 3. Import a Test Product
```bash
# Import Coca-Cola (known to have images)
curl -X POST https://localhost:7001/api/admin/import/openfoodfacts \
  -H "Content-Type: application/json" \
  -d '{"query": "coca-cola", "maxResults": 1}'
```

### 4. Check the Logs
Look for the log patterns shown above.

### 5. Verify Database
```sql
-- Check if ProductImage table exists
SELECT * FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME = 'ProductImage';

-- Check if any images were inserted
SELECT COUNT(*) FROM ProductImage;

-- Check products
SELECT Id, Name, Barcode, ImageUrl FROM Product;

-- Check images for a specific product
SELECT * FROM ProductImage WHERE ProductId = 'YOUR_PRODUCT_ID';
```

---

## Common Issues

### "Product already exists" on fresh database
**Cause:** Database wasn't actually wiped
**Fix:** Set `DropDatabasesOnStartup: true` or manually drop the database

### No JSON in logs
**Cause:** Logging level not set to Debug for that namespace
**Fix:** Verify `appsettings.Development.json` has `"Default": "Debug"`

### No image URLs found
**Cause:** OpenFoodFacts product doesn't have images, or API structure changed
**Fix:** Check the debug endpoint `/api/admin/debug/openfoodfacts/{barcode}` to see raw response

### ProductImage table doesn't exist
**Cause:** Migration 011 didn't run
**Fix:** Check migration ran in logs: `[MigrationRunner] Running migration: 011_CreateProductImageTable.sql`

---

## Quick Test

1. **Wipe everything:**
   ```json
   // Config/appsettings.DatabaseManagement.json
   "DropDatabasesOnStartup": true
   ```

2. **Restart AppHost**

3. **Import one product:**
   ```bash
   curl -X POST https://localhost:7001/api/admin/import/openfoodfacts \
     -H "Content-Type: application/json" \
     -d '{"query": "5449000000996"}'  # Coca-Cola barcode
   ```

4. **Check logs for:**
   - ">>> Extracting image URLs"
   - "✓ Product created with ID"
   - "🖼️ Imported front image"
   - "✅ Successfully imported N image(s)"

5. **Verify in database:**
   ```sql
   SELECT p.Name, p.Barcode, p.ImageUrl, pi.ImageType, pi.ImageUrl as ProductImageUrl
   FROM Product p
   LEFT JOIN ProductImage pi ON p.Id = pi.ProductId
   WHERE p.Barcode = '5449000000996';
   ```

Expected: Should see 1-4 rows (one for each image type found)

---

## Contact Points for Errors

If still not working, check these specific files:

1. **OpenFoodFactsImportService.cs** - Lines 47-193 (logging & import)
2. **ProductImageRepository.cs** - Lines 29-115 (image insertion)
3. **DatabaseManager.cs** - Lines 60-73 (table dropping)
4. **011_CreateProductImageTable.sql** - Table creation

Share the relevant error logs from these sections.
