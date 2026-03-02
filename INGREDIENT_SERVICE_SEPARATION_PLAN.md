# Service Separation: IngredientService as Single Source of Truth

## Date: January 2025

## Problem

**ProductService and RecipeService are directly accessing the Ingredient table** in their own databases, bypassing the IngredientService:

### Current (Incorrect) Architecture:
```
ProductService → productdb.Ingredient table (direct access)
RecipeService → recipedb.Ingredient table (direct access)
IngredientService → ingredientdb.Ingredient table (centralized)
```

This causes:
- ❌ **Data duplication** - 3 copies of ingredient data
- ❌ **Data inconsistency** - ingredients can be out of sync
- ❌ **Maintenance nightmare** - 3 places to update
- ❌ **Violates microservice boundaries** - services accessing each other's data directly

## Solution: Use IngredientService API for ALL Ingredient Operations

### Target Architecture:
```
ProductService → IngredientServiceClient → IngredientService → ingredientdb
RecipeService → IngredientServiceClient → IngredientService → ingredientdb
```

All ingredient operations (lookups, creates, updates, deletes) go through the IngredientService REST API.

## Changes Required

### 1. **Quiet IngredientService Logs** ✅ DONE

Updated `src/Services/ExpressRecipe.IngredientService/appsettings.json`:
```json
"Logging": {
  "LogLevel": {
    "Microsoft.AspNetCore.Routing": "Warning",
    "Microsoft.AspNetCore.Mvc": "Warning",
    "Microsoft.AspNetCore.Hosting": "Warning"
  }
}
```

Now only action logs visible:
```
info: ExpressRecipe.IngredientService[0] - Bulk insert completed: 50 ingredients
info: ExpressRecipe.IngredientService[0] - Bulk lookup returned 245 ingredients
```

### 2. **Remove Direct Database Access from ProductService** 🔜 TODO

#### Current Code (ProductService/Program.cs line 76):
```csharp
builder.Services.AddScoped<IIngredientRepository>(sp =>
{
    var cache = sp.GetRequiredService<HybridCacheService>();
    var logger = sp.GetRequiredService<ILogger<IngredientRepository>>();
    return new IngredientRepository(connectionString, cache, logger); // ❌ Direct DB access
});
```

#### Replace With:
```csharp
// IngredientServiceClient already registered - use it instead of direct DB access
// Remove IIngredientRepository registration entirely
```

#### Refactor Required Files:
1. **OpenFoodFactsImportService** - Replace `IIngredientRepository` with `IngredientServiceClient`
2. **BatchProductProcessor** - Replace `IIngredientRepository` with `IngredientServiceClient`
3. **ProductRepository** - Remove ingredient-related methods, use `IngredientServiceClient`
4. **Remove** `src/Services/ExpressRecipe.ProductService/Data/IngredientRepository.cs`

### 3. **Remove Direct Database Access from RecipeService** 🔜 TODO

#### Check RecipeService:
```bash
# Search for IIngredientRepository in RecipeService
```

If found, same refactoring as ProductService.

### 4. **Ensure IngredientServiceClient Supports All Operations** ✅ DONE

IngredientServiceClient already supports:
- ✅ `LookupIngredientIdsAsync` - Bulk lookup
- ✅ `GetIngredientIdByNameAsync` - Single lookup
- ✅ `GetIngredientAsync` - Get by ID
- ✅ `CreateIngredientAsync` - Create single
- ✅ `BulkCreateIngredientsAsync` - Bulk create

## Implementation Plan

### Phase 1: Audit ✅ DONE
- ✅ Identified ProductService has direct DB access
- ✅ Identified RecipeService may have direct DB access
- ✅ Confirmed IngredientServiceClient has all needed methods

### Phase 2: ProductService Refactoring 🔜 NEXT
1. Update `OpenFoodFactsImportService` constructor - replace `IIngredientRepository` with `IngredientServiceClient`
2. Update all ingredient operations to use REST API calls
3. Remove `IIngredientRepository` registration from Program.cs
4. Test bulk product import still works

### Phase 3: RecipeService Refactoring 🔜 AFTER PHASE 2
1. Check if RecipeService accesses Ingredient table directly
2. If yes, same refactoring as ProductService
3. Test bulk recipe import still works

### Phase 4: Database Cleanup 🔜 FINAL
1. Remove Ingredient tables from productdb and recipedb (if they exist)
2. Only ingredientdb should have Ingredient table
3. Update migrations to remove Ingredient table creation

## Benefits

✅ **Single source of truth** - Only IngredientService owns ingredient data  
✅ **Data consistency** - No duplication, no sync issues  
✅ **Better microservice isolation** - Services communicate via API, not direct DB  
✅ **Easier maintenance** - One place to update ingredient logic  
✅ **Better caching** - IngredientService can cache effectively  
✅ **Proper service boundaries** - Each service owns its domain  

## Risks & Mitigation

### Risk 1: Performance
**Concern:** REST API calls slower than direct DB access  
**Mitigation:** 
- IngredientServiceClient already has bulk operations
- IngredientService uses HybridCache for frequently accessed data
- Batch operations minimize round trips

### Risk 2: Network Failures
**Concern:** IngredientService unavailable breaks imports  
**Mitigation:**
- Resilience policies already configured (Polly retries)
- Can add circuit breaker if needed
- Ingredient creation is not time-critical (can retry later)

### Risk 3: Breaking Changes
**Concern:** Existing imports may fail  
**Mitigation:**
- IngredientServiceClient interface matches IIngredientRepository closely
- Test thoroughly before deploying
- Can rollback if issues arise

## Testing Checklist

After refactoring:
- [ ] Product import creates ingredients via IngredientService API
- [ ] Recipe import creates ingredients via IngredientService API
- [ ] Bulk lookups still fast enough (< 50ms for 100 ingredients)
- [ ] No direct database access to Ingredient table from Products/Recipe services
- [ ] Ingredient cache working properly
- [ ] Retry policies handling IngredientService failures

## Files to Change

### ProductService:
- `src/Services/ExpressRecipe.ProductService/Program.cs` - Remove IIngredientRepository registration
- `src/Services/ExpressRecipe.ProductService/Services/OpenFoodFactsImportService.cs` - Use IngredientServiceClient
- `src/Services/ExpressRecipe.ProductService/Services/BatchProductProcessor.cs` - Use IngredientServiceClient
- `src/Services/ExpressRecipe.ProductService/Data/IngredientRepository.cs` - DELETE (no longer needed)

### RecipeService:
- Check if direct access exists, refactor if found

### IngredientService:
- ✅ Already complete with REST API endpoints

## Status

- ✅ Phase 1: Audit - COMPLETE
- ⏳ Phase 2: ProductService Refactoring - READY TO START
- 🔜 Phase 3: RecipeService Refactoring - PENDING
- 🔜 Phase 4: Database Cleanup - PENDING

## Notes

This is a **significant architectural improvement** that will make the system more maintainable and aligned with microservice best practices. The refactoring should be done carefully with thorough testing at each phase.
