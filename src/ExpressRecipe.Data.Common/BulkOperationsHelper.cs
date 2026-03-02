using Microsoft.Data.SqlClient;
using System.Data;

namespace ExpressRecipe.Data.Common;

/// <summary>
/// Helper class for efficient bulk database operations with minimal allocations
/// </summary>
public static class BulkOperationsHelper
{
    /// <summary>
    /// Generates a sequential GUID (COMB) to minimize index fragmentation.
    /// This is critical for sustained high-speed bulk imports into tables with GUID clustered indexes.
    /// </summary>
    public static Guid CreateSequentialGuid()
    {
        var guidBytes = Guid.NewGuid().ToByteArray();
        var timestamp = DateTime.UtcNow.Ticks / 10000L;
        var timestampBytes = BitConverter.GetBytes(timestamp);

        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(timestampBytes);
        }

        var sequentialGuid = new byte[16];
        Buffer.BlockCopy(guidBytes, 0, sequentialGuid, 0, 10);
        Buffer.BlockCopy(timestampBytes, 2, sequentialGuid, 10, 6);

        return new Guid(sequentialGuid);
    }

    /// <summary>
    /// Performs a bulk insert or update (upsert) operation using SqlBulkCopy and MERGE
    /// </summary>
    public static async Task<int> BulkUpsertAsync<T>(
        string connectionString,
        IEnumerable<T> items,
        string targetTable,
        string tempTableName,
        string[] keyColumns,
        Func<T, DataRow, DataRow> mapToDataRow,
        DataTable dataTableStructure,
        CancellationToken cancellationToken = default)
    {
        if (!items.Any()) return 0;

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        using var transaction = connection.BeginTransaction();
        try
        {
            // Create temp table
            var createTempTableSql = GenerateCreateTempTableSql(tempTableName, dataTableStructure);
            using (var cmd = new SqlCommand(createTempTableSql, connection, transaction))
            {
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // Bulk insert into temp table
            var dataTable = CreateDataTable(items, dataTableStructure, mapToDataRow);
            using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default | SqlBulkCopyOptions.TableLock | SqlBulkCopyOptions.CheckConstraints, transaction))
            {
                bulkCopy.DestinationTableName = tempTableName;
                bulkCopy.BatchSize = 5000;
                bulkCopy.BulkCopyTimeout = 600;

                foreach (DataColumn column in dataTable.Columns)
                {
                    bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                }

                await bulkCopy.WriteToServerAsync(dataTable, cancellationToken);
            }

            // Perform MERGE operation
            var mergeSql = GenerateMergeSql(targetTable, tempTableName, keyColumns, dataTableStructure);
            int affectedRows;
            using (var cmd = new SqlCommand(mergeSql, connection, transaction))
            {
                cmd.CommandTimeout = 600;
                affectedRows = await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // Clean up temp table
            using (var cmd = new SqlCommand($"DROP TABLE {tempTableName}", connection, transaction))
            {
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return affectedRows;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Performs a bulk upsert and returns the mapping of source IDs to database IDs and the action taken (INSERT/UPDATE).
    /// Version that accepts an existing connection and transaction.
    /// </summary>
    public static async Task<Dictionary<TKey, (Guid Id, string Action)>> BulkUpsertWithOutputAsync<T, TKey>(
        SqlConnection connection,
        SqlTransaction transaction,
        IEnumerable<T> items,
        string targetTable,
        string tempTableName,
        string[] keyColumns,
        Func<T, TKey> getKey,
        Func<T, DataRow, DataRow> mapToDataRow,
        DataTable dataTableStructure,
        CancellationToken cancellationToken = default) where TKey : notnull
    {
        if (!items.Any()) return new Dictionary<TKey, (Guid Id, string Action)>();

        // Add RowIndex to track source items
        if (!dataTableStructure.Columns.Contains("_RowIndex"))
        {
            dataTableStructure.Columns.Add("_RowIndex", typeof(int));
        }

        // Create temp table
        var createTempTableSql = GenerateCreateTempTableSql(tempTableName, dataTableStructure);
        using (var cmd = new SqlCommand(createTempTableSql, connection, transaction))
        {
            cmd.CommandTimeout = 600;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // Bulk insert into temp table
        var itemsList = items.ToList();
        var dataTable = dataTableStructure.Clone();
        for (int i = 0; i < itemsList.Count; i++)
        {
            var row = dataTable.NewRow();
            mapToDataRow(itemsList[i], row);
            row["_RowIndex"] = i;
            dataTable.Rows.Add(row);
        }

        using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default | SqlBulkCopyOptions.TableLock | SqlBulkCopyOptions.CheckConstraints, transaction))
        {
            bulkCopy.DestinationTableName = tempTableName;
            bulkCopy.BulkCopyTimeout = 600;
            foreach (DataColumn column in dataTable.Columns)
            {
                bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
            }
            await bulkCopy.WriteToServerAsync(dataTable, cancellationToken);
        }

        // Prepare results table with case-insensitive comparer for string keys
        var resultMapping = typeof(TKey) == typeof(string)
            ? new Dictionary<TKey, (Guid Id, string Action)>((IEqualityComparer<TKey>)StringComparer.OrdinalIgnoreCase)
            : new Dictionary<TKey, (Guid Id, string Action)>();

        // Perform MERGE with OUTPUT
        var keyJoin = string.Join(" AND ", keyColumns.Select(k => $"target.[{k}] = source.[{k}]"));
        var updateColumns = dataTableStructure.Columns.Cast<DataColumn>()
            .Where(c => !keyColumns.Contains(c.ColumnName) && c.ColumnName != "_RowIndex")
            .Select(c => $"target.[{c.ColumnName}] = source.[{c.ColumnName}]")
            .ToList();

        var insertColumns = string.Join(", ", dataTableStructure.Columns.Cast<DataColumn>().Where(c => c.ColumnName != "_RowIndex").Select(c => $"[{c.ColumnName}]"));
        var insertValues = string.Join(", ", dataTableStructure.Columns.Cast<DataColumn>().Where(c => c.ColumnName != "_RowIndex").Select(c => $"source.[{c.ColumnName}]"));

        var updateClause = updateColumns.Any() ? $"WHEN MATCHED THEN UPDATE SET {string.Join(", ", updateColumns)}" : "";

        var mergeSql = $@"
MERGE {targetTable} AS target
USING {tempTableName} AS source
ON {keyJoin}
{updateClause}
WHEN NOT MATCHED THEN
    INSERT ({insertColumns})
    VALUES ({insertValues})
OUTPUT source._RowIndex, inserted.Id, $action;";

        using (var cmd = new SqlCommand(mergeSql, connection, transaction))
        {
            cmd.CommandTimeout = 600;
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var rowIndex = reader.GetInt32(0);
                var id = reader.GetGuid(1);
                var action = reader.GetString(2);
                resultMapping[getKey(itemsList[rowIndex])] = (id, action);
            }
        }

        // Clean up
        using (var cmd = new SqlCommand($"DROP TABLE {tempTableName}", connection, transaction))
        {
            cmd.CommandTimeout = 600;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        return resultMapping;
    }

    /// <summary>
    /// Performs a bulk upsert and returns the mapping of source IDs to database IDs and the action taken (INSERT/UPDATE)
    /// </summary>
    public static async Task<Dictionary<TKey, (Guid Id, string Action)>> BulkUpsertWithOutputAsync<T, TKey>(
        string connectionString,
        IEnumerable<T> items,
        string targetTable,
        string tempTableName,
        string[] keyColumns,
        Func<T, TKey> getKey,
        Func<T, DataRow, DataRow> mapToDataRow,
        DataTable dataTableStructure,
        CancellationToken cancellationToken = default) where TKey : notnull
    {
        if (!items.Any()) return new Dictionary<TKey, (Guid Id, string Action)>();

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        using var transaction = connection.BeginTransaction();
        try
        {
            var result = await BulkUpsertWithOutputAsync(connection, transaction, items, targetTable, tempTableName, keyColumns, getKey, mapToDataRow, dataTableStructure, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Bulk insert with deduplication based on key columns
    /// </summary>
    public static async Task<int> BulkInsertWithDeduplicationAsync<T>(
        string connectionString,
        IEnumerable<T> items,
        string targetTable,
        string[] keyColumns,
        Func<T, DataRow, DataRow> mapToDataRow,
        DataTable dataTableStructure,
        CancellationToken cancellationToken = default)
    {
        var itemsList = items.ToList();
        if (!itemsList.Any()) return 0;

        // Deduplicate in memory first
        var deduped = DeduplicateByKeys(itemsList, keyColumns, mapToDataRow, dataTableStructure);

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        using var transaction = connection.BeginTransaction();
        try
        {
            var dataTable = CreateDataTable(deduped, dataTableStructure, mapToDataRow);

            using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default | SqlBulkCopyOptions.TableLock | SqlBulkCopyOptions.CheckConstraints, transaction);
            bulkCopy.DestinationTableName = targetTable;
            bulkCopy.BatchSize = 1000;
            bulkCopy.BulkCopyTimeout = 300;

            foreach (DataColumn column in dataTable.Columns)
            {
                bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
            }

            await bulkCopy.WriteToServerAsync(dataTable, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return dataTable.Rows.Count;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static DataTable CreateDataTable<T>(
        IEnumerable<T> items,
        DataTable structure,
        Func<T, DataRow, DataRow> mapToDataRow)
    {
        var dataTable = structure.Clone();
        foreach (var item in items)
        {
            var row = dataTable.NewRow();
            mapToDataRow(item, row);
            dataTable.Rows.Add(row);
        }
        return dataTable;
    }

    private static List<T> DeduplicateByKeys<T>(
        List<T> items,
        string[] keyColumns,
        Func<T, DataRow, DataRow> mapToDataRow,
        DataTable structure)
    {
        var seen = new HashSet<string>();
        var result = new List<T>(items.Count);

        foreach (var item in items)
        {
            var row = structure.NewRow();
            mapToDataRow(item, row);

            var key = string.Join("|", keyColumns.Select(col => row[col]?.ToString() ?? ""));
            if (seen.Add(key))
            {
                result.Add(item);
            }
        }

        return result;
    }

    private static string GenerateCreateTempTableSql(string tempTableName, DataTable structure)
    {
        var columns = new List<string>();
        foreach (DataColumn column in structure.Columns)
        {
            var sqlType = GetSqlType(column.DataType, column.MaxLength);
            var nullable = column.AllowDBNull ? "NULL" : "NOT NULL";
            columns.Add($"[{column.ColumnName}] {sqlType} {nullable}");
        }

        return $"CREATE TABLE {tempTableName} ({string.Join(", ", columns)})";
    }

    private static string GenerateMergeSql(string targetTable, string tempTable, string[] keyColumns, DataTable structure)
    {
        var keyJoin = string.Join(" AND ", keyColumns.Select(k => $"target.[{k}] = source.[{k}]"));
        var updateColumns = structure.Columns.Cast<DataColumn>()
            .Where(c => !keyColumns.Contains(c.ColumnName))
            .Select(c => $"target.[{c.ColumnName}] = source.[{c.ColumnName}]")
            .ToList();

        var insertColumns = string.Join(", ", structure.Columns.Cast<DataColumn>().Select(c => $"[{c.ColumnName}]"));
        var insertValues = string.Join(", ", structure.Columns.Cast<DataColumn>().Select(c => $"source.[{c.ColumnName}]"));

        var updateClause = updateColumns.Any()
            ? $"WHEN MATCHED THEN UPDATE SET {string.Join(", ", updateColumns)}"
            : "";

        return $@"
MERGE {targetTable} AS target
USING {tempTable} AS source
ON {keyJoin}
{updateClause}
WHEN NOT MATCHED THEN
    INSERT ({insertColumns})
    VALUES ({insertValues});
";
    }

    private static string GetSqlType(Type dataType, int maxLength)
    {
        return dataType.Name switch
        {
            nameof(String) => maxLength == -1 ? "NVARCHAR(MAX)" : $"NVARCHAR({maxLength})",
            nameof(Int32) => "INT",
            nameof(Int64) => "BIGINT",
            nameof(Boolean) => "BIT",
            nameof(DateTime) => "DATETIME2",
            nameof(Decimal) => "DECIMAL(18,2)",
            nameof(Guid) => "UNIQUEIDENTIFIER",
            _ => "NVARCHAR(MAX)"
        };
    }
}
