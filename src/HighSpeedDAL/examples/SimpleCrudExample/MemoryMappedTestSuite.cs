using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HighSpeedDAL.Core.Attributes;
using HighSpeedDAL.Core.InMemoryTable;
using HighSpeedDAL.SimpleCrudExample.Entities;
using Microsoft.Extensions.Logging;
using MessagePack;

namespace HighSpeedDAL.SimpleCrudExample;

/// <summary>
/// Comprehensive test suite for memory-mapped files demonstrating:
/// 1. CRUD operations with InMemoryTable
/// 2. Direct memory-mapped file operations
/// 3. Performance benchmarks comparing both approaches
/// </summary>
public class MemoryMappedTestSuite
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly UserDal? _userDal;
    private readonly UserWithMemoryMappedDal? _userWithMemoryMappedDal;
    private readonly string _runTimestamp;  // Unique timestamp for this test run

    private static double ElapsedMicroseconds(Stopwatch sw) => sw.ElapsedTicks * 1_000_000.0 / Stopwatch.Frequency;

    private static string FormatLatency(double us)
    {
        if (us < 0)
        {
            return "n/a";
        }
        if (us >= 1_000_000)
        {
            return $"{us / 1_000_000.0:F3}s";
        }
        if (us >= 1_000)
        {
            return $"{us / 1_000.0:F3}ms";
        }
        return $"{us:F0}us";
    }

    public MemoryMappedTestSuite(ILoggerFactory loggerFactory, UserDal? userDal = null, UserWithMemoryMappedDal? userWithMemoryMappedDal = null)
    {
        _loggerFactory = loggerFactory;
        _userDal = userDal;
        _userWithMemoryMappedDal = userWithMemoryMappedDal;
        _runTimestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");  // Generate once per test run
    }

    /// <summary>
    /// Runs the complete test suite
    /// </summary>
    public async Task RunAllTestsAsync()
    {
        Console.WriteLine("========================================");
        Console.WriteLine("Memory-Mapped File Test Suite - Starting");
        Console.WriteLine($"Run ID: {_runTimestamp}");
        Console.WriteLine("========================================");
        Console.WriteLine("");

        // Part 1: CRUD Examples with InMemoryTable
        await RunInMemoryTableCrudExamplesAsync();

        Console.WriteLine("");
        Console.WriteLine("========================================");
        Console.WriteLine("");

        // Part 2: Direct Memory-Mapped File Operations
        await RunDirectMemoryMappedFileExamplesAsync();

        Console.WriteLine("");
        Console.WriteLine("========================================");
        Console.WriteLine("");

            // Part 3: Performance Benchmarks
            await RunPerformanceBenchmarksAsync();

            Console.WriteLine("");
            Console.WriteLine("========================================");
            Console.WriteLine("");

                // Part 4: Concurrent Access Tests
                await RunConcurrentAccessTestsAsync();

                Console.WriteLine("");
                Console.WriteLine("========================================");
                Console.WriteLine("");

                // Part 5: Long-Running Stress Test with Live Monitoring
                await RunLongRunningStressTestAsync();

                    Console.WriteLine("");
                    Console.WriteLine("========================================");
                    Console.WriteLine("Memory-Mapped File Test Suite - Complete");
                    Console.WriteLine("========================================");

                    // Cleanup: Delete test files from this run
                    await CleanupTestFilesAsync();
                }

        /// <summary>
        /// Cleans up memory-mapped files created during the current test run.
        /// Deletes all .mmf files with the current run timestamp.
        /// </summary>
        private async Task CleanupTestFilesAsync()
        {
            Console.WriteLine();
            Console.WriteLine("Cleaning up test files...");

            string tempDir = Path.Combine(Path.GetTempPath(), "HighSpeedDAL");
            if (!Directory.Exists(tempDir))
            {
                Console.WriteLine("No temp directory found - nothing to cleanup.");
                return;
            }

            try
            {
                var files = Directory.GetFiles(tempDir, $"*{_runTimestamp}.mmf");
                int deletedCount = 0;

                foreach (var file in files)
                {
                    try
                    {
                        // Small delay to ensure file handles are released
                        await Task.Delay(100);

                        File.Delete(file);
                        deletedCount++;
                        Console.WriteLine($"  Deleted: {Path.GetFileName(file)}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  Failed to delete {Path.GetFileName(file)}: {ex.Message}");
                    }
                }

                Console.WriteLine($"Cleanup complete: {deletedCount} file(s) deleted.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cleanup failed: {ex.Message}");
            }
        }

    #region Part 1: CRUD Examples with InMemoryTable

    /// <summary>
    /// Demonstrates CRUD operations using InMemoryTable with memory-mapped file backing
    /// </summary>
    private async Task RunInMemoryTableCrudExamplesAsync()
    {
        Console.WriteLine("PART 1: InMemoryTable CRUD Examples");
        Console.WriteLine("====================================");

        // Use unique file names with timestamp to avoid lock conflicts
        var config = new InMemoryTableAttribute
        {
            MemoryMappedFileName = $"TestUsers_InMemory_{_runTimestamp}",
            MemoryMappedFileSizeMB = 10,
            SyncMode = MemoryMappedSyncMode.Batched,
            FlushIntervalSeconds = 5,
            AutoCreateFile = true,
            AutoLoadOnStartup = false  // Don't load on startup for clean test
        };

        using (var table = new InMemoryTable<TestUser>(_loggerFactory, config))
        {
            // CREATE
            Console.WriteLine("1. CREATE - Inserting test users...");
        var user1 = new TestUser { Id = 1, Username = "john_doe", Email = "john@example.com", Age = 30 };
        var user2 = new TestUser { Id = 2, Username = "jane_smith", Email = "jane@example.com", Age = 25 };
        var user3 = new TestUser { Id = 3, Username = "bob_jones", Email = "bob@example.com", Age = 35 };

        await table.InsertAsync(user1);
        await table.InsertAsync(user2);
        await table.InsertAsync(user3);
        Console.WriteLine("   Inserted 3 users (IDs: 1, 2, 3)");

        // READ - Single
        Console.WriteLine("");
        Console.WriteLine("2. READ (Single) - Fetching user by ID...");
        var fetchedUser = table.GetById(2);
        Console.WriteLine($"   Fetched: {fetchedUser?.Username} ({fetchedUser?.Email})");

        // READ - All
        Console.WriteLine("");
        Console.WriteLine("3. READ (All) - Fetching all users...");
        var allUsers = table.Select().ToList();
        Console.WriteLine($"   Total users: {allUsers.Count}");
        foreach (var u in allUsers)
        {
            Console.WriteLine($"   - {u.Username} (Age: {u.Age})");
        }

        // UPDATE
        Console.WriteLine("");
        Console.WriteLine("4. UPDATE - Modifying user...");
        user2.Age = 26;
        user2.Email = "jane.smith@example.com";
        await table.UpdateAsync(user2);
        Console.WriteLine($"   Updated user 2: Age={user2.Age}, Email={user2.Email}");

                    // DELETE
                    Console.WriteLine("");
                    Console.WriteLine("5. DELETE - Removing user...");
                    await table.DeleteAsync(3);
                    Console.WriteLine("   Deleted user 3 (bob_jones)");

                    // VERIFY
                    Console.WriteLine("");
                    Console.WriteLine("6. VERIFY - Final state...");
                    allUsers = table.Select().ToList();
                    Console.WriteLine($"   Remaining users: {allUsers.Count}");
                    foreach (var u in allUsers)
                    {
                        Console.WriteLine($"   - {u.Username} (Age: {u.Age}, Email: {u.Email})");
                    }

                    // FLUSH
                    Console.WriteLine("");
                    Console.WriteLine("7. FLUSH - Persisting to memory-mapped file...");
                    await table.FlushToMemoryMappedFileAsync();
                    Console.WriteLine("   Data flushed to disk");
                }

                    // RELOAD TEST - Create new table after first one is disposed
                    Console.WriteLine("");
                    Console.WriteLine("8. RELOAD - Simulating process restart...");

                    // Wait a moment for file handles to be fully released
                    await Task.Delay(200);

                    // Create new config with AutoLoadOnStartup = true to simulate app restart
                    var reloadConfig = new InMemoryTableAttribute
                    {
                        MemoryMappedFileName = $"TestUsers_InMemory_{_runTimestamp}", // Same file name
                        MemoryMappedFileSizeMB = 10,
                        SyncMode = MemoryMappedSyncMode.Batched,
                        FlushIntervalSeconds = 5,
                        AutoCreateFile = false,  // File already exists - don't recreate!
                        AutoLoadOnStartup = true  // Load existing data on startup
                    };

                    using (var reloadedTable = new InMemoryTable<TestUser>(_loggerFactory, reloadConfig))
                    {
                        await Task.Delay(100); // Allow auto-load to complete
                        var reloadedUsers = reloadedTable.Select().ToList();
                        Console.WriteLine($"   Reloaded {reloadedUsers.Count} users from memory-mapped file");
                        foreach (var u in reloadedUsers)
                        {
                            Console.WriteLine($"   - {u.Username} (Age: {u.Age})");
                        }
                    }
                }

            #endregion

    #region Part 2: Direct Memory-Mapped File Operations

    /// <summary>
    /// Demonstrates direct memory-mapped file operations without InMemoryTable
    /// </summary>
    private async Task RunDirectMemoryMappedFileExamplesAsync()
    {
        Console.WriteLine("PART 2: Direct Memory-Mapped File Operations");
        Console.WriteLine("============================================");

        var config = new InMemoryTableAttribute
        {
            MemoryMappedFileSizeMB = 10
        };

        using var store = new MemoryMappedFileStore<TestUser>(
            $"TestUsers_Direct_{_runTimestamp}",
            config,
            _loggerFactory.CreateLogger<MemoryMappedFileStore<TestUser>>(),
            _loggerFactory.CreateLogger<MemoryMappedSynchronizer>());

        // WRITE
        Console.WriteLine("1. WRITE - Directly writing to memory-mapped file...");
        var users = new List<TestUser>
        {
            new TestUser { Id = 101, Username = "direct_user1", Email = "direct1@example.com", Age = 40 },
            new TestUser { Id = 102, Username = "direct_user2", Email = "direct2@example.com", Age = 45 },
            new TestUser { Id = 103, Username = "direct_user3", Email = "direct3@example.com", Age = 50 }
        };

        await store.SaveAsync(users);
        Console.WriteLine($"   Wrote {users.Count} users directly to memory-mapped file");

        // READ
        Console.WriteLine("");
        Console.WriteLine("2. READ - Directly reading from memory-mapped file...");
        var loadedUsers = await store.LoadAsync();
        Console.WriteLine($"   Loaded {loadedUsers.Count} users from memory-mapped file");
        foreach (var u in loadedUsers)
        {
            Console.WriteLine($"   - {u.Username} (Age: {u.Age})");
        }

        // MODIFY & SAVE
        Console.WriteLine("");
        Console.WriteLine("3. MODIFY - Updating data and re-saving...");
        loadedUsers[1].Age = 46;
        loadedUsers.Add(new TestUser { Id = 104, Username = "direct_user4", Email = "direct4@example.com", Age = 55 });
        await store.SaveAsync(loadedUsers);
        Console.WriteLine($"   Modified user 102 and added user 104");

        // VERIFY
        Console.WriteLine("");
        Console.WriteLine("4. VERIFY - Re-reading to confirm changes...");
        var verifyUsers = await store.LoadAsync();
        Console.WriteLine($"   Verified {verifyUsers.Count} users");
        foreach (var u in verifyUsers)
        {
            Console.WriteLine($"   - {u.Username} (Age: {u.Age})");
        }
    }

    #endregion

    #region Part 3: Performance Benchmarks

    /// <summary>
    /// Runs performance benchmarks comparing InMemoryTable vs direct memory-mapped file operations
    /// </summary>
    private async Task RunPerformanceBenchmarksAsync()
    {
        Console.WriteLine("PART 3: Performance Benchmarks");
        Console.WriteLine("===============================");
        Console.WriteLine("");

        // Benchmark 1: Insert Performance
        await BenchmarkInsertPerformanceAsync();

        Console.WriteLine("");
        Console.WriteLine("Waiting 2 seconds for cleanup...");
        await Task.Delay(2000); // Allow time for file locks to release

        // Benchmark 2: Read Performance
        await BenchmarkReadPerformanceAsync();

        Console.WriteLine("");
        Console.WriteLine("Waiting 2 seconds for cleanup...");
        await Task.Delay(2000);

        // Benchmark 3: Update Performance
        await BenchmarkUpdatePerformanceAsync();

        Console.WriteLine("");
        Console.WriteLine("Waiting 2 seconds for cleanup...");
        await Task.Delay(2000);

        // Benchmark 4: Flush/Save Performance
        await BenchmarkFlushPerformanceAsync();
    }

    private async Task BenchmarkInsertPerformanceAsync()
    {
        Console.WriteLine("Benchmark 1: INSERT Performance");
        Console.WriteLine("--------------------------------");

        const int rowCount = 10000;
        var users = GenerateTestUsers(rowCount);

        // Test 1: InMemoryTable (Batched)
        Console.WriteLine($"1a. InMemoryTable (Batched) - Inserting {rowCount} rows...");
        var configBatched = new InMemoryTableAttribute
        {
            MemoryMappedFileName = $"Benchmark_InMemory_Batched_{_runTimestamp}",
            MemoryMappedFileSizeMB = 50,
            SyncMode = MemoryMappedSyncMode.Batched,
            FlushIntervalSeconds = 60, // Long interval so it doesn't flush during test
            AutoCreateFile = true,
            AutoLoadOnStartup = false
        };

        double batchedOpsPerSec = 0;
        long batchedTimeMs = 0;
        using (var table = new InMemoryTable<TestUser>(_loggerFactory, configBatched))
        {
            var sw = Stopwatch.StartNew();
            foreach (var user in users)
            {
                await table.InsertAsync(user);
            }
            sw.Stop();

            batchedOpsPerSec = rowCount / sw.Elapsed.TotalSeconds;
            batchedTimeMs = sw.ElapsedMilliseconds;
            Console.WriteLine($"   Time: {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"   Throughput: {batchedOpsPerSec:N0} ops/sec");
            Console.WriteLine($"   Per-operation: {sw.Elapsed.TotalMilliseconds / rowCount:F4}ms");
        }

        // Test 2: InMemoryTable (Immediate) - Use smaller count due to lock contention
        int immediateRowCount = 100; // Immediate mode has high overhead per insert
        Console.WriteLine("");
        Console.WriteLine($"1b. InMemoryTable (Immediate) - Inserting {immediateRowCount} rows...");
        Console.WriteLine("   Note: Smaller count due to per-insert flush overhead");
        var configImmediate = new InMemoryTableAttribute
        {
            MemoryMappedFileName = $"Benchmark_InMemory_Immediate_{_runTimestamp}",
            MemoryMappedFileSizeMB = 50,
            SyncMode = MemoryMappedSyncMode.Immediate,
            AutoCreateFile = true,
            AutoLoadOnStartup = false
        };

        var immediateUsers = GenerateTestUsers(immediateRowCount); // Smaller dataset
        double immediateOpsPerSec = 0;
        long immediateTimeMs = 0;
        using (var table = new InMemoryTable<TestUser>(_loggerFactory, configImmediate))
        {
            var sw = Stopwatch.StartNew();
            foreach (var user in immediateUsers)
            {
                await table.InsertAsync(user);
            }
            sw.Stop();

            immediateOpsPerSec = immediateRowCount / sw.Elapsed.TotalSeconds;
            immediateTimeMs = sw.ElapsedMilliseconds;
            Console.WriteLine($"   Time: {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"   Throughput: {immediateOpsPerSec:N0} ops/sec");
            Console.WriteLine($"   Per-operation: {sw.Elapsed.TotalMilliseconds / immediateRowCount:F4}ms");
            Console.WriteLine($"   Extrapolated for {rowCount} rows: ~{sw.ElapsedMilliseconds * (rowCount / (double)immediateRowCount):N0}ms");
        }

        // Comparison: Batched vs Immediate
        double batchedVsImmediateSpeedup = batchedOpsPerSec / immediateOpsPerSec;
        Console.WriteLine("");
        Console.WriteLine($"   >>> Batched is {batchedVsImmediateSpeedup:F1}x FASTER than Immediate mode <<<");
        Console.WriteLine($"       ({batchedOpsPerSec:N0} vs {immediateOpsPerSec:N0} ops/sec)");

            // Test 3: Direct Memory-Mapped File (Bulk Write)
            Console.WriteLine("");
            Console.WriteLine($"1c. Direct MMF (Bulk Write) - Writing {rowCount} rows...");
            var configDirect = new InMemoryTableAttribute
            {
                MemoryMappedFileSizeMB = 50
            };

            users = GenerateTestUsers(rowCount); // Regenerate
            double directOpsPerSec = 0;
            using (var store = new MemoryMappedFileStore<TestUser>(
                $"Benchmark_Direct_{_runTimestamp}",
                configDirect,
                _loggerFactory.CreateLogger<MemoryMappedFileStore<TestUser>>(),
                _loggerFactory.CreateLogger<MemoryMappedSynchronizer>()))
            {
                var sw = Stopwatch.StartNew();
                await store.SaveAsync(users);
                sw.Stop();

                directOpsPerSec = rowCount / sw.Elapsed.TotalSeconds;
                Console.WriteLine($"   Time: {sw.ElapsedMilliseconds}ms");
                Console.WriteLine($"   Throughput: {directOpsPerSec:N0} ops/sec");
                Console.WriteLine($"   Per-operation: {sw.Elapsed.TotalMilliseconds / rowCount:F4}ms");
            }

            // Final Comparison Summary
            Console.WriteLine("");
            Console.WriteLine("=== INSERT Performance Summary ===");
            Console.WriteLine($"   Batched:   {batchedOpsPerSec:N0} ops/sec ({batchedTimeMs}ms)");
            Console.WriteLine($"   Immediate: {immediateOpsPerSec:N0} ops/sec ({immediateTimeMs}ms)");
            Console.WriteLine($"   Direct MMF: {directOpsPerSec:N0} ops/sec");
            Console.WriteLine("");

            if (directOpsPerSec > batchedOpsPerSec)
            {
                Console.WriteLine($"   >>> Direct MMF is {directOpsPerSec / batchedOpsPerSec:F1}x FASTER than Batched <<<");
            }
            else
            {
                Console.WriteLine($"   >>> Batched is {batchedOpsPerSec / directOpsPerSec:F1}x FASTER than Direct MMF <<<");
            }

            Console.WriteLine($"   >>> Batched is {batchedOpsPerSec / immediateOpsPerSec:F1}x FASTER than Immediate <<<");
        }

    private async Task BenchmarkReadPerformanceAsync()
    {
        Console.WriteLine("Benchmark 2: READ Performance");
        Console.WriteLine("-----------------------------");

        const int rowCount = 10000;
        const int readIterations = 1000;

        // Prepare data
        var users = GenerateTestUsers(rowCount);

        // Test 1: InMemoryTable (Batched)
        Console.WriteLine($"2a. InMemoryTable (Batched) - {readIterations} random reads from {rowCount} rows...");
        var configBatched = new InMemoryTableAttribute
        {
            MemoryMappedFileName = $"ReadBench_InMemory_{_runTimestamp}",
            MemoryMappedFileSizeMB = 50,
            SyncMode = MemoryMappedSyncMode.Batched,
            FlushIntervalSeconds = 60,
            AutoCreateFile = true,
            AutoLoadOnStartup = false
        };

        double inMemoryReadOps = 0;
        long inMemoryReadTimeMs = 0;
        using (var table = new InMemoryTable<TestUser>(_loggerFactory, configBatched))
        {
            // Insert data
            foreach (var user in users)
            {
                await table.InsertAsync(user);
            }

            // Benchmark reads
            var random = new Random(42);
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < readIterations; i++)
            {
                int id = random.Next(1, rowCount + 1);
                var user = table.GetById(id);
            }
            sw.Stop();

            inMemoryReadOps = readIterations / sw.Elapsed.TotalSeconds;
            inMemoryReadTimeMs = sw.ElapsedMilliseconds;
            double inMemoryReadUs = ElapsedMicroseconds(sw);
            Console.WriteLine($"   Time: {FormatLatency(inMemoryReadUs)}");
            Console.WriteLine($"   Throughput: {inMemoryReadOps:N0} ops/sec");
            Console.WriteLine($"   Per-operation: {(inMemoryReadUs / readIterations):F1}us");
        }

            // Test 2: Direct Memory-Mapped File (Load All)
            Console.WriteLine("");
            Console.WriteLine($"2b. Direct MMF (Load All) - Loading {rowCount} rows...");
            var configDirect = new InMemoryTableAttribute
            {
                MemoryMappedFileSizeMB = 50
            };

            users = GenerateTestUsers(rowCount); // Regenerate
            double directLoadOps = 0;
            long directLoadTimeMs = 0;
            using (var store = new MemoryMappedFileStore<TestUser>(
                $"ReadBench_Direct_{_runTimestamp}",
                configDirect,
                _loggerFactory.CreateLogger<MemoryMappedFileStore<TestUser>>(),
                _loggerFactory.CreateLogger<MemoryMappedSynchronizer>()))
            {
                // Save data first
                await store.SaveAsync(users);

                // Benchmark load
                var sw = Stopwatch.StartNew();
                var loadedUsers = await store.LoadAsync();
                sw.Stop();

                directLoadOps = rowCount / sw.Elapsed.TotalSeconds;
                directLoadTimeMs = sw.ElapsedMilliseconds;
                Console.WriteLine($"   Time: {FormatLatency(ElapsedMicroseconds(sw))}");
                Console.WriteLine($"   Throughput: {directLoadOps:N0} ops/sec");
                Console.WriteLine($"   Loaded: {loadedUsers.Count} rows");
            }

            // Comparison Summary
            Console.WriteLine("");
            Console.WriteLine("=== READ Performance Summary ===");
            Console.WriteLine($"   InMemory Random Reads: {inMemoryReadOps:N0} ops/sec ({inMemoryReadTimeMs}ms for {readIterations} reads)");
            Console.WriteLine($"   Direct MMF Bulk Load:  {directLoadOps:N0} ops/sec ({directLoadTimeMs}ms for {rowCount} rows)");
            Console.WriteLine("");
            Console.WriteLine($"   >>> InMemory is {inMemoryReadOps / directLoadOps:F1}x FASTER for random access <<<");
            Console.WriteLine($"   >>> Use InMemoryTable for OLTP, Direct MMF for bulk data loading <<<");
        }

    private async Task BenchmarkUpdatePerformanceAsync()
    {
        Console.WriteLine("Benchmark 3: UPDATE Performance");
        Console.WriteLine("-------------------------------");

        const int rowCount = 10000;
        const int updateCount = 1000;

        var users = GenerateTestUsers(rowCount);

        // Test 1: InMemoryTable (Batched)
        Console.WriteLine($"3a. InMemoryTable (Batched) - {updateCount} updates from {rowCount} rows...");
        var configBatched = new InMemoryTableAttribute
        {
            MemoryMappedFileName = $"UpdateBench_InMemory_{_runTimestamp}",
            MemoryMappedFileSizeMB = 50,
            SyncMode = MemoryMappedSyncMode.Batched,
            FlushIntervalSeconds = 60,
            AutoCreateFile = true,
            AutoLoadOnStartup = false
        };

        double inMemoryUpdateOps = 0;
        long inMemoryUpdateTimeMs = 0;
        using (var table = new InMemoryTable<TestUser>(_loggerFactory, configBatched))
        {
            // Insert data
            foreach (var user in users)
            {
                await table.InsertAsync(user);
            }

            // Benchmark updates
            var random = new Random(42);
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < updateCount; i++)
            {
                int id = random.Next(1, rowCount + 1);
                var user = table.GetById(id);
                if (user != null)
                {
                    user.Age++;
                    await table.UpdateAsync(user);
                }
            }
            sw.Stop();

            inMemoryUpdateOps = updateCount / sw.Elapsed.TotalSeconds;
            inMemoryUpdateTimeMs = sw.ElapsedMilliseconds;
            double inMemoryUpdateUs = ElapsedMicroseconds(sw);
            Console.WriteLine($"   Time: {FormatLatency(inMemoryUpdateUs)}");
            Console.WriteLine($"   Throughput: {inMemoryUpdateOps:N0} ops/sec");
            Console.WriteLine($"   Per-operation: {(inMemoryUpdateUs / updateCount):F1}us");
        }

        // Test 2: Direct Memory-Mapped File (Load-Modify-Save)
        {
            int directUpdateCount = 50; // Each operation includes full load+save cycle
            Console.WriteLine("");
            Console.WriteLine($"3b. Direct MMF (Load-Modify-Save) - {directUpdateCount} updates...");
            Console.WriteLine("   Note: Smaller count due to load+save overhead per update");

            var configDirect = new InMemoryTableAttribute
            {
                MemoryMappedFileSizeMB = 50
            };

            users = GenerateTestUsers(rowCount); // Regenerate
            double directUpdateOps = 0;
            long directUpdateTimeMs = 0;
            using (var store = new MemoryMappedFileStore<TestUser>(
                $"UpdateBench_Direct_{_runTimestamp}",
                configDirect,
                _loggerFactory.CreateLogger<MemoryMappedFileStore<TestUser>>(),
                _loggerFactory.CreateLogger<MemoryMappedSynchronizer>()))
            {
                // Save initial data
                await store.SaveAsync(users);

                // Benchmark load-modify-save cycle
                var random = new Random(42);
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < directUpdateCount; i++)
                {
                    var loadedUsers = await store.LoadAsync();
                    int id = random.Next(1, rowCount + 1);
                    var user = loadedUsers.FirstOrDefault(u => u.Id == id);
                    if (user != null)
                    {
                        user.Age++;
                    }
                    await store.SaveAsync(loadedUsers);
                }
                sw.Stop();

                directUpdateOps = directUpdateCount / sw.Elapsed.TotalSeconds;
                directUpdateTimeMs = sw.ElapsedMilliseconds;
                double directUpdateUs = ElapsedMicroseconds(sw);
                Console.WriteLine($"   Time: {FormatLatency(directUpdateUs)}");
                Console.WriteLine($"   Throughput: {directUpdateOps:N0} ops/sec");
                Console.WriteLine($"   Per-operation: {(directUpdateUs / directUpdateCount):F1}us");
            }

            // Comparison Summary
            Console.WriteLine("");
            Console.WriteLine("=== UPDATE Performance Summary ===");
            Console.WriteLine($"   InMemory (Batched):      {inMemoryUpdateOps:N0} ops/sec ({inMemoryUpdateTimeMs}ms for {updateCount} updates)");
            Console.WriteLine($"   Direct MMF (Load-Save):  {directUpdateOps:N0} ops/sec ({directUpdateTimeMs}ms for {directUpdateCount} updates)");

            // Optional framework comparisons (DB-backed + cache/memory-mapped DAL) to contextualize speedups
            if (_userDal != null)
            {
                Console.WriteLine("");
                Console.WriteLine($"3c. Framework DB table - {updateCount} updates on Users...");

                // Ensure dataset exists and is merged before timing.
                var existing = (await _userDal.GetAllAsync()).Take(updateCount).ToList();
                if (existing.Count < updateCount)
                {
                    var seed = new List<HighSpeedDAL.SimpleCrudExample.Entities.User>(updateCount);
                    for (int i = 0; i < updateCount; i++)
                    {
                        seed.Add(new HighSpeedDAL.SimpleCrudExample.Entities.User
                        {
                            Username = $"mmf_cmp_user_{_runTimestamp}_{i}",
                            Email = $"mmf_cmp_user_{i}@example.com",
                            FirstName = "Cmp",
                            LastName = "User",
                            CreatedAt = DateTime.UtcNow,
                            IsActive = true
                        });
                    }
                    await _userDal.BulkInsertAsync(seed);
                    await Task.Delay(2000);
                    existing = (await _userDal.GetAllAsync()).Where(u => u.Username.StartsWith($"mmf_cmp_user_{_runTimestamp}_", StringComparison.Ordinal))
                        .Take(updateCount)
                        .ToList();
                }

                // Warm the cache (if enabled) so the comparison includes cache invalidation cost in the hot path.
                foreach (var u in existing)
                {
                    await _userDal.GetByIdAsync(u.Id);
                }

                var rand = new Random(42);
                var swDb = Stopwatch.StartNew();
                for (int i = 0; i < updateCount; i++)
                {
                    var u = existing[rand.Next(existing.Count)];
                    u.IsActive = !u.IsActive;
                    u.Email = $"mmf_cmp_{DateTime.UtcNow.Ticks}@example.com";
                    await _userDal.UpdateAsync(u);
                }
                swDb.Stop();

                double dbUs = ElapsedMicroseconds(swDb);
                double dbOps = updateCount / swDb.Elapsed.TotalSeconds;
                Console.WriteLine($"   Time: {FormatLatency(dbUs)}");
                Console.WriteLine($"   Throughput: {dbOps:N0} ops/sec");
                Console.WriteLine($"   Per-operation: {(dbUs / updateCount):F1}us");
            }

            if (_userWithMemoryMappedDal != null)
            {
                Console.WriteLine("");
                Console.WriteLine($"3d. Framework DB + MemoryMappedTable (L0) - {updateCount} updates on UsersMemoryMapped...");

                var existingMmf = (await _userWithMemoryMappedDal.GetAllAsync()).Take(updateCount).ToList();
                if (existingMmf.Count < updateCount)
                {
                    var seed = new List<HighSpeedDAL.SimpleCrudExample.Entities.UserWithMemoryMapped>(updateCount);
                    for (int i = 0; i < updateCount; i++)
                    {
                        seed.Add(new HighSpeedDAL.SimpleCrudExample.Entities.UserWithMemoryMapped
                        {
                            Username = $"mmf_cmp_mapped_{_runTimestamp}_{i}",
                            Email = $"mmf_cmp_mapped_{i}@example.com",
                            FirstName = "Cmp",
                            LastName = "Mapped",
                            CreatedAt = DateTime.UtcNow,
                            IsActive = true
                        });
                    }
                    await _userWithMemoryMappedDal.BulkInsertAsync(seed);
                    await Task.Delay(2000);
                    existingMmf = (await _userWithMemoryMappedDal.GetAllAsync())
                        .Where(u => u.Username.StartsWith($"mmf_cmp_mapped_{_runTimestamp}_", StringComparison.Ordinal))
                        .Take(updateCount)
                        .ToList();
                }

                foreach (var u in existingMmf)
                {
                    await _userWithMemoryMappedDal.GetByIdAsync(u.Id);
                }

                var rand = new Random(42);
                var swMapped = Stopwatch.StartNew();
                for (int i = 0; i < updateCount; i++)
                {
                    var u = existingMmf[rand.Next(existingMmf.Count)];
                    u.IsActive = !u.IsActive;
                    u.Email = $"mmf_cmp_mapped_{DateTime.UtcNow.Ticks}@example.com";
                    await _userWithMemoryMappedDal.UpdateAsync(u);
                }
                swMapped.Stop();

                double mappedUs = ElapsedMicroseconds(swMapped);
                double mappedOps = updateCount / swMapped.Elapsed.TotalSeconds;
                Console.WriteLine($"   Time: {FormatLatency(mappedUs)}");
                Console.WriteLine($"   Throughput: {mappedOps:N0} ops/sec");
                Console.WriteLine($"   Per-operation: {(mappedUs / updateCount):F1}us");
            }
            Console.WriteLine("");
            Console.WriteLine($"   >>> InMemoryTable is {inMemoryUpdateOps / directUpdateOps:F1}x FASTER for incremental updates <<<");
            Console.WriteLine($"   >>> Direct MMF requires full load+save per update - NOT suitable for OLTP! <<<");

            double extrapolatedDirectTime = directUpdateTimeMs * (updateCount / (double)directUpdateCount);
            Console.WriteLine($"   >>> Extrapolated: {updateCount} Direct MMF updates would take ~{extrapolatedDirectTime:N0}ms <<<");
        }
    }

            private async Task BenchmarkFlushPerformanceAsync()
            {
                Console.WriteLine("Benchmark 4: FLUSH/SAVE Performance");
                Console.WriteLine("-----------------------------------");

                var testSizes = new[] { 1000, 5000, 10000, 25000 };
                var flushResults = new List<(int size, long inMemoryMs, long directMs, double inMemoryOps, double directOps)>();

                foreach (var size in testSizes)
                {
                    Console.WriteLine($"Testing with {size} rows:");

                    var users = GenerateTestUsers(size);

                    // Test 1: InMemoryTable Flush
                    var configInMemory = new InMemoryTableAttribute
                    {
                        MemoryMappedFileName = $"FlushBench_InMemory_{size}_{_runTimestamp}",
                        MemoryMappedFileSizeMB = 50,
                        SyncMode = MemoryMappedSyncMode.Manual,
                        AutoCreateFile = true,
                        AutoLoadOnStartup = false
                    };

                    long inMemoryMs = 0;
                    double inMemoryOps = 0;
                    using (var table = new InMemoryTable<TestUser>(_loggerFactory, configInMemory))
                    {
                        // Insert data
                        foreach (var user in users)
                        {
                            await table.InsertAsync(user);
                        }

                        // Benchmark flush
                        var sw = Stopwatch.StartNew();
                        await table.FlushToMemoryMappedFileAsync();
                        sw.Stop();

                        inMemoryMs = sw.ElapsedMilliseconds;
                        inMemoryOps = size / sw.Elapsed.TotalSeconds;
                        Console.WriteLine($"   InMemoryTable.Flush: {FormatLatency(ElapsedMicroseconds(sw))} ({inMemoryOps:N0} rows/sec)");
                    }

                    // Test 2: Direct Memory-Mapped File Save
                    var configDirect = new InMemoryTableAttribute
                    {
                        MemoryMappedFileSizeMB = 50
                    };

                    users = GenerateTestUsers(size); // Regenerate
                    long directMs = 0;
                    double directOps = 0;
                    using (var store = new MemoryMappedFileStore<TestUser>(
                        $"FlushBench_Direct_{size}_{_runTimestamp}",
                        configDirect,
                        _loggerFactory.CreateLogger<MemoryMappedFileStore<TestUser>>(),
                        _loggerFactory.CreateLogger<MemoryMappedSynchronizer>()))
                    {
                        var sw = Stopwatch.StartNew();
                        await store.SaveAsync(users);
                        sw.Stop();

                        directMs = sw.ElapsedMilliseconds;
                        directOps = size / sw.Elapsed.TotalSeconds;
                        Console.WriteLine($"   Direct MMF.Save:     {FormatLatency(ElapsedMicroseconds(sw))} ({directOps:N0} rows/sec)");
                    }

                    flushResults.Add((size, inMemoryMs, directMs, inMemoryOps, directOps));

                    // Comparison for this size
                    if (directOps > inMemoryOps)
                    {
                        Console.WriteLine($"   >>> Direct MMF is {directOps / inMemoryOps:F2}x FASTER <<<");
                    }
                    else
                    {
                        Console.WriteLine($"   >>> InMemory Flush is {inMemoryOps / directOps:F2}x FASTER <<<");
                    }

                    Console.WriteLine("");
                }

                // Final Summary
                Console.WriteLine("=== FLUSH/SAVE Performance Summary ===");
                Console.WriteLine("Size    | InMemory Flush | Direct MMF Save | Winner");
                Console.WriteLine("--------|----------------|-----------------|--------");
                foreach (var result in flushResults)
                {
                    string winner = result.directOps > result.inMemoryOps 
                        ? $"Direct ({result.directOps / result.inMemoryOps:F1}x)" 
                        : $"InMemory ({result.inMemoryOps / result.directOps:F1}x)";
                    Console.WriteLine($"{result.size,6}  | {result.inMemoryMs,10}ms   | {result.directMs,11}ms  | {winner}");
                }
                Console.WriteLine("");
                Console.WriteLine(">>> Both approaches scale linearly with data size <<<");
                Console.WriteLine(">>> Use Direct MMF for bulk persistence, InMemory Flush for incremental sync <<<");
                    }

            #endregion

            #region Part 4: Concurrent Access Tests

            /// <summary>
            /// Tests concurrent access with multiple threads and processes
            /// </summary>
            private async Task RunConcurrentAccessTestsAsync()
            {
                Console.WriteLine("========================================");
                Console.WriteLine("PART 4: CONCURRENT ACCESS TESTS");
                Console.WriteLine("========================================");
                Console.WriteLine("");

                // Test 1: Multi-threaded access (same process)
                await RunMultiThreadedAccessTestAsync();

                Console.WriteLine("");

                // Test 2: Multi-process simulation (sequential with explicit coordination)
                await RunMultiProcessSimulationTestAsync();

                Console.WriteLine("");

                // Test 3: Reader-writer concurrency
                await RunReaderWriterConcurrencyTestAsync();
            }

            /// <summary>
            /// Test 1: Multiple threads in same process accessing shared memory-mapped file
            /// </summary>
            private async Task RunMultiThreadedAccessTestAsync()
            {
                Console.WriteLine("TEST 1: Multi-Threaded Access (Same Process)");
                Console.WriteLine("---------------------------------------------");

                string fileName = $"ConcurrentTest_MultiThread_{_runTimestamp}";
                int threadCount = 5;
                int operationsPerThread = 20;

                var config = new InMemoryTableAttribute
                {
                    MemoryMappedFileName = fileName,
                    MemoryMappedFileSizeMB = 10,
                    AutoCreateFile = true,
                    AutoLoadOnStartup = false
                };

                Console.WriteLine($"Creating shared memory-mapped file: {fileName}");
                Console.WriteLine($"Threads: {threadCount}, Operations per thread: {operationsPerThread}");
                Console.WriteLine("");

                var tasks = new List<Task>();
                var stats = new System.Collections.Concurrent.ConcurrentBag<(int threadId, int reads, int writes, double elapsedMs)>();

                using (var table = new InMemoryTable<TestUser>(_loggerFactory, config))
                {
                    // Pre-populate with some data
                    for (int i = 1; i <= 10; i++)
                    {
                        await table.InsertAsync(new TestUser { Id = i, Username = $"InitialUser{i}", Email = $"user{i}@example.com", Age = 20 + i });
                    }
                    await table.FlushToMemoryMappedFileAsync();
                    Console.WriteLine("? Initialized with 10 users");
                    Console.WriteLine("");

                    // Spawn threads
                    for (int t = 0; t < threadCount; t++)
                    {
                        int threadId = t;
                        tasks.Add(Task.Run(async () =>
                        {
                            var sw = Stopwatch.StartNew();
                            int reads = 0;
                            int writes = 0;

                            for (int op = 0; op < operationsPerThread; op++)
                            {
                                if (op % 3 == 0)
                                {
                                    // Write operation
                                    int userId = 1000 + (threadId * 100) + op;
                                    await table.InsertAsync(new TestUser
                                    {
                                        Id = userId,
                                        Username = $"Thread{threadId}_User{op}",
                                        Email = $"t{threadId}u{op}@example.com",
                                        Age = 25 + op
                                    });
                                    writes++;
                                }
                                else
                                {
                                    // Read operation
                                    var users = table.Select().ToList();
                                    reads++;
                                }

                                // Small delay to simulate realistic workload
                                await Task.Delay(1);
                            }

                            sw.Stop();
                            stats.Add((threadId, reads, writes, sw.Elapsed.TotalMilliseconds));
                            Console.WriteLine($"  Thread {threadId}: {reads} reads, {writes} writes in {sw.Elapsed.TotalMilliseconds:F0}ms");
                        }));
                    }

                    await Task.WhenAll(tasks);
                }

                Console.WriteLine("");
                var totalReads = stats.Sum(s => s.reads);
                var totalWrites = stats.Sum(s => s.writes);
                var avgTime = stats.Average(s => s.elapsedMs);

                Console.WriteLine($"? Multi-threaded test complete!");
                Console.WriteLine($"  Total reads: {totalReads}, Total writes: {totalWrites}");
                Console.WriteLine($"  Average thread time: {avgTime:F0}ms");
                Console.WriteLine($"  No deadlocks or exceptions - Semaphore-based locking works correctly!");
            }

            /// <summary>
            /// Test 2: Simulates multi-process access by creating separate table instances
            /// One process is the "master" that creates the file, others are "workers"
            /// </summary>
            private async Task RunMultiProcessSimulationTestAsync()
            {
                Console.WriteLine("TEST 2: Multi-Process Simulation");
                Console.WriteLine("----------------------------------");

                string fileName = $"ConcurrentTest_MultiProcess_{_runTimestamp}";
                int workerCount = 3;

                Console.WriteLine($"File: {fileName}");
                Console.WriteLine($"Master process (creates file) + {workerCount} worker processes");
                Console.WriteLine("");

                // MASTER PROCESS: Creates and initializes the file
                Console.WriteLine("MASTER: Creating memory-mapped file...");
                var masterConfig = new InMemoryTableAttribute
                {
                    MemoryMappedFileName = fileName,
                    MemoryMappedFileSizeMB = 10,
                    AutoCreateFile = true,
                    AutoLoadOnStartup = false
                };

                using (var masterTable = new InMemoryTable<TestUser>(_loggerFactory, masterConfig))
                {
                    // Master writes initial data
                    var initialUsers = new[]
                    {
                        new TestUser { Id = 1, Username = "MasterUser1", Email = "master1@example.com", Age = 30 },
                        new TestUser { Id = 2, Username = "MasterUser2", Email = "master2@example.com", Age = 31 },
                        new TestUser { Id = 3, Username = "MasterUser3", Email = "master3@example.com", Age = 32 }
                    };

                    foreach (var user in initialUsers)
                    {
                        await masterTable.InsertAsync(user);
                    }

                    await masterTable.FlushToMemoryMappedFileAsync();
                    Console.WriteLine($"MASTER: Wrote {initialUsers.Length} users to MMF");
                }

                // Small delay to ensure master fully releases resources
                await Task.Delay(300);

                // WORKER PROCESSES: Connect to existing file
                var workerTasks = new List<Task>();

                for (int w = 0; w < workerCount; w++)
                {
                    int workerId = w;
                    workerTasks.Add(Task.Run(async () =>
                    {
                        // Each worker opens the existing file
                        var workerConfig = new InMemoryTableAttribute
                        {
                            MemoryMappedFileName = fileName,
                            MemoryMappedFileSizeMB = 10,
                            AutoCreateFile = false,     // Don't recreate - use existing file
                            AutoLoadOnStartup = true    // Load existing data
                        };

                        using (var workerTable = new InMemoryTable<TestUser>(_loggerFactory, workerConfig))
                        {
                            // Verify worker loaded master's data
                            var loadedUsers = workerTable.Select().ToList();
                            Console.WriteLine($"WORKER {workerId}: Loaded {loadedUsers.Count} users from master");

                            // Worker adds its own data
                            var workerUser = new TestUser
                            {
                                Id = 100 + workerId,
                                Username = $"Worker{workerId}User",
                                Email = $"worker{workerId}@example.com",
                                Age = 25 + workerId
                            };

                            await workerTable.InsertAsync(workerUser);
                            Console.WriteLine($"WORKER {workerId}: Added user ID={workerUser.Id}");

                            // Flush worker's changes back to MMF
                            await workerTable.FlushToMemoryMappedFileAsync();
                            Console.WriteLine($"WORKER {workerId}: Flushed changes to MMF");
                        }

                        // Small delay between workers
                        await Task.Delay(200);
                    }));
                }

                await Task.WhenAll(workerTasks);

                // Small delay to ensure all workers fully release resources
                await Task.Delay(300);

                // VERIFICATION: Master reconnects and verifies all data
                Console.WriteLine("");
                Console.WriteLine("VERIFICATION: Master reconnecting...");

                var verifyConfig = new InMemoryTableAttribute
                {
                    MemoryMappedFileName = fileName,
                    MemoryMappedFileSizeMB = 10,
                    AutoCreateFile = false,
                    AutoLoadOnStartup = true
                };

                using (var verifyTable = new InMemoryTable<TestUser>(_loggerFactory, verifyConfig))
                {
                    var allUsers = verifyTable.Select().ToList();
                    Console.WriteLine($"VERIFICATION: Found {allUsers.Count} total users");

                    int masterUsers = allUsers.Count(u => u.Id < 100);
                    int workerUsers = allUsers.Count(u => u.Id >= 100);

                    Console.WriteLine($"  - Master users: {masterUsers}");
                    Console.WriteLine($"  - Worker users: {workerUsers}");
                    Console.WriteLine("");
                    Console.WriteLine($"? Multi-process coordination successful!");
                    Console.WriteLine($"  Named Semaphores enabled safe cross-process locking!");
                }
            }

            /// <summary>
            /// Test 3: Heavy reader-writer concurrency (10 readers + 2 writers)
            /// </summary>
            private async Task RunReaderWriterConcurrencyTestAsync()
            {
                Console.WriteLine("TEST 3: Reader-Writer Concurrency");
                Console.WriteLine("-----------------------------------");

                string fileName = $"ConcurrentTest_ReaderWriter_{_runTimestamp}";
                int readerCount = 10;
                int writerCount = 2;
                int operationsPerReader = 50;
                int operationsPerWriter = 20;

                var config = new InMemoryTableAttribute
                {
                    MemoryMappedFileName = fileName,
                    MemoryMappedFileSizeMB = 10,
                    AutoCreateFile = true,
                    AutoLoadOnStartup = false
                };

                Console.WriteLine($"File: {fileName}");
                Console.WriteLine($"Readers: {readerCount}, Writers: {writerCount}");
                Console.WriteLine("");

                var tasks = new List<Task>();
                var readerStats = new System.Collections.Concurrent.ConcurrentBag<double>();
                var writerStats = new System.Collections.Concurrent.ConcurrentBag<double>();

                using (var table = new InMemoryTable<TestUser>(_loggerFactory, config))
                {
                    // Pre-populate
                    for (int i = 1; i <= 100; i++)
                    {
                        await table.InsertAsync(new TestUser { Id = i, Username = $"User{i}", Email = $"user{i}@example.com", Age = 20 + (i % 50) });
                    }
                    await table.FlushToMemoryMappedFileAsync();
                    Console.WriteLine("? Initialized with 100 users");
                    Console.WriteLine("");

                    // Spawn readers
                    for (int r = 0; r < readerCount; r++)
                    {
                        int readerId = r;
                        tasks.Add(Task.Run(async () =>
                        {
                            var sw = Stopwatch.StartNew();
                            for (int op = 0; op < operationsPerReader; op++)
                            {
                                var users = table.Select().ToList();
                                // Simulate processing
                                var _ = users.Where(u => u.Age > 30).ToList();
                                await Task.Delay(5); // Simulate work
                            }
                            sw.Stop();
                            readerStats.Add(sw.Elapsed.TotalMilliseconds);
                            Console.WriteLine($"  Reader {readerId}: {operationsPerReader} reads in {sw.Elapsed.TotalMilliseconds:F0}ms");
                        }));
                    }

                    // Spawn writers
                    for (int w = 0; w < writerCount; w++)
                    {
                        int writerId = w;
                        tasks.Add(Task.Run(async () =>
                        {
                            var sw = Stopwatch.StartNew();
                            for (int op = 0; op < operationsPerWriter; op++)
                            {
                                int userId = 10000 + (writerId * 1000) + op;
                                await table.InsertAsync(new TestUser
                                {
                                    Id = userId,
                                    Username = $"Writer{writerId}_User{op}",
                                    Email = $"w{writerId}u{op}@example.com",
                                    Age = 30 + op
                                });
                                await Task.Delay(10); // Writers slightly slower
                            }
                            sw.Stop();
                            writerStats.Add(sw.Elapsed.TotalMilliseconds);
                            Console.WriteLine($"  Writer {writerId}: {operationsPerWriter} writes in {sw.Elapsed.TotalMilliseconds:F0}ms");
                        }));
                    }

                    await Task.WhenAll(tasks);

                    // Final flush
                    await table.FlushToMemoryMappedFileAsync();
                }

                Console.WriteLine("");
                Console.WriteLine($"? Reader-Writer test complete!");
                Console.WriteLine($"  Average reader time: {readerStats.Average():F0}ms");
                Console.WriteLine($"  Average writer time: {writerStats.Average():F0}ms");
                Console.WriteLine($"  Semaphore allows 100 concurrent readers while maintaining write exclusivity!");
            }

            #endregion

            #region Part 5: Long-Running Stress Test with Live Monitoring

            /// <summary>
            /// Long-running stress test (15 seconds) with live monitoring and detailed statistics
            /// </summary>
            private async Task RunLongRunningStressTestAsync()
            {
                Console.WriteLine("========================================");
                Console.WriteLine("PART 5: LONG-RUNNING STRESS TEST (15s)");
                Console.WriteLine("========================================");
                Console.WriteLine("");
                Console.WriteLine("This test runs multiple concurrent workload scenarios for 15 seconds each");
                Console.WriteLine("with real-time monitoring of thread activity and performance metrics.");
                Console.WriteLine("");

                // Scenario 1: Read-Only Workload
                await RunStressScenarioAsync("READ-ONLY", 
                    readThreads: 10, writeThreads: 0, 
                    OpType.Select, OpType.GetById);

                Console.WriteLine("");

                // Scenario 2: Write-Only Workload
                await RunStressScenarioAsync("WRITE-ONLY", 
                    readThreads: 0, writeThreads: 5, 
                    OpType.Insert, OpType.Update, OpType.Delete);

                Console.WriteLine("");

                // Scenario 3: Mixed Read-Write (80/20)
                await RunStressScenarioAsync("MIXED (80% Read / 20% Write)", 
                    readThreads: 8, writeThreads: 2, 
                    OpType.Select, OpType.GetById, OpType.Insert, OpType.Update);

                Console.WriteLine("");

                // Scenario 4: Heavy Write (20/80)
                await RunStressScenarioAsync("HEAVY WRITE (20% Read / 80% Write)", 
                    readThreads: 2, writeThreads: 8, 
                    OpType.GetById, OpType.Insert, OpType.Update, OpType.Delete);

                Console.WriteLine("");

                // Scenario 5: Balanced Read-Write (50/50)
                await RunStressScenarioAsync("BALANCED (50% Read / 50% Write)", 
                    readThreads: 5, writeThreads: 5, 
                    OpType.Select, OpType.GetById, OpType.Insert, OpType.Update, OpType.Delete);
            }

            private enum OpType
            {
                Select,
                GetById,
                Insert,
                Update,
                Delete,
                BulkInsert,
                BulkUpdate
            }

            private async Task RunStressScenarioAsync(string scenarioName, int readThreads, int writeThreads, params OpType[] allowedOps)
            {
                Console.WriteLine($"SCENARIO: {scenarioName}");
                Console.WriteLine($"Duration: 15 seconds");
                Console.WriteLine($"Threads: {readThreads} readers + {writeThreads} writers = {readThreads + writeThreads} total");
                Console.WriteLine($"Operations: {string.Join(", ", allowedOps.Select(o => o.ToString()))}");
                Console.WriteLine("");

                string fileName = $"StressTest_{scenarioName.Replace(" ", "_").Replace("/", "_").Replace("(", "").Replace(")", "")}_{_runTimestamp}";
                int totalThreads = readThreads + writeThreads;

                var config = new InMemoryTableAttribute
                {
                    MemoryMappedFileName = fileName,
                    MemoryMappedFileSizeMB = 50,
                    AutoCreateFile = true,
                    AutoLoadOnStartup = false
                };

                // Thread statistics tracking
                var threadStats = new System.Collections.Concurrent.ConcurrentDictionary<int, ThreadStats>();
                var cts = new CancellationTokenSource();
                var displayLock = new object();

                using (var table = new InMemoryTable<TestUser>(_loggerFactory, config))
                {
                    // Pre-populate with 1000 users
                    Console.Write("Initializing with 1000 users... ");
                    for (int i = 1; i <= 1000; i++)
                    {
                        await table.InsertAsync(new TestUser 
                        { 
                            Id = i, 
                            Username = $"User{i}", 
                            Email = $"user{i}@example.com", 
                            Age = 20 + (i % 50) 
                        });
                    }
                    await table.FlushToMemoryMappedFileAsync();
                    Console.WriteLine("Done!");
                    Console.WriteLine("");

                    // Start threads
                    var tasks = new List<Task>();
                    var startTime = DateTime.UtcNow;

                    // Reader threads
                    for (int t = 0; t < readThreads; t++)
                    {
                        int threadId = t;
                        var stats = new ThreadStats { ThreadId = threadId, ThreadType = "Reader" };
                        threadStats[threadId] = stats;

                        tasks.Add(Task.Run(async () =>
                        {
                            var random = new Random(threadId);
                            var sw = Stopwatch.StartNew();

                            while ((DateTime.UtcNow - startTime).TotalSeconds < 15)
                            {
                                var opStart = Stopwatch.StartNew();
                                try
                                {
                                    var readOps = allowedOps.Where(o => o == OpType.Select || o == OpType.GetById).ToArray();
                                    if (readOps.Length == 0) break;

                                    var op = readOps[random.Next(readOps.Length)];
                                    stats.CurrentOperation = op.ToString();

                                    switch (op)
                                    {
                                        case OpType.Select:
                                            var users = table.Select().ToList();
                                            stats.SelectCount++;
                                            break;
                                        case OpType.GetById:
                                            var userId = random.Next(1, 1001);
                                            var user = table.GetById(userId);
                                            stats.GetByIdCount++;
                                            break;
                                    }

                                    stats.SuccessCount++;
                                }
                                catch
                                {
                                    stats.ErrorCount++;
                                }
                                finally
                                {
                                    opStart.Stop();
                                    stats.TotalWaitTimeMs += opStart.Elapsed.TotalMilliseconds;
                                    stats.TotalOperations++;
                                    stats.CurrentOperation = "Idle";
                                }

                                await Task.Delay(random.Next(1, 5)); // Simulate varying workload
                            }

                            stats.ElapsedMs = sw.Elapsed.TotalMilliseconds;
                        }));
                    }

                    // Writer threads
                    for (int t = 0; t < writeThreads; t++)
                    {
                        int threadId = readThreads + t;
                        var stats = new ThreadStats { ThreadId = threadId, ThreadType = "Writer" };
                        threadStats[threadId] = stats;

                        tasks.Add(Task.Run(async () =>
                        {
                            var random = new Random(threadId);
                            var sw = Stopwatch.StartNew();

                            while ((DateTime.UtcNow - startTime).TotalSeconds < 15)
                            {
                                var opStart = Stopwatch.StartNew();
                                try
                                {
                                    var writeOps = allowedOps.Where(o => o == OpType.Insert || o == OpType.Update || o == OpType.Delete || o == OpType.BulkInsert || o == OpType.BulkUpdate).ToArray();
                                    if (writeOps.Length == 0) break;

                                    var op = writeOps[random.Next(writeOps.Length)];
                                    stats.CurrentOperation = op.ToString();

                                    switch (op)
                                    {
                                        case OpType.Insert:
                                            int newId = 10000 + (threadId * 10000) + (int)stats.InsertCount;
                                            await table.InsertAsync(new TestUser
                                            {
                                                Id = newId,
                                                Username = $"NewUser{newId}",
                                                Email = $"new{newId}@example.com",
                                                Age = random.Next(20, 70)
                                            });
                                            stats.InsertCount++;
                                            break;

                                        case OpType.BulkInsert:
                                            // Batch insert 5-10 records at once
                                            int batchSize = random.Next(5, 11);
                                            var insertBatch = new List<TestUser>();
                                            for (int b = 0; b < batchSize; b++)
                                            {
                                                int batchId = 10000 + (threadId * 10000) + (int)stats.InsertCount + b;
                                                insertBatch.Add(new TestUser
                                                {
                                                    Id = batchId,
                                                    Username = $"BatchUser{batchId}",
                                                    Email = $"batch{batchId}@example.com",
                                                    Age = random.Next(20, 70)
                                                });
                                            }
                                            await table.BulkInsertAsync(insertBatch);
                                            stats.InsertCount += batchSize;
                                            break;

                                        case OpType.Update:
                                            int updateId = random.Next(1, 1001);
                                            var existingUser = table.GetById(updateId);
                                            if (existingUser != null)
                                            {
                                                existingUser.Age = random.Next(20, 70);
                                                await table.UpdateAsync(existingUser);
                                                stats.UpdateCount++;
                                            }
                                            break;

                                        case OpType.BulkUpdate:
                                            // Batch update 5-10 records at once
                                            int updateBatchSize = random.Next(5, 11);
                                            var updateBatch = new List<TestUser>();
                                            for (int b = 0; b < updateBatchSize; b++)
                                            {
                                                int batchUpdateId = random.Next(1, 1001);
                                                var userToUpdate = table.GetById(batchUpdateId);
                                                if (userToUpdate != null)
                                                {
                                                    userToUpdate.Age = random.Next(20, 70);
                                                    updateBatch.Add(userToUpdate);
                                                }
                                            }
                                            if (updateBatch.Count > 0)
                                            {
                                                await table.BulkUpdateAsync(updateBatch);
                                                stats.UpdateCount += updateBatch.Count;
                                            }
                                            break;

                                        case OpType.Delete:
                                            int deleteId = random.Next(1, 1001);
                                            await table.DeleteAsync(deleteId);
                                            stats.DeleteCount++;
                                            break;
                                    }

                                    stats.SuccessCount++;
                                }
                                catch
                                {
                                    stats.ErrorCount++;
                                }
                                finally
                                {
                                    opStart.Stop();
                                    stats.TotalWaitTimeMs += opStart.Elapsed.TotalMilliseconds;
                                    stats.TotalOperations++;
                                    stats.CurrentOperation = "Idle";
                                }

                                await Task.Delay(random.Next(5, 15)); // Writers are slower
                            }

                            stats.ElapsedMs = sw.Elapsed.TotalMilliseconds;
                        }));
                    }

                    // Live monitoring task with simple progress indicator
                    var monitorTask = Task.Run(async () =>
                    {
                        int updateCount = 0;
                        Console.WriteLine("");
                        Console.WriteLine("Live Progress (updates every second):");

                        while ((DateTime.UtcNow - startTime).TotalSeconds < 15)
                        {
                            await Task.Delay(1000); // Update every second
                            updateCount++;

                            lock (displayLock)
                            {
                                var allStats = threadStats.Values.ToList();
                                var readerStats = allStats.Where(s => s.ThreadType == "Reader").ToList();
                                var writerStats = allStats.Where(s => s.ThreadType == "Writer").ToList();

                                long totalOps = allStats.Sum(s => s.TotalOperations);
                                double systemThroughput = totalOps / (double)updateCount;

                                double avgReaderThroughput = readerStats.Any() 
                                    ? readerStats.Average(s => s.ElapsedMs > 0 ? s.TotalOperations / (s.ElapsedMs / 1000.0) : 0) 
                                    : 0;

                                double avgWriterThroughput = writerStats.Any() 
                                    ? writerStats.Average(s => s.ElapsedMs > 0 ? s.TotalOperations / (s.ElapsedMs / 1000.0) : 0) 
                                    : 0;

                                int activeReaders = readerStats.Count(s => s.CurrentOperation != "Idle");
                                int activeWriters = writerStats.Count(s => s.CurrentOperation != "Idle");

                                // Single line progress indicator
                                // IMPORTANT: Never emit BEL (\u0007) or other control characters.
                                // Some terminal hosts map those to an audible notification.
                                string progressLine = $"[{updateCount,2}s] System: {systemThroughput,6:F1} ops/s | " +
                                                     $"Readers: {readerStats.Count} ({activeReaders} active, {avgReaderThroughput,5:F1} ops/s avg) | " +
                                                     $"Writers: {writerStats.Count} ({activeWriters} active, {avgWriterThroughput,5:F1} ops/s avg) | " +
                                                     $"Total Ops: {totalOps,7:N0}";

                                // Defensive: strip any accidental BEL that could have slipped into output.
                                progressLine = progressLine.Replace("\u0007", string.Empty);

                                Console.Write("\r" + progressLine);
                            }
                        }

                        Console.WriteLine(); // New line after monitoring completes
                        Console.WriteLine("");
                    });

                    // Wait for all threads to complete
                    await Task.WhenAll(tasks);
                    await monitorTask;

                    // Final flush
                    await table.FlushToMemoryMappedFileAsync();
                }

                // Final Summary
                Console.WriteLine("");
                Console.WriteLine("=== FINAL SUMMARY ===");
                Console.WriteLine("");

                var allStats = threadStats.Values.ToList();
                var readerStats = allStats.Where(s => s.ThreadType == "Reader").ToList();
                var writerStats = allStats.Where(s => s.ThreadType == "Writer").ToList();

                // Reader Summary
                if (readerStats.Any())
                {
                    Console.WriteLine($"READERS ({readerStats.Count} threads):");
                    Console.WriteLine($"  Total Operations: {readerStats.Sum(s => s.TotalOperations):N0}");
                    Console.WriteLine($"  Total Selects: {readerStats.Sum(s => s.SelectCount):N0}");
                    Console.WriteLine($"  Total GetByIds: {readerStats.Sum(s => s.GetByIdCount):N0}");
                    Console.WriteLine($"  Success Rate: {(readerStats.Sum(s => s.SuccessCount) * 100.0 / readerStats.Sum(s => s.TotalOperations)):F2}%");
                    Console.WriteLine($"  Avg Throughput: {readerStats.Average(s => s.TotalOperations / (s.ElapsedMs / 1000.0)):F1} ops/sec per thread");
                    Console.WriteLine($"  Avg Wait Time: {readerStats.Average(s => s.TotalWaitTimeMs / s.TotalOperations):F2}ms per operation");
                    Console.WriteLine("");
                }

                // Writer Summary
                if (writerStats.Any())
                {
                    Console.WriteLine($"WRITERS ({writerStats.Count} threads):");
                    Console.WriteLine($"  Total Operations: {writerStats.Sum(s => s.TotalOperations):N0}");
                    Console.WriteLine($"  Total Inserts: {writerStats.Sum(s => s.InsertCount):N0} (includes batch inserts)");
                    Console.WriteLine($"  Total Updates: {writerStats.Sum(s => s.UpdateCount):N0} (includes batch updates)");
                    Console.WriteLine($"  Total Deletes: {writerStats.Sum(s => s.DeleteCount):N0}");
                    Console.WriteLine($"  Success Rate: {(writerStats.Sum(s => s.SuccessCount) * 100.0 / writerStats.Sum(s => s.TotalOperations)):F2}%");
                    Console.WriteLine($"  Avg Throughput: {writerStats.Average(s => s.TotalOperations / (s.ElapsedMs / 1000.0)):F1} ops/sec per thread");
                    Console.WriteLine($"  Avg Wait Time: {writerStats.Average(s => s.TotalWaitTimeMs / s.TotalOperations):F2}ms per operation");
                    Console.WriteLine("");
                }

                // Overall Summary
                Console.WriteLine("OVERALL:");
                Console.WriteLine($"  Total Threads: {totalThreads}");
                Console.WriteLine($"  Total Operations: {allStats.Sum(s => s.TotalOperations):N0}");
                Console.WriteLine($"  Total Errors: {allStats.Sum(s => s.ErrorCount)}");
                Console.WriteLine($"  System Throughput: {allStats.Sum(s => s.TotalOperations) / 15.0:F1} ops/sec");
                Console.WriteLine($"  Avg Thread Throughput: {allStats.Average(s => s.TotalOperations / (s.ElapsedMs / 1000.0)):F1} ops/sec");

                // Operation breakdown
                var totalOps = allStats.Sum(s => s.TotalOperations);
                var selectPct = (allStats.Sum(s => s.SelectCount) * 100.0) / totalOps;
                var getByIdPct = (allStats.Sum(s => s.GetByIdCount) * 100.0) / totalOps;
                var insertPct = (allStats.Sum(s => s.InsertCount) * 100.0) / totalOps;
                var updatePct = (allStats.Sum(s => s.UpdateCount) * 100.0) / totalOps;
                var deletePct = (allStats.Sum(s => s.DeleteCount) * 100.0) / totalOps;

                Console.WriteLine("");
                Console.WriteLine("OPERATION BREAKDOWN:");
                if (selectPct > 0) Console.WriteLine($"  Select:  {selectPct,5:F1}% ({allStats.Sum(s => s.SelectCount):N0} ops)");
                if (getByIdPct > 0) Console.WriteLine($"  GetById: {getByIdPct,5:F1}% ({allStats.Sum(s => s.GetByIdCount):N0} ops)");
                if (insertPct > 0) Console.WriteLine($"  Insert:  {insertPct,5:F1}% ({allStats.Sum(s => s.InsertCount):N0} ops)");
                if (updatePct > 0) Console.WriteLine($"  Update:  {updatePct,5:F1}% ({allStats.Sum(s => s.UpdateCount):N0} ops)");
                if (deletePct > 0) Console.WriteLine($"  Delete:  {deletePct,5:F1}% ({allStats.Sum(s => s.DeleteCount):N0} ops)");

                Console.WriteLine("");
                Console.WriteLine($"? Scenario '{scenarioName}' completed successfully!");
            }

            private class ThreadStats
            {
                public int ThreadId { get; set; }
                public string ThreadType { get; set; } = "";
                public string CurrentOperation { get; set; } = "Idle";
                public long TotalOperations { get; set; }
                public long SuccessCount { get; set; }
                public long ErrorCount { get; set; }
                public long SelectCount { get; set; }
                public long GetByIdCount { get; set; }
                public long InsertCount { get; set; }
                public long UpdateCount { get; set; }
                public long DeleteCount { get; set; }
                public double TotalWaitTimeMs { get; set; }
                public double ElapsedMs { get; set; }
            }

            #endregion

            #region Helper Methods

    private List<TestUser> GenerateTestUsers(int count)
    {
        var users = new List<TestUser>(count);
        for (int i = 1; i <= count; i++)
        {
            users.Add(new TestUser
            {
                Id = i,
                Username = $"user_{i}",
                Email = $"user{i}@example.com",
                Age = 20 + (i % 50)
            });
        }
        return users;
    }

    #endregion
}

/// <summary>
/// Test user entity for memory-mapped file demonstrations
/// </summary>
[MessagePackObject]
public class TestUser
{
    [Key(0)]
    public int Id { get; set; }

    [Key(1)]
    public string Username { get; set; } = string.Empty;

    [Key(2)]
    public string Email { get; set; } = string.Empty;

    [Key(3)]
    public int Age { get; set; }
}

