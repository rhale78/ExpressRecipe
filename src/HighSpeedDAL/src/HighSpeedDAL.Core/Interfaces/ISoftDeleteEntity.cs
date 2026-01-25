using System;

namespace HighSpeedDAL.Core.Interfaces
{
    /// <summary>
    /// Standard interface for entities with soft delete support
    /// </summary>
    public interface ISoftDeleteEntity
    {
        bool IsDeleted { get; set; }
        DateTime? DeletedDate { get; set; }
        string? DeletedBy { get; set; }
    }
}
