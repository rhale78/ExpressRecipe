using System;

namespace HighSpeedDAL.Core.Interfaces
{
    /// <summary>
    /// Standard interface for entities with audit tracking
    /// </summary>
    public interface IAuditableEntity
    {
        DateTime CreatedDate { get; set; }
        string? CreatedBy { get; set; }
        DateTime? ModifiedDate { get; set; }
        string? ModifiedBy { get; set; }
    }
}
