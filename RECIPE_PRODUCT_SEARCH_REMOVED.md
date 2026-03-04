# Recipe & Price Services Configuration Updates

## Changes Made

### 1. ✅ Removed Product Search from RecipeService
**Issue:** RecipeService had unnecessary ProductServiceClient dependency
**Fix:** Removed `builder.AddProductClient()` from RecipeService Program.cs
**Impact:** RecipeService now only depends on IngredientService (which is what it needs)

### 2. ✅ Recipe Import Path Configuration
**Issue:** Concern about hardcoded paths
**Status:** Already config-driven via `RecipeImport:FilePath` in appsettings.json
**Added:** Centralized configuration in `Config\appsettings.Global.json`

```json
"RecipeImport": {
  "AutoImport": true,
  "AutoProcessing": true,
  "FilePath": "E:\\recipes\\final_merged_recipes.json",
  "ImageSourcePath": "E:\\recipes\\Source",
  "MaxParallelism": 8,
  "BatchSize": 1000,
  "BufferSize": 5000,
  "CheckIntervalSeconds": 60,
  "ImportChunkSize": 5000,
  "ImportIntervalHours": 24,
  "ProcessingResetMinutes": 30,
  "FailedResetMinutes": 120
}
```

### 3. ✅ Fixed Logging Colors
**Issue:** Serilog console output was using muted colors (AnsiConsoleTheme.Code)
**Fix:** Changed to `AnsiConsoleTheme.Literate` with proper colored output:
- **Fatal/Error**: Red
- **Warning**: Yellow  
- **Information**: White
- **Debug**: Gray
- **Verbose**: Gray

**File:** `src\ExpressRecipe.ServiceDefaults\Extensions.cs`

```csharp
.WriteTo.Console(
    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
    theme: AnsiConsoleTheme.Literate)
```

### 4. ℹ️ PriceService Data Scope

**Question:** Does PriceService only deal with US locations/USD?

**Answer:** **No, it's international by default.**

- **Source:** OpenPrices.org (Open Food Facts project)
- **Coverage:** Global price data from 100+ countries
- **Currency:** Multi-currency (USD, EUR, GBP, etc.)
- **Current Behavior:** Imports ALL international price data

**Considerations:**
- Price data includes `currency` field (USD, EUR, etc.)
- Prices are stored with their original currency
- No automatic currency conversion
- Can filter by country/currency at query time if needed

**To Filter to US Only (if desired):**
1. Check `location_osm_address_country_code` == "US" in OpenPrices data
2. Or check `currency` == "USD"
3. Add configuration: `"FilterToUSOnly": true`

**Recommendation:** Keep international data - it's valuable for:
- Users traveling abroad
- International product price comparisons
- Multi-currency price tracking
- Users with family abroad

## Impact Summary

✅ **RecipeService** - Cleaner dependencies, only uses IngredientService  
✅ **Configuration** - Centralized recipe import paths  
✅ **Logging** - Proper colored console output for all services  
ℹ️ **PriceService** - International by design, can add US filtering if needed

## Testing Checklist

- [ ] Restart all services to see colored logging
- [ ] Verify RecipeService works without ProductServiceClient
- [ ] Check recipe import uses configured path
- [ ] Confirm price data includes international records

## Next Steps (Optional)

1. **Add US filtering to PriceService** if you only want US prices
2. **Add currency conversion service** for international prices
3. **Add country/currency filters** to price search endpoints
