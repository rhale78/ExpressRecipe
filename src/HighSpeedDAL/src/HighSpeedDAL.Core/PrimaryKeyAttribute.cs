using System;

namespace HighSpeedDAL.Core.Attributes
{
    /// <summary>
    /// Explicitly marks a property as the primary key.
    /// 
    /// DEFAULT BEHAVIOR (no attribute needed):
    /// - Framework looks for "int Id" property → auto-increment primary key
    /// - If no "Id" property found → framework auto-generates "public int Id { get; set; }"
    /// 
    /// USE THIS ATTRIBUTE when:
    /// - Primary key is NOT named "Id" (e.g., ProductCode, CustomerId)
    /// - Primary key is NOT auto-increment (e.g., Guid, string)
    /// - Composite primary keys (apply to multiple properties)
    /// 
    /// HighSpeedDAL Framework v0.1
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class PrimaryKeyAttribute : Attribute
    {
        /// <summary>
        /// If true, database auto-generates values (IDENTITY for SQL Server, AUTOINCREMENT for SQLite).
        /// Default: true for int/long types, false for others
        /// </summary>
        public bool AutoGenerate { get; set; }

        /// <summary>
        /// For composite keys, specifies the order of columns in the key.
        /// Default: 0 (single-column key)
        /// </summary>
        public int Order { get; set; }

        public PrimaryKeyAttribute()
        {
            AutoGenerate = true; // Default, but overridden based on property type
            Order = 0;
        }
    }
}
