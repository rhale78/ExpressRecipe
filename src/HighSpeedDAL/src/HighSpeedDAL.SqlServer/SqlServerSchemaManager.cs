using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HighSpeedDAL.Core.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace HighSpeedDAL.SqlServer.Schema;

/// <summary>
/// Manages database schema operations for SQL Server
/// </summary>
public sealed class SqlServerSchemaManager : ISchemaManager
{
    private readonly string _connectionString;
    private readonly ILogger _logger;

    public SqlServerSchemaManager(string connectionString, ILogger logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> TableExistsAsync(string tableName, CancellationToken cancellationToken = default)
    {
        string sql = @"
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_NAME = @TableName AND TABLE_SCHEMA = 'dbo'";

        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);

            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@TableName", tableName);
                int count = (int)await command.ExecuteScalarAsync(cancellationToken);
                return count > 0;
            }
        }
    }

    public async Task<bool> ColumnExistsAsync(string tableName, string columnName, CancellationToken cancellationToken = default)
    {
        string sql = @"
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = @TableName AND COLUMN_NAME = @ColumnName AND TABLE_SCHEMA = 'dbo'";

        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);

            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@TableName", tableName);
                command.Parameters.AddWithValue("@ColumnName", columnName);
                int count = (int)await command.ExecuteScalarAsync(cancellationToken);
                return count > 0;
            }
        }
    }

    public async Task CreateTableAsync(string createSql, CancellationToken cancellationToken = default)
    {
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);

            using (SqlCommand command = new SqlCommand(createSql, connection))
            {
                _logger.LogInformation("Creating table with SQL: {Sql}", createSql);
                await command.ExecuteNonQueryAsync(cancellationToken);
                _logger.LogInformation("Table created successfully");
            }
        }
    }

    public async Task DropTableAsync(string tableName, CancellationToken cancellationToken = default)
    {
        string sql = $"DROP TABLE IF EXISTS [{tableName}]";

        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);

            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                _logger.LogWarning("Dropping table: {TableName}", tableName);
                await command.ExecuteNonQueryAsync(cancellationToken);
                _logger.LogInformation("Table {TableName} dropped successfully", tableName);
            }
        }
    }

    public async Task<TableSchema> GetTableSchemaAsync(string tableName, CancellationToken cancellationToken = default)
    {
        TableSchema schema = new TableSchema { TableName = tableName };

        // Get columns
        schema.Columns = await GetColumnsAsync(tableName, cancellationToken);

        // Get indexes
        schema.Indexes = await GetIndexesAsync(tableName, cancellationToken);

        return schema;
    }

    private async Task<List<ColumnSchema>> GetColumnsAsync(string tableName, CancellationToken cancellationToken)
    {
        List<ColumnSchema> columns = new List<ColumnSchema>();

        string sql = @"
            SELECT 
                c.COLUMN_NAME,
                c.DATA_TYPE,
                c.IS_NULLABLE,
                c.CHARACTER_MAXIMUM_LENGTH,
                c.NUMERIC_PRECISION,
                c.NUMERIC_SCALE,
                CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IS_PRIMARY_KEY,
                CASE WHEN COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME), c.COLUMN_NAME, 'IsIdentity') = 1 THEN 1 ELSE 0 END AS IS_IDENTITY
            FROM INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN (
                SELECT ku.TABLE_NAME, ku.COLUMN_NAME
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                    ON tc.CONSTRAINT_TYPE = 'PRIMARY KEY' 
                    AND tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                WHERE ku.TABLE_NAME = @TableName
            ) pk ON c.COLUMN_NAME = pk.COLUMN_NAME
            WHERE c.TABLE_NAME = @TableName AND c.TABLE_SCHEMA = 'dbo'
            ORDER BY c.ORDINAL_POSITION";

        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);

            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@TableName", tableName);

                using (SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
                {
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        ColumnSchema column = new ColumnSchema
                        {
                            ColumnName = reader.GetString(0),
                            DataType = reader.GetString(1),
                            IsNullable = reader.GetString(2) == "YES",
                            MaxLength = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                            Precision = reader.IsDBNull(4) ? null : reader.GetByte(4),
                            Scale = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                            IsPrimaryKey = reader.GetInt32(6) == 1,
                            IsIdentity = reader.GetInt32(7) == 1
                        };

                        columns.Add(column);
                    }
                }
            }
        }

        return columns;
    }

    private async Task<List<IndexSchema>> GetIndexesAsync(string tableName, CancellationToken cancellationToken)
    {
        List<IndexSchema> indexes = new List<IndexSchema>();

        string sql = @"
            SELECT 
                i.name AS INDEX_NAME,
                i.is_unique AS IS_UNIQUE,
                i.is_primary_key AS IS_PRIMARY_KEY,
                c.name AS COLUMN_NAME
            FROM sys.indexes i
            JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
            JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
            WHERE OBJECT_NAME(i.object_id) = @TableName
            ORDER BY i.name, ic.index_column_id";

        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);

            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@TableName", tableName);

                using (SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
                {
                    Dictionary<string, IndexSchema> indexDict = new Dictionary<string, IndexSchema>();

                    while (await reader.ReadAsync(cancellationToken))
                    {
                        string indexName = reader.GetString(0);

                        if (!indexDict.ContainsKey(indexName))
                        {
                            indexDict[indexName] = new IndexSchema
                            {
                                IndexName = indexName,
                                IsUnique = reader.GetBoolean(1),
                                IsPrimaryKey = reader.GetBoolean(2),
                                Columns = new List<string>()
                            };
                        }

                        indexDict[indexName].Columns.Add(reader.GetString(3));
                    }

                    indexes.AddRange(indexDict.Values);
                }
            }
        }

        return indexes;
    }

    public async Task EnsureSchemaAsync(Type entityType, CancellationToken cancellationToken = default)
    {
        // This will be implemented by source generators
        await Task.CompletedTask;
        throw new NotImplementedException("Schema creation is handled by source generators");
    }

    public async Task MigrateSchemaAsync(Type entityType, TableSchema currentSchema, CancellationToken cancellationToken = default)
    {
        // This will be implemented by source generators
        await Task.CompletedTask;
        throw new NotImplementedException("Schema migration is handled by source generators");
    }

    /// <summary>
    /// Adds a column to an existing table
    /// </summary>
    public async Task AddColumnAsync(
        string tableName,
        string columnName,
        string dataType,
        bool isNullable,
        CancellationToken cancellationToken = default)
    {
        StringBuilder sql = new StringBuilder();
        sql.Append($"ALTER TABLE [{tableName}] ADD [{columnName}] {dataType}");

        if (!isNullable)
        {
            sql.Append(" NOT NULL");
        }

        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);

            using (SqlCommand command = new SqlCommand(sql.ToString(), connection))
            {
                _logger.LogInformation("Adding column {ColumnName} to table {TableName}", columnName, tableName);
                await command.ExecuteNonQueryAsync(cancellationToken);
                _logger.LogInformation("Column added successfully");
            }
        }
    }

    /// <summary>
    /// Removes a column from an existing table
    /// </summary>
    public async Task DropColumnAsync(string tableName, string columnName, CancellationToken cancellationToken = default)
    {
        string sql = $"ALTER TABLE [{tableName}] DROP COLUMN [{columnName}]";

        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);

            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                _logger.LogWarning("Dropping column {ColumnName} from table {TableName}", columnName, tableName);
                await command.ExecuteNonQueryAsync(cancellationToken);
                _logger.LogInformation("Column dropped successfully");
            }
        }
    }
}
