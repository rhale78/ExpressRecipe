using System;
using HighSpeedDAL.Core.Attributes;

namespace HighSpeedDAL.SimpleCrudExample.Entities;

/// <summary>
/// Example entity demonstrating Guid primary key auto-generation.
/// Framework will auto-generate: public Guid Id { get; set; }
/// </summary>
[Table(PrimaryKeyType = PrimaryKeyType.Guid)]
public partial class UserWithGuidId
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}
