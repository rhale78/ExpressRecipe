using System;
using HighSpeedDAL.Core.Attributes;
using MessagePack;

namespace HighSpeedDAL.SimpleCrudExample.Entities;

/// <summary>
/// User entity with in-memory caching and staging table support
/// </summary>
[Table("Users")]
[Cache(CacheStrategy.Memory, MaxSize = 1000, ExpirationSeconds = 300)]
[StagingTable(SyncIntervalSeconds = 30)]
[DalEntity]
[MessagePackObject]
public partial class User
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
