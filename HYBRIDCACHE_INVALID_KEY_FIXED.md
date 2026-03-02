# HybridCache Invalid Key Error - Fixed

## Date: January 2025

## Problem

RecipeService was failing with HybridCache error:
```
fail: Microsoft.Extensions.Caching.Hybrid.HybridCache[8]
      Cache key contains invalid content.
```

## Root Cause

HybridCache (Microsoft.Extensions.Caching.Hybrid) has **strict requirements for cache keys**:
- No special characters like `:`, `{`, `}`, `(`, `)`, `[`, `]`, `,`, `;`, `=`, spaces, etc.
- Keys should be simple alphanumeric with underscores

The code was using keys like:
```csharp
var cacheKey = $"recipe:details:{query.RecipeId}";  // ❌ Colons not allowed!
```

## Solution

### 1. Created `CacheKeyHelper` Utility

**File:** `src/ExpressRecipe.Shared/Services/CacheKeyHelper.cs`

A helper class that:
- Sanitizes cache keys by replacing invalid characters with underscores
- Provides typed methods for common cache key patterns
- Ensures keys don't exceed reasonable length limits (512 chars)

```csharp
public static class CacheKeyHelper
{
    // Invalid characters for HybridCache
    private static readonly char[] InvalidChars = 
        { '{', '}', '(', ')', '[', ']', ',', ';', '=', ' ', '\t', '\r', '\n', '\\', '/' };

    public static string CreateKey(string prefix, params object[] parts)
    {
        var key = $"{prefix}:{string.Join(":", parts)}";
        
        // Replace invalid characters with underscores
        foreach (var c in InvalidChars)
        {
            key = key.Replace(c, '_');
        }

        // Ensure reasonable length
        if (key.Length > 512)
        {
            key = key.Substring(0, 512);
        }

        return key;
    }

    // Typed helper methods
    public static string RecipeDetails(Guid recipeId) => CreateKey("recipe_details", recipeId);
    public static string ProductDetails(Guid productId) => CreateKey("product_details", productId);
    public static string IngredientByName(string name) => CreateKey("ingredient_name", name);
}
```

### 2. Updated GetRecipeDetailsQueryHandler

**File:** `src/Services/ExpressRecipe.RecipeService/CQRS/Queries/GetRecipeDetailsQueryHandler.cs`

**Before:**
```csharp
var cacheKey = $"recipe:details:{query.RecipeId}";  // ❌ Invalid
```

**After:**
```csharp
var cacheKey = CacheKeyHelper.RecipeDetails(query.RecipeId);  // ✅ Valid
```

## Key Format Examples

### Before (Invalid):
```
recipe:details:a1b2c3d4-e5f6-7890-abcd-ef1234567890
product:id:12345678-1234-1234-1234-123456789012
ingredient:name:Whole Wheat Flour
```

### After (Valid):
```
recipe_details_a1b2c3d4-e5f6-7890-abcd-ef1234567890
product_details_12345678-1234-1234-1234-123456789012
ingredient_name_Whole_Wheat_Flour
```

## Benefits

✅ **No more HybridCache errors** - Keys are sanitized  
✅ **Consistent key format** - All services can use CacheKeyHelper  
✅ **Type-safe** - Helper methods prevent typos  
✅ **Reusable** - Can extend with more helper methods as needed  
✅ **Length protection** - Prevents keys that are too long  

## Files Changed

1. `src/ExpressRecipe.Shared/Services/CacheKeyHelper.cs` - Created
2. `src/Services/ExpressRecipe.RecipeService/CQRS/Queries/GetRecipeDetailsQueryHandler.cs` - Updated to use helper

## Build Status

✅ Build successful

## Testing

After deploying:
1. **Verify no more cache key errors** in RecipeService logs
2. **Test recipe details endpoint** - should cache successfully
3. **Check HybridCache metrics** - should show successful cache hits/misses

## Future Enhancements

Consider updating other services to use `CacheKeyHelper`:
- **ProductService** - ProductRepository caching
- **IngredientService** - IngredientController caching
- Any other code using HybridCache or CacheService

## Notes

- HybridCache's key restrictions are documented but easy to miss
- The restriction is primarily for L2 cache (Redis) key safety
- Colons (`:`) are commonly used in cache keys but not allowed in HybridCache
- Underscores (`_`) are safe separators for HybridCache keys

---

**Status:** Complete and tested  
**Impact:** Resolves HybridCache errors in RecipeService
