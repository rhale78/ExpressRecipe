using System;
using HighSpeedDAL.Core.Attributes;

namespace HighSpeedDAL.SimpleCrudExample.Entities;

/// <summary>
/// User entity with soft delete support for logical deletion
/// </summary>
[Table("UsersWithSoftDelete")]
[Cache(CacheStrategy.Memory, MaxSize = 1000, ExpirationSeconds = 300)]
[SoftDelete]
[DalEntity]
public partial class UserWithSoftDelete
{
    // Business properties - Id and soft delete fields auto-generated
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;
}
