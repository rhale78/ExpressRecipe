using HighSpeedDAL.Core.Attributes;
using HighSpeedDAL.DataManagement;

namespace HighSpeedDAL.FrameworkUsage.Tests.Entities;

/// <summary>
/// Example entity demonstrating staging table for high-write scenarios.
/// Staging table allows non-blocking writes with background synchronization.
/// </summary>
[Table("Customers")]
[Cache(CacheStrategy.Memory, ExpirationSeconds = 900)]
[StagingTable(SyncIntervalSeconds = 30)]
[AutoAudit]
public partial class Customer
{
    // Id and audit properties auto-generated
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
