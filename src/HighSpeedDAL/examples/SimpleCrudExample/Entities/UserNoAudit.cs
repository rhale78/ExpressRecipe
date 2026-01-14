using System;
using HighSpeedDAL.Core.Attributes;

namespace HighSpeedDAL.SimpleCrudExample.Entities;

/// <summary>
/// User entity without audit fields for performance comparison
/// </summary>
[Table("UsersNoAudit")]
[Cache(CacheStrategy.Memory, MaxSize = 1000, ExpirationSeconds = 300)]
[DalEntity]
public partial class UserNoAudit
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;
}
