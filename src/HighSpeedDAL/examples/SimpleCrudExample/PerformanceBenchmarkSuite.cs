using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HighSpeedDAL.SimpleCrudExample.Entities;

namespace HighSpeedDAL.SimpleCrudExample;

/// <summary>
/// Comprehensive performance benchmark suite comparing:
/// - DB only (no cache)
/// - DB with Memory cache
/// - DB with TwoLayer cache
/// Planned future comparisons:
/// - InMemoryTable (coming soon)
/// - Memory-mapped files (coming soon)
/// Shows live metrics, tracks top performers, and provides detailed summaries
/// </summary>
public class PerformanceBenchmarkSuite
{
    private readonly UserDal _userDal;
    private readonly UserWithMemoryMappedDal? _userWithMemoryMappedDal;
    private readonly UserWithAuditDal? _userWithAuditDal;
    private readonly UserNoAuditDal? _userNoAuditDal;
    private readonly UserWithSoftDeleteDal? _userWithSoftDeleteDal;
    private readonly Dictionary<string, BenchmarkCategory> _categoryResults = new();
    private const int MaxTestDurationSeconds = 10;
    private const long MaxMemoryBytes = 8L * 1024 * 1024 * 1024; // 8GB

    public PerformanceBenchmarkSuite(
        UserDal userDal,
        UserWithMemoryMappedDal? userWithMemoryMappedDal = null,
        UserWithAuditDal? userWithAuditDal = null,
        UserNoAuditDal? userNoAuditDal = null,
        UserWithSoftDeleteDal? userWithSoftDeleteDal = null)
    {
        _userDal = userDal;
        _userWithMemoryMappedDal = userWithMemoryMappedDal;
        _userWithAuditDal = userWithAuditDal;
        _userNoAuditDal = userNoAuditDal;
        _userWithSoftDeleteDal = userWithSoftDeleteDal;
        InitializeCategories();
    }

    private void InitializeCategories()
    {
        _categoryResults["SELECT"] = new BenchmarkCategory { Name = "SELECT" };
        _categoryResults["INSERT"] = new BenchmarkCategory { Name = "INSERT" };
        _categoryResults["UPDATE"] = new BenchmarkCategory { Name = "UPDATE" };
        _categoryResults["UPSERT"] = new BenchmarkCategory { Name = "UPSERT" };
        _categoryResults["DELETE"] = new BenchmarkCategory { Name = "DELETE" };
        _categoryResults["BULK_INSERT"] = new BenchmarkCategory { Name = "BULK INSERT" };
        _categoryResults["BULK_UPDATE"] = new BenchmarkCategory { Name = "BULK UPDATE" };
        _categoryResults["BULK_DELETE"] = new BenchmarkCategory { Name = "BULK DELETE" };
        _categoryResults["AUDIT_COMPARISON"] = new BenchmarkCategory { Name = "AUDIT COMPARISON (INSERT)" };
        _categoryResults["SOFTDELETE_COMPARISON"] = new BenchmarkCategory { Name = "SOFT DELETE COMPARISON" };
    }

    public async Task RunAllBenchmarksAsync()
    {
        Console.Clear();
        PrintHeader();

        // Check initial memory usage
        long initialMemory = GC.GetTotalMemory(true);
        Console.WriteLine($"Initial Memory: {FormatBytes(initialMemory)}");
        Console.WriteLine();

        // Prepare test data
        await PrepareTestDataAsync();

        // Run all benchmark scenarios
        await BenchmarkSelectOperationsAsync();
        await BenchmarkInsertOperationsAsync();
        await BenchmarkUpdateOperationsAsync();
        await BenchmarkUpsertOperationsAsync();
        await BenchmarkDeleteOperationsAsync();
        await BenchmarkBulkInsertOperationsAsync();
        await BenchmarkBulkUpdateOperationsAsync();
        await BenchmarkBulkDeleteOperationsAsync();

        // Run comparison benchmarks if DALs are available
        if (_userWithAuditDal != null && _userNoAuditDal != null)
        {
            await BenchmarkAuditComparisonAsync();
        }
        
        if (_userWithSoftDeleteDal != null)
        {
            await BenchmarkSoftDeleteComparisonAsync();
        }

        // Check final memory usage
        long finalMemory = GC.GetTotalMemory(false);
        Console.WriteLine();
        Console.WriteLine($"Final Memory: {FormatBytes(finalMemory)}");
        Console.WriteLine($"Memory Used: {FormatBytes(finalMemory - initialMemory)}");
        Console.WriteLine();

        // Print final summary
        PrintFinalSummary();
    }

    private void PrintHeader()
    {
        Console.WriteLine("+-------------------------------------------------------------------------------+");
        Console.WriteLine("|          HIGH-PERFORMANCE DATA ACCESS LAYER BENCHMARK SUITE                  |");
        Console.WriteLine("+-------------------------------------------------------------------------------+");
        Console.WriteLine();
        Console.WriteLine("Testing Scenarios:");
        Console.WriteLine("  - DB Only (No Cache)         - Baseline database performance");
        Console.WriteLine("  - DB + Memory Cache          - In-memory dictionary caching");
        Console.WriteLine("  - DB + TwoLayer Cache        - L1 (fast) + L2 (thread-safe) caching");
        Console.WriteLine();
        Console.WriteLine("Note: InMemoryTable and Memory-Mapped Files are now available!");
        Console.WriteLine("      See MemoryMappedTestSuite for dedicated benchmarks.");
        Console.WriteLine();
        Console.WriteLine("Constraints:");
        Console.WriteLine($"  - Max Test Duration: {MaxTestDurationSeconds} seconds per test");
        Console.WriteLine($"  - Max Memory Usage: {MaxMemoryBytes / (1024 * 1024 * 1024)}GB (monitored)");
        Console.WriteLine("  - Live metrics updated every second");
        Console.WriteLine();
        Console.WriteLine("-------------------------------------------------------------------------------");
        Console.WriteLine();
    }

    private async Task PrepareTestDataAsync()
    {
        Console.WriteLine("PREPARING TEST DATA");
        Console.WriteLine("-------------------------------------------------------------------------------");
        Console.WriteLine();

        // Create baseline test users
        var testUsers = new List<User>();
        for (int i = 0; i < 1000; i++)
        {
            testUsers.Add(new User
            {
                Username = $"benchmark_user_{i}",
                Email = $"bench{i}@example.com",
                FirstName = $"First{i}",
                LastName = $"Last{i}",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
        }

        Console.Write("Inserting 1,000 test users... ");
        await _userDal.BulkInsertAsync(testUsers);
        Console.WriteLine("[OK] Done");

        Console.WriteLine("Waiting for staging table merge...");
        await Task.Delay(2000);
        Console.WriteLine("[OK] Ready");
        Console.WriteLine();
    }

    private async Task BenchmarkSelectOperationsAsync()
    {
        Console.WriteLine("BENCHMARK: SELECT OPERATIONS");
        Console.WriteLine("-------------------------------------------------------------------------------");
        Console.WriteLine();

        var category = _categoryResults["SELECT"];

        // Get test IDs
        var allUsers = await _userDal.GetAllAsync();
        var testIds = allUsers.Take(100).Select(u => u.Id).ToList();

        // Test 1: DB Only (Cold reads)
        Console.WriteLine("Test 1: DB Only (No Cache) - Cold Reads");
        Console.WriteLine("-------------------------------------------------------------------------------");
        var dbResult = await RunTimedBenchmarkAsync(
            "DB Only",
            async (cts) =>
            {
                int count = 0;
                while (!cts.Token.IsCancellationRequested)
                {
                    await _userDal.GetByIdAsync(testIds[count % testIds.Count], cts.Token);
                    count++;
                }
                return count;
            });
        category.AddResult("DB Only", dbResult.OpsPerSecond, dbResult.TotalOps, dbResult.AvgLatencyMs);
        Console.WriteLine();

        // Test 2: DB + Memory Cache (Warm reads)
        Console.WriteLine("Test 2: DB + Memory Cache - Warm Reads");
        Console.WriteLine("-------------------------------------------------------------------------------");
        // Warm up cache
        foreach (var id in testIds.Take(50))
        {
            await _userDal.GetByIdAsync(id);
        }
        var memoryCacheResult = await RunTimedBenchmarkAsync(
            "Memory Cache",
            async (cts) =>
            {
                int count = 0;
                while (!cts.Token.IsCancellationRequested)
                {
                    await _userDal.GetByIdAsync(testIds[count % testIds.Count], cts.Token);
                    count++;
                }
                return count;
            });
        category.AddResult("Memory Cache", memoryCacheResult.OpsPerSecond, memoryCacheResult.TotalOps, memoryCacheResult.AvgLatencyMs);
        Console.WriteLine();

        // Test 3: DB + TwoLayer Cache
        Console.WriteLine("Test 3: DB + TwoLayer Cache - Hot Reads");
        Console.WriteLine("-------------------------------------------------------------------------------");
        var twoLayerResult = await RunTimedBenchmarkAsync(
            "TwoLayer Cache",
            async (cts) =>
            {
                int count = 0;
                while (!cts.Token.IsCancellationRequested)
                {
                    await _userDal.GetByIdAsync(testIds[count % testIds.Count], cts.Token);
                    count++;
                }
                return count;
            });
        category.AddResult("TwoLayer Cache", twoLayerResult.OpsPerSecond, twoLayerResult.TotalOps, twoLayerResult.AvgLatencyMs);
        Console.WriteLine();

        PrintCategoryWinner(category, dbResult.OpsPerSecond);
    }

    private async Task BenchmarkInsertOperationsAsync()
    {
        Console.WriteLine("BENCHMARK: INSERT OPERATIONS");
        Console.WriteLine("-------------------------------------------------------------------------------");
        Console.WriteLine();

        var category = _categoryResults["INSERT"];

        // Test 1: DB Only
        Console.WriteLine("Test 1: DB Only - Single Inserts");
        Console.WriteLine("-------------------------------------------------------------------------------");
        var dbResult = await RunTimedBenchmarkAsync(
            "DB Only",
            async (cts) =>
            {
                int count = 0;
                while (!cts.Token.IsCancellationRequested)
                {
                    var user = new User
                    {
                        Username = $"insert_db_{Guid.NewGuid():N}",
                        Email = $"insert_db_{count}@example.com",
                        FirstName = $"Insert{count}",
                        LastName = "DBOnly",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    };
                    await _userDal.InsertAsync(user, cts.Token);
                    count++;
                }
                return count;
            });
        category.AddResult("DB Only", dbResult.OpsPerSecond, dbResult.TotalOps, dbResult.AvgLatencyMs);
        Console.WriteLine();

        // Test 2: DB + Cache
        Console.WriteLine("Test 2: DB + Cache - Single Inserts (with cache write-through)");
        Console.WriteLine("-------------------------------------------------------------------------------");
        var cacheResult = await RunTimedBenchmarkAsync(
            "DB + Cache",
            async (cts) =>
            {
                int count = 0;
                while (!cts.Token.IsCancellationRequested)
                {
                    var user = new User
                    {
                        Username = $"insert_cache_{Guid.NewGuid():N}",
                        Email = $"insert_cache_{count}@example.com",
                        FirstName = $"Insert{count}",
                        LastName = "Cache",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    };
                    await _userDal.InsertAsync(user, cts.Token);
                    count++;
                }
                return count;
            });
        category.AddResult("DB + Cache", cacheResult.OpsPerSecond, cacheResult.TotalOps, cacheResult.AvgLatencyMs);
        Console.WriteLine();

        PrintCategoryWinner(category, dbResult.OpsPerSecond);
    }

    private async Task BenchmarkUpdateOperationsAsync()
    {
        Console.WriteLine("BENCHMARK: UPDATE OPERATIONS");
        Console.WriteLine("-------------------------------------------------------------------------------");
        Console.WriteLine();

        var category = _categoryResults["UPDATE"];

        // Get test users
        var allUsers = await _userDal.GetAllAsync();
        var testUsers = allUsers.Take(100).ToList();

        // Test 1: DB Only
        Console.WriteLine("Test 1: DB Only - Single Updates");
        Console.WriteLine("-------------------------------------------------------------------------------");
        var dbResult = await RunTimedBenchmarkAsync(
            "DB Only",
            async (cts) =>
            {
                int count = 0;
                while (!cts.Token.IsCancellationRequested)
                {
                    var user = testUsers[count % testUsers.Count];
                    user.Email = $"updated_{DateTime.Now.Ticks}@example.com";
                    await _userDal.UpdateAsync(user, cts.Token);
                    count++;
                }
                return count;
            });
        category.AddResult("DB Only", dbResult.OpsPerSecond, dbResult.TotalOps, dbResult.AvgLatencyMs);
        Console.WriteLine();

        // Test 2: DB + Cache
        Console.WriteLine("Test 2: DB + Cache - Single Updates (with cache invalidation)");
        Console.WriteLine("-------------------------------------------------------------------------------");
        var cacheResult = await RunTimedBenchmarkAsync(
            "DB + Cache",
            async (cts) =>
            {
                int count = 0;
                while (!cts.Token.IsCancellationRequested)
                {
                    var user = testUsers[count % testUsers.Count];
                    user.IsActive = !user.IsActive;
                    await _userDal.UpdateAsync(user, cts.Token);
                    count++;
                }
                return count;
            });
        category.AddResult("DB + Cache", cacheResult.OpsPerSecond, cacheResult.TotalOps, cacheResult.AvgLatencyMs);
        Console.WriteLine();

        PrintCategoryWinner(category, dbResult.OpsPerSecond);
    }

    private async Task BenchmarkUpsertOperationsAsync()
    {
        Console.WriteLine("BENCHMARK: UPSERT OPERATIONS (Mixed Insert/Update)");
        Console.WriteLine("-------------------------------------------------------------------------------");
        Console.WriteLine();

        var category = _categoryResults["UPSERT"];

        // Get existing users for updates
        var allUsers = await _userDal.GetAllAsync();
        var existingUsers = allUsers.Take(50).ToList();

        // Test 1: DB Only - Mixed Insert/Update
        Console.WriteLine("Test 1: DB Only - 50/50 Mix of Inserts and Updates");
        Console.WriteLine("-------------------------------------------------------------------------------");
        var dbResult = await RunTimedBenchmarkAsync(
            "DB Only",
            async (cts) =>
            {
                int count = 0;
                while (!cts.Token.IsCancellationRequested)
                {
                    if (count % 2 == 0 && existingUsers.Count > 0)
                    {
                        // Update existing user
                        var user = existingUsers[count % existingUsers.Count];
                        user.Email = $"upsert_db_{DateTime.Now.Ticks}@example.com";
                        await _userDal.UpdateAsync(user, cts.Token);
                    }
                    else
                    {
                        // Insert new user
                        var newUser = new User
                        {
                            Username = $"upsert_db_{Guid.NewGuid():N}",
                            Email = $"upsert_db_{count}@example.com",
                            FirstName = $"Upsert{count}",
                            LastName = "DBOnly",
                            CreatedAt = DateTime.UtcNow,
                            IsActive = true
                        };
                        await _userDal.InsertAsync(newUser, cts.Token);
                    }
                    count++;
                }
                return count;
            });
        category.AddResult("DB Only", dbResult.OpsPerSecond, dbResult.TotalOps, dbResult.AvgLatencyMs);
        Console.WriteLine();

        // Test 2: DB + Cache - Mixed Insert/Update
        Console.WriteLine("Test 2: DB + Cache - 50/50 Mix with cache write-through and invalidation");
        Console.WriteLine("-------------------------------------------------------------------------------");
        var cacheResult = await RunTimedBenchmarkAsync(
            "DB + Cache",
            async (cts) =>
            {
                int count = 0;
                while (!cts.Token.IsCancellationRequested)
                {
                    if (count % 2 == 0 && existingUsers.Count > 0)
                    {
                        // Update existing user
                        var user = existingUsers[count % existingUsers.Count];
                        user.Email = $"upsert_cache_{DateTime.Now.Ticks}@example.com";
                        await _userDal.UpdateAsync(user, cts.Token);
                    }
                    else
                    {
                        // Insert new user
                        var newUser = new User
                        {
                            Username = $"upsert_cache_{Guid.NewGuid():N}",
                            Email = $"upsert_cache_{count}@example.com",
                            FirstName = $"Upsert{count}",
                            LastName = "Cache",
                            CreatedAt = DateTime.UtcNow,
                            IsActive = true
                        };
                        await _userDal.InsertAsync(newUser, cts.Token);
                    }
                    count++;
                }
                return count;
            });
        category.AddResult("DB + Cache", cacheResult.OpsPerSecond, cacheResult.TotalOps, cacheResult.AvgLatencyMs);
        Console.WriteLine();

        PrintCategoryWinner(category, dbResult.OpsPerSecond);
    }

    private async Task BenchmarkDeleteOperationsAsync()
    {
        Console.WriteLine("BENCHMARK: DELETE OPERATIONS");
        Console.WriteLine("-------------------------------------------------------------------------------");
        Console.WriteLine();

        var category = _categoryResults["DELETE"];

        // Create test users to delete
        var deleteUsers = new List<User>();
        for (int i = 0; i < 500; i++)
        {
            deleteUsers.Add(new User
            {
                Username = $"delete_test_{i}",
                Email = $"delete{i}@example.com",
                FirstName = $"Delete{i}",
                LastName = "Test",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
        }
        await _userDal.BulkInsertAsync(deleteUsers);
        await Task.Delay(2000);

        var allUsers = await _userDal.GetAllAsync();
        var testIds = allUsers.Where(u => u.Username.StartsWith("delete_test_"))
                              .Select(u => u.Id)
                              .ToList();

        // Test 1: DB Only
        Console.WriteLine("Test 1: DB Only - Single Deletes");
        Console.WriteLine("-------------------------------------------------------------------------------");
        var dbResult = await RunTimedBenchmarkAsync(
            "DB Only",
            async (cts) =>
            {
                int count = 0;
                while (!cts.Token.IsCancellationRequested && count < testIds.Count / 2)
                {
                    await _userDal.DeleteAsync(testIds[count], cts.Token);
                    count++;
                }
                return count;
            });
        category.AddResult("DB Only", dbResult.OpsPerSecond, dbResult.TotalOps, dbResult.AvgLatencyMs);
        Console.WriteLine();

        // Test 2: DB + Cache
        Console.WriteLine("Test 2: DB + Cache - Single Deletes (with cache invalidation)");
        Console.WriteLine("-------------------------------------------------------------------------------");
        var cacheResult = await RunTimedBenchmarkAsync(
            "DB + Cache",
            async (cts) =>
            {
                int count = 0;
                int startIndex = testIds.Count / 2;
                while (!cts.Token.IsCancellationRequested && count < testIds.Count / 2)
                {
                    await _userDal.DeleteAsync(testIds[startIndex + count], cts.Token);
                    count++;
                }
                return count;
            });
        category.AddResult("DB + Cache", cacheResult.OpsPerSecond, cacheResult.TotalOps, cacheResult.AvgLatencyMs);
        Console.WriteLine();

        PrintCategoryWinner(category, dbResult.OpsPerSecond);
    }

    private async Task BenchmarkBulkInsertOperationsAsync()
    {
        Console.WriteLine("BENCHMARK: BULK INSERT OPERATIONS");
        Console.WriteLine("-------------------------------------------------------------------------------");
        Console.WriteLine();

        var category = _categoryResults["BULK_INSERT"];

        // Test: Bulk Insert with various batch sizes
        var batchSizes = new[] { 100, 1000, 10000 };

        foreach (var batchSize in batchSizes)
        {
            Console.WriteLine($"Test: Bulk Insert - Batch Size {batchSize:N0}");
            Console.WriteLine("-------------------------------------------------------------------------------");

            var users = new List<User>();
            for (int i = 0; i < batchSize; i++)
            {
                users.Add(new User
                {
                    Username = $"bulk_insert_{Guid.NewGuid():N}",
                    Email = $"bulk{i}@example.com",
                    FirstName = $"Bulk{i}",
                    LastName = "Insert",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                });
            }

            var sw = Stopwatch.StartNew();
            await _userDal.BulkInsertAsync(users);
            sw.Stop();

            double opsPerSecond = batchSize / sw.Elapsed.TotalSeconds;
            double avgLatency = sw.Elapsed.TotalMilliseconds / batchSize;

            Console.WriteLine($"  [OK] Completed: {batchSize:N0} inserts in {sw.ElapsedMilliseconds:N0}ms");
            Console.WriteLine($"  - Throughput: {opsPerSecond:N0} ops/sec");
            Console.WriteLine($"  - Avg Latency: {avgLatency:F3}ms per record");
            Console.WriteLine($"  - Total Time: {sw.Elapsed.TotalSeconds:F2}s");
            Console.WriteLine();

            category.AddResult($"Batch {batchSize:N0}", opsPerSecond, batchSize, avgLatency);

            // Small delay between batches
            await Task.Delay(1000);
        }

        PrintCategoryWinner(category, 0);
    }

    private async Task BenchmarkBulkUpdateOperationsAsync()
    {
        Console.WriteLine("BENCHMARK: BULK UPDATE OPERATIONS");
        Console.WriteLine("-------------------------------------------------------------------------------");
        Console.WriteLine();

        var category = _categoryResults["BULK_UPDATE"];

        var allUsers = await _userDal.GetAllAsync();
        var batchSizes = new[] { 100, 500, 1000 };

        foreach (var batchSize in batchSizes)
        {
            Console.WriteLine($"Test: Bulk Update - Batch Size {batchSize:N0}");
            Console.WriteLine("-------------------------------------------------------------------------------");

            var usersToUpdate = allUsers.Take(batchSize).ToList();
            foreach (var user in usersToUpdate)
            {
                user.Email = $"bulk_update_{Guid.NewGuid():N}@example.com";
            }

            var sw = Stopwatch.StartNew();
            await _userDal.BulkUpdateAsync(usersToUpdate);
            sw.Stop();

            double opsPerSecond = batchSize / sw.Elapsed.TotalSeconds;
            double avgLatency = sw.Elapsed.TotalMilliseconds / batchSize;

            Console.WriteLine($"  [OK] Completed: {batchSize:N0} updates in {sw.ElapsedMilliseconds:N0}ms");
            Console.WriteLine($"  - Throughput: {opsPerSecond:N0} ops/sec");
            Console.WriteLine($"  - Avg Latency: {avgLatency:F3}ms per record");
            Console.WriteLine($"  - Total Time: {sw.Elapsed.TotalSeconds:F2}s");
            Console.WriteLine();

            category.AddResult($"Batch {batchSize:N0}", opsPerSecond, batchSize, avgLatency);

            await Task.Delay(1000);
        }

        PrintCategoryWinner(category, 0);
    }

    private async Task BenchmarkBulkDeleteOperationsAsync()
    {
        Console.WriteLine("BENCHMARK: BULK DELETE OPERATIONS");
        Console.WriteLine("-------------------------------------------------------------------------------");
        Console.WriteLine();

        var category = _categoryResults["BULK_DELETE"];

        // Create test data for bulk deletes
        var bulkDeleteUsers = new List<User>();
        for (int i = 0; i < 2000; i++)
        {
            bulkDeleteUsers.Add(new User
            {
                Username = $"bulk_delete_{i}",
                Email = $"bulkdel{i}@example.com",
                FirstName = $"BulkDel{i}",
                LastName = "Test",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
        }
        await _userDal.BulkInsertAsync(bulkDeleteUsers);
        await Task.Delay(2000);

        var allUsers = await _userDal.GetAllAsync();
        var deleteIds = allUsers.Where(u => u.Username.StartsWith("bulk_delete_"))
                                .Select(u => u.Id)
                                .ToList();

        var batchSizes = new[] { 100, 500, 1000 };
        int currentIndex = 0;

        foreach (var batchSize in batchSizes)
        {
            if (currentIndex + batchSize > deleteIds.Count)
                break;

            Console.WriteLine($"Test: Bulk Delete - Batch Size {batchSize:N0}");
            Console.WriteLine("-------------------------------------------------------------------------------");

            var idsToDelete = deleteIds.Skip(currentIndex).Take(batchSize).ToList();

            var sw = Stopwatch.StartNew();
            await _userDal.BulkDeleteAsync(idsToDelete);
            sw.Stop();

            double opsPerSecond = batchSize / sw.Elapsed.TotalSeconds;
            double avgLatency = sw.Elapsed.TotalMilliseconds / batchSize;

            Console.WriteLine($"  [OK] Completed: {batchSize:N0} deletes in {sw.ElapsedMilliseconds:N0}ms");
            Console.WriteLine($"  - Throughput: {opsPerSecond:N0} ops/sec");
            Console.WriteLine($"  - Avg Latency: {avgLatency:F3}ms per record");
            Console.WriteLine($"  - Total Time: {sw.Elapsed.TotalSeconds:F2}s");
            Console.WriteLine();

            category.AddResult($"Batch {batchSize:N0}", opsPerSecond, batchSize, avgLatency);

            currentIndex += batchSize;
            await Task.Delay(1000);
        }

        PrintCategoryWinner(category, 0);
    }

    private async Task<BenchmarkResult> RunTimedBenchmarkAsync(
        string scenarioName,
        Func<CancellationTokenSource, Task<int>> benchmarkFunc)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(MaxTestDurationSeconds));
        var sw = Stopwatch.StartNew();
        
        Console.Write($"Running {scenarioName}... (max {MaxTestDurationSeconds}s) ");
        
        // Track operations with shared counter for live metrics (thread-safe)
        long operationCounter = 0;
        var liveMetricsRunning = true;
        
        // Start live metrics task.
        // IMPORTANT: Avoid carriage-return style progress ("\r...") because some terminal/host
        // configurations generate an audible bell / notification when rapidly rewriting a line.
        // We print periodic status lines instead.
        var metricsTask = Task.Run(async () =>
        {
            long lastCount = 0;
            try
            {
                while (liveMetricsRunning && !cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(1000, cts.Token);
                    long currentCount = Interlocked.Read(ref operationCounter);
                    long opsThisSecond = currentCount - lastCount;
                    lastCount = currentCount;

                    Console.WriteLine($"Running {scenarioName}... {sw.Elapsed.Seconds}s | {currentCount:N0} ops | {opsThisSecond:N0} ops/sec");
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when the benchmark duration elapses.
            }
        });

        int totalOps = 0;
        try
        {
            // Wrap benchmark function to track operations
            totalOps = await benchmarkFunc(cts);
            Interlocked.Exchange(ref operationCounter, totalOps);
        }
        catch (OperationCanceledException)
        {
            // Expected when time limit reached
            totalOps = (int)Interlocked.Read(ref operationCounter);
        }
        finally
        {
            liveMetricsRunning = false;
            await metricsTask;
        }
        
        sw.Stop();

        // Guard against division by zero
        double opsPerSecond = totalOps > 0 ? totalOps / sw.Elapsed.TotalSeconds : 0;
        double avgLatencyMs = totalOps > 0 ? sw.Elapsed.TotalMilliseconds / totalOps : 0;

        // New line after live metrics (already line-based, keep spacing between sections)
        Console.WriteLine();
        Console.WriteLine($"  [OK] Completed: {totalOps:N0} operations in {sw.ElapsedMilliseconds:N0}ms");
        Console.WriteLine($"  - Throughput: {opsPerSecond:N0} ops/sec");
        Console.WriteLine($"  - Avg Latency: {avgLatencyMs:F3}ms per operation");
        Console.WriteLine($"  - Total Time: {sw.Elapsed.TotalSeconds:F2}s");

        return new BenchmarkResult
        {
            ScenarioName = scenarioName,
            TotalOps = totalOps,
            ElapsedMs = sw.ElapsedMilliseconds,
            OpsPerSecond = opsPerSecond,
            AvgLatencyMs = avgLatencyMs
        };
    }

    private void PrintCategoryWinner(BenchmarkCategory category, double baselineOpsPerSec)
    {
        if (!category.Results.Any())
            return;

        var winner = category.Results.OrderByDescending(r => r.OpsPerSecond).First();
        category.Winner = winner.ScenarioName;

        Console.WriteLine("-------------------------------------------------------------------------------");
        Console.WriteLine($"WINNER: {winner.ScenarioName}");
        Console.WriteLine($"   - {winner.OpsPerSecond:N0} ops/sec");

        if (baselineOpsPerSec > 0)
        {
            double improvement = winner.OpsPerSecond / baselineOpsPerSec;
            Console.WriteLine($"   - {improvement:F1}x faster than baseline");
        }
        
        Console.WriteLine("-------------------------------------------------------------------------------");
        Console.WriteLine();
    }

    private void PrintFinalSummary()
    {
        Console.WriteLine();
        Console.WriteLine("+-------------------------------------------------------------------------------+");
        Console.WriteLine("|                          FINAL SUMMARY                                        |");
        Console.WriteLine("+-------------------------------------------------------------------------------+");
        Console.WriteLine();

        foreach (var kvp in _categoryResults)
        {
            var category = kvp.Value;
            if (!category.Results.Any())
                continue;

            Console.WriteLine($"Category: {category.Name}");
            Console.WriteLine("-----------------------------------------------------------------------------");

            var sorted = category.Results.OrderByDescending(r => r.OpsPerSecond).ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                var result = sorted[i];
                string medal = i == 0 ? "[1st]" : i == 1 ? "[2nd]" : i == 2 ? "[3rd]" : "     ";
                Console.WriteLine($"  {medal} {result.ScenarioName,-20} {result.OpsPerSecond,10:N0} ops/sec  |  {result.AvgLatencyMs,8:F3}ms avg  |  {result.TotalOps,10:N0} total ops");
            }

            if (sorted.Count > 1 && sorted[0].OpsPerSecond > 0 && sorted[sorted.Count - 1].OpsPerSecond > 0)
            {
                double improvement = sorted[0].OpsPerSecond / sorted[sorted.Count - 1].OpsPerSecond;
                Console.WriteLine($"  -> Best performer is {improvement:F1}x faster than slowest");
            }

            Console.WriteLine();
        }

        Console.WriteLine("-------------------------------------------------------------------------------");
        Console.WriteLine("                  BENCHMARK SUITE COMPLETED SUCCESSFULLY");
        Console.WriteLine("-------------------------------------------------------------------------------");
    }

    private async Task BenchmarkAuditComparisonAsync()
    {
        Console.WriteLine("BENCHMARK: AUDIT vs NO AUDIT COMPARISON");
        Console.WriteLine("-------------------------------------------------------------------------------");
        Console.WriteLine();
        Console.WriteLine("Comparing INSERT performance with and without auto-audit fields");
        Console.WriteLine("(CreatedBy, CreatedDate, ModifiedBy, ModifiedDate)");
        Console.WriteLine();

        var category = _categoryResults["AUDIT_COMPARISON"];

        // Test 1: INSERT without Audit
        Console.WriteLine("Test 1: INSERT - No Audit Fields");
        Console.WriteLine("-------------------------------------------------------------------------------");
        var noAuditResult = await RunTimedBenchmarkAsync(
            "No Audit",
            async (cts) =>
            {
                int count = 0;
                while (!cts.Token.IsCancellationRequested)
                {
                    var user = new UserNoAudit
                    {
                        Username = $"noaudit_{Guid.NewGuid():N}",
                        Email = $"noaudit{count}@example.com",
                        FirstName = $"NoAudit{count}",
                        LastName = "Test",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    };
                    await _userNoAuditDal!.InsertAsync(user);
                    count++;
                }
                return count;
            });
        category.AddResult("No Audit", noAuditResult.OpsPerSecond, noAuditResult.TotalOps, noAuditResult.AvgLatencyMs);
        Console.WriteLine();

        // Test 2: INSERT with Audit
        Console.WriteLine("Test 2: INSERT - With Audit Fields (auto-populated)");
        Console.WriteLine("-------------------------------------------------------------------------------");
        var withAuditResult = await RunTimedBenchmarkAsync(
            "With Audit",
            async (cts) =>
            {
                int count = 0;
                while (!cts.Token.IsCancellationRequested)
                {
                    var user = new UserWithAudit
                    {
                        Username = $"audit_{Guid.NewGuid():N}",
                        Email = $"audit{count}@example.com",
                        FirstName = $"Audit{count}",
                        LastName = "Test",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    };
                    await _userWithAuditDal!.InsertAsync(user, "benchmark_user");
                    count++;
                }
                return count;
            });
        category.AddResult("With Audit", withAuditResult.OpsPerSecond, withAuditResult.TotalOps, withAuditResult.AvgLatencyMs);
        Console.WriteLine();

        // Calculate overhead
        double overheadPercent = ((noAuditResult.OpsPerSecond - withAuditResult.OpsPerSecond) / noAuditResult.OpsPerSecond) * 100;
        Console.WriteLine("-------------------------------------------------------------------------------");
        Console.WriteLine($"Audit Overhead: {Math.Abs(overheadPercent):F2}% slower");
        Console.WriteLine($"   No Audit: {noAuditResult.OpsPerSecond:N0} ops/sec");
        Console.WriteLine($"   With Audit: {withAuditResult.OpsPerSecond:N0} ops/sec");
        Console.WriteLine("   Note: Audit adds 4 extra columns (CreatedBy, CreatedDate, ModifiedBy, ModifiedDate)");
        Console.WriteLine("-------------------------------------------------------------------------------");
        Console.WriteLine();
    }

    private async Task BenchmarkSoftDeleteComparisonAsync()
    {
        Console.WriteLine("BENCHMARK: SOFT DELETE vs HARD DELETE COMPARISON");
        Console.WriteLine("-------------------------------------------------------------------------------");
        Console.WriteLine();
        Console.WriteLine("Comparing DELETE performance: Soft Delete (update IsDeleted flag) vs Hard Delete (actual removal)");
        Console.WriteLine();

        var category = _categoryResults["SOFTDELETE_COMPARISON"];

        // Prepare test data for soft delete
        Console.WriteLine("Preparing test data for Soft Delete comparison...");
        var softDeleteUsers = new List<UserWithSoftDelete>();
        for (int i = 0; i < 500; i++)
        {
            softDeleteUsers.Add(new UserWithSoftDelete
            {
                Username = $"softdelete_{i}",
                Email = $"softdel{i}@example.com",
                FirstName = $"Soft{i}",
                LastName = "Delete",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
        }
        await _userWithSoftDeleteDal!.BulkInsertAsync(softDeleteUsers);
        await Task.Delay(2000); // Wait for staging merge

        var allSoftDeleteUsers = await _userWithSoftDeleteDal.GetAllAsync();
        var softDeleteIds = allSoftDeleteUsers.Where(u => u.Username.StartsWith("softdelete_"))
                                               .Select(u => u.Id)
                                               .ToList();

        // Test 1: Soft Delete (logical)
        Console.WriteLine("Test 1: Soft Delete - Logical deletion (sets IsDeleted=true)");
        Console.WriteLine("-------------------------------------------------------------------------------");
        var softDeleteResult = await RunTimedBenchmarkAsync(
            "Soft Delete",
            async (cts) =>
            {
                int count = 0;
                while (!cts.Token.IsCancellationRequested && count < softDeleteIds.Count / 2)
                {
                    await _userWithSoftDeleteDal.DeleteAsync(softDeleteIds[count], cts.Token);
                    count++;
                }
                return count;
            });
        category.AddResult("Soft Delete", softDeleteResult.OpsPerSecond, softDeleteResult.TotalOps, softDeleteResult.AvgLatencyMs);
        Console.WriteLine();

        // Prepare test data for hard delete
        var hardDeleteUsers = new List<User>();
        for (int i = 0; i < 500; i++)
        {
            hardDeleteUsers.Add(new User
            {
                Username = $"harddelete_{i}",
                Email = $"harddel{i}@example.com",
                FirstName = $"Hard{i}",
                LastName = "Delete",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
        }
        await _userDal.BulkInsertAsync(hardDeleteUsers);
        await Task.Delay(2000); // Wait for staging merge

        var allHardDeleteUsers = await _userDal.GetAllAsync();
        var hardDeleteIds = allHardDeleteUsers.Where(u => u.Username.StartsWith("harddelete_"))
                                              .Select(u => u.Id)
                                              .ToList();

        // Test 2: Hard Delete (physical)
        Console.WriteLine("Test 2: Hard Delete - Physical deletion (removes from database)");
        Console.WriteLine("-------------------------------------------------------------------------------");
        var hardDeleteResult = await RunTimedBenchmarkAsync(
            "Hard Delete",
            async (cts) =>
            {
                int count = 0;
                while (!cts.Token.IsCancellationRequested && count < hardDeleteIds.Count / 2)
                {
                    await _userDal.DeleteAsync(hardDeleteIds[count], cts.Token);
                    count++;
                }
                return count;
            });
        category.AddResult("Hard Delete", hardDeleteResult.OpsPerSecond, hardDeleteResult.TotalOps, hardDeleteResult.AvgLatencyMs);
        Console.WriteLine();

        // Calculate difference
        double diffPercent = ((softDeleteResult.OpsPerSecond - hardDeleteResult.OpsPerSecond) / hardDeleteResult.OpsPerSecond) * 100;
        string comparison = diffPercent > 0 ? "faster" : "slower";
        Console.WriteLine("-------------------------------------------------------------------------------");
        Console.WriteLine($"Soft Delete is {Math.Abs(diffPercent):F2}% {comparison} than Hard Delete");
        Console.WriteLine($"   Soft Delete: {softDeleteResult.OpsPerSecond:N0} ops/sec (UPDATE IsDeleted=true)");
        Console.WriteLine($"   Hard Delete: {hardDeleteResult.OpsPerSecond:N0} ops/sec (DELETE FROM table)");
        Console.WriteLine("   Note: Soft Delete preserves data for auditing and recovery");
        Console.WriteLine("-------------------------------------------------------------------------------");
        Console.WriteLine();
    }

    private string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:F2} {sizes[order]}";
    }

    private class BenchmarkResult
    {
        public string ScenarioName { get; set; } = "";
        public int TotalOps { get; set; }
        public long ElapsedMs { get; set; }
        public double OpsPerSecond { get; set; }
        public double AvgLatencyMs { get; set; }
    }

    private class BenchmarkCategory
    {
        public string Name { get; set; } = "";
        public string Winner { get; set; } = "";
        public List<BenchmarkResult> Results { get; } = new();

        public void AddResult(string scenarioName, double opsPerSecond, int totalOps, double avgLatencyMs)
        {
            Results.Add(new BenchmarkResult
            {
                ScenarioName = scenarioName,
                OpsPerSecond = opsPerSecond,
                TotalOps = totalOps,
                AvgLatencyMs = avgLatencyMs
            });
        }
    }
}
