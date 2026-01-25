using System.Collections.Generic;

namespace HighSpeedDAL.SourceGenerators.Models
{
    /// <summary>
    /// Metadata about a DAL entity extracted from source code
    /// </summary>
    internal sealed class EntityMetadata
    {
        public string ClassName { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public string? Schema { get; set; }
        public string ConnectionClassName { get; set; } = string.Empty;
        public List<PropertyMetadata> Properties { get; set; } = [];
        public List<IndexMetadata> Indexes { get; set; } = [];
    
        // Attributes
        public bool AutoCreate { get; set; } = true;
        public bool AutoMigrate { get; set; } = true;
        public bool DropOnStartup { get; set; }
    
        // Caching
        public bool HasCache { get; set; }
        public string CacheStrategy { get; set; } = "LocalMemory";
        public int CacheMaxSize { get; set; }
        public int CacheExpirationSeconds { get; set; }
        public int CachePromotionIntervalSeconds { get; set; } = 5;
        public bool CachePreloadOnStartup { get; set; }
    
        // Features
        public bool IsReferenceTable { get; set; }
        public string? ReferenceCsvPath { get; set; }
        public bool ReferenceLoadFromCode { get; set; }
    
        public bool IsAuditable { get; set; }
        public bool HasRowVersion { get; set; }
        public bool RowVersionTrackHistory { get; set; }
        public bool HasSoftDelete { get; set; }

        // Audit custom column names (from AutoAuditAttribute)
        public string CreatedDateColumn { get; set; } = "CreatedDate";
        public string CreatedByColumn { get; set; } = "CreatedBy";
        public string ModifiedDateColumn { get; set; } = "ModifiedDate";
        public string ModifiedByColumn { get; set; } = "ModifiedBy";

        // Soft delete custom column names (from SoftDeleteAttribute)
        public string SoftDeleteColumn { get; set; } = "IsDeleted";
        public string SoftDeleteDateColumn { get; set; } = "DeletedDate";
        public string SoftDeleteByColumn { get; set; } = "DeletedBy";

        public bool HasStagingTable { get; set; }
        public int StagingMergeIntervalSeconds { get; set; }
        public int StagingMinBatchSize { get; set; }
    
        public bool HasAutoPurge { get; set; }
        public int AutoPurgeRetentionDays { get; set; }
        public string? AutoPurgeDatePropertyName { get; set; }

            // In-Memory Table
            public bool HasInMemoryTable { get; set; }
            public int InMemoryFlushIntervalSeconds { get; set; } = 30;
            public int InMemoryMaxRowCount { get; set; } = 100000;
            public bool InMemoryFlushToStaging { get; set; } = true;
            public bool InMemoryAutoGenerateId { get; set; } = true;
            public bool InMemoryEnforceConstraints { get; set; } = true;
            public bool InMemoryValidateOnWrite { get; set; } = true;
            public int InMemoryFlushBatchSize { get; set; } = 1000;
            public bool InMemoryRetainAfterFlush { get; set; }
            public int InMemoryFlushPriority { get; set; }
            public bool InMemoryTrackOperations { get; set; } = true;

            // Memory-Mapped Table (L0 Cache for DAL)
            public bool HasMemoryMappedTable { get; set; }
            public string? MemoryMappedFileName { get; set; }
            public int MemoryMappedSizeMB { get; set; } = 100;
            public int MemoryMappedSyncMode { get; set; } = 1; // Batched
            public int MemoryMappedFlushIntervalSeconds { get; set; } = 30;
            public bool MemoryMappedAutoCreateFile { get; set; } = true;
            public bool MemoryMappedAutoLoadOnStartup { get; set; } = true;
            public bool MemoryMappedReadOnlyCache { get; set; }
            public int MemoryMappedMaxCachedRows { get; set; }
            public int MemoryMappedTimeToLiveSeconds { get; set; }

                // Primary key
                public PropertyMetadata? PrimaryKeyProperty { get; set; }
                public bool HasCustomPrimaryKey { get; set; }

                /// <summary>
                /// Primary key data type when auto-generating Id property.
                /// Values: "Int" (default) or "Guid"
                /// </summary>
                public string PrimaryKeyType { get; set; } = "Int";

                // Named queries
                public List<NamedQueryMetadata> NamedQueries { get; set; } = [];
            }

    /// <summary>
    /// Metadata about a property/column
    /// </summary>
    internal sealed class PropertyMetadata
    {
        public string PropertyName { get; set; } = string.Empty;
        public string PropertyType { get; set; } = string.Empty;
        public string ColumnName { get; set; } = string.Empty;
        public bool IsNullable { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsAutoIncrement { get; set; } = true;
        public bool IsNotMapped { get; set; }
    
        // Custom SQL type
        public string? CustomSqlType { get; set; }
    
        // String constraints
        public int? MaxLength { get; set; }
    
        // Index
        public bool IsIndexed { get; set; }
        public bool IsUniqueIndex { get; set; }
        public string? IndexName { get; set; }
    
        // Navigation properties
        public bool IsOneToOne { get; set; }
        public bool IsOneToMany { get; set; }
        public bool IsManyToMany { get; set; }
        public string? ForeignKeyProperty { get; set; }
        public string? JunctionTableName { get; set; }
        public string? RelatedEntityType { get; set; }
    }

    /// <summary>
    /// Metadata about an index
    /// </summary>
    internal sealed class IndexMetadata
    {
        public string IndexName { get; set; } = string.Empty;
        public List<string> ColumnNames { get; set; } = [];
        public bool IsUnique { get; set; }
    }

    /// <summary>
    /// Metadata about a database connection class
    /// </summary>
    internal sealed class ConnectionMetadata
    {
        public string ClassName { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string DatabaseProvider { get; set; } = "SqlServer";
        public List<EntityMetadata> Entities { get; set; } = [];
    }

    /// <summary>
    /// Metadata about a named query defined via [NamedQuery] attribute
    /// </summary>
    internal sealed class NamedQueryMetadata
    {
        /// <summary>
        /// The name of the query (used in method name: Get{Name}Async)
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The property names to include in the WHERE clause
        /// </summary>
        public List<string> PropertyNames { get; set; } = [];

        /// <summary>
        /// If true, the query returns a single result (FirstOrDefault)
        /// </summary>
        public bool IsSingle { get; set; }

        /// <summary>
        /// If true, automatically adds IsDeleted = 0 filter for [SoftDelete] entities
        /// </summary>
        public bool AutoFilterDeleted { get; set; } = true;

        /// <summary>
        /// If true, results are cached using the entity's cache strategy
        /// </summary>
        public bool EnableCache { get; set; } = true;
    }
}
