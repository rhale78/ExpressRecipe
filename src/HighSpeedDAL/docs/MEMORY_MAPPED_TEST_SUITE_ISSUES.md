# Memory-Mapped Test Suite - Known Issues

## Summary

Successfully created a comprehensive memory-mapped file test suite for SimpleCrudExample demonstrating:
- Part 1: CRUD operations with InMemoryTable
- Part 2: Direct memory-mapped file operations
- Part 3: Performance benchmarks (Insert, Read, Update, Flush)

## Current Status

? **COMPLETED**:
- Full test suite implementation (~700 lines)
- Comprehensive documentation (README + implementation summary)
- Unique timestamped file names to avoid conflicts
- Benchmark framework with 4 detailed tests
- Integration into SimpleCrudExample (option 3 + `--mmf-tests`)

? **BLOCKED**: Test execution fails due to mutex thread-affinity issue in `MemoryMappedSynchronizer`

## Critical Issue: Mutex Thread Affinity

### Problem

The `MemoryMappedSynchronizer` uses `System.Threading.Mutex` for cross-process locking, but `Mutex.ReleaseMutex()` **must be called from the same thread that acquired it**. This is incompatible with async/await patterns where:

1. Thread A acquires the mutex via `Mutex.WaitOne()`
2. Async operation awaits (e.g., `await SaveAsync()`)
3. Thread B resumes execution after await
4. Thread B tries to release the mutex ? **ApplicationException**: "Object synchronization method was called from an unsynchronized block of code"

### Error Stack Trace

```
System.ApplicationException: Object synchronization method was called from an unsynchronized block of code.
   at System.Threading.Mutex.ReleaseMutex()
   at HighSpeedDAL.Core.InMemoryTable.MemoryMappedSynchronizer.WriteLockHandle.Dispose()
```

### Where It Fails

1. **Batched Sync Mode**: Timer callback (`System.Threading.Timer`) runs on thread pool, tries to flush
2. **Immediate Sync Mode**: High-frequency lock/release in tight loop causes timeouts
3. **Update Benchmark**: Load?Modify?Save loop with repeated lock acquisition

### Why It Matters

- **All memory-mapped file operations are affected**
- **Cross-process locking doesn't work reliably with async patterns**
- **Test suite cannot execute benchmarks**

## Solution Required: Replace Mutex with SemaphoreSlim

### Recommended Fix

Replace `System.Threading.Mutex` with `System.Threading.SemaphoreSlim` in `MemoryMappedSynchronizer.cs`:

**Current (Broken)**:
```csharp
private readonly Mutex _writeMutex;  // Thread-affine!

public async Task<WriteLockHandle> AcquireWriteLockAsync(CancellationToken cancellationToken)
{
    bool acquired = await Task.Run(() => _writeMutex.WaitOne(_lockTimeout), cancellationToken);
    if (!acquired) throw new TimeoutException(...);
    return new WriteLockHandle(_writeMutex, ...);
}

private sealed class WriteLockHandle : IDisposable
{
    public void Dispose()
    {
        mutex.ReleaseMutex();  // FAILS if called from different thread!
    }
}
```

**Proposed (Async-Safe)**:
```csharp
private readonly SemaphoreSlim _writeSemaphore;  // Async-safe!

public async Task<WriteLockHandle> AcquireWriteLockAsync(CancellationToken cancellationToken)
{
    bool acquired = await _writeSemaphore.WaitAsync(_lockTimeout, cancellationToken);
    if (!acquired) throw new TimeoutException(...);
    return new WriteLockHandle(_writeSemaphore, ...);
}

private sealed class WriteLockHandle : IDisposable
{
    public void Dispose()
    {
        semaphore.Release();  // Works from any thread!
    }
}
```

### Trade-offs

**Mutex Advantages**:
- ? Cross-process locking (named system object)
- ? Ownership tracking (only owner can release)
- ? Thread-affine (incompatible with async/await)

**SemaphoreSlim Advantages**:
- ? Async-native (`WaitAsync`, `Release`)
- ? No thread affinity
- ? Better performance (lighter weight)
- ? **Single-process only** (cannot use for cross-process locking)

### Alternative: Named Semaphore

For cross-process support with async-safety, use `Semaphore` (not `SemaphoreSlim`):

```csharp
private readonly Semaphore _writeSemaphore = new Semaphore(1, 1, $"Global\\HighSpeedDAL_{fileName}");

public async Task<WriteLockHandle> AcquireWriteLockAsync(CancellationToken cancellationToken)
{
    bool acquired = await Task.Run(() => _writeSemaphore.WaitOne(_lockTimeout), cancellationToken);
    if (!acquired) throw new TimeoutException(...);
    return new WriteLockHandle(_writeSemaphore, ...);
}

private sealed class WriteLockHandle : IDisposable
{
    public void Dispose()
    {
        semaphore.Release();  // Works from any thread (unlike Mutex!)
    }
}
```

**Named Semaphore**:
- ? Cross-process locking
- ? No thread affinity (`Release()` works from any thread)
- ? Compatible with async/await
- ?? Still requires `Task.Run` wrapper (no native `WaitAsync`)

## Impact on Test Suite

Once the `MemoryMappedSynchronizer` is fixed:

1. ? Part 1 (CRUD) will work - batched sync mode timer flushes
2. ? Part 2 (Direct) will work - single Save/Load operations
3. ? Part 3 Benchmark 1 (Insert) will work - immediate mode stress test
4. ? Part 3 Benchmark 2 (Read) will work - single Load operation
5. ? Part 3 Benchmark 3 (Update) will work currently (skipped direct MMF)
6. ? Part 3 Benchmark 4 (Flush) will work - single flush per size

## Workarounds

### Current State

The test suite has been modified to:
1. **Use unique timestamped file names** (`_{_runTimestamp}`) to avoid lock conflicts
2. **Skip problematic benchmarks** (direct MMF update loop commented out)
3. **Add cleanup delays** (2-second pauses between benchmarks)
4. **Reduce iteration counts** (immediate mode: 100 inserts vs 10K)

### Manual Testing

Until the fix is implemented, test individual scenarios:

```bash
# Test 1: Single insert/flush (works if disposed properly)
dotnet run --project examples/SimpleCrudExample

# Test 2: Manual mode only (no automatic flushes)
# Modify code to use SyncMode.Manual instead of Batched

# Test 3: Direct MMF operations (single Save/Load)
# Works if no concurrent access
```

## Files Modified

| File | Changes | Status |
|------|---------|--------|
| `examples/SimpleCrudExample/MemoryMappedTestSuite.cs` | Complete test suite | ? Compiles |
| `examples/SimpleCrudExample/Program.cs` | Added option 3 + DI | ? Integrated |
| `examples/SimpleCrudExample/README_MEMORY_MAPPED_TEST_SUITE.md` | User guide | ? Complete |
| `docs/MEMORY_MAPPED_TEST_SUITE_SUMMARY.md` | Implementation summary | ? Complete |
| `docs/MEMORY_MAPPED_TEST_SUITE_ISSUES.md` | This document | ? Complete |

## Next Steps

1. **Fix `MemoryMappedSynchronizer.cs`**: Replace `Mutex` with `Semaphore` (cross-process) or `SemaphoreSlim` (single-process)
2. **Test cross-process locking**: Run 2+ instances simultaneously
3. **Re-enable commented benchmarks**: Uncomment direct MMF update loop
4. **Increase test counts**: Restore to 10K rows for immediate mode
5. **Measure actual performance**: Document real-world metrics

## Expected Results (After Fix)

Based on design expectations:

| Benchmark | InMemoryTable | Direct MMF | Winner |
|-----------|--------------|------------|---------|
| **INSERT (10K, batched)** | ~200K ops/sec | ~500K ops/sec | Direct MMF |
| **INSERT (100, immediate)** | ~5K ops/sec | ~500 ops/sec | InMemoryTable |
| **READ (1K random)** | ~500K ops/sec | ~666K ops/sec | Direct MMF |
| **UPDATE (1K incremental)** | ~100K ops/sec | ~66 ops/sec | **InMemoryTable (1,500x!)** |
| **FLUSH (10K rows)** | ~200K rows/sec | ~300K rows/sec | Direct MMF |

**Key Insight**: InMemoryTable dramatically faster for incremental updates (1,500x) because it modifies in-memory structures. Direct MMF requires load-modify-save cycle (serialize entire dataset).

## Conclusion

The test suite is **fully implemented and documented** but **blocked by a critical bug** in `MemoryMappedSynchronizer`. The bug affects all async memory-mapped file operations and must be fixed before the test suite can execute.

**Recommended Priority**: HIGH - affects production usage of memory-mapped files in multi-threaded/async scenarios.

---

*Document created: 2026-01-09*  
*Test Suite Version: 1.0*  
*Status: Awaiting synchronizer fix*
