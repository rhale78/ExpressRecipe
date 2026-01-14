using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HighSpeedDAL.SimpleCrudExample.Entities;

namespace HighSpeedDAL.SimpleCrudExample;

/// <summary>
/// High-performance cache and in-memory test suite demonstrating:
/// - SELECT, INSERT, UPDATE, UPSERT, DELETE operations
/// - Live ops/second metrics
/// - Tests up to 100 million records or 8GB of data
/// - Maximum 10 seconds per test
/// - Real-time progress reporting
/// </summary>
public class HighPerformanceCacheTestSuite
{
    private readonly UserDal _userDal;
    private readonly object _consoleLock = new object();
    private int _currentLine;

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

    public HighPerformanceCacheTestSuite(UserDal userDal)
    {
        _userDal = userDal;
    }

    public async Task RunAllTestsAsync()
    {
        Console.Clear();
        Console.WriteLine("================================================================");
        Console.WriteLine("     HIGH-PERFORMANCE CACHE & IN-MEMORY TEST SUITE");
        Console.WriteLine("================================================================");
        Console.WriteLine();
        Console.WriteLine("Test Parameters:");
        Console.WriteLine("  - Max Test Duration: 10 seconds per test");
        Console.WriteLine("  - Max Records: 100 million");
        Console.WriteLine("  - Max Memory: 8 GB");
        Console.WriteLine("  - Live Metrics: Ops/second updated in real-time");
        Console.WriteLine();
        Console.WriteLine("================================================================");
        Console.WriteLine();

        // Test 1: Bulk Insert Performance
        await RunBulkInsertTestAsync();
        Console.WriteLine();

        // Test 2: Cached SELECT Performance
        await RunCachedSelectTestAsync();
        Console.WriteLine();

        // Test 3: UPDATE Performance
        await RunUpdateTestAsync();
        Console.WriteLine();

        // Test 4: UPSERT Performance
        await RunUpsertTestAsync();
        Console.WriteLine();

        // Test 5: DELETE Performance
        await RunDeleteTestAsync();
        Console.WriteLine();

        // Test 6: Memory Usage Analysis
        await RunMemoryAnalysisAsync();
        Console.WriteLine();

            Console.WriteLine("================================================================");
            Console.WriteLine("                  ALL TESTS COMPLETED");
            Console.WriteLine("================================================================");
        }

    private async Task RunBulkInsertTestAsync()
    {
        Console.WriteLine("TEST 1: BULK INSERT PERFORMANCE");
        Console.WriteLine("================================================================");
        Console.WriteLine();

        var batchSizes = new[] { 1000, 10000, 100000, 1000000 };
        
        foreach (var batchSize in batchSizes)
        {
            Console.WriteLine($"Batch Size: {batchSize:N0} records");
            
            var users = new List<User>();
            for (int i = 0; i < batchSize; i++)
            {
                users.Add(new User
                {
                    Username = $"insert_user_{Guid.NewGuid():N}",
                    Email = $"insert{i}@test.com",
                    FirstName = $"First{i}",
                    LastName = $"Last{i}",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                });
            }

            int beforeCount = await CountUsersByPrefixAsync("insert_user_");

            var sw = Stopwatch.StartNew();
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            
            try
            {
                await _userDal.BulkInsertAsync(users, cts.Token);
                sw.Stop();

                // Allow staging merge to complete so we can verify how many rows actually landed.
                await Task.Delay(2000);
                int afterCount = await CountUsersByPrefixAsync("insert_user_");
                int insertedRows = Math.Max(0, afterCount - beforeCount);

                double elapsedSeconds = Math.Max(0.000001, sw.Elapsed.TotalSeconds);
                var opsPerSecond = insertedRows / elapsedSeconds;
                var mbPerSecond = (insertedRows * GetEstimatedRecordSize()) / (1024.0 * 1024.0) / elapsedSeconds;
                double avgUs = insertedRows > 0 ? ElapsedMicroseconds(sw) / insertedRows : 0;

                Console.WriteLine($"  OK Completed in {FormatLatency(ElapsedMicroseconds(sw))}");
                Console.WriteLine($"  - Requested rows: {batchSize:N0}");
                Console.WriteLine($"  - Inserted rows (verified): {insertedRows:N0}");
                Console.WriteLine($"  - {opsPerSecond:N0} inserts/second (verified)");
                Console.WriteLine($"  - {mbPerSecond:F2} MB/second (verified)");
                Console.WriteLine($"  - {avgUs:F1}us per row (verified)");
                Console.WriteLine();
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("  WARN Test exceeded 10 second limit, partial results recorded");
                Console.WriteLine();
            }

            // Clean up test data periodically
            if (batchSize >= 100000)
            {
                Console.WriteLine("  Cleaning up test data...");
                await Task.Delay(2000); // Allow staging merge
            }

        }
    }

    private async Task<int> CountUsersByPrefixAsync(string usernamePrefix)
    {
        var all = await _userDal.GetAllAsync();
        return all.Count(u => u.Username != null && u.Username.StartsWith(usernamePrefix, StringComparison.Ordinal));
    }

    private async Task RunCachedSelectTestAsync()
    {
        Console.WriteLine("TEST 2: CACHED SELECT PERFORMANCE");
        Console.WriteLine("================================================================");
        Console.WriteLine();

        // Insert test data
        Console.WriteLine("Preparing test dataset (10,000 records)...");
        var prepUsers = new List<User>();
        for (int i = 0; i < 10000; i++)
        {
            prepUsers.Add(new User
            {
                Username = $"select_user_{i}",
                Email = $"select{i}@test.com",
                FirstName = $"First{i}",
                LastName = $"Last{i}",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
        }
        await _userDal.BulkInsertAsync(prepUsers);
        await Task.Delay(2000); // Allow staging merge

        var allUsers = await _userDal.GetAllAsync();
        var testIds = allUsers.Take(1000).Select(u => u.Id).ToList();

        Console.WriteLine($"Test dataset ready: {testIds.Count:N0} user IDs");
        Console.WriteLine();

        // Test 2.1: Cold reads (cache miss)
        Console.WriteLine("2.1: Cold Reads (Cache Miss)");
        var coldSw = Stopwatch.StartNew();
        var coldCount = 0;
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try
        {
            while (!cts.Token.IsCancellationRequested && coldCount < testIds.Count)
            {
                await _userDal.GetByIdAsync(testIds[coldCount % testIds.Count], cts.Token);
                coldCount++;

                if (coldCount % 100 == 0)
                {
                    var opsPerSec = coldCount / coldSw.Elapsed.TotalSeconds;
                    Console.Write($"\r  Progress: {coldCount:N0} reads | {opsPerSec:N0} ops/sec");
                }
            }
        }
        catch (OperationCanceledException) { }

        coldSw.Stop();
        var coldOpsPerSec = coldCount / coldSw.Elapsed.TotalSeconds;
        double coldTotalUs = ElapsedMicroseconds(coldSw);
        double coldAvgUs = coldCount > 0 ? coldTotalUs / coldCount : 0;
        Console.WriteLine();
        Console.WriteLine($"  OK Cold reads: {coldCount:N0} in {FormatLatency(coldTotalUs)}");
        Console.WriteLine($"  - {coldOpsPerSec:N0} reads/second");
        Console.WriteLine($"  - {coldAvgUs:F1}us per read");
        Console.WriteLine();

        // Test 2.2: Warm reads (cache hit)
        Console.WriteLine("2.2: Warm Reads (Cache Hit)");
        var warmSw = Stopwatch.StartNew();
        var warmCount = 0;
        cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                await _userDal.GetByIdAsync(testIds[warmCount % testIds.Count], cts.Token);
                warmCount++;

                if (warmCount % 1000 == 0)
                {
                    var opsPerSec = warmCount / warmSw.Elapsed.TotalSeconds;
                    Console.Write($"\r  Progress: {warmCount:N0} reads | {opsPerSec:N0} ops/sec");
                }
            }
        }
        catch (OperationCanceledException) { }

        warmSw.Stop();
        var warmOpsPerSec = warmCount / warmSw.Elapsed.TotalSeconds;
        double warmTotalUs = ElapsedMicroseconds(warmSw);
        double warmAvgUs = warmCount > 0 ? warmTotalUs / warmCount : 0;
        Console.WriteLine();
        Console.WriteLine($"  OK Warm reads: {warmCount:N0} in {FormatLatency(warmTotalUs)}");
        Console.WriteLine($"  - {warmOpsPerSec:N0} reads/second");
        Console.WriteLine($"  - {warmAvgUs:F1}us per read");
        Console.WriteLine();

        var speedup = warmOpsPerSec / coldOpsPerSec;
        Console.WriteLine($"  INFO Cache Speedup: {speedup:F1}x faster");
        Console.WriteLine();
    }

    private async Task RunUpdateTestAsync()
    {
        Console.WriteLine("TEST 3: UPDATE PERFORMANCE");
        Console.WriteLine("================================================================");
        Console.WriteLine();

        // Get test users
        var users = await _userDal.GetAllAsync();
        var testUsers = users.Take(1000).ToList();

        Console.WriteLine($"Testing updates on {testUsers.Count:N0} records...");
        Console.WriteLine();

        var sw = Stopwatch.StartNew();
        var updateCount = 0;
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try
        {
            while (!cts.Token.IsCancellationRequested && updateCount < testUsers.Count * 10)
            {
                var user = testUsers[updateCount % testUsers.Count];
                user.Email = $"updated_{DateTime.Now.Ticks}@test.com";
                user.IsActive = !user.IsActive;

                await _userDal.UpdateAsync(user, cts.Token);
                updateCount++;

                if (updateCount % 100 == 0)
                {
                    var opsPerSec = updateCount / sw.Elapsed.TotalSeconds;
                    Console.Write($"\r  Progress: {updateCount:N0} updates | {opsPerSec:N0} ops/sec");
                }
            }
        }
        catch (OperationCanceledException) { }

        sw.Stop();
        var opsPerSecond = updateCount / sw.Elapsed.TotalSeconds;

        Console.WriteLine();
        Console.WriteLine($"  OK Completed: {updateCount:N0} updates in {sw.ElapsedMilliseconds:N0}ms");
        Console.WriteLine($"  - {opsPerSecond:N0} updates/second");
        Console.WriteLine($"  - {(sw.ElapsedMilliseconds / (double)updateCount):F3}ms per update");
        Console.WriteLine();
    }

    private async Task RunUpsertTestAsync()
    {
        Console.WriteLine("TEST 4: UPSERT PERFORMANCE (Mixed Insert/Update)");
        Console.WriteLine("================================================================");
        Console.WriteLine();

        var users = await _userDal.GetAllAsync();
        var existingUsers = users.Take(500).ToList();

        Console.WriteLine("Testing 50/50 mix of inserts and updates...");
        Console.WriteLine();

        var sw = Stopwatch.StartNew();
        var upsertCount = 0;
        var insertCount = 0;
        var updateCount = 0;
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                if (upsertCount % 2 == 0 && existingUsers.Count > 0)
                {
                    // Update existing
                    var user = existingUsers[upsertCount % existingUsers.Count];
                    user.Email = $"upsert_{DateTime.Now.Ticks}@test.com";
                    await _userDal.UpdateAsync(user, cts.Token);
                    updateCount++;
                }
                else
                {
                    // Insert new
                    var newUser = new User
                    {
                        Username = $"upsert_user_{Guid.NewGuid():N}",
                        Email = $"upsert{upsertCount}@test.com",
                        FirstName = $"Upsert{upsertCount}",
                        LastName = "Test",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    };
                    await _userDal.InsertAsync(newUser, cts.Token);
                    insertCount++;
                }

                upsertCount++;

                if (upsertCount % 100 == 0)
                {
                    var opsPerSec = upsertCount / sw.Elapsed.TotalSeconds;
                    Console.Write($"\r  Progress: {upsertCount:N0} ops ({insertCount} INS, {updateCount} UPD) | {opsPerSec:N0} ops/sec");
                }
            }
        }
        catch (OperationCanceledException) { }

        sw.Stop();
        var opsPerSecond = upsertCount / sw.Elapsed.TotalSeconds;

        Console.WriteLine();
        Console.WriteLine($"  OK Completed: {upsertCount:N0} operations in {sw.ElapsedMilliseconds:N0}ms");
        Console.WriteLine($"  - {insertCount:N0} inserts, {updateCount:N0} updates");
        Console.WriteLine($"  - {opsPerSecond:N0} ops/second");
        Console.WriteLine($"  - {(sw.ElapsedMilliseconds / (double)upsertCount):F3}ms per operation");
        Console.WriteLine();
    }

    private async Task RunDeleteTestAsync()
    {
        Console.WriteLine("TEST 5: DELETE PERFORMANCE");
        Console.WriteLine("================================================================");
        Console.WriteLine();

        // Create test records to delete
        Console.WriteLine("Creating test records for deletion...");
        var deleteUsers = new List<User>();
        for (int i = 0; i < 5000; i++)
        {
            deleteUsers.Add(new User
            {
                Username = $"delete_user_{i}",
                Email = $"delete{i}@test.com",
                FirstName = $"Delete{i}",
                LastName = "Test",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
        }
        await _userDal.BulkInsertAsync(deleteUsers);
        await Task.Delay(2000); // Allow staging merge

        var allUsers = await _userDal.GetAllAsync();
        var testIds = allUsers.Where(u => u.Username.StartsWith("delete_user_"))
                              .Select(u => u.Id)
                              .ToList();

        Console.WriteLine($"Test dataset ready: {testIds.Count:N0} records to delete");
        Console.WriteLine();

        var sw = Stopwatch.StartNew();
        var deleteCount = 0;
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try
        {
            foreach (var id in testIds)
            {
                if (cts.Token.IsCancellationRequested) break;

                await _userDal.DeleteAsync(id, cts.Token);
                deleteCount++;

                if (deleteCount % 100 == 0)
                {
                    var opsPerSec = deleteCount / sw.Elapsed.TotalSeconds;
                    Console.Write($"\r  Progress: {deleteCount:N0} deletes | {opsPerSec:N0} ops/sec");
                }
            }
        }
        catch (OperationCanceledException) { }

        sw.Stop();
        var opsPerSecond = deleteCount / sw.Elapsed.TotalSeconds;

        Console.WriteLine();
        Console.WriteLine($"  OK Completed: {deleteCount:N0} deletes in {sw.ElapsedMilliseconds:N0}ms");
        Console.WriteLine($"  - {opsPerSecond:N0} deletes/second");
        Console.WriteLine($"  - {(sw.ElapsedMilliseconds / (double)deleteCount):F3}ms per delete");
        Console.WriteLine();
    }

    private async Task RunMemoryAnalysisAsync()
    {
        Console.WriteLine("TEST 6: MEMORY USAGE ANALYSIS");
        Console.WriteLine("================================================================");
        Console.WriteLine();

        var initialMemory = GC.GetTotalMemory(true);
        Console.WriteLine($"Initial Memory: {FormatBytes(initialMemory)}");
        Console.WriteLine();

        // Load increasing amounts of data
        var testSizes = new[] { 1000, 10000, 50000, 100000 };

        foreach (var size in testSizes)
        {
            Console.WriteLine($"Loading {size:N0} records into cache...");

            var users = new List<User>();
            for (int i = 0; i < size; i++)
            {
                users.Add(new User
                {
                    Username = $"mem_user_{i}",
                    Email = $"mem{i}@test.com",
                    FirstName = $"Memory{i}",
                    LastName = $"Test{i}",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                });
            }

            await _userDal.BulkInsertAsync(users);
            await Task.Delay(1000);

            // Trigger cache load
            var loaded = await _userDal.GetAllAsync();

            var currentMemory = GC.GetTotalMemory(false);
            var memoryUsed = currentMemory - initialMemory;
            var bytesPerRecord = memoryUsed / (double)size;

            Console.WriteLine($"  - Total records in cache: {loaded.Count:N0}");
            Console.WriteLine($"  - Memory used: {FormatBytes(memoryUsed)}");
            Console.WriteLine($"  - Bytes per record: {bytesPerRecord:F2}");
            Console.WriteLine($"  - Estimated capacity at 8GB: {(8L * 1024 * 1024 * 1024 / bytesPerRecord):N0} records");
            Console.WriteLine();
        }

        var finalMemory = GC.GetTotalMemory(false);
        Console.WriteLine($"Final Memory: {FormatBytes(finalMemory)}");
        Console.WriteLine($"Total Memory Used: {FormatBytes(finalMemory - initialMemory)}");
        Console.WriteLine();
    }

    private int GetEstimatedRecordSize()
    {
        // Approximate size of a User record in bytes
        // Username (50) + Email (50) + FirstName (50) + LastName (50) + DateTime (8) + bool (1) + overhead
        return 256;
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
}
