using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeGenerator.Core.SourceDataStrategies
{
    public class DatabaseDataSourceStrategy : DataSourceStrategyBase
    {
        public List<string> TableNamesToIgnore { get; set; }

        public DatabaseDataSourceStrategy()
        {
            TableNamesToIgnore = new List<string>();
        }
        
        public override List<TableDefinition> GetAllTables()
        {
            List<TableDefinition> returnValues = new List<TableDefinition>();

            List<string> tableNames = GetTableNames();
            foreach (string tableName in tableNames)
            {
                TableDefinition tableDef = new TableDefinition();
                tableDef.TableName = tableName;
                tableDef.Indexes = GetIndexes(tableName);

                List<string> uniqueKeys = GetUniqueKeys(tableDef);

                tableDef.Columns = GetColumnsForTable(tableName);
                GetReferencedTablesAndIndexes(tableName, tableDef, uniqueKeys);

                returnValues.Add(tableDef);
            }
            return returnValues;
        }

        public void GetReferencedTablesAndIndexes(string tableName, TableDefinition tableDef, List<string> uniqueKeys)
        {
            if (tableDef != null)
            {
                if (tableDef.Columns != null)
                {
                    foreach (ColumnDefinition colDef in tableDef.Columns)
                    {
                        if (colDef != null)
                        {
                            List<ReferencedTable> references = GetReferencedTablesForColumn(tableName, colDef.ColumnName);
                            if (references != null)
                            {
                                if (references.Count() > 0)
                                {
                                    colDef.ReferencedTables = references;
                                }
                            }
                            GetIsColumnIndex(uniqueKeys, colDef);
                        }
                    }
                }
            }
        }

        public void GetIsColumnIndex(List<string> uniqueKeys, ColumnDefinition colDef)
        {
            if (uniqueKeys != null)
            {
                foreach (string uniqueKey in uniqueKeys)
                {
                    if (colDef != null)
                    {
                        if (string.Equals(colDef.ColumnName, uniqueKey, StringComparison.OrdinalIgnoreCase))
                        {
                            colDef.IsIndex = true;
                        }
                    }
                }
            }
        }

        public List<string> GetUniqueKeys(TableDefinition tableDef)
        {
            List<string> uniqueKeys = new List<string>();
            if (tableDef != null)
            {
                if (tableDef.Indexes != null)
                {
                    if (tableDef.Indexes.Count > 0)
                    {
                        foreach (string key in tableDef.Indexes.Keys)
                        {
                            if (!string.IsNullOrEmpty(key))
                            {
                                if (tableDef.Indexes.ContainsKey(key))
                                {
                                    List<string> list = tableDef.Indexes[key];
                                    if (list != null)
                                    {
                                        foreach (string index in list)
                                        {
                                            if (!string.IsNullOrEmpty(index))
                                            {
                                                if (!uniqueKeys.Contains(index))
                                                {
                                                    uniqueKeys.Add(index);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return uniqueKeys;
        }

        public List<string> GetTableNames()
        {
            List<string> returnValue = new List<string>();

            using (SqlConnection connection = new SqlConnection(Settings))
            {
                connection.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT Table_Name FROM INFORMATION_SCHEMA.TABLES", connection))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.HasRows)
                        {
                            if (reader.Read())
                            {
                                string tableName = reader.GetString(0);
                                if (!TableNamesToIgnore.Contains(tableName))
                                {
                                    returnValue.Add(tableName);
                                }
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }
            }
            return returnValue;
        }

        public List<ColumnDefinition> GetColumnsForTable(string table)
        {
            List<ColumnDefinition> returnValues = new List<ColumnDefinition>();

            using (SqlConnection connection = new SqlConnection(Settings))
            {
                connection.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT Column_Name,Ordinal_Position,Is_Nullable,Data_Type,CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.Columns WHERE TABLE_NAME='" + table + "'", connection))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.HasRows)
                        {
                            if (reader.Read())
                            {
                                ColumnDefinition column = new ColumnDefinition();
                                column.ColumnName = reader.GetString(0);
                                column.ColumnIndex = reader.GetInt32(1);
                                string isNullable = reader.GetString(2);
                                column.IsNullable = string.Equals(isNullable, "YES", StringComparison.OrdinalIgnoreCase);
                                column.ColumnType = reader.GetString(3);
                                if (!reader.IsDBNull(4))
                                {
                                    column.ColumnSize = reader.GetInt32(4);
                                }
                                column.ReferencedTables = GetReferencedTablesForColumn(table, column.ColumnName);
                                returnValues.Add(column);
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }
            }
            return returnValues;
        }
        public List<ReferencedTable> GetReferencedTablesForColumn(string tableName, string columnName)
        {
            List<ReferencedTable> returnValues = new List<ReferencedTable>();

            using (SqlConnection connection = new SqlConnection(Settings))
            {
                connection.Open();
                using (SqlCommand cmd = new SqlCommand("SELECT tab1.name AS [table], col1.name AS [column], tab2.name AS [referenced_table], col2.name AS [referenced_column] FROM sys.foreign_key_columns fkc INNER JOIN sys.objects obj ON obj.object_id = fkc.constraint_object_id INNER JOIN sys.tables tab1 ON tab1.object_id = fkc.parent_object_id INNER JOIN sys.schemas sch ON tab1.schema_id = sch.schema_id INNER JOIN sys.columns col1 ON col1.column_id = parent_column_id AND col1.object_id = tab1.object_id INNER JOIN sys.tables tab2 ON tab2.object_id = fkc.referenced_object_id INNER JOIN sys.columns col2 ON col2.column_id = referenced_column_id AND col2.object_id = tab2.object_id where tab1.name='" + tableName + "' and col1.name='" + columnName + "'", connection))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.HasRows)
                        {
                            if (reader.Read())
                            {
                                ReferencedTable refTable = new ReferencedTable();
                                refTable.TableName = reader.GetString(2);
                                refTable.ColumnName = reader.GetString(3);
                                returnValues.Add(refTable);
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }
            }
            return returnValues;
        }

        public Dictionary<string, List<string>> GetIndexes(string tableName)
        {
            Dictionary<string, List<string>> returnValues = new Dictionary<string, List<string>>();

            using (SqlConnection connection = new SqlConnection(Settings))
            {
                connection.Open();
                using (SqlCommand cmd = new SqlCommand("select i.name as IndexName, o.name as TableName, ic.key_ordinal as ColumnOrder, ic.is_included_column as IsIncluded, co.[name] as ColumnName from sys.indexes i join sys.objects o on i.object_id = o.object_id join sys.index_columns ic on ic.object_id = i.object_id     and ic.index_id = i.index_id join sys.columns co on co.object_id = i.object_id    and co.column_id = ic.column_id where i.[type] = 2 and i.is_unique = 0 and i.is_primary_key = 0 and o.[type] = 'U' and ic.is_included_column = 0 AND o.name='" + tableName + "' order by o.[name], i.[name], ic.is_included_column, ic.key_ordinal", connection))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.HasRows)
                        {
                            if (reader.Read())
                            {
                                string indexName = reader.GetString(0);
                                if (!returnValues.ContainsKey(indexName))
                                {
                                    returnValues.Add(indexName, new List<string>());
                                }
                                returnValues[indexName].Add(reader.GetString(4));
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }
            }
            return returnValues;
        }
    }
}
