using System.Collections.Generic;
using System.Linq;
using System.Text;
using HighSpeedDAL.SourceGenerators.Models;

namespace HighSpeedDAL.SourceGenerators.Generation;

/// <summary>
/// Generates SQL statements for entity operations
/// </summary>
internal sealed class SqlGenerator
{
    private readonly EntityMetadata _metadata;
    private readonly string _provider;

    public SqlGenerator(EntityMetadata metadata, string provider = "SqlServer")
    {
        _metadata = metadata;
        _provider = provider;
    }

    public string GenerateCreateTableSql()
    {
        bool isSqlite = _provider == "Sqlite";
        StringBuilder sql = new StringBuilder();

        if (isSqlite)
        {
            // Sqlite: CREATE TABLE IF NOT EXISTS
            sql.AppendLine($"CREATE TABLE IF NOT EXISTS [{_metadata.TableName}] (");
        }
        else
        {
            // SQL Server: IF OBJECT_ID ... BEGIN
            sql.AppendLine($"IF OBJECT_ID(N'[{_metadata.TableName}]', 'U') IS NULL");
            sql.AppendLine("BEGIN");
            sql.AppendLine($"CREATE TABLE [{_metadata.TableName}] (");
        }

        List<string> columnDefinitions = new List<string>();

        // Add all columns
        foreach (PropertyMetadata property in _metadata.Properties)
        {
            string columnDef = GenerateColumnDefinition(property);
            columnDefinitions.Add($"    {columnDef}");
        }

        // Add audit columns if auditable
        if (_metadata.IsAuditable)
        {
            if (isSqlite)
            {
                columnDefinitions.Add("    [CreatedBy] TEXT NOT NULL");
                columnDefinitions.Add("    [CreatedDate] TEXT NOT NULL DEFAULT (datetime('now'))");
                columnDefinitions.Add("    [ModifiedBy] TEXT NOT NULL");
                columnDefinitions.Add("    [ModifiedDate] TEXT NOT NULL DEFAULT (datetime('now'))");
            }
            else
            {
                columnDefinitions.Add("    [CreatedBy] NVARCHAR(256) NOT NULL");
                columnDefinitions.Add("    [CreatedDate] DATETIME2 NOT NULL DEFAULT GETUTCDATE()");
                columnDefinitions.Add("    [ModifiedBy] NVARCHAR(256) NOT NULL");
                columnDefinitions.Add("    [ModifiedDate] DATETIME2 NOT NULL DEFAULT GETUTCDATE()");
            }
        }

        // Add row version if enabled
        if (_metadata.HasRowVersion)
        {
            if (isSqlite)
            {
                columnDefinitions.Add("    [RowVersion] INTEGER NOT NULL DEFAULT 1");
            }
            else
            {
                columnDefinitions.Add("    [RowVersion] ROWVERSION NOT NULL");
            }
        }

        // Add soft delete column if enabled
        if (_metadata.HasSoftDelete)
        {
            string isDeletedColumn = _metadata.SoftDeleteColumn ?? "IsDeleted";
            string deletedDateColumn = _metadata.SoftDeleteDateColumn ?? "DeletedDate";

            if (isSqlite)
            {
                columnDefinitions.Add($"    [{isDeletedColumn}] INTEGER NOT NULL DEFAULT 0");
                columnDefinitions.Add($"    [{deletedDateColumn}] TEXT NULL");
            }
            else
            {
                columnDefinitions.Add($"    [{isDeletedColumn}] BIT NOT NULL DEFAULT 0");
                columnDefinitions.Add($"    [{deletedDateColumn}] DATETIME2 NULL");
            }
        }

        sql.AppendLine(string.Join(",\n", columnDefinitions));

        // Add primary key constraint
        if (_metadata.PrimaryKeyProperty != null)
        {
            if (isSqlite)
            {
                // Sqlite: PRIMARY KEY is already in column definition for autoincrement, skip if autoincrement
                if (!_metadata.PrimaryKeyProperty.IsAutoIncrement)
                {
                    sql.AppendLine($"    ,PRIMARY KEY ([{_metadata.PrimaryKeyProperty.ColumnName}])");
                }
            }
            else
            {
                sql.AppendLine($"    ,CONSTRAINT [PK_{_metadata.TableName}] PRIMARY KEY CLUSTERED ([{_metadata.PrimaryKeyProperty.ColumnName}] ASC)");
            }
        }

        sql.AppendLine(");");

            // Add indexes
            foreach (IndexMetadata index in _metadata.Indexes)
            {
                sql.AppendLine();
                sql.AppendLine(GenerateCreateIndexSql(index));
            }

            if (!isSqlite)
            {
                sql.AppendLine("END;");
            }

            return sql.ToString();
        }

    private string GenerateColumnDefinition(PropertyMetadata property)
    {
        bool isSqlite = _provider == "Sqlite";
        StringBuilder columnDef = new StringBuilder();
        columnDef.Append($"[{property.ColumnName}] ");

        // Use custom SQL type if specified, otherwise infer from C# type
        string sqlType = property.CustomSqlType ?? InferSqlType(property);
        columnDef.Append(sqlType);

        // Primary key with identity/autoincrement
        if (property.IsPrimaryKey && property.IsAutoIncrement)
        {
            if (isSqlite)
            {
                // Sqlite: INTEGER PRIMARY KEY AUTOINCREMENT (must be together)
                columnDef.Append(" PRIMARY KEY AUTOINCREMENT");
            }
            else
            {
                columnDef.Append(" IDENTITY(1,1)");
            }
        }
        else
        {
            // Nullable (not for autoincrement primary keys)
            columnDef.Append(property.IsNullable ? " NULL" : " NOT NULL");
        }

        return columnDef.ToString();
    }

    private string InferSqlType(PropertyMetadata property)
    {
        bool isSqlite = _provider == "Sqlite";
        string baseType = property.PropertyType.Replace("?", "").Trim();

        // Remove System. prefix
        if (baseType.StartsWith("System."))
        {
            baseType = baseType.Substring(7);
        }

        if (isSqlite)
        {
            // Sqlite type affinity: NULL, INTEGER, REAL, TEXT, BLOB
            return baseType switch
            {
                "int" => "INTEGER",
                "Int32" => "INTEGER",
                "long" => "INTEGER",
                "Int64" => "INTEGER",
                "short" => "INTEGER",
                "Int16" => "INTEGER",
                "byte" => "INTEGER",
                "Byte" => "INTEGER",
                "bool" => "INTEGER",
                "Boolean" => "INTEGER",
                "decimal" => "REAL",
                "Decimal" => "REAL",
                "double" => "REAL",
                "Double" => "REAL",
                "float" => "REAL",
                "Single" => "REAL",
                "DateTime" => "TEXT",
                "DateTimeOffset" => "TEXT",
                "TimeSpan" => "TEXT",
                "Guid" => "TEXT",
                "byte[]" => "BLOB",
                "string" when property.MaxLength.HasValue => $"TEXT",
                "String" when property.MaxLength.HasValue => $"TEXT",
                "string" => "TEXT",
                "String" => "TEXT",
                _ => "TEXT"
            };
        }
        else
        {
            // SQL Server types
            return baseType switch
            {
                "int" => "INT",
                "Int32" => "INT",
                "long" => "BIGINT",
                "Int64" => "BIGINT",
                "short" => "SMALLINT",
                "Int16" => "SMALLINT",
                "byte" => "TINYINT",
                "Byte" => "TINYINT",
                "bool" => "BIT",
                "Boolean" => "BIT",
                "decimal" => "DECIMAL(18,2)",
                "Decimal" => "DECIMAL(18,2)",
                "double" => "FLOAT",
                "Double" => "FLOAT",
                "float" => "REAL",
                "Single" => "REAL",
                "DateTime" => "DATETIME2",
                "DateTimeOffset" => "DATETIMEOFFSET",
                "TimeSpan" => "TIME",
                "Guid" => "UNIQUEIDENTIFIER",
                "byte[]" => "VARBINARY(MAX)",
                "string" when property.MaxLength.HasValue => $"NVARCHAR({property.MaxLength.Value})",
                "String" when property.MaxLength.HasValue => $"NVARCHAR({property.MaxLength.Value})",
                "string" => "NVARCHAR(MAX)",
                "String" => "NVARCHAR(MAX)",
                _ => "NVARCHAR(MAX)"
            };
        }
    }

    private string GenerateCreateIndexSql(IndexMetadata index)
    {
        bool isSqlite = _provider == "Sqlite";
        string uniqueKeyword = index.IsUnique ? "UNIQUE " : "";
        string columns = string.Join(", ", index.ColumnNames.Select(c => $"[{c}]"));

        if (isSqlite)
        {
            return $"CREATE {uniqueKeyword}INDEX IF NOT EXISTS [{index.IndexName}] ON [{_metadata.TableName}] ({columns});";
        }
        else
        {
            return $"CREATE {uniqueKeyword}NONCLUSTERED INDEX [{index.IndexName}] ON [{_metadata.TableName}] ({columns});";
        }
    }

    public string GenerateInsertSql()
    {
        bool isSqlite = _provider == "Sqlite";
        List<PropertyMetadata> insertableProperties = _metadata.Properties
            .Where(p => !p.IsPrimaryKey || !p.IsAutoIncrement)
            .ToList();

        List<string> columnNames = new List<string>();
        List<string> parameterNames = new List<string>();

        foreach (PropertyMetadata property in insertableProperties)
        {
            columnNames.Add($"[{property.ColumnName}]");
            parameterNames.Add($"@{property.PropertyName}");
        }

        // Add audit columns
        if (_metadata.IsAuditable)
        {
            columnNames.AddRange(new[] { "[CreatedBy]", "[CreatedDate]", "[ModifiedBy]", "[ModifiedDate]" });
            parameterNames.AddRange(new[] { "@CreatedBy", "@CreatedDate", "@ModifiedBy", "@ModifiedDate" });
        }

        StringBuilder sql = new StringBuilder();
        sql.AppendLine($"INSERT INTO [{_metadata.TableName}]");
        sql.AppendLine($"({string.Join(", ", columnNames)})");
        sql.AppendLine($"VALUES");
        sql.AppendLine($"({string.Join(", ", parameterNames)});");

        if (_metadata.PrimaryKeyProperty?.IsAutoIncrement == true)
        {
            if (isSqlite)
            {
                sql.AppendLine("SELECT last_insert_rowid();");
            }
            else
            {
                sql.AppendLine("SELECT CAST(SCOPE_IDENTITY() AS INT);");
            }
        }

        return sql.ToString();
    }

    public string GenerateUpdateSql()
    {
        List<PropertyMetadata> updatableProperties = _metadata.Properties
            .Where(p => !p.IsPrimaryKey)
            .ToList();

        List<string> setStatements = new List<string>();

        foreach (PropertyMetadata property in updatableProperties)
        {
            setStatements.Add($"[{property.ColumnName}] = @{property.PropertyName}");
        }

        // Add audit columns
        if (_metadata.IsAuditable)
        {
            setStatements.Add("[ModifiedBy] = @ModifiedBy");
            setStatements.Add("[ModifiedDate] = @ModifiedDate");
        }

        StringBuilder sql = new StringBuilder();
        sql.AppendLine($"UPDATE [{_metadata.TableName}]");
        sql.AppendLine($"SET {string.Join(", ", setStatements)}");
        sql.Append($"WHERE [{_metadata.PrimaryKeyProperty?.ColumnName ?? "Id"}] = @{_metadata.PrimaryKeyProperty?.PropertyName ?? "Id"}");

        // Add row version check for optimistic concurrency
        if (_metadata.HasRowVersion)
        {
            sql.Append(" AND [RowVersion] = @RowVersion");
        }

        sql.AppendLine(";");

        return sql.ToString();
    }

    public string GenerateDeleteSql()
    {
        StringBuilder sql = new StringBuilder();

        if (_metadata.HasSoftDelete)
        {
            // Soft delete - update IsDeleted flag
            sql.AppendLine($"UPDATE [{_metadata.TableName}]");
            sql.AppendLine("SET [IsDeleted] = 1, [DeletedDate] = GETUTCDATE()");
        }
        else
        {
            // Hard delete
            sql.AppendLine($"DELETE FROM [{_metadata.TableName}]");
        }

        sql.Append($"WHERE [{_metadata.PrimaryKeyProperty?.ColumnName ?? "Id"}] = @Id");

        if (_metadata.HasSoftDelete)
        {
            sql.Append(" AND [IsDeleted] = 0");
        }

        sql.AppendLine(";");

        return sql.ToString();
    }

    public string GenerateGetByIdSql()
    {
        StringBuilder sql = new StringBuilder();
        sql.AppendLine($"SELECT * FROM [{_metadata.TableName}]");
        sql.Append($"WHERE [{_metadata.PrimaryKeyProperty?.ColumnName ?? "Id"}] = @Id");

        if (_metadata.HasSoftDelete)
        {
            sql.Append(" AND [IsDeleted] = 0");
        }

        sql.AppendLine(";");

        return sql.ToString();
    }

    public string GenerateGetAllSql()
    {
        StringBuilder sql = new StringBuilder();
        sql.Append($"SELECT * FROM [{_metadata.TableName}]");

        if (_metadata.HasSoftDelete)
        {
            sql.Append(" WHERE [IsDeleted] = 0");
        }

        sql.AppendLine(";");

        return sql.ToString();
    }

    public string GenerateCountSql()
    {
        StringBuilder sql = new StringBuilder();
        sql.Append($"SELECT COUNT(*) FROM [{_metadata.TableName}]");

        if (_metadata.HasSoftDelete)
        {
            sql.Append(" WHERE [IsDeleted] = 0");
        }

        sql.AppendLine(";");

        return sql.ToString();
    }

    public string GenerateExistsSql()
    {
        StringBuilder sql = new StringBuilder();
        sql.AppendLine($"SELECT CASE WHEN EXISTS (");
        sql.AppendLine($"    SELECT 1 FROM [{_metadata.TableName}]");
        sql.Append($"    WHERE [{_metadata.PrimaryKeyProperty?.ColumnName ?? "Id"}] = @Id");

        if (_metadata.HasSoftDelete)
        {
            sql.Append(" AND [IsDeleted] = 0");
        }

        sql.AppendLine();
        sql.AppendLine(") THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END;");

        return sql.ToString();
    }

    public string GenerateGetByNameSql()
    {
        // For reference tables
        StringBuilder sql = new StringBuilder();
        sql.AppendLine($"SELECT * FROM [{_metadata.TableName}]");
        sql.Append("WHERE [Name] = @Name");

        if (_metadata.HasSoftDelete)
        {
            sql.Append(" AND [IsDeleted] = 0");
        }

        sql.AppendLine(";");

        return sql.ToString();
    }

    public string GenerateDeleteAllSql()
    {
        StringBuilder sql = new StringBuilder();

        if (_metadata.HasSoftDelete)
        {
            sql.AppendLine($"UPDATE [{_metadata.TableName}]");
            sql.AppendLine("SET [IsDeleted] = 1, [DeletedDate] = GETUTCDATE()");
            sql.AppendLine("WHERE [IsDeleted] = 0;");
        }
        else
        {
            sql.AppendLine($"DELETE FROM [{_metadata.TableName}];");
        }

        return sql.ToString();
    }

    public string GenerateHardDeleteSql()
    {
        // For soft delete entities, provides a way to permanently delete
        StringBuilder sql = new StringBuilder();
        sql.AppendLine($"DELETE FROM [{_metadata.TableName}]");
        sql.Append($"WHERE [{_metadata.PrimaryKeyProperty?.ColumnName ?? "Id"}] = @Id;");
        return sql.ToString();
    }

    public string GenerateGetByIdsSql()
    {
        StringBuilder sql = new StringBuilder();
        sql.AppendLine($"SELECT * FROM [{_metadata.TableName}]");
        sql.Append($"WHERE [{_metadata.PrimaryKeyProperty?.ColumnName ?? "Id"}] IN (SELECT value FROM STRING_SPLIT(@Ids, ','))");

        if (_metadata.HasSoftDelete)
        {
            sql.Append(" AND [IsDeleted] = 0");
        }

        sql.AppendLine(";");
        return sql.ToString();
    }

    public string GenerateGetAllIncludingDeletedSql()
    {
        // For soft delete entities, gets all records including deleted ones
        StringBuilder sql = new StringBuilder();
        sql.AppendLine($"SELECT * FROM [{_metadata.TableName}];");
        return sql.ToString();
    }

    public string GenerateBulkDeleteSql()
    {
        StringBuilder sql = new StringBuilder();

        if (_metadata.HasSoftDelete)
        {
            sql.AppendLine($"UPDATE [{_metadata.TableName}]");
            sql.AppendLine("SET [IsDeleted] = 1, [DeletedDate] = GETUTCDATE()");
            sql.Append($"WHERE [{_metadata.PrimaryKeyProperty?.ColumnName ?? "Id"}] IN (SELECT value FROM STRING_SPLIT(@Ids, ','))");
            sql.AppendLine(" AND [IsDeleted] = 0;");
        }
        else
        {
            sql.AppendLine($"DELETE FROM [{_metadata.TableName}]");
            sql.AppendLine($"WHERE [{_metadata.PrimaryKeyProperty?.ColumnName ?? "Id"}] IN (SELECT value FROM STRING_SPLIT(@Ids, ','));");
        }

        return sql.ToString();
    }
}
