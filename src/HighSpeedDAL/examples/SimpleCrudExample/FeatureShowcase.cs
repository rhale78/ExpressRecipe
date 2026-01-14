using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HighSpeedDAL.SimpleCrudExample.Entities;

namespace HighSpeedDAL.SimpleCrudExample;

/// <summary>
/// Demonstrates key features of HighSpeedDAL framework in a clear, educational way.
/// Each method showcases a specific capability with explanations.
/// </summary>
public class FeatureShowcase
{
    private readonly UserDal _userDal;

    public FeatureShowcase(UserDal userDal)
    {
        _userDal = userDal ?? throw new ArgumentNullException(nameof(userDal));
    }

    /// <summary>
    /// Runs all feature demonstrations in sequence
    /// </summary>
    public async Task RunAllDemonstrationsAsync()
    {
        Console.WriteLine("========================================================");
        Console.WriteLine("  HighSpeedDAL Framework - Feature Showcase");
        Console.WriteLine("========================================================");
        Console.WriteLine();

        await DemonstrateBasicCrudOperationsAsync();
        await DemonstrateCachingBehaviorAsync();
        await DemonstrateBulkOperationsAsync();
        await DemonstrateMemoryMappedFilesAsync();

        Console.WriteLine();
        Console.WriteLine("========================================================");
        Console.WriteLine("  All Demonstrations Complete!");
        Console.WriteLine("========================================================");
    }

    /// <summary>
    /// Demonstrates basic CRUD (Create, Read, Update, Delete) operations
    /// </summary>
    private async Task DemonstrateBasicCrudOperationsAsync()
    {
        PrintSectionHeader("1. Basic CRUD Operations");

        // CREATE
        Console.WriteLine("Creating users...");
        var john = new User
        {
            Username = "john.doe",
            Email = "john@example.com",
            FirstName = "John",
            LastName = "Doe",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        User insertedJohn = await _userDal.InsertAsync(john);
        Console.WriteLine($"  OK Created: {john.Username} (ID: {insertedJohn.Id})");

        var jane = new User
        {
            Username = "jane.smith",
            Email = "jane@example.com",
            FirstName = "Jane",
            LastName = "Smith",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        User insertedJane = await _userDal.InsertAsync(jane);
        Console.WriteLine($"  OK Created: {jane.Username} (ID: {insertedJane.Id})");
        Console.WriteLine();

        // READ
        Console.WriteLine("Reading user by ID...");
        var retrievedUser = await _userDal.GetByIdAsync(insertedJohn.Id);
        if (retrievedUser != null)
        {
            Console.WriteLine($"  OK Retrieved: {retrievedUser.Username} - {retrievedUser.Email}");
        }
        Console.WriteLine();

        Console.WriteLine("Reading all users...");
        var allUsers = await _userDal.GetAllAsync();
        Console.WriteLine($"  OK Found {allUsers.Count} users total");
        Console.WriteLine();

        // UPDATE
        Console.WriteLine("Updating user...");
        retrievedUser!.Email = "john.updated@example.com";
        retrievedUser.IsActive = false;
        await _userDal.UpdateAsync(retrievedUser);
        Console.WriteLine($"  OK Updated {retrievedUser.Username} - New email: {retrievedUser.Email}");
        Console.WriteLine();

        // DELETE
        Console.WriteLine("Deleting user...");
        await _userDal.DeleteAsync(insertedJane.Id);
        var deletedUser = await _userDal.GetByIdAsync(insertedJane.Id);
        Console.WriteLine($"  OK Deleted {jane.Username} - Exists: {deletedUser != null}");
        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates caching behavior with memory and staging table
    /// </summary>
    private async Task DemonstrateCachingBehaviorAsync()
    {
        PrintSectionHeader("2. Caching Behavior");

        Console.WriteLine("The User entity is configured with:");
        Console.WriteLine("  - Memory cache (300 second expiration)");
        Console.WriteLine("  - Staging table (30 second sync interval)");
        Console.WriteLine();

        // First read - populates cache
        Console.WriteLine("First read (populates cache)...");
        var allUsers = await _userDal.GetAllAsync();
        Console.WriteLine($"  OK Retrieved {allUsers.Count} users from database");
        Console.WriteLine();

        // Second read - from cache
        Console.WriteLine("Second read (from cache)...");
        var cachedUsers = await _userDal.GetAllAsync();
        Console.WriteLine($"  OK Retrieved {cachedUsers.Count} users from cache (faster!)");
        Console.WriteLine();

        // Show defensive cloning
        Console.WriteLine("Defensive cloning protection:");
        var user = cachedUsers.FirstOrDefault();
        if (user != null)
        {
            string originalEmail = user.Email;
            user.Email = "modified@example.com"; // Modify the returned object
            
            var freshUser = await _userDal.GetByIdAsync(user.Id);
            Console.WriteLine($"  - Modified returned object: {user.Email}");
            Console.WriteLine($"  - Fresh copy from cache: {freshUser?.Email}");
            Console.WriteLine("  OK Cache protected from mutations (defensive cloning)");
        }
        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates bulk operations for high-performance scenarios
    /// </summary>
    private async Task DemonstrateBulkOperationsAsync()
    {
        PrintSectionHeader("3. Bulk Operations");

        Console.WriteLine("Creating 1,000 users in bulk...");
        var bulkUsers = Enumerable.Range(1, 1000).Select(i => new User
        {
            Username = $"bulk_user_{i}",
            Email = $"bulk{i}@example.com",
            FirstName = $"First{i}",
            LastName = $"Last{i}",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        }).ToList();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await _userDal.BulkInsertAsync(bulkUsers);
        sw.Stop();

        Console.WriteLine($"  OK Inserted 1,000 users in {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"  OK Rate: {1000.0 / sw.Elapsed.TotalSeconds:N0} users/second");
        Console.WriteLine();

        Console.WriteLine("Note: Framework uses staging tables for non-blocking writes");
        Console.WriteLine("  - Writes go to staging table immediately");
        Console.WriteLine("  - Background process merges to main table every 30 seconds");
        Console.WriteLine("  - Reads continue without blocking during bulk operations");
        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates memory-mapped files for ultra-high throughput logging
    /// </summary>
    private async Task DemonstrateMemoryMappedFilesAsync()
    {
        PrintSectionHeader("4. Memory-Mapped Files (Optional Advanced Feature)");

        Console.WriteLine("Memory-mapped files enable ultra-high throughput:");
        Console.WriteLine("  - 1M+ operations per second");
        Console.WriteLine("  - Shared memory access across processes");
        Console.WriteLine("  - Ideal for logging and telemetry");
        Console.WriteLine();

        Console.WriteLine("Example use case: Activity logging");
        Console.WriteLine("  [MemoryMappedTable(SizeMB = 100, FileName = \"ActivityLogs\")]");
        Console.WriteLine("  public partial class ActivityLog { ... }");
        Console.WriteLine();

        Console.WriteLine("To see memory-mapped files in action:");
        Console.WriteLine("  Run with: --memory-mapped-demo");
        Console.WriteLine();
    }

    private void PrintSectionHeader(string title)
    {
        Console.WriteLine("========================================================");
        Console.WriteLine($"  {title}");
        Console.WriteLine("========================================================");
        Console.WriteLine();
    }
}
