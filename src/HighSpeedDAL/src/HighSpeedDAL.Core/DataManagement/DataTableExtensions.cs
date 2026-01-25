using System;
using System.Data;
using HighSpeedDAL.Core.Interfaces;

namespace HighSpeedDAL.Core.DataManagement
{
    /// <summary>
    /// Extensions for DataTable to support standardized Audit and Soft Delete columns.
    /// </summary>
    public static class DataTableExtensions
    {
        /// <summary>
        /// Adds standard Audit columns to the DataTable (CreatedDate, CreatedBy, ModifiedDate, ModifiedBy).
        /// </summary>
        public static void AddAuditColumns(this DataTable table)
        {
            if (!table.Columns.Contains("CreatedDate")) table.Columns.Add("CreatedDate", typeof(DateTime));
            if (!table.Columns.Contains("CreatedBy")) table.Columns.Add("CreatedBy", typeof(string));
            if (!table.Columns.Contains("ModifiedDate")) table.Columns.Add("ModifiedDate", typeof(DateTime));
            if (!table.Columns.Contains("ModifiedBy")) table.Columns.Add("ModifiedBy", typeof(string));
        }

        /// <summary>
        /// Adds standard Soft Delete columns to the DataTable (IsDeleted, DeletedDate, DeletedBy).
        /// </summary>
        public static void AddSoftDeleteColumns(this DataTable table)
        {
            if (!table.Columns.Contains("IsDeleted")) table.Columns.Add("IsDeleted", typeof(bool));
            if (!table.Columns.Contains("DeletedDate")) table.Columns.Add("DeletedDate", typeof(DateTime));
            if (!table.Columns.Contains("DeletedBy")) table.Columns.Add("DeletedBy", typeof(string));
        }

        /// <summary>
        /// Maps standard Audit properties from an entity to a DataRow.
        /// </summary>
        public static void MapAuditColumns(this DataRow row, IAuditableEntity entity)
        {
            row["CreatedDate"] = entity.CreatedDate != default ? entity.CreatedDate : DateTime.UtcNow;
            row["CreatedBy"] = (object?)entity.CreatedBy ?? DBNull.Value;
            row["ModifiedDate"] = (object?)entity.ModifiedDate ?? DBNull.Value;
            row["ModifiedBy"] = (object?)entity.ModifiedBy ?? DBNull.Value;
        }

        /// <summary>
        /// Maps standard Soft Delete properties from an entity to a DataRow.
        /// </summary>
        public static void MapSoftDeleteColumns(this DataRow row, ISoftDeleteEntity entity)
        {
            row["IsDeleted"] = entity.IsDeleted;
            row["DeletedDate"] = (object?)entity.DeletedDate ?? DBNull.Value;
            row["DeletedBy"] = (object?)entity.DeletedBy ?? DBNull.Value;
        }
    }
}