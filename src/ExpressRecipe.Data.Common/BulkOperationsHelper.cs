using Microsoft.Data.SqlClient;
using System.Data;

namespace ExpressRecipe.Data.Common
{
    /// <summary>
    /// Helper class for efficient bulk database operations with minimal allocations
    /// </summary>
    public static class BulkOperationsHelper
    {
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
            if (!items.Any())
            {
                return 0;
            }

            using SqlConnection connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            using SqlTransaction transaction = connection.BeginTransaction();
            try
            {
                // Create temp table
                var createTempTableSql = GenerateCreateTempTableSql(tempTableName, dataTableStructure);
                using (SqlCommand cmd = new SqlCommand(createTempTableSql, connection, transaction))
                {
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }

                // Bulk insert into temp table
                DataTable dataTable = CreateDataTable(items, dataTableStructure, mapToDataRow);
                using (SqlBulkCopy bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction))
                {
                    bulkCopy.DestinationTableName = tempTableName;
                    bulkCopy.BatchSize = 1000;
                    bulkCopy.BulkCopyTimeout = 300;

                    foreach (DataColumn column in dataTable.Columns)
                    {
                        bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                    }

                    await bulkCopy.WriteToServerAsync(dataTable, cancellationToken);
                }

                // Perform MERGE operation
                var mergeSql = GenerateMergeSql(targetTable, tempTableName, keyColumns, dataTableStructure);
                int affectedRows;
                using (SqlCommand cmd = new SqlCommand(mergeSql, connection, transaction))
                {
                    cmd.CommandTimeout = 300;
                    affectedRows = await cmd.ExecuteNonQueryAsync(cancellationToken);
                }

                // Clean up temp table
                using (SqlCommand cmd = new SqlCommand($"DROP TABLE {tempTableName}", connection, transaction))
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
            List<T> itemsList = items.ToList();
            if (itemsList.Count == 0)
            {
                return 0;
            }

            // Deduplicate in memory first
            List<T> deduped = DeduplicateByKeys(itemsList, keyColumns, mapToDataRow, dataTableStructure);

            using SqlConnection connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            using SqlTransaction transaction = connection.BeginTransaction();
            try
            {
                DataTable dataTable = CreateDataTable(deduped, dataTableStructure, mapToDataRow);

                using SqlBulkCopy bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction);
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
            DataTable dataTable = structure.Clone();
            foreach (T? item in items)
            {
                DataRow row = dataTable.NewRow();
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
            HashSet<string> seen = [];
            List<T> result = new List<T>(items.Count);

            foreach (T? item in items)
            {
                DataRow row = structure.NewRow();
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
            List<string> columns = [];
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
            List<string> updateColumns = structure.Columns.Cast<DataColumn>()
                .Where(c => !keyColumns.Contains(c.ColumnName))
                .Select(c => $"target.[{c.ColumnName}] = source.[{c.ColumnName}]")
                .ToList();

            var insertColumns = string.Join(", ", structure.Columns.Cast<DataColumn>().Select(c => $"[{c.ColumnName}]"));
            var insertValues = string.Join(", ", structure.Columns.Cast<DataColumn>().Select(c => $"source.[{c.ColumnName}]"));

            var updateClause = updateColumns.Count != 0
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
}
