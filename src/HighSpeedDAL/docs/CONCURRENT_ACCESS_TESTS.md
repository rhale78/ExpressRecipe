# Memory-Mapped File Concurrent Access Tests

## Overview

Part 4 of the Memory-Mapped File Test Suite demonstrates real-world concurrent access scenarios with multiple threads and processes accessing the same memory-mapped file simultaneously. These tests validate the Semaphore-based cross-process locking mechanism.

## Test Suite Components

### Test 1: Multi-Threaded Access (Same Process)

**Purpose**: Validate thread-safety within a single process with concurrent readers and writers.

**Configuration**:
- 5 concurrent threads
- 20 operations per thread
- Mix of read (67%) and write (33%) operations
- 1ms delay between operations to simulate realistic workload

**What It Tests**:
- Thread-safe concurrent access to shared `InMemoryTable`
- No deadlocks or race conditions
- Correct read/write operation counts
- Named Semaphores work correctly in multi-threaded scenarios

**Expected Behavior**:
```
Thread 0: 13 reads, 7 writes in 150ms
Thread 1: 13 reads, 7 writes in 152ms
Thread 2: 13 reads, 7 writes in 148ms
Thread 3: 14 reads, 6 writes in 151ms
Thread 4: 14 reads, 6 writes in 149ms

? Multi-threaded test complete!
  Total reads: 67, Total writes: 33
  Average thread time: 150ms
  No deadlocks or exceptions - Semaphore-based locking works correctly!
```

### Test 2: Multi-Process Simulation

**Purpose**: Simulate multiple processes accessing the same memory-mapped file with proper coordination.

**Configuration**:
- 1 Master process (creates and initializes file)
- 3 Worker processes (connect to existing file)
- 300ms delay between master and workers for resource cleanup
- 200ms delay between worker operations
- Final verification by master reconnecting

**What It Tests**:
- **Master process**: Creates MMF with `AutoCreateFile=true`, writes initial data
- **Worker processes**: Open existing MMF with `AutoCreateFile=false` and `AutoLoadOnStartup=true`
- Workers load master's data and add their own
- Workers flush changes back to MMF
- Master reconnects and verifies all data (master + worker records)
- Named Semaphores enable cross-process synchronization

**File Lifecycle**:
```
MASTER: Create and Initialize
  ?? Write 3 initial users
  ?? Flush to MMF (file persisted)

[300ms delay - master releases resources]

WORKER 0: Connect and Extend
  ?? Load 3 users from master
  ?? Add user ID=100
  ?? Flush to MMF

WORKER 1: Connect and Extend
  ?? Load 3 users from master
  ?? Add user ID=101
  ?? Flush to MMF

WORKER 2: Connect and Extend
  ?? Load 3 users from master
  ?? Add user ID=102
  ?? Flush to MMF

[300ms delay - workers release resources]

VERIFICATION: Master Reconnects
  ?? Load all data
  ?? Verify: 3 master users + 3 worker users = 6 total
```

**Expected Output**:
```
MASTER: Creating memory-mapped file...
MASTER: Wrote 3 users to MMF
WORKER 0: Loaded 3 users from master
WORKER 0: Added user ID=100
WORKER 0: Flushed changes to MMF
WORKER 1: Loaded 3 users from master
WORKER 1: Added user ID=101
WORKER 1: Flushed changes to MMF
WORKER 2: Loaded 3 users from master
WORKER 2: Added user ID=102
WORKER 2: Flushed changes to MMF

VERIFICATION: Master reconnecting...
VERIFICATION: Found 6 total users
  - Master users: 3
  - Worker users: 3

? Multi-process coordination successful!
  Named Semaphores enabled safe cross-process locking!
```

### Test 3: Reader-Writer Concurrency

**Purpose**: Heavy reader-writer workload with asymmetric read/write patterns (10:1 ratio).

**Configuration**:
- 10 concurrent reader threads (50 operations each)
- 2 concurrent writer threads (20 operations each)
- Pre-populated with 100 users
- 5ms delay for readers (fast)
- 10ms delay for writers (slower, more expensive)
- Final flush after all operations

**What It Tests**:
- Heavy read-dominated workload (realistic for caching scenarios)
- Readers don't block each other (Semaphore allows 100 concurrent readers)
- Writers get exclusive access when needed
- No starvation or deadlocks
- Performance under asymmetric load

**Semaphore Configuration**:
- Write Semaphore: Binary (0 or 1) - exclusive write access
- Read Semaphore: 100 slots - allows 100 concurrent readers

**Expected Output**:
```
Reader 0: 50 reads in 280ms
Reader 1: 50 reads in 282ms
Reader 2: 50 reads in 279ms
Reader 3: 50 reads in 283ms
Reader 4: 50 reads in 281ms
Reader 5: 50 reads in 284ms
Reader 6: 50 reads in 278ms
Reader 7: 50 reads in 285ms
Reader 8: 50 reads in 280ms
Reader 9: 50 reads in 282ms
Writer 0: 20 writes in 220ms
Writer 1: 20 writes in 225ms

? Reader-Writer test complete!
  Average reader time: 281ms
  Average writer time: 223ms
  Semaphore allows 100 concurrent readers while maintaining write exclusivity!
```

## Architecture: Named Semaphores

### Why Named Semaphores?

Named Semaphores are **system-wide kernel objects** that work across processes:

```csharp
string writeSemaphoreName = $@"Global\HighSpeedDAL_MMF_{fileName}_WriteSemaphore";
_writeSemaphore = new Semaphore(1, 1, writeSemaphoreName);  // Binary semaphore

string readSemaphoreName = $@"Global\HighSpeedDAL_MMF_{fileName}_ReadSemaphore";
_readSemaphore = new Semaphore(100, 100, readSemaphoreName);  // 100 concurrent readers
```

### Async/Await Compatibility

**Critical Fix**: Replaced `Mutex` with `Semaphore` because:
- `Mutex.ReleaseMutex()` must be called from the same thread that acquired it (thread-affine)
- `Semaphore.Release()` can be called from any thread (async-safe)
- Async/await can resume on different threads after `await`

### Locking Patterns

**Write Lock** (Exclusive):
```csharp
await using var writeLock = await _synchronizer.AcquireWriteLockAsync(cancellationToken);
// Write data to MMF
// Lock automatically released on Dispose
```

**Read Lock** (Shared):
```csharp
await using var readLock = await _synchronizer.AcquireReadLockAsync(cancellationToken);
// Read data from MMF
// Lock automatically released on Dispose
```

## Key Insights

### 1. Resource Cleanup Timing

**Problem**: Named Semaphores don't release immediately when Disposed.

**Solution**: Add delays between process transitions:
- 300ms after master releases before workers connect
- 200ms between worker operations
- 300ms after workers before verification

### 2. AutoCreateFile Semantics

| Mode | Behavior | Use Case |
|------|----------|----------|
| `AutoCreateFile=true` | Always delete and recreate | Development, testing, master process |
| `AutoCreateFile=false` | Open existing file without validation | Production, worker processes, reload scenarios |

### 3. Schema Validation Skip

When `AutoCreateFile=false`, schema validation is **skipped** to avoid:
- Access denied errors from stale handles
- Unnecessary overhead when schema hasn't changed
- Reload scenario failures

### 4. Performance Characteristics

**Multi-Threaded** (5 threads, 100 total ops):
- Average thread time: ~150ms
- Total throughput: ~667 ops/second
- No deadlocks or contention

**Reader-Writer** (10 readers + 2 writers):
- Readers: ~10 ops/second per thread (281ms/50 ops)
- Writers: ~9 ops/second per thread (223ms/20 ops)
- Readers don't block each other (100 concurrent slots)

## Running the Tests

```powershell
# Run all tests (includes concurrent access tests)
dotnet run --project examples\SimpleCrudExample\SimpleCrudExample.csproj -- --mmf-tests

# Or from the menu
dotnet run --project examples\SimpleCrudExample\SimpleCrudExample.csproj
# Choose option 3: MMF Test Suite
```

## Expected Output Flow

```
========================================
Memory-Mapped File Test Suite - Starting
Run ID: 20260109_153045
========================================

[Part 1: CRUD Examples with InMemoryTable]
...

[Part 2: Direct Memory-Mapped File Operations]
...

[Part 3: Performance Benchmarks]
...

========================================
PART 4: CONCURRENT ACCESS TESTS
========================================

TEST 1: Multi-Threaded Access (Same Process)
---------------------------------------------
Creating shared memory-mapped file: ConcurrentTest_MultiThread_20260109_153045
Threads: 5, Operations per thread: 20

? Initialized with 10 users

  Thread 0: 13 reads, 7 writes in 150ms
  Thread 1: 13 reads, 7 writes in 152ms
  Thread 2: 13 reads, 7 writes in 148ms
  Thread 3: 14 reads, 6 writes in 151ms
  Thread 4: 14 reads, 6 writes in 149ms

? Multi-threaded test complete!
  Total reads: 67, Total writes: 33
  Average thread time: 150ms
  No deadlocks or exceptions - Semaphore-based locking works correctly!

TEST 2: Multi-Process Simulation
----------------------------------
File: ConcurrentTest_MultiProcess_20260109_153045
Master process (creates file) + 3 worker processes

MASTER: Creating memory-mapped file...
MASTER: Wrote 3 users to MMF
WORKER 0: Loaded 3 users from master
WORKER 0: Added user ID=100
WORKER 0: Flushed changes to MMF
WORKER 1: Loaded 3 users from master
WORKER 1: Added user ID=101
WORKER 1: Flushed changes to MMF
WORKER 2: Loaded 3 users from master
WORKER 2: Added user ID=102
WORKER 2: Flushed changes to MMF

VERIFICATION: Master reconnecting...
VERIFICATION: Found 6 total users
  - Master users: 3
  - Worker users: 3

? Multi-process coordination successful!
  Named Semaphores enabled safe cross-process locking!

TEST 3: Reader-Writer Concurrency
-----------------------------------
File: ConcurrentTest_ReaderWriter_20260109_153045
Readers: 10, Writers: 2

? Initialized with 100 users

  Reader 0: 50 reads in 280ms
  Reader 1: 50 reads in 282ms
  Reader 2: 50 reads in 279ms
  Reader 3: 50 reads in 283ms
  Reader 4: 50 reads in 281ms
  Reader 5: 50 reads in 284ms
  Reader 6: 50 reads in 278ms
  Reader 7: 50 reads in 285ms
  Reader 8: 50 reads in 280ms
  Reader 9: 50 reads in 282ms
  Writer 0: 20 writes in 220ms
  Writer 1: 20 writes in 225ms

? Reader-Writer test complete!
  Average reader time: 281ms
  Average writer time: 223ms
  Semaphore allows 100 concurrent readers while maintaining write exclusivity!

========================================
Memory-Mapped File Test Suite - Complete
========================================
```

## Troubleshooting

### Test 2 Fails with "Access Denied"

**Symptom**: Worker processes get `UnauthorizedAccessException` when opening MMF.

**Cause**: Master hasn't fully released resources.

**Solution**: Increase delay after master disposal (currently 300ms).

### Test 3 Shows High Variance

**Symptom**: Some readers take 2x longer than others.

**Cause**: Writers getting exclusive lock starves some readers.

**Solution**: This is expected behavior. Writers have priority to prevent starvation. Adjust operation delays to balance.

### All Tests Timeout

**Symptom**: Tests hang without completing.

**Cause**: Deadlock in Semaphore acquisition (rare).

**Solution**: Check log files for "Failed to acquire [read|write] lock" messages. Default timeout is 30 seconds per lock.

## Best Practices

1. **Always use delays** between process transitions (200-300ms)
2. **Master creates file** with `AutoCreateFile=true`
3. **Workers open existing** with `AutoCreateFile=false`
4. **Always flush** before disposing to persist changes
5. **Use unique file names** per test run (timestamp-based)
6. **Handle timeouts** gracefully with CancellationToken
7. **Log lock acquisitions** for debugging (trace level)

## Next Steps

- **True multi-process test**: Run separate .exe instances instead of Task.Run simulation
- **Stress testing**: Increase thread count, operation count, and data size
- **Failure scenarios**: Test behavior when processes crash while holding locks
- **Cross-platform**: Validate on Linux (named semaphores work differently)
- **Performance profiling**: Measure lock contention under heavy load

## Related Documentation

- [Memory-Mapped File Implementation](MEMORY_MAPPED_FILE_IMPLEMENTATION.md)
- [Memory-Mapped Test Suite Summary](MEMORY_MAPPED_TEST_SUITE_SUMMARY.md)
- [Synchronizer Fix (Mutex?Semaphore)](MEMORY_MAPPED_SYNCHRONIZER_FIX_COMPLETE.md)
