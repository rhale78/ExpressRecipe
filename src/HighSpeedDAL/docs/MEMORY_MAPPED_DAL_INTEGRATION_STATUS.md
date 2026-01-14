# Memory-Mapped DAL Integration - Implementation Progress

## Current Status: **IN PROGRESS** (60% Complete)

### ? Completed Steps

1. **New Attribute Created**: `MemoryMappedTableAttribute`
   - File: `src\HighSpeedDAL.Core\Attributes\MemoryMappedTableAttribute.cs`
   - Properties: FileName, SizeMB, SyncMode, FlushIntervalSeconds, AutoCreateFile, AutoLoadOnStartup, ReadOnlyCache, MaxCachedRows, TimeToLiveSeconds
   - Full XML documentation with examples

2. **Entity Parser Updated**
   - File: `src\HighSpeedDAL.SourceGenerators\Parsing\EntityParser.cs`
   - Added: `ParseMemoryMappedTableAttribute()` method
   - Detects `[MemoryMappedTable]` attribute on entities

3. **Entity Metadata Extended**
   - File: `src\HighSpeedDAL.SourceGenerators\Models\EntityMetadata.cs`
   - Added 10 new properties for memory-mapped configuration
   - `HasMemoryMappedTable`, `MemoryMappedFileName`, etc.

4. **Build Verification**
   - ? HighSpeedDAL.Core builds successfully
   - ? HighSpeedDAL.SourceGenerators builds successfully

### ?? Remaining Steps

#### Step 1: Update DAL Class Generator (LARGEST TASK)
**File**: `src\HighSpeedDAL.SourceGenerators\Generation\DalClassGenerator.Part1.cs` and `Part2.cs`

**Required Changes**:

##### A. Add Memory-Mapped Store Field
```csharp
// In GenerateClass method, add field:
if (metadata.HasMemoryMappedTable)
{
    sb.AppendLine($"    private readonly MemoryMappedFileStore<{metadata.ClassName}>? _memoryMappedStore;");
    sb.AppendLine($"    private readonly Timer? _memoryMappedSyncTimer;");
}
```

##### B. Initialize in Constructor
```csharp
// In GenerateConstructor method:
if (metadata.HasMemoryMappedTable)
{
    sb.AppendLine($"        // Initialize memory-mapped L0 cache");
    sb.AppendLine($"        if (loggerFactory != null)");
    sb.AppendLine($"        {{");
    sb.AppendLine($"            var mmfConfig = new InMemoryTableAttribute");
    sb.AppendLine($"            {{");
    sb.AppendLine($"                MemoryMappedFileName = \"{metadata.MemoryMappedFileName}\",");
    sb.AppendLine($"                MemoryMappedFileSizeMB = {metadata.MemoryMappedSizeMB},");
    sb.AppendLine($"                SyncMode = (MemoryMappedSyncMode){metadata.MemoryMappedSyncMode},");
    sb.AppendLine($"                FlushIntervalSeconds = {metadata.MemoryMappedFlushIntervalSeconds},");
    sb.AppendLine($"                AutoCreateFile = {metadata.MemoryMappedAutoCreateFile.ToString().ToLower()},");
    sb.AppendLine($"                AutoLoadOnStartup = {metadata.MemoryMappedAutoLoadOnStartup.ToString().ToLower()}");
    sb.AppendLine($"            }};");
    sb.AppendLine();
    sb.AppendLine($"            var storeLogger = loggerFactory.CreateLogger<MemoryMappedFileStore<{metadata.ClassName}>>();");
    sb.AppendLine($"            var syncLogger = loggerFactory.CreateLogger<MemoryMappedSynchronizer>();");
    sb.AppendLine($"            _memoryMappedStore = new MemoryMappedFileStore<{metadata.ClassName}>(");
    sb.AppendLine($"                mmfConfig.MemoryMappedFileName,");
    sb.AppendLine($"                mmfConfig,");
    sb.AppendLine($"                storeLogger,");
    sb.AppendLine($"                syncLogger);");
    sb.AppendLine();
    sb.AppendLine($"            // Auto-load if configured");
    sb.AppendLine($"            if (mmfConfig.AutoLoadOnStartup)");
    sb.AppendLine($"            {{");
    sb.AppendLine($"                var loadedRows = _memoryMappedStore.LoadAsync().GetAwaiter().GetResult();");
    sb.AppendLine($"                // Rows are now in memory-mapped file, will be checked on reads");
    sb.AppendLine($"            }}");
    sb.AppendLine();
    sb.AppendLine($"            // Setup sync timer for batched mode");
    sb.AppendLine($"            if (mmfConfig.SyncMode == MemoryMappedSyncMode.Batched && mmfConfig.FlushIntervalSeconds > 0)");
    sb.AppendLine($"            {{");
    sb.AppendLine($"                _memoryMappedSyncTimer = new Timer(");
    sb.AppendLine($"                    _ => FlushMemoryMappedCacheAsync().GetAwaiter().GetResult(),");
    sb.AppendLine($"                    null,");
    sb.AppendLine($"                    TimeSpan.FromSeconds(mmfConfig.FlushIntervalSeconds),");
    sb.AppendLine($"                    TimeSpan.FromSeconds(mmfConfig.FlushIntervalSeconds));");
    sb.AppendLine($"            }}");
    sb.AppendLine($"        }}");
}
```

##### C. Modify GetByIdAsync (L0 Cache Check)
```csharp
// BEFORE database query:
if (metadata.HasMemoryMappedTable)
{
    sb.AppendLine($"        // Check L0 cache (memory-mapped file) first");
    sb.AppendLine($"        if (_memoryMappedStore != null)");
    sb.AppendLine($"        {{");
    sb.AppendLine($"            var cachedRows = await _memoryMappedStore.LoadAsync(cancellationToken);");
    sb.AppendLine($"            var cachedRow = cachedRows.FirstOrDefault(r => r.{metadata.PrimaryKeyProperty.PropertyName} == id);");
    sb.AppendLine($"            if (cachedRow != null)");
    sb.AppendLine($"            {{");
    sb.AppendLine($"                _logger.LogDebug(\"L0 cache HIT for {{EntityType}} ID={{Id}}\", typeof({metadata.ClassName}).Name, id);");
    sb.AppendLine($"                return cachedRow;");
    sb.AppendLine($"            }}");
    sb.AppendLine($"            _logger.LogDebug(\"L0 cache MISS for {{EntityType}} ID={{Id}}\", typeof({metadata.ClassName}).Name, id);");
    sb.AppendLine($"        }}");
    sb.AppendLine();
}

// Existing L1/L2 cache checks follow...
// Then database query...

// AFTER successful database query:
if (metadata.HasMemoryMappedTable)
{
    sb.AppendLine($"        // Update L0 cache (memory-mapped file)");
    sb.AppendLine($"        if (_memoryMappedStore != null && entity != null)");
    sb.AppendLine($"        {{");
    sb.AppendLine($"            await UpdateMemoryMappedCacheAsync(entity, cancellationToken);");
    sb.AppendLine($"        }}");
}
```

##### D. Modify GetAllAsync (L0 Cache)
```csharp
if (metadata.HasMemoryMappedTable)
{
    sb.AppendLine($"        // Check if L0 cache has data");
    sb.AppendLine($"        if (_memoryMappedStore != null)");
    sb.AppendLine($"        {{");
    sb.AppendLine($"            var cachedRows = await _memoryMappedStore.LoadAsync(cancellationToken);");
    sb.AppendLine($"            if (cachedRows.Count > 0)");
    sb.AppendLine($"            {{");
    sb.AppendLine($"                _logger.LogDebug(\"L0 cache returned {{Count}} rows for {{EntityType}}\", cachedRows.Count, typeof({metadata.ClassName}).Name);");
    sb.AppendLine($"                return cachedRows;");
    sb.AppendLine($"            }}");
    sb.AppendLine($"        }}");
}

// Then query database and update L0 cache with results
```

##### E. Modify InsertAsync/UpdateAsync (L0 Cache Invalidation)
```csharp
// AFTER successful database write:
if (metadata.HasMemoryMappedTable)
{
    sb.AppendLine($"        // Invalidate/update L0 cache");
    sb.AppendLine($"        if (_memoryMappedStore != null)");
    sb.AppendLine($"        {{");
    sb.AppendLine($"            await UpdateMemoryMappedCacheAsync(entity, cancellationToken);");
    sb.AppendLine($"        }}");
}
```

##### F. Add Helper Methods
```csharp
// Add to DAL class:
private async Task UpdateMemoryMappedCacheAsync(TEntity entity, CancellationToken cancellationToken = default)
{
    if (_memoryMappedStore == null) return;
    
    try
    {
        // Load current cache
        var cached = await _memoryMappedStore.LoadAsync(cancellationToken);
        
        // Update or add entity
        var existing = cached.FirstOrDefault(e => e.Id == entity.Id);
        if (existing != null)
        {
            cached.Remove(existing);
        }
        cached.Add(entity);
        
        // Save back
        await _memoryMappedStore.SaveAsync(cached, cancellationToken);
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to update memory-mapped cache for {EntityType}", typeof(TEntity).Name);
    }
}

private async Task FlushMemoryMappedCacheAsync(CancellationToken cancellationToken = default)
{
    if (_memoryMappedStore == null) return;
    
    try
    {
        // Reload from database
        var allRows = await GetAllAsync(cancellationToken);
        await _memoryMappedStore.SaveAsync(allRows, cancellationToken);
        _logger.LogInformation("Flushed {Count} rows to memory-mapped cache for {EntityType}", 
            allRows.Count, typeof(TEntity).Name);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to flush memory-mapped cache for {EntityType}", typeof(TEntity).Name);
    }
}
```

##### G. Add Dispose Logic
```csharp
// In Dispose method:
if (metadata.HasMemoryMappedTable)
{
    sb.AppendLine($"        _memoryMappedSyncTimer?.Dispose();");
    sb.AppendLine($"        _memoryMappedStore?.Dispose();");
}
```

#### Step 2: Constructor Signature Change
**Challenge**: DAL constructors currently don't accept `ILoggerFactory`

**Options**:
1. **Add optional parameter** (Breaking change for existing code)
   ```csharp
   public UserDal(
       DatabaseConnection connection,
       ILogger<UserDal> logger,
       ICache? cache = null,
       ILoggerFactory? loggerFactory = null) // NEW
   ```

2. **Constructor overload** (Non-breaking)
   ```csharp
   // Existing constructor (no memory-mapped support)
   public UserDal(DatabaseConnection connection, ILogger<UserDal> logger, ICache? cache = null)
   
   // New constructor (with memory-mapped support)
   public UserDal(DatabaseConnection connection, ILogger<UserDal> logger, ICache? cache, ILoggerFactory loggerFactory)
   ```

**Recommendation**: Option 2 (overload) to avoid breaking existing code.

#### Step 3: Example Entity Update
**File**: `examples\SimpleCrudExample\Entities\User.cs`

Add attribute:
```csharp
[MemoryMappedTable(FileName = "UserCache", SizeMB = 100, SyncMode = MemoryMappedSyncMode.Batched)]
[Cached(Strategy = CacheStrategy.TwoLayer, MaxSize = 1000)]
[Table("Users")]
[DalEntity]
public class User
{
    // ... properties
}
```

#### Step 4: Update Program.cs to Pass ILoggerFactory
**File**: `examples\SimpleCrudExample\Program.cs`

```csharp
// Add ILoggerFactory to DI
services.AddSingleton<ILoggerFactory>(sp => sp.GetRequiredService<ILoggerFactory>());

// Update DAL registration to pass factory
services.AddSingleton<UserDal>(sp => new UserDal(
    sp.GetRequiredService<UserDatabaseConnection>(),
    sp.GetRequiredService<ILogger<UserDal>>(),
    sp.GetRequiredService<ICache>(),
    sp.GetRequiredService<ILoggerFactory>() // NEW
));
```

#### Step 5: Add Benchmarks
**File**: `examples\SimpleCrudExample\BenchmarkRunner.cs`

Add new benchmark method:
```csharp
private async Task BenchmarkMemoryMappedDAL()
{
    Console.WriteLine("\n=== Memory-Mapped DAL Benchmarks ===");
    
    // Test L0 cache performance
    // 1. Cold start (empty cache)
    // 2. Warm cache (loaded from file)
    // 3. Write-through performance
    // 4. Cross-process simulation
}
```

#### Step 6: Create Multi-Process Example
**New Project**: `examples\MultiProcessQueueExample`

Demonstrate:
- Producer service writing to queue
- Multiple consumer services reading from same memory-mapped file
- Real cross-process data sharing

### Estimated Remaining Effort

- **DAL Generator Changes**: 4-5 hours (complex, many methods to update)
- **Testing & Debugging**: 2-3 hours
- **Example Updates**: 1 hour
- **Documentation**: 1 hour

**Total**: 8-10 hours

### Decision Needed

Given the scope of remaining work, we should decide:

**Option A: Complete Full Implementation**
- Pros: Full feature parity, works with all DAL features
- Cons: 8-10 hours of work, complex changes to generator
- Best for: Production use, long-term maintenance

**Option B: Simplified Hybrid Approach**
- Create wrapper class that uses InMemoryTable + periodic DB sync
- Much faster implementation (2-3 hours)
- Less integrated but functional
- Best for: Quick prototyping, proof of concept

**Option C: Document Current State & Defer**
- Keep InMemoryTable with memory-mapped files (already working)
- Document how to use it for queue scenarios
- Defer DAL integration until real-world need confirmed
- Best for: Validation phase, MVP

### My Recommendation

I suggest **Option C** for now:

1. **Current Implementation Works**:
   - `InMemoryTable<T>` with memory-mapped files is fully functional
   - Suitable for queue scenarios (your primary use case)
   - Already benchmarked and tested

2. **DAL Integration is Complex**:
   - Requires changes to all CRUD methods
   - Constructor signature changes (breaking or overload complexity)
   - More testing surface area
   - Higher maintenance burden

3. **You Can Use Both**:
   - Use `InMemoryTable` for queues/caches (cross-process)
   - Use standard DAL for database operations (with L1/L2 cache)
   - Keep concerns separated

4. **Easier to Add Later**:
   - If you need DAL + memory-mapped, we have the design ready
   - Can implement incrementally (one method at a time)
   - Can validate need with real usage first

### What You Get Today

**Fully Functional**:
- ? InMemoryTable with memory-mapped files (cross-process queues)
- ? Standard DAL with L1/L2 caching (database operations)
- ? Benchmarks showing performance
- ? Documentation and examples

**Would Need Full Implementation For**:
- ? DAL classes using memory-mapped as L0 cache
- ? Automatic cache population from database queries
- ? Unified cache hierarchy (L0 + L1 + L2 + DB)

### Next Action?

Please choose:
1. **Proceed with full DAL integration** (8-10 hours) - I'll complete steps 1-6
2. **Try simplified hybrid approach** (2-3 hours) - Wrapper class solution
3. **Keep current state** (0 hours) - Use InMemoryTable for queues, DAL for database
4. **Something else** - Tell me what you need

What's your preference?
