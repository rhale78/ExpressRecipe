# Performance Optimization Summary

## ✅ COMPLETED FIXES

### 1. Singleton DAL Registration (CRITICAL)
**Status:** ✅ DONE

**What was fixed:**
- Changed all DAL registrations from `Scoped` to `Singleton` in `Program.cs`
- This prevents creating new DAL instances for each service scope
- New DAL instance = reload entire in-memory table from database

**Impact:**
- **Eliminated 46 second reload after each batch**
- Before: 46s reload × 860 batches = ~34 hours lost to reloads
- After: No reloads, in-memory tables persist across batch operations

**Files modified:**
- `Program.cs` (lines 75-132)
- Added unique constraint migration on ProductImage (ProductId, ImageUrl)

---

## 📋 HIGH-PRIORITY REMAINING OPTIMIZATIONS

### 2. Dictionary-Based Lookups for Named Queries (HIGH IMPACT)
**Status:** 📋 PLANNED

**Problem:**
- GetByNameAsync uses `_inMemoryTable.Select().FirstOrDefault(e => string.Equals(e.Name, name, ...))`
- Scans entire table: O(n) = 131,000 iterations for each lookup
- Called 50+ times per batch (for ingredient lookups)

**Solution:**
- Add `GetByPropertyAsync(propertyName, value)` method to `InMemoryTable<TEntity>`
- Maintains lazy-built cache: `Dictionary<string, Dictionary<string, TEntity>>`
- Source generator calls cached lookup instead of `.Select().FirstOrDefault()`

**Estimated gain:** 50-100x faster ingredient lookups

**Files to modify:**
- `src/HighSpeedDAL/src/HighSpeedDAL.Core/InMemoryTable/InMemoryTable.cs` - Add GetByProperty method
- `src/HighSpeedDAL/src/HighSpeedDAL.SourceGenerators/Generation/DalClassGenerator.Part4.cs` - Use property lookup

---

### 3. Eliminate ToEntity Conversion Overhead (HIGH IMPACT - 35% of time!)
**Status:** 📋 PLANNED

**Problem:**
- Every `.Select()` converts InMemoryRow to TEntity via reflection
- `.Select().FirstOrDefault(predicate)` converts ALL rows, then filters on result
- ConvertValue/GetValueOrDefault calls eating 35% of execution time

**Example of waste:**
```csharp
// Current - converts all 131K ingredients to objects, then filters
var result = _inMemoryTable.Select()  // ← ToEntity called 131K times
    .FirstOrDefault(e => e.Name == "Sugar" && !e.IsDeleted);
```

**Solution:**
- Return InMemoryRow from Select(predicate)
- Only convert to TEntity if found or needed
- Cache frequently accessed entities to avoid repeated conversions

**Estimated gain:** 30-50% reduction in conversion overhead

**Key change needed:**
- Modify `Select()` to have optional `skipConversion` parameter
- Or create `SelectRaw()` that returns InMemoryRow for filtering

---

### 4. Compile Regex Patterns (MEDIUM IMPACT)
**Status:** 📋 PLANNED

**Problem:**
- `IsValidIngredient` and other validation use non-compiled regex
- Regex pattern recompiled on every validation call

**Solution:**
```csharp
private static readonly Regex NameValidationRegex = new Regex(
    pattern,
    RegexOptions.Compiled | RegexOptions.IgnoreCase
);
```

**Estimated gain:** 10-20% for validation-heavy operations

**Files to modify:**
- Validation code in `IngredientEntityDal` or validation helper

---

### 5. Direct Object Instantiation (LOW IMPACT)
**Status:** 📋 PLANNED

**Problem:**
- Source generator uses `new TEntity()` which uses reflection
- TEntity is generic type, requires reflection to instantiate

**Solution:**
- Generate direct `new IngredientEntity()` instead of `new TEntity()`
- Eliminates reflection overhead for object creation

**Estimated gain:** 5-10%

---

## Performance Benchmarks

### Current Baseline
```
Speed: 6.1 items/sec
Total Items: 1,717,837
Time: 77+ hours
Processing per batch: 5m 27s (2000 items) + 46s reload = ~5m 73s
```

### Expected After All Optimizations
```
Speed: 200-500+ items/sec (25-80x improvement)
Total Time: 1-2 hours for 1.7M items
Per batch: 4-10 seconds (2000 items)
```

---

## Implementation Priority & Effort

| # | Fix | Effort | Impact | Status |
|---|-----|--------|--------|--------|
| 1 | Singleton DAL | ✓ DONE | 46s/batch | ✓ **COMPLETE** |
| 2 | Dict-based lookups | Medium | 50-100x | 📋 Next |
| 3 | ToEntity overhead | Medium | 30-50% | 📋 Next |
| 4 | Compiled regex | Low | 10-20% | 📋 Later |
| 5 | Direct instantiation | Low | 5-10% | 📋 Later |

---

## Key Code Locations

**HighSpeedDAL Source Generators:**
- `src/HighSpeedDAL/src/HighSpeedDAL.SourceGenerators/Generation/DalClassGenerator.Part4.cs` (line 164)
  - `GenerateInMemoryTableFilter()` - Generates LINQ query code

**InMemoryTable:**
- `src/HighSpeedDAL/src/HighSpeedDAL.Core/InMemoryTable/InMemoryTable.cs` (line 417)
  - `Select()` - Converts rows to entities, should be optimized
  - Need to add: `GetByPropertyAsync()` method

**Generated DAL Code:**
- `obj/Generated/HighSpeedDAL.SourceGenerators/.../IngredientEntityDal.g.cs` (line 941)
  - `GetByNameAsync()` - Uses inefficient `.Select().FirstOrDefault()`

---

## Next Steps

1. **Add GetByProperty to InMemoryTable**
   - Create cached dictionary for indexed/unique properties
   - O(1) lookup instead of O(n) LINQ scan

2. **Modify source generator**
   - Detect single-property queries on indexed columns
   - Generate GetByProperty call instead of Select().FirstOrDefault()

3. **Optimize Select() method**
   - Add option to skip ToEntity conversion for filtering
   - Return InMemoryRow for predicate filtering

4. **Add compiled regex patterns**
   - Static readonly patterns in validation code
   - RegexOptions.Compiled flag

5. **Test & Benchmark**
   - Profile before/after each change
   - Measure actual speedup vs theoretical

---

## Notes

- Singleton DAL fix is the single biggest win (46 sec per batch)
- LINQ scans on 131K rows are the second biggest bottleneck
- ToEntity reflection overhead is systemic - affects every lookup
- These fixes are complementary - combining them gives exponential benefit
- After Singleton fix, dict-based lookups become next critical path
