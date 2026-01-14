using System;
using HighSpeedDAL.Core.Attributes;
using MessagePack;

namespace HighSpeedDAL.SimpleCrudExample.Entities;

/// <summary>
/// User entity with memory-mapped L0 cache for cross-process data sharing.
/// Demonstrates the mandatory memory-mapped file integration with database backing.
/// </summary>
[Table("UsersMemoryMapped")]
[MemoryMappedTable(
    FileName = "UsersMapped",
    SizeMB = 50,
    SyncMode = MemoryMappedSyncMode.Batched,
    FlushIntervalSeconds = 15,
    AutoCreateFile = true,
    AutoLoadOnStartup = true)]
[DalEntity]
[MessagePackObject]
public partial class UserWithMemoryMapped
{
    // Primary key - auto-increment identity
    [Key(0)]
    public int Id { get; set; }

    // Business properties
    [Key(1)]
    public string Username { get; set; } = string.Empty;

    [Key(2)]
    public string Email { get; set; } = string.Empty;

    [Key(3)]
    public string FirstName { get; set; } = string.Empty;

    [Key(4)]
    public string LastName { get; set; } = string.Empty;

    [Key(5)]
    public DateTime CreatedAt { get; set; }

    [Key(6)]
    public bool IsActive { get; set; } = true;
}
