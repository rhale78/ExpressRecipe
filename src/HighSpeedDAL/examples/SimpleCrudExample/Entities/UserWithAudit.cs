using System;
using HighSpeedDAL.Core.Attributes;

namespace HighSpeedDAL.SimpleCrudExample.Entities;

/// <summary>
/// User entity with auto-audit fields for tracking who created/modified records
/// </summary>
[Table("UsersWithAudit")]
[Cache(CacheStrategy.Memory, MaxSize = 1000, ExpirationSeconds = 300)]
[AutoAudit]
[DalEntity]
public partial class UserWithAudit
{
    // Business properties - Id and audit fields auto-generated
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;
}
