# CSV Import Filter Fix - Language Filter Too Restrictive

## Problem Identified
CSV import was skipping **4.2 million records** because of overly restrictive language filtering.

### Original Code Issue
```csharp
// Filter: English products only
if (string.IsNullOrWhiteSpace(lang) || !lang.StartsWith("en", StringComparison.OrdinalIgnoreCase))
{
    skippedCount++;
    continue;
}
```

This code was:
1. **Skipping products with empty/null `lang` field** - which are likely from the English export
2. **Requiring `lang` to start with "en"** - but many products in `en.openfoodfacts.org` don't have this set

## Root Cause
When downloading from `en.openfoodfacts.org`, the CSV already contains English products. The `lang` field:
- May be **empty/null** for many products (they're from English export, so it's implied)
- May contain language codes that don't match our filter
- Shouldn't be used as primary filter when already downloading English-specific export

## Fix Applied

### Changed Filter Logic
```csharp
// Skip products without barcode or name (primary filter)
if (string.IsNullOrWhiteSpace(barcode) || string.IsNullOrWhiteSpace(productName))
{
    skippedCount++;
    continue;
}

// Language filter: Since we're downloading from en.openfoodfacts.org,
// we accept products with no lang specified OR lang starting with "en"
// This is more permissive than before
if (!string.IsNullOrWhiteSpace(lang) && 
    !lang.StartsWith("en", StringComparison.OrdinalIgnoreCase))
{
    // Only skip if lang is explicitly set to non-English
    skippedCount++;
    continue;
}
```

### Key Changes
1. **Accepts products with empty `lang` field** - assumes they're English since we're downloading from English export
2. **Primary filter is barcode + product_name** - these are required
3. **Only skips if `lang` is explicitly non-English** - more permissive approach
4. **Added debug logging** - logs first 10 records showing `code`, `lang`, and `product_name` values

## New Filter Logic

### Products ACCEPTED ?
- Products with empty/null `lang` field
- Products with `lang` = "en"
- Products with `lang` = "en-US", "en-GB", etc.
- Products from `en.openfoodfacts.org` export

### Products SKIPPED ?
- Products without barcode
- Products without product_name
- Products with explicit non-English `lang` (e.g., "fr", "es", "de")
- Beauty products, pet food, cosmetics (category filter still applies)

## Expected Results

### Before Fix
```
CSV bulk import completed: 0 successful, 0 failed, 4242380 skipped
```

### After Fix (Expected)
```
CSV bulk import completed: 50000+ successful, X failed, Y skipped
```

Should now import thousands/millions of products instead of skipping everything.

## Debug Logging Added

For the first 10 CSV records, you'll now see:
```
CSV Debug Record 1: code=0000000000000, lang=en, product_name=Product Name
CSV Debug Record 2: code=0000000000001, lang=(null), product_name=Another Product
```

This helps diagnose what values are actually in the CSV `lang` field.

## Testing Steps

1. **Restart ProductService** (changes are already built)
2. **Watch logs** for first 10 debug records
3. **Verify** products are being imported instead of skipped
4. **Check** ProductStaging table has records
5. **Monitor** ProductProcessingWorker processing staged products

## Next Actions

If products are still being skipped:
1. Check debug logs for `lang` values in first 10 records
2. Check if barcode/product_name are actually populated
3. May need to adjust category filters if too restrictive
4. May need to check if CSV format matches expectations

## Files Changed
- `src/Services/ExpressRecipe.ProductService/Services/OpenFoodFactsImportService.cs`
  - Fixed language filter logic (lines 1244-1273)
  - Added debug logging for first 10 records
  - Reordered filters (barcode/name check before language check)
