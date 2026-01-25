using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using HighSpeedDAL.Core.Base;

namespace HighSpeedDAL.Core.Interfaces
{
    /// <summary>
    /// Interface for entities that support defensive cloning for cache isolation
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    public interface IEntityCloneable<out T>
    {
        /// <summary>
        /// Creates a shallow clone of this entity (direct property copy)
        /// </summary>
        T ShallowClone();

        /// <summary>
        /// Creates a deep clone of this entity (recursive copy including collections)
        /// </summary>
        T DeepClone();
    }

    /// <summary>
    /// Factory for creating database connections
    /// </summary>
    public interface IDbConnectionFactory
    {
        /// <summary>
        /// Creates and opens a database connection
        /// </summary>
        Task<IDbConnection> CreateConnectionAsync(
            string connectionString,
            DatabaseProvider provider,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Interface for DAL entities with CRUD operations
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <typeparam name="TKey">The primary key type</typeparam>
    public interface IDalRepository<TEntity, TKey> 
        where TEntity : class, new()
    {
        // Read operations
        Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);
        Task<List<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<List<TEntity>> GetByIdsAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default);

        // Write operations
        Task<TKey> InsertAsync(TEntity entity, CancellationToken cancellationToken = default);
        Task<int> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);
        Task<int> DeleteAsync(TKey id, CancellationToken cancellationToken = default);
        Task<int> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default);

        // Bulk operations
        Task<int> BulkInsertAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);
        Task<int> BulkUpdateAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);
        Task<int> BulkDeleteAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default);
        Task<int> BulkMergeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

        // Utility operations
        Task<int> CountAsync(CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(TKey id, CancellationToken cancellationToken = default);
        Task<int> DeleteAllAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Interface for reference table operations (includes GetByName)
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <typeparam name="TKey">The primary key type</typeparam>
    public interface IReferenceTableRepository<TEntity, TKey> : IDalRepository<TEntity, TKey>
        where TEntity : class, new()
    {
        Task<TEntity?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
        Task PreloadDataAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Interface for cache operations
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <typeparam name="TKey">The primary key type</typeparam>
    public interface ICacheManager<TEntity, TKey>
        where TEntity : class
    {
        /// <summary>
        /// Gets or sets whether the cache is enabled.
        /// If false, GetAsync returns null and SetAsync does nothing.
        /// </summary>
        bool IsEnabled { get; set; }

        Task<TEntity?> GetAsync(TKey key, CancellationToken cancellationToken = default);
        Task SetAsync(TKey key, TEntity entity, CancellationToken cancellationToken = default);
        Task RemoveAsync(TKey key, CancellationToken cancellationToken = default);
        Task ClearAsync(CancellationToken cancellationToken = default);
        Task<bool> ContainsAsync(TKey key, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Interface for schema management
    /// </summary>
    public interface ISchemaManager
    {
        Task EnsureSchemaAsync(Type entityType, CancellationToken cancellationToken = default);
        Task<bool> TableExistsAsync(string tableName, CancellationToken cancellationToken = default);
        Task<bool> ColumnExistsAsync(string tableName, string columnName, CancellationToken cancellationToken = default);
        Task CreateTableAsync(string createSql, CancellationToken cancellationToken = default);
        Task DropTableAsync(string tableName, CancellationToken cancellationToken = default);
        Task<TableSchema> GetTableSchemaAsync(string tableName, CancellationToken cancellationToken = default);
        Task MigrateSchemaAsync(Type entityType, TableSchema currentSchema, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents a database table schema
    /// </summary>
    public sealed class TableSchema
    {
        public string TableName { get; set; } = string.Empty;
        public List<ColumnSchema> Columns { get; set; } = [];
        public List<IndexSchema> Indexes { get; set; } = [];
    }

    /// <summary>
    /// Represents a database column schema
    /// </summary>
    public sealed class ColumnSchema
    {
        public string ColumnName { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public bool IsNullable { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsIdentity { get; set; }
        public int? MaxLength { get; set; }
        public int? Precision { get; set; }
        public int? Scale { get; set; }
    }

    /// <summary>
    /// Represents a database index schema
    /// </summary>
    public sealed class IndexSchema
    {
        public string IndexName { get; set; } = string.Empty;
        public List<string> Columns { get; set; } = [];
        public bool IsUnique { get; set; }
        public bool IsPrimaryKey { get; set; }
    }

    /// <summary>
    /// Interface for unit of work pattern
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        IDbTransaction Transaction { get; }
        Task CommitAsync(CancellationToken cancellationToken = default);
        Task RollbackAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Interface for staging table operations
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    public interface IStagingTableManager<TEntity> where TEntity : class
    {
        Task MergeToMainTableAsync(CancellationToken cancellationToken = default);
        Task<int> GetStagingCountAsync(CancellationToken cancellationToken = default);
        Task ClearStagingAsync(CancellationToken cancellationToken = default);
    }
}
