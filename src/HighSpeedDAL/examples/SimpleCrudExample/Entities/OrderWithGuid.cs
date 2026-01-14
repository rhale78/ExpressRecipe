using System;
using HighSpeedDAL.Core.Attributes;

namespace HighSpeedDAL.SimpleCrudExample.Entities;

/// <summary>
/// Example entity with explicit Guid primary key (custom property).
/// No auto-generation - user provides the Guid Id property explicitly.
/// </summary>
[Table]
public partial class OrderWithGuid
{
    [PrimaryKey(AutoGenerate = false)]
    public Guid Id { get; set; }

    public string OrderNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime OrderDate { get; set; }
    public string CustomerName { get; set; } = string.Empty;
}
