# MessagePack Serialization Fix for Memory-Mapped Files

**Date:** 2025-01-09  
**Issue:** `MessagePack.FormatterNotRegisteredException` when using memory-mapped files  
**Status:** ? Fixed

## Problem

When running memory-mapped file benchmarks, the following error occurred:

```
MessagePack.FormatterNotRegisteredException: HighSpeedDAL.SimpleCrudExample.Entities.User 
is not registered in resolver: MessagePack.Resolvers.StandardResolver
```

## Root Cause

The `User` entity class was missing required MessagePack serialization attributes:
- Missing `[MessagePackObject]` class attribute
- Missing `[Key(n)]` property attributes

MessagePack requires explicit opt-in for serialization to ensure performance and security.

## Solution

Added MessagePack attributes to the `User` entity:

### Before

```csharp
using System;
using HighSpeedDAL.Core.Attributes;

[Table("Users")]
[Cache(CacheStrategy.Memory, MaxSize = 1000, ExpirationSeconds = 300)]
[StagingTable(SyncIntervalSeconds = 30)]
[DalEntity]
public partial class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;
}
```

### After

```csharp
using System;
using HighSpeedDAL.Core.Attributes;
using MessagePack;  // ? Added

[Table("Users")]
[Cache(CacheStrategy.Memory, MaxSize = 1000, ExpirationSeconds = 300)]
[StagingTable(SyncIntervalSeconds = 30)]
[DalEntity]
[MessagePackObject]  // ? Added
public partial class User
{
    [Key(0)]  // ? Added
    public int Id { get; set; }

    [Key(1)]  // ? Added
    public string Username { get; set; } = string.Empty;

    [Key(2)]  // ? Added
    public string Email { get; set; } = string.Empty;

    [Key(3)]  // ? Added
    public string FirstName { get; set; } = string.Empty;

    [Key(4)]  // ? Added
    public string LastName { get; set; } = string.Empty;

    [Key(5)]  // ? Added
    public DateTime CreatedAt { get; set; }

    [Key(6)]  // ? Added
    public bool IsActive { get; set; } = true;
}
```

## Key Points

### MessagePack Requirements

1. **Class-level attribute:** `[MessagePackObject]` marks the class as serializable
2. **Property-level attributes:** `[Key(n)]` with sequential integers (0, 1, 2, ...)
3. **Key ordering:** Must be sequential and unique within the class
4. **Using statement:** Add `using MessagePack;` at the top of the file

### Why MessagePack?

Memory-mapped files use MessagePack for binary serialization because:
- ? **Fast:** Much faster than JSON or XML
- ? **Compact:** Small binary format saves disk space
- ? **Cross-platform:** Works on Windows, Linux, macOS
- ? **Cross-process:** Binary format is stable across process boundaries
- ? **Versioning:** Key-based approach supports schema evolution

### Key Attribute Guidelines

```csharp
[MessagePackObject]
public class MyEntity
{
    [Key(0)]  // Start at 0
    public int Id { get; set; }
    
    [Key(1)]  // Increment by 1
    public string Name { get; set; }
    
    [Key(2)]  // Continue sequentially
    public DateTime Created { get; set; }
    
    // DO NOT skip numbers (0, 1, 2, not 0, 2, 5)
    // DO NOT reorder existing keys (breaks compatibility)
}
```

## Impact

### Files Modified

- ? `examples\SimpleCrudExample\Entities\User.cs` - Added MessagePack attributes

### Build Status

? Build successful  
? No compilation errors  
? Memory-mapped file benchmarks now work correctly

## Usage Pattern

Any entity class used with memory-mapped files MUST have MessagePack attributes:

```csharp
using MessagePack;

[InMemoryTable(
    MemoryMappedFileName = "MyData",
    MemoryMappedFileSizeMB = 100)]
[MessagePackObject]  // ? Required for memory-mapped files
public class MyEntity
{
    [Key(0)]  // ? Required
    public int Id { get; set; }
    
    [Key(1)]  // ? Required
    public string Name { get; set; }
}
```

## Testing

### Manual Test

1. Run SimpleCrudExample with benchmarks:
   ```bash
   cd examples\SimpleCrudExample
   dotnet run
   ```

2. Select memory-mapped file benchmarks (option 2)

3. Verify no serialization errors

4. Verify data persists across process restarts

## Other Entities

### Entities That Need MessagePack Attributes

If using memory-mapped files with these entities, they also need MessagePack attributes:

- ? `User.cs` - Fixed
- ?? `UserWithMemoryMapped.cs` - Already has MessagePack attributes
- ?? `BenchmarkEntities.cs` (`UserNoCache`, `UserTwoLayer`) - Check if used with memory-mapped files

### Check Before Using Memory-Mapped Files

Before configuring an entity with `MemoryMappedFileName`, ensure:

1. Class has `[MessagePackObject]` attribute
2. All properties have `[Key(n)]` attributes with sequential integers
3. `using MessagePack;` is at the top of the file

## Related Documentation

- [MEMORY_MAPPED_FILE_IMPLEMENTATION.md](MEMORY_MAPPED_FILE_IMPLEMENTATION.md) - Core implementation
- [MEMORY_MAPPED_FILE_QUICKSTART.md](MEMORY_MAPPED_FILE_QUICKSTART.md) - Getting started guide
- [MEMORY_MAPPED_FILE_CLEANUP.md](MEMORY_MAPPED_FILE_CLEANUP.md) - Cleanup strategies
- [MessagePack for C# Documentation](https://github.com/MessagePack-CSharp/MessagePack-CSharp) - Official docs

## Future Improvements

### Source Generator Enhancement (Future)

Could add automatic validation or generation of MessagePack attributes:

```csharp
// Potential future enhancement in DalSourceGenerator
if (entity.HasMemoryMappedFile && !entity.HasMessagePackAttributes)
{
    context.ReportDiagnostic(Diagnostic.Create(
        new DiagnosticDescriptor(
            "HSDAL001",
            "MessagePack attributes required",
            "Entity '{0}' uses memory-mapped files but is missing [MessagePackObject] and [Key] attributes",
            "HighSpeedDAL",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true),
        entity.Location,
        entity.Name));
}
```

### Documentation Update (Future)

Add MessagePack requirements to quickstart guides and examples.

---

**Status:** ? Complete  
**Build:** ? Passing  
**Impact:** Critical fix for memory-mapped file functionality  

**HighSpeedDAL Framework** - MessagePack Serialization Fix  
Version: 0.1 | Fixed: 2025-01-09
