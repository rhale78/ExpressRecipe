# Memory-Mapped File Quick Start Guide

## Overview

Memory-mapped files enable ultra-fast cross-process data sharing for HighSpeedDAL `InMemoryTable<T>`. Perfect for distributed queue systems, caching layers, and inter-service communication.

## Installation

Memory-mapped file support is built into `HighSpeedDAL.Core` v0.1+. No additional packages required.

## ?? Prerequisites: MessagePack Attributes Required

**IMPORTANT:** Any entity class used with memory-mapped files MUST have MessagePack serialization attributes.

### Required Attributes

```csharp
using MessagePack;  // ? Add this using statement

[MessagePackObject]  // ? Required on class
public class Message
{
    [Key(0)]  // ? Required on all properties (sequential integers starting at 0)
    public int Id { get; set; }

    [Key(1)]
    public string Text { get; set; } = string.Empty;

    [Key(2)]
    public DateTime Timestamp { get; set; }
}
```

### Why MessagePack?

Memory-mapped files use MessagePack for binary serialization because:
- ? **Fast:** 5-10x faster than JSON
- ? **Compact:** Small binary format saves disk space
- ? **Cross-platform:** Works on Windows, Linux, macOS
- ? **Cross-process:** Stable binary format across process boundaries

### Key Attribute Rules

1. Class must have `[MessagePackObject]` attribute
2. All properties must have `[Key(n)]` with sequential integers (0, 1, 2, ...)
3. Don't skip numbers (0, 1, 2, not 0, 2, 5)
4. Don't reorder existing keys (breaks compatibility)

### Quick Example: Correct vs Incorrect

? **CORRECT:**
```csharp
using MessagePack;

[MessagePackObject]
public class WorkItem
{
    [Key(0)]
    public Guid Id { get; set; }

    [Key(1)]
    public string Status { get; set; } = string.Empty;

    [Key(2)]
    public byte[] Payload { get; set; } = Array.Empty<byte>();
}
```

? **INCORRECT (Missing Attributes):**
```csharp
// ? Missing [MessagePackObject] - will throw FormatterNotRegisteredException
public class WorkItem
{
    // ? Missing [Key(n)] - will not serialize
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public byte[] Payload { get; set; } = Array.Empty<byte>();
}
```

## Basic Usage

### Step 1: Create a Shared Table (Producer)

```csharp
using HighSpeedDAL.Core.InMemoryTable;
using HighSpeedDAL.Core.Attributes;
using Microsoft.Extensions.Logging;

// Configure memory-mapped backing store
var config = new InMemoryTableAttribute
{
    MemoryMappedFileName = "MyQueue",        // Unique file name
    MemoryMappedFileSizeMB = 100,            // 100MB max
    SyncMode = MemoryMappedSyncMode.Batched, // Auto-flush every 30s
    AutoCreateFile = true,                    // Create if missing
    AutoLoadOnStartup = true,                 // Load existing data
    FlushIntervalSeconds = 30                 // Flush interval (for Batched mode)
};

// Create table with logger factory (required for memory-mapped files)
var table = new InMemoryTable<Message>(loggerFactory, config);

// Insert messages (automatically synced based on SyncMode)
await table.InsertAsync(new Message 
{ 
    Text = "Hello", 
    Timestamp = DateTime.UtcNow 
});
```

### Step 2: Read from Another Process (Consumer)

```csharp
// Configure for reading
var config = new InMemoryTableAttribute
{
    MemoryMappedFileName = "MyQueue",  // Same name as producer
    MemoryMappedFileSizeMB = 100,      // Same size
    AutoCreateFile = false,             // Expect existing file
    AutoLoadOnStartup = true            // Load on init
};

// Create table - data is instantly available!
var table = new InMemoryTable<Message>(loggerFactory, config);

// Query the data
var recent = table.Select(m => m.Timestamp > DateTime.UtcNow.AddMinutes(-5));
Console.WriteLine($"Found {recent.Count()} recent messages");
```

## Sync Modes Explained

### Immediate Mode (Highest Durability)
```csharp
var config = new InMemoryTableAttribute
{
    MemoryMappedFileName = "CriticalQueue",
    SyncMode = MemoryMappedSyncMode.Immediate
};
```
- **When**: Every insert/update immediately written to file
- **Performance**: ~10K-50K ops/sec
- **Use Case**: Critical data, low volume, cannot tolerate data loss

### Batched Mode (Balanced - RECOMMENDED)
```csharp
var config = new InMemoryTableAttribute
{
    MemoryMappedFileName = "StandardQueue",
    SyncMode = MemoryMappedSyncMode.Batched,
    FlushIntervalSeconds = 30  // Flush every 30 seconds
};
```
- **When**: Periodic auto-flush at configured intervals
- **Performance**: ~100K-500K ops/sec
- **Use Case**: Most production scenarios, balanced durability/performance

### Manual Mode (Highest Performance)
```csharp
var config = new InMemoryTableAttribute
{
    MemoryMappedFileName = "BulkQueue",
    SyncMode = MemoryMappedSyncMode.Manual
};

// ... perform many operations ...

// Manually flush when ready
await table.FlushToMemoryMappedFileAsync();
```
- **When**: Flush only when explicitly requested
- **Performance**: ~1M+ ops/sec (pure memory speed)
- **Use Case**: Bulk operations, batch processing, periodic snapshots

## Common Patterns

### Pattern 1: Distributed Work Queue

**Producer Service:**
```csharp
var config = new InMemoryTableAttribute
{
    MemoryMappedFileName = "WorkQueue",
    MemoryMappedFileSizeMB = 200,
    SyncMode = MemoryMappedSyncMode.Batched,
    FlushIntervalSeconds = 10
};

var queue = new InMemoryTable<WorkItem>(loggerFactory, config);

// Add work items
await queue.InsertAsync(new WorkItem 
{ 
    TaskId = Guid.NewGuid(),
    Status = "Pending",
    Payload = data
});
```

**Consumer Service (multiple instances):**
```csharp
var config = new InMemoryTableAttribute
{
    MemoryMappedFileName = "WorkQueue",
    MemoryMappedFileSizeMB = 200,
    AutoLoadOnStartup = true
};

var queue = new InMemoryTable<WorkItem>(loggerFactory, config);

// Poll for work
while (true)
{
    var pending = queue.Select(w => w.Status == "Pending").FirstOrDefault();
    if (pending != null)
    {
        // Process work item
        await ProcessAsync(pending);
        
        // Update status in own memory (producer will see changes on next load)
        pending.Status = "Completed";
        await queue.UpdateAsync(pending);
        await queue.FlushToMemoryMappedFileAsync();
    }
    
    await Task.Delay(1000);
}
```

### Pattern 2: Reference Data Cache

**Loader Service:**
```csharp
var config = new InMemoryTableAttribute
{
    MemoryMappedFileName = "ProductCatalog",
    MemoryMappedFileSizeMB = 500,
    SyncMode = MemoryMappedSyncMode.Manual
};

var catalog = new InMemoryTable<Product>(loggerFactory, config);

// Load from database (once per hour)
var products = await LoadFromDatabaseAsync();
await catalog.BulkInsertAsync(products);
await catalog.FlushToMemoryMappedFileAsync();
```

**Multiple Consumer Services:**
```csharp
var config = new InMemoryTableAttribute
{
    MemoryMappedFileName = "ProductCatalog",
    MemoryMappedFileSizeMB = 500,
    AutoLoadOnStartup = true
};

// Instant access to 500MB of product data!
var catalog = new InMemoryTable<Product>(loggerFactory, config);

// Query at memory speed
var result = catalog.Select(p => p.CategoryId == categoryId && p.InStock);
```

### Pattern 3: Inter-Service Communication

**Service A (Writer):**
```csharp
var config = new InMemoryTableAttribute
{
    MemoryMappedFileName = "ServiceA_Messages",
    SyncMode = MemoryMappedSyncMode.Immediate
};

var outbox = new InMemoryTable<Message>(loggerFactory, config);
await outbox.InsertAsync(new Message { To = "ServiceB", Body = "..." });
```

**Service B (Reader):**
```csharp
var config = new InMemoryTableAttribute
{
    MemoryMappedFileName = "ServiceA_Messages",
    AutoLoadOnStartup = true
};

var inbox = new InMemoryTable<Message>(loggerFactory, config);

// Poll for new messages
var newMessages = inbox.Select(m => m.To == "ServiceB" && m.Status == "Unread");
```

## File Locations

Memory-mapped files are stored at:
```
Windows: %TEMP%\HighSpeedDAL\{FileName}.mmf
Example: C:\Users\username\AppData\Local\Temp\HighSpeedDAL\MyQueue.mmf
```

## Troubleshooting

### Schema Mismatch Error

**Error**: `InvalidDataException: Schema mismatch`

**Cause**: Entity properties changed (name, type, or count)

**Solution**: 
1. File is automatically recreated
2. Old data is lost (by design - prevents corruption)
3. For production: backup file before schema changes

### File Size Exceeded

**Error**: `InvalidOperationException: Data size exceeds file capacity`

**Solution**: Increase `MemoryMappedFileSizeMB`:
```csharp
var config = new InMemoryTableAttribute
{
    MemoryMappedFileName = "MyQueue",
    MemoryMappedFileSizeMB = 500  // Increase from 100 to 500
};
```

### Lock Timeout

**Error**: `TimeoutException: Failed to acquire write lock`

**Cause**: Another process holds the lock too long

**Solution**:
1. Check for hung processes
2. Use shorter transactions
3. Optimize batch sizes

### Missing ILoggerFactory Error

**Error**: `InvalidOperationException: ILoggerFactory must be provided`

**Solution**: Use the LoggerFactory constructor:
```csharp
// ? Wrong - won't work with memory-mapped files
var table = new InMemoryTable<Message>(logger, config);

// ? Correct - use loggerFactory
var table = new InMemoryTable<Message>(loggerFactory, config);
```

## Performance Tips

### Tip 1: Right-Size Your File
```csharp
// Calculate: (avg row size × max rows) + 20% overhead
int avgRowBytes = 150;  // Measure your entity
int maxRows = 100_000;
int fileSizeMB = ((avgRowBytes * maxRows) / 1024 / 1024) * 1.2;

var config = new InMemoryTableAttribute
{
    MemoryMappedFileSizeMB = fileSizeMB
};
```

### Tip 2: Choose the Right Sync Mode
- **< 1K ops/sec**: Use Immediate (data safety)
- **1K-10K ops/sec**: Use Batched (balanced)
- **> 10K ops/sec**: Use Manual (maximum speed)

### Tip 3: Batch Operations
```csharp
// ? Slow - 1000 individual operations
for (int i = 0; i < 1000; i++)
{
    await table.InsertAsync(items[i]);
}

// ? Fast - single bulk operation
await table.BulkInsertAsync(items);
```

### Tip 4: Pre-Size Collections
```csharp
// Reserve capacity for better insert performance
var items = new List<Message>(capacity: 10000);
```

## Monitoring

### Track File Size
```csharp
string filePath = Path.Combine(
    Path.GetTempPath(), 
    "HighSpeedDAL", 
    $"{config.MemoryMappedFileName}.mmf"
);

var fileInfo = new FileInfo(filePath);
Console.WriteLine($"File size: {fileInfo.Length / 1024 / 1024}MB");
```

### Track Row Count
```csharp
Console.WriteLine($"Rows in memory: {table.RowCount}");
Console.WriteLine($"Total rows (including deleted): {table.TotalRowCount}");
```

### Track Flush Performance
```csharp
var sw = Stopwatch.StartNew();
await table.FlushToMemoryMappedFileAsync();
sw.Stop();
Console.WriteLine($"Flush took {sw.ElapsedMilliseconds}ms");
```

## Best Practices

1. **Always dispose tables**: Ensures final flush
   ```csharp
   using var table = new InMemoryTable<Message>(loggerFactory, config);
   // ... use table ...
   // Dispose automatically flushes and cleans up
   ```

2. **Use descriptive file names**: Include service name and purpose
   ```csharp
   MemoryMappedFileName = "OrderService_WorkQueue"
   ```

3. **Handle startup carefully**: Check if file exists before `AutoLoadOnStartup = false`
   ```csharp
   string filePath = Path.Combine(Path.GetTempPath(), "HighSpeedDAL", "MyQueue.mmf");
   bool fileExists = File.Exists(filePath);
   
   var config = new InMemoryTableAttribute
   {
       AutoCreateFile = !fileExists,  // Create only if missing
       AutoLoadOnStartup = fileExists // Load only if exists
   };
   ```

4. **Test cross-process scenarios**: Use multiple console apps
   ```bash
   # Terminal 1 - Producer
   dotnet run --project Producer
   
   # Terminal 2 - Consumer
   dotnet run --project Consumer
   ```

5. **Monitor memory usage**: Large files consume RAM
   ```csharp
   // Check current process memory
   long memoryBytes = GC.GetTotalMemory(false);
   Console.WriteLine($"Memory used: {memoryBytes / 1024 / 1024}MB");
   ```

## Next Steps

- Review [benchmarks](../examples/SimpleCrudExample/BenchmarkRunner.cs) for performance characteristics
- Read [full implementation documentation](MEMORY_MAPPED_FILE_IMPLEMENTATION.md)
- Explore [InMemoryTable API](../src/HighSpeedDAL.Core/InMemoryTable/InMemoryTable.cs)

## Support

For issues or questions:
1. Check the [troubleshooting section](#troubleshooting) above
2. Review [benchmark results](../examples/SimpleCrudExample/BenchmarkRunner.cs)
3. Open an issue on GitHub with:
   - Entity class definition
   - Configuration settings
   - Error messages and stack traces
   - File size and row count estimates
