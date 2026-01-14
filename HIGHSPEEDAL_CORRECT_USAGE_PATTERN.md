# HighSpeedDAL Framework - Correct Usage Pattern

## Problem
We were **fighting the framework** by:
1. Adding manual `HybridCacheService` calls when `[Cache]` attribute already handles caching
2. Creating custom `InMemoryBackingStore` when `[InMemoryTable]` attribute already provides it
3. Not inheriting from `SqlServerDalBase` which provides core functionality

## How HighSpeedDAL Actually Works

### 1. Entity Configuration
```csharp
[Table("Product", PrimaryKeyType = PrimaryKeyType.Guid)]
[Cache(CacheStrategy.TwoLayer, MaxSize = 10000, ExpirationSeconds = 900)]
[InMemoryTable(FlushIntervalSeconds = 30, MaxRowCount = 100000)]
[DalEntity] // Triggers source generator
public class ProductEntity
{
    [PrimaryKey]
    public Guid Id { get; set; }
    
    [Index]
    public string Name { get; set; }
}
```

### 2. Source Generator Creates DAL
The `[DalEntity]` attribute triggers generation of:
```csharp
public sealed class ProductEntityDal : SqlServerDalBase<ProductEntity, ProductConnection>
{
    private readonly ICacheManager<ProductEntity, Guid>? _cache;
    private readonly InMemoryTable<ProductEntity>? _memoryTable;
    
    public ProductEntityDal(
        ProductConnection connection,
        ILogger<ProductEntityDal> logger,
        ICacheManager<ProductEntity, Guid>? cache = null,
        InMemoryTable<ProductEntity>? memoryTable = null)
        : base(connection, logger)
    {
        _cache = cache; // Automatically used based on [Cache] attribute
        _memoryTable = memoryTable; // Automatically used based on [InMemoryTable] attribute
    }
    
    public async Task<ProductEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        // 1. Check cache (if configured)
        if (_cache != null)
        {
            var cached = await _cache.GetAsync(id, ct);
            if (cached != null) return cached;
        }
        
        // 2. Check in-memory table (if configured)
        if (_memoryTable != null)
        {
            var inMemory = _memoryTable.GetById(id);
            if (inMemory != null)
            {
                // Populate cache
                if (_cache != null) await _cache.SetAsync(id, inMemory, ct);
                return inMemory;
            }
        }
        
        // 3. Load from SQL
        var result = await ExecuteGetByIdAsync(id, ct);
        
        // 4. Populate both caches
        if (result != null)
        {
            if (_memoryTable != null) await _memoryTable.InsertAsync(result, ct);
            if (_cache != null) await _cache.SetAsync(id, result, ct);
        }
        
        return result;
    }
    
    public async Task<Guid> InsertAsync(ProductEntity entity, CancellationToken ct = default)
    {
        if (entity.Id == Guid.Empty)
        {
            entity.Id = Guid.NewGuid();
        }
        
        // Write to in-memory table first (instant)
        if (_memoryTable != null)
        {
            await _memoryTable.InsertAsync(entity, ct);
            // Auto-flushes to SQL every 30 seconds (from [InMemoryTable] config)
        }
        else
        {
            // Direct SQL write if no in-memory table
            await ExecuteInsertAsync(entity, ct);
        }
        
        // Invalidate cache
        if (_cache != null) await _cache.RemoveAsync(entity.Id, ct);
        
        return entity.Id;
    }
    
    // ... other CRUD methods follow same pattern
}
```

### 3. The Framework Handles
- ? **Caching** - Based on `[Cache]` attribute, automatic L1/L2 management
- ? **In-Memory Writes** - Based on `[InMemoryTable]` attribute, automatic 30s flush
- ? **SQL Fallback** - Automatic when cache/memory miss
- ? **Cache Invalidation** - On write operations
- ? **Bulk Operations** - Optimized batch inserts/updates
- ? **Retry Logic** - Built into `SqlServerDalBase`

### 4. What YOU Should Do
**Option A: Fix the Source Generator** (Best long-term)
1. Fix the Guid/int type mismatch bug in `HighSpeedDAL.SourceGenerators`
2. Re-enable `[DalEntity]` attribute on entities
3. Let the generator create all DAL code automatically
4. Register generated DALs in DI

**Option B: Manual DAL Following Framework Pattern** (Current workaround)
1. Manually write DAL classes that inherit from `SqlServerDalBase<TEntity, TConnection>`
2. Accept `ICacheManager` and `InMemoryTable` in constructor
3. Follow the 3-tier pattern: Cache ? Memory ? SQL
4. Let the framework handle flush timers and cache management

**Option C: Simple SqlHelper Approach** (What you have now)
1. Keep using `DalOperationsBase` with direct SQL
2. No caching, no in-memory tables
3. Simple, works, but slower writes

## Current Status
- ? Entities have `[Cache]` and `[Table]` attributes configured
- ? Source generator disabled due to Guid/int bug
- ? We were adding redundant manual caching (HybridCacheService)
- ? We were creating redundant InMemoryBackingStore

## Recommendation
**Delete all the V2 and custom backing store code** (which we just did ?)

Then choose:
1. **Fix source generator** - Tackle the Guid/int bug, enable `[DalEntity]` ? BEST
2. **Manual framework-compliant DAL** - Write what generator would create
3. **Keep current simple approach** - Direct SQL with `DalOperationsBase` ? FASTEST TO IMPLEMENT

## Example: Proper Manual DAL (Option B)
```csharp
public class ProductDal : SqlServerDalBase<ProductDto, ProductConnection>
{
    private readonly ICacheManager<ProductDto, Guid>? _cache;
    private readonly InMemoryTable<ProductDto>? _memoryTable;
    
    public ProductDal(
        ProductConnection connection,
        ILogger<ProductDal> logger,
        ICacheManager<ProductDto, Guid>? cache = null,
        InMemoryTableManager? tableManager = null)
        : base(connection, logger)
    {
        _cache = cache;
        
        if (tableManager != null)
        {
            var config = new InMemoryTableAttribute 
            { 
                FlushIntervalSeconds = 30, 
                MaxRowCount = 100000 
            };
            _memoryTable = tableManager.RegisterTable<ProductDto>(config, "Product");
        }
    }
    
    // Follow 3-tier pattern: Cache ? Memory ? SQL
    // Framework handles flush timers automatically
}
```

## Key Insight
**Don't add your own caching/memory layers.** 
**Use the framework's `ICacheManager` and `InMemoryTable` directly.**
**Let the attributes (`[Cache]`, `[InMemoryTable]`) drive behavior.**

This is the "pit of success" design - the framework makes it easy to do the right thing!
