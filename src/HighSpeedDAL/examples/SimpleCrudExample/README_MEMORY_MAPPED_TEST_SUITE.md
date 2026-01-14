# Memory-Mapped File Test Suite

## Overview

The Memory-Mapped File Test Suite is a comprehensive testing and benchmarking framework for demonstrating memory-mapped file capabilities in HighSpeedDAL. It provides:

1. **CRUD Examples** - Complete create, read, update, delete operations
2. **Direct Memory-Mapped File Operations** - Low-level file access without InMemoryTable abstraction
3. **Performance Benchmarks** - Head-to-head comparison of different approaches

## Running the Test Suite

### Option 1: From Command Line
```bash
cd examples\SimpleCrudExample
dotnet run -- --mmf-tests
```

### Option 2: Interactive Menu
```bash
cd examples\SimpleCrudExample
dotnet run
# Select option 3: "Run Memory-Mapped File Test Suite"
```

### Option 3: From Visual Studio
- Set `SimpleCrudExample` as the startup project
- Run (F5 or Ctrl+F5)
- Select option 3 from the menu

## Test Suite Structure

### Part 1: InMemoryTable CRUD Examples

Demonstrates complete lifecycle of data in memory-mapped files:

**Operations Covered:**
- **CREATE**: Insert 3 test users
- **READ (Single)**: Fetch user by ID
- **READ (All)**: Fetch all users
- **UPDATE**: Modify user properties (age, email)
- **DELETE**: Remove a user
- **VERIFY**: Confirm final state
- **FLUSH**: Persist to memory-mapped file
- **RELOAD**: Simulate process restart and verify data persistence

**Key Features:**
- Batched sync mode (flushes every 5 seconds)
- Auto-load on startup
- 10MB file size

### Part 2: Direct Memory-Mapped File Operations

Demonstrates low-level file access without InMemoryTable abstraction:

**Operations:**
- **WRITE**: Direct serialization to file
- **READ**: Direct deserialization from file
- **MODIFY**: Update data and save
- **VERIFY**: Confirm changes persisted

**Use Case:** Maximum performance for bulk operations

### Part 3: Performance Benchmarks

Head-to-head comparisons with detailed speedup calculations:

**Benchmark 1: INSERT Performance**
- Batched mode vs Immediate mode vs Direct MMF
- Shows 40x speedup for batched inserts

**Benchmark 2: READ Performance**
- InMemory random access vs Direct MMF bulk load
- Shows when each approach excels

**Benchmark 3: UPDATE Performance**
- InMemory batched updates vs Direct MMF load-modify-save
- Shows 1,515x speedup for batched updates

**Benchmark 4: FLUSH/SAVE Performance**
- Per-size comparison (1K, 5K, 10K, 25K rows)
- Linear scaling analysis

### Part 4: Concurrent Access Tests

Multi-threaded and multi-process testing:

**Test 1: Multi-Threaded Access**
- 5 threads with mixed read/write operations
- Validates thread-safety and Semaphore locking

**Test 2: Multi-Process Simulation**
- Master process creates file
- 3 worker processes extend data
- Verification of cross-process coordination

**Test 3: Reader-Writer Concurrency**
- 10 concurrent readers + 2 writers
- Heavy read workload validation

### Part 5: Long-Running Stress Test (15 seconds each)

**NEW!** Real-time monitoring of sustained concurrent workloads:

**Scenario 1: READ-ONLY** (10 readers, 0 writers)
- Maximum read throughput validation
- Expected: 2,400+ ops/sec system throughput

**Scenario 2: WRITE-ONLY** (0 readers, 5 writers)
- Write throughput and exclusive locking
- Expected: 350+ ops/sec system throughput

**Scenario 3: MIXED (80% Read / 20% Write)** (8 readers, 2 writers)
- Realistic caching workload
- Expected: 2,000+ ops/sec system throughput

**Scenario 4: HEAVY WRITE (20% Read / 80% Write)** (2 readers, 8 writers)
- Write-heavy workload stress test
- Expected: 1,880+ ops/sec system throughput

**Scenario 5: BALANCED (50% Read / 50% Write)** (5 readers, 5 writers)
- Balanced workload validation
- Expected: 1,565+ ops/sec system throughput

**Live Monitoring Features:**
- Updates every 1 second
- Per-thread: Current operation, ops/sec, total ops, errors, avg wait time
- Final summary: Throughput, success rate, operation breakdown

**See:** [Long-Running Stress Test Documentation](../../docs/LONG_RUNNING_STRESS_TEST.md)


**Expected Output:**
```
PART 1: InMemoryTable CRUD Examples
====================================
1. CREATE - Inserting test users...
   Inserted 3 users (IDs: 1, 2, 3)

2. READ (Single) - Fetching user by ID...
   Fetched: jane_smith (jane@example.com)

3. READ (All) - Fetching all users...
   Total users: 3
   - john_doe (Age: 30)
   - jane_smith (Age: 25)
   - bob_jones (Age: 35)

4. UPDATE - Modifying user...
   Updated user 2: Age=26, Email=jane.smith@example.com

5. DELETE - Removing user...
   Deleted user 3 (bob_jones)

6. VERIFY - Final state...
   Remaining users: 2
   - john_doe (Age: 30, Email: john@example.com)
   - jane_smith (Age: 26, Email: jane.smith@example.com)

7. FLUSH - Persisting to memory-mapped file...
   Data flushed to disk

8. RELOAD - Simulating process restart...
   Reloaded 2 users from memory-mapped file
   - john_doe (Age: 30)
   - jane_smith (Age: 26)
```

### Part 2: Direct Memory-Mapped File Operations

Demonstrates low-level memory-mapped file access without InMemoryTable abstraction:

**Operations Covered:**
- **WRITE**: Direct bulk write to file (3 users)
- **READ**: Direct read from file
- **MODIFY**: Update data and re-save
- **VERIFY**: Re-read to confirm changes

**Key Features:**
- Uses `MemoryMappedFileStore<T>` directly
- No in-memory caching layer
- Cross-process locking with Mutex/Semaphore
- Schema validation with SHA256 hash

**Expected Output:**
```
PART 2: Direct Memory-Mapped File Operations
============================================
1. WRITE - Directly writing to memory-mapped file...
   Wrote 3 users directly to memory-mapped file

2. READ - Directly reading from memory-mapped file...
   Loaded 3 users from memory-mapped file
   - direct_user1 (Age: 40)
   - direct_user2 (Age: 45)
   - direct_user3 (Age: 50)

3. MODIFY - Updating data and re-saving...
   Modified user 102 and added user 104

4. VERIFY - Re-reading to confirm changes...
   Verified 4 users
   - direct_user1 (Age: 40)
   - direct_user2 (Age: 46)
   - direct_user3 (Age: 50)
   - direct_user4 (Age: 55)
```

### Part 3: Performance Benchmarks

Compares InMemoryTable vs direct memory-mapped file operations across multiple scenarios:

#### Benchmark 1: INSERT Performance
Tests insert throughput at 10,000 rows:

- **InMemoryTable (Batched)**: Inserts with deferred flush
- **InMemoryTable (Immediate)**: Inserts with flush after each operation
- **Direct MMF (Bulk Write)**: Single bulk write operation

**Expected Results:**
```
Benchmark 1: INSERT Performance
--------------------------------
1a. InMemoryTable (Batched) - Inserting 10000 rows...
   Time: ~50ms
   Throughput: ~200,000 ops/sec
   Per-operation: 0.0050ms

1b. InMemoryTable (Immediate) - Inserting 10000 rows...
   Time: ~2000ms
   Throughput: ~5,000 ops/sec
   Per-operation: 0.2000ms

1c. Direct MMF (Bulk Write) - Writing 10000 rows...
   Time: ~20ms
   Throughput: ~500,000 ops/sec
   Per-operation: 0.0020ms
```

**Key Insights:**
- Batched mode: 40x faster than immediate mode
- Direct bulk write: Fastest for write-once scenarios
- Immediate mode: Highest consistency, lowest throughput

#### Benchmark 2: READ Performance
Tests read throughput with 1,000 random reads from 10,000 rows:

- **InMemoryTable (Batched)**: In-memory dictionary lookup (O(1))
- **Direct MMF (Load All)**: Load entire file into memory

**Expected Results:**
```
Benchmark 2: READ Performance
-----------------------------
2a. InMemoryTable (Batched) - 1000 random reads from 10000 rows...
   Time: ~2ms
   Throughput: ~500,000 ops/sec
   Per-operation: 0.0020ms

2b. Direct MMF (Load All) - Loading 10000 rows...
   Time: ~15ms
   Throughput: ~666,666 ops/sec
   Loaded: 10000 rows
```

**Key Insights:**
- InMemoryTable: Optimized for individual lookups
- Direct MMF: Optimized for bulk loads
- InMemoryTable read: O(1) dictionary lookup

#### Benchmark 3: UPDATE Performance
Tests update throughput with 1,000 random updates:

- **InMemoryTable (Batched)**: In-memory update with deferred flush
- **Direct MMF (Load-Modify-Save)**: Full cycle per update

**Expected Results:**
```
Benchmark 3: UPDATE Performance
-------------------------------
3a. InMemoryTable (Batched) - 1000 updates from 10000 rows...
   Time: ~10ms
   Throughput: ~100,000 ops/sec
   Per-operation: 0.0100ms

3b. Direct MMF (Load-Modify-Save) - 1000 updates...
   Time: ~15000ms
   Throughput: ~66 ops/sec
   Per-operation: 15.0000ms
   Note: Each operation includes Load+Modify+Save cycle
```

**Key Insights:**
- InMemoryTable: 1,500x faster for incremental updates
- Direct MMF: Each update requires full file reload/save
- InMemoryTable ideal for high-frequency updates

#### Benchmark 4: FLUSH/SAVE Performance
Tests flush performance at different data sizes:

**Test Sizes**: 1,000 | 5,000 | 10,000 | 25,000 rows

**Expected Results:**
```
Benchmark 4: FLUSH/SAVE Performance
-----------------------------------
Testing with 1000 rows:
   InMemoryTable.Flush: 8ms (125,000 rows/sec)
   Direct MMF.Save:     5ms (200,000 rows/sec)

Testing with 5000 rows:
   InMemoryTable.Flush: 25ms (200,000 rows/sec)
   Direct MMF.Save:     18ms (277,777 rows/sec)

Testing with 10000 rows:
   InMemoryTable.Flush: 45ms (222,222 rows/sec)
   Direct MMF.Save:     35ms (285,714 rows/sec)

Testing with 25000 rows:
   InMemoryTable.Flush: 110ms (227,272 rows/sec)
   Direct MMF.Save:     85ms (294,117 rows/sec)
```

**Key Insights:**
- Direct MMF: Slightly faster (no in-memory conversion)
- Both scale linearly with data size
- Flush overhead minimal (consistent throughput)

## Performance Summary

### When to Use InMemoryTable + Memory-Mapped Files

? **Best For:**
- High-frequency CRUD operations (100k+ ops/sec)
- Random access patterns (by ID)
- Incremental updates
- Cross-process data sharing with local caching
- Microservices architecture (100 services sharing 5-10 files)

? **Not Ideal For:**
- Write-once, read-never scenarios
- Full table scans only
- Simple key-value storage without indexes

### When to Use Direct Memory-Mapped Files

? **Best For:**
- Bulk data dumps (highest write throughput)
- Full table reload scenarios
- Simplest possible implementation
- Minimal memory overhead

? **Not Ideal For:**
- Random individual reads/updates
- High-frequency operations
- Complex query patterns

## Architecture Details

### InMemoryTable with Memory-Mapped Files

```
????????????????????????????????????????
?   Application Code                   ?
????????????????????????????????????????
               ? InsertAsync()
               ? GetById()
               ? UpdateAsync()
               ? DeleteAsync()
               ?
????????????????????????????????????????
?   InMemoryTable<T>                   ?
?   • ConcurrentDictionary<int, T>     ?
?   • Indexes (Unique + Non-Unique)    ?
?   • Constraint Validation            ?
?   • WHERE Clause Parser              ?
????????????????????????????????????????
               ? FlushToMemoryMappedFileAsync()
               ?
????????????????????????????????????????
?   MemoryMappedFileStore<T>           ?
?   • MessagePack Serialization        ?
?   • Schema Validation (SHA256)       ?
?   • Cross-Process Locking            ?
????????????????????????????????????????
               ?
               ?
????????????????????????????????????????
?   MemoryMappedFile                   ?
?   • %TEMP%\HighSpeedDAL\*.mmf        ?
?   • 16KB Header + Data Section       ?
?   • Named Mutex (Write Lock)         ?
?   • Named Semaphore (100 Readers)    ?
????????????????????????????????????????
```

### Direct Memory-Mapped File Access

```
????????????????????????????????????????
?   Application Code                   ?
????????????????????????????????????????
               ? SaveAsync(List<T>)
               ? LoadAsync()
               ?
????????????????????????????????????????
?   MemoryMappedFileStore<T>           ?
?   • MessagePack Serialization        ?
?   • Schema Validation (SHA256)       ?
?   • Cross-Process Locking            ?
????????????????????????????????????????
               ?
               ?
????????????????????????????????????????
?   MemoryMappedFile                   ?
?   • %TEMP%\HighSpeedDAL\*.mmf        ?
?   • 16KB Header + Data Section       ?
?   • Named Mutex (Write Lock)         ?
?   • Named Semaphore (100 Readers)    ?
????????????????????????????????????????
```

## File Locations

All memory-mapped files are stored in:
```
Windows: C:\Users\{username}\AppData\Local\Temp\HighSpeedDAL\
Linux:   /tmp/HighSpeedDAL/
macOS:   /tmp/HighSpeedDAL/
```

**Test Files Created:**
- `TestUsers_InMemory.mmf` - Part 1 CRUD examples
- `TestUsers_Direct.mmf` - Part 2 direct operations
- `Benchmark_InMemory_Batched.mmf` - Benchmark 1a
- `Benchmark_InMemory_Immediate.mmf` - Benchmark 1b
- `Benchmark_Direct.mmf` - Benchmark 1c
- `ReadBench_InMemory.mmf` - Benchmark 2a
- `ReadBench_Direct.mmf` - Benchmark 2b
- `UpdateBench_InMemory.mmf` - Benchmark 3a
- `UpdateBench_Direct.mmf` - Benchmark 3b
- `FlushBench_InMemory_*.mmf` - Benchmark 4 (various sizes)
- `FlushBench_Direct_*.mmf` - Benchmark 4 (various sizes)

## Cross-Process Testing

To test cross-process data sharing:

1. **Run Test Suite** (creates and populates files):
   ```bash
   dotnet run -- --mmf-tests
   ```

2. **Run Again** (loads from existing files):
   ```bash
   dotnet run -- --mmf-tests
   ```

3. **Observe**:
   - Part 1, Step 8 (RELOAD) loads data from previous run
   - All benchmarks can read data written by previous processes
   - Schema validation ensures data integrity

## Cleanup

To clear test files:

**Windows:**
```bash
del %TEMP%\HighSpeedDAL\*.mmf
```

**Linux/macOS:**
```bash
rm /tmp/HighSpeedDAL/*.mmf
```

## Troubleshooting

### Issue: "Schema mismatch" error
**Cause**: TestUser entity definition changed between runs
**Solution**: Delete existing .mmf files and re-run tests

### Issue: "Timeout acquiring write lock"
**Cause**: Another process holds the write lock
**Solution**: Wait for other process to complete, or kill holding process

### Issue: "File size exceeded"
**Cause**: Data size exceeds configured file size (default 10-50MB)
**Solution**: Increase `MemoryMappedFileSizeMB` in test configuration

### Issue: Poor performance in benchmarks
**Cause**: Debug build, antivirus scanning, disk I/O contention
**Solution**:
- Use Release build: `dotnet run -c Release -- --mmf-tests`
- Exclude temp directory from antivirus
- Close disk-intensive applications

## Extending the Test Suite

To add custom tests:

1. **Add New Test Method**:
```csharp
private async Task MyCustomTestAsync()
{
    _logger.LogInformation("My Custom Test");
    // Your test logic here
}
```

2. **Register in RunAllTestsAsync()**:
```csharp
public async Task RunAllTestsAsync()
{
    await RunInMemoryTableCrudExamplesAsync();
    await RunDirectMemoryMappedFileExamplesAsync();
    await RunPerformanceBenchmarksAsync();
    await MyCustomTestAsync(); // Add here
}
```

## Related Documentation

- [Memory-Mapped File Implementation](../../../docs/MEMORY_MAPPED_FILE_IMPLEMENTATION.md)
- [Memory-Mapped File Quickstart](../../../docs/MEMORY_MAPPED_FILE_QUICKSTART.md)
- [Memory-Mapped DAL Integration](../../../docs/MEMORY_MAPPED_DAL_IMPLEMENTATION_COMPLETE.md)

## License

This test suite is part of the HighSpeedDAL project and subject to the same license.
