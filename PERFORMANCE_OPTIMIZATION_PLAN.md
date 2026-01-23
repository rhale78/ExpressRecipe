# HighSpeedDAL Performance Optimization Plan

## Critical Bottlenecks Identified

### 1. **LINQ Searches Instead of Dictionary Lookups** (HIGH IMPACT)
**Current:** `_inMemoryTable.Select().FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase) && !e.IsDeleted)`
- Scans entire table (131K ingredients = 131K iterations)
- O(n) complexity instead of O(1)

**Solution:**
- Add method to InMemoryTable: `GetByPropertyAsync(string propertyName, object value)`
- Maintains lazy-built dictionary cache `Dictionary<string, Dictionary<string|int, TEntity>>`
- Source generator calls cached lookup method instead of `.Select().FirstOrDefault()`
- **Estimated gain:** 50-100x faster for indexed lookups

### 2. **ToEntity Overhead (35% of execution time)** (HIGH IMPACT)
**Current:** Every Select() converts InMemoryRow to TEntity by copying all property values
```csharp
public IEnumerable<TEntity> Select()
{
    return _rows.Values
        .Where(r => r.State != RowState.Deleted)
        .Select(r => r.ToEntity<TEntity>());  // ← Expensive generic conversion
}
```

**Problems:**
- ToEntity does property-by-property copy reflection
- `.Select().FirstOrDefault()` calls ToEntity on EVERY row, then filters
- ConvertValue calls with GetValueOrDefault eating time

**Solution:**
- Add `GetByProperty` that returns InMemoryRow directly, not TEntity
- Only convert to TEntity if found (not for every row)
- Cache frequently accessed entities in a dictionary
- **Estimated gain:** 30-50% reduction in conversion overhead

### 3. **Non-Compiled Regex** (MEDIUM IMPACT)
**Current:** `IsValidIngredient` uses non-compiled regex pattern (lines in validation code)

**Solution:**
```csharp
private static readonly Regex ValidationRegex = new Regex(pattern, RegexOptions.Compiled);
```
- **Estimated gain:** 10-20% for validation-heavy operations

### 4. **Generic Type Instantiation** (MEDIUM IMPACT)
**Current:** Source generator creates `new TEntity()` which uses reflection
**Solution:** Direct instantiation `new IngredientEntity()` in specific DAL
- **Estimated gain:** 5-10%

### 5. **Excessive GetAllAsync Calls** (ALREADY PARTIALLY FIXED)
- ✅ FIXED: DALs now Singleton to prevent table reloads
- REMAINING: Some repository methods still calling GetAllAsync
- **Action:** Audit and replace with GetByProperty calls

## Implementation Priority

| Priority | Item | Effort | Gain | Status |
|----------|------|--------|------|--------|
| 1 | Singleton DAL registration | ✓ DONE | 46s/batch | ✓ DONE |
| 2 | GetByProperty with cached dict | Medium | 50-100x lookup | 📋 PLAN |
| 3 | Eliminate Select().Where()ToEntity | Medium | 30-50% | 📋 PLAN |
| 4 | Compiled Regex | Low | 10-20% | 📋 PLAN |
| 5 | Direct object instantiation | Low | 5-10% | 📋 PLAN |

## Expected Overall Performance Improvement

**Before:** 6.1 items/sec → 77+ hours for 1.7M
**After all optimizations:** 200-500+ items/sec → 1-2 hours for 1.7M

(25-80x speedup)

## Next Steps

1. Add `GetByPropertyAsync(propertyName, value)` to InMemoryTable with cache dict
2. Modify source generator to use property lookup instead of `.Select().FirstOrDefault()`
3. Implement cached entity storage for frequently accessed entities
4. Add regex compilation in validation code
5. Profile & measure improvements

## Notes

- User correctly identified that storing refs is same memory cost as storing IDs
- LINQ on 131K items kills perf - need O(1) dictionary lookups
- ToEntity conversion is expensive - avoid converting entire table just to filter one row
- 35% of time in ConvertValue/GetValueOrDefault - major bottleneck
