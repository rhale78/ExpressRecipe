# ProductService HighSpeedDAL Configuration Status

**Date:** 2026-01-13
**Status:** ⚠️ **BLOCKED** - Source Generator Guid/Int Type Mismatch

---

## What Has Been Completed ✅

### 1. Entity Classes Created with InMemoryTable Configuration

All entities configured with **30-second flush to SQL** as requested:

- ✅ **ProductEntity.cs** - Existing, added InMemoryTable attributes
  - `[InMemoryTable(FlushIntervalSeconds = 30, MaxRowCount = 100000)]`
  - `[Cache(CacheStrategy.TwoLayer, MaxSize = 10000, ExpirationSeconds = 900)]`

- ✅ **IngredientEntity.cs** - Existing, added InMemoryTable attributes
  - `[InMemoryTable(FlushIntervalSeconds = 30, MaxRowCount = 100000)]`
  - `[Cache(CacheStrategy.TwoLayer, MaxSize = 20000, ExpirationSeconds = 1800)]`

- ✅ **ProductImageEntity.cs** - **NEWLY CREATED**
  - `[InMemoryTable(FlushIntervalSeconds = 30, MaxRowCount = 200000)]`
  - `[Cache(CacheStrategy.TwoLayer, MaxSize = 50000, ExpirationSeconds = 900)]`

- ✅ **ProductStagingEntity.cs** - **NEWLY CREATED**
  - `[InMemoryTable(FlushIntervalSeconds = 30, MaxRowCount = 50000)]`
  - `[Cache(CacheStrategy.Memory, MaxSize = 10000, ExpirationSeconds = 300)]`

- ✅ **ProductAllergenEntity.cs** - **NEWLY CREATED**
  - `[InMemoryTable(FlushIntervalSeconds = 30, MaxRowCount = 100000)]`
  - `[Cache(CacheStrategy.TwoLayer, MaxSize = 20000, ExpirationSeconds = 1800)]`

All entities also include:
- `[AutoAudit]` - Automatic CreatedAt/UpdatedAt tracking
- `[SoftDelete]` - Soft delete support
- `[MessagePackObject]` - Serialization support
- `[DalEntity]` - Triggers HighSpeedDAL source generator

### 2. Program.cs Configuration

✅ **Added HighSpeedDAL Infrastructure:**
```csharp
// Register ProductDatabaseConnection for HighSpeedDAL
builder.Services.AddSingleton<ProductDatabaseConnection>();

// Register InMemoryTableManager for 30s flush to SQL
builder.Services.AddSingleton<InMemoryTableManager>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<InMemoryTableManager>>();
    var connectionFactory = sp.GetRequiredService<ProductDatabaseConnection>();
    return new InMemoryTableManager(logger, () =>
    {
        var conn = new Microsoft.Data.SqlClient.SqlConnection(connectionFactory.ConnectionString);
        return conn;
    });
});
```

✅ **DAL class registrations prepared** (commented out until build succeeds):
```csharp
// builder.Services.AddScoped<ProductEntityDal>();
// builder.Services.AddScoped<IngredientEntityDal>();
// builder.Services.AddScoped<ProductImageEntityDal>();
// builder.Services.AddScoped<ProductStagingEntityDal>();
// builder.Services.AddScoped<ProductAllergenEntityDal>();
```

### 3. Project Configuration

✅ **.csproj updated** with HighSpeedDAL references:
- HighSpeedDAL.Core
- HighSpeedDAL.SqlServer
- HighSpeedDAL.SourceGenerators (enabled)

---

## Critical Issue ❌

### HighSpeedDAL Source Generator - Guid/Int Type Mismatch

**Problem:** The HighSpeedDAL source generator does **NOT properly handle Guid primary keys**.

Even though entities specify:
```csharp
[Table("Product", PrimaryKeyType = PrimaryKeyType.Guid)]
[PrimaryKey]
public Guid Id { get; set; }
```

The generated DAL code expects `int` IDs, causing **20+ compilation errors**:

```
error CS1503: Argument 1: cannot convert from 'System.Guid' to 'int'
error CS0029: Cannot implicitly convert type 'int' to 'System.Guid'
```

**Affected Files:**
- ProductEntityDal.g.cs
- IngredientEntityDal.g.cs
- ProductImageEntityDal.g.cs
- ProductStagingEntityDal.g.cs
- ProductAllergenEntityDal.g.cs

**Current State:** Build fails with 20 errors, 12 warnings

---

## User Report vs. Observed Behavior

**User states:** "I'm not seeing the guid/int id issues when I compile"

**Claude observes:** Build consistently fails with Guid/int type mismatch errors

**Possible explanations:**
1. User has a patched/updated version of HighSpeedDAL.SourceGenerators
2. User's environment is different (different .NET SDK, different HighSpeedDAL version)
3. User's entities use `int` IDs instead of `Guid` IDs
4. There's a configuration setting that fixes this that we're missing

---

## Next Steps - Three Options

### Option 1: Fix HighSpeedDAL Source Generator ⭐ (BEST)

**What:** Fix the Guid support bug in `HighSpeedDAL.SourceGenerators`

**Location:** `src/HighSpeedDAL/src/HighSpeedDAL.SourceGenerators/`

**Benefits:**
- Proper auto-generated DAL classes
- Full in-memory table support with 30s flush
- All HighSpeedDAL features work correctly

**Effort:** Medium (requires understanding source generator codebase)

### Option 2: Manual DAL Classes Following HighSpeedDAL Pattern

**What:** Manually write DAL classes that:
- Inherit from `SqlServerDalBase<TEntity, ProductDatabaseConnection>`
- Use `InMemoryTableManager` for in-memory operations
- Implement 30s flush to SQL manually

**Benefits:**
- Works around source generator bug
- Still gets in-memory tables with SQL backing
- Follows HighSpeedDAL architecture

**Effort:** High (5 DAL classes × significant code each)

### Option 3: Keep Current Repositories, Add In-Memory Layer

**What:** Enhance existing `ProductRepository` and `IngredientRepository`:
- Keep current SqlHelper-based code
- Add in-memory caching wrapper
- Implement manual 30s flush

**Benefits:**
- Minimal changes to working code
- Simpler implementation
- No dependency on broken source generator

**Effort:** Medium (wrapper + flush logic for each repo)

---

## Recommended Action

**Please clarify:**

1. **Are you able to compile successfully?** If yes:
   - What .NET SDK version are you using?
   - What version of HighSpeedDAL are you referencing?
   - Can you share your build output?

2. **Which option do you prefer?**
   - Fix the source generator (best long-term)
   - Manual DAL classes following HighSpeedDAL pattern
   - Enhanced current repositories with in-memory layer

3. **Database schema question:**
   - All tables currently use `UNIQUEIDENTIFIER` (Guid) primary keys
   - Should we migrate to `INT IDENTITY` to match HighSpeedDAL expectations?
   - Or keep Guids and work around the source generator?

---

## Files Modified

1. `src/Services/ExpressRecipe.ProductService/Entities/ProductEntity.cs` - Added attributes
2. `src/Services/ExpressRecipe.ProductService/Entities/IngredientEntity.cs` - Added attributes
3. `src/Services/ExpressRecipe.ProductService/Entities/ProductImageEntity.cs` - **NEW FILE**
4. `src/Services/ExpressRecipe.ProductService/Entities/ProductStagingEntity.cs` - **NEW FILE**
5. `src/Services/ExpressRecipe.ProductService/Entities/ProductAllergenEntity.cs` - **NEW FILE**
6. `src/Services/ExpressRecipe.ProductService/Program.cs` - Added HighSpeedDAL configuration
7. `src/Services/ExpressRecipe.ProductService/ExpressRecipe.ProductService.csproj` - Enabled source generator

---

## Build Output Summary

```
Build started...
Restored successfully
Compiling...
HighSpeedDAL.SourceGenerators generating DAL classes...
❌ 20 Errors (all Guid/int type mismatches)
⚠️  12 Warnings (XML comments, nullable references)
Build FAILED
Time Elapsed: 00:00:02.24
```

---

**Waiting for user direction before proceeding.**
