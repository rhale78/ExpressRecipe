using System.Collections.Generic;
using System.Linq;
using System.Text;
using HighSpeedDAL.SourceGenerators.Models;

namespace HighSpeedDAL.SourceGenerators.Generation
{
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

            List<string> columnDefinitions = [];

            // Add all columns
            foreach (PropertyMetadata property in _metadata.Properties)
            {
                string columnDef = GenerateColumnDefinition(property);
                columnDefinitions.Add($"    {columnDef}");
            }

            // Track existing column names to avoid duplicates when adding generated audit/soft-delete/rowversion columns
            HashSet<string> existingColumns = new HashSet<string>(_metadata.Properties.Select(p => p.ColumnName), System.StringComparer.OrdinalIgnoreCase);

                    // Add audit columns if auditable (but only if not already present in the entity definition)
                    if (_metadata.IsAuditable)
                    {
                        if (isSqlite)
                        {
                            if (!existingColumns.Contains(_metadata.CreatedByColumn))
                    {
                        columnDefinitions.Add($"    [{_metadata.CreatedByColumn}] TEXT NOT NULL");
                    }

                    if (!existingColumns.Contains(_metadata.CreatedDateColumn))
                    {
                        columnDefinitions.Add($"    [{_metadata.CreatedDateColumn}] TEXT NOT NULL DEFAULT (datetime('now'))");
                    }

                    if (!existingColumns.Contains(_metadata.ModifiedByColumn))
                    {
                        columnDefinitions.Add($"    [{_metadata.ModifiedByColumn}] TEXT NOT NULL");
                    }

                    if (!existingColumns.Contains(_metadata.ModifiedDateColumn))
                    {
                        columnDefinitions.Add($"    [{_metadata.ModifiedDateColumn}] TEXT NOT NULL DEFAULT (datetime('now'))");
                    }
                }
                        else
                        {
                            if (!existingColumns.Contains(_metadata.CreatedByColumn))
                    {
                        columnDefinitions.Add($"    [{_metadata.CreatedByColumn}] NVARCHAR(256) NOT NULL");
                    }

                    if (!existingColumns.Contains(_metadata.CreatedDateColumn))
                    {
                        columnDefinitions.Add($"    [{_metadata.CreatedDateColumn}] DATETIME2 NOT NULL DEFAULT GETUTCDATE()");
                    }

                    if (!existingColumns.Contains(_metadata.ModifiedByColumn))
                    {
                        columnDefinitions.Add($"    [{_metadata.ModifiedByColumn}] NVARCHAR(256) NOT NULL");
                    }

                    if (!existingColumns.Contains(_metadata.ModifiedDateColumn))
                    {
                        columnDefinitions.Add($"    [{_metadata.ModifiedDateColumn}] DATETIME2 NOT NULL DEFAULT GETUTCDATE()");
                    }
                }
                    }
        
                    // Add row version if enabled
                    if (_metadata.HasRowVersion)
                    {
                        if (!existingColumns.Contains("RowVersion"))
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
                    }
        
                    // Add soft delete column if enabled
                    if (_metadata.HasSoftDelete)
                    {
                        if (isSqlite)
                        {
                            if (!existingColumns.Contains(_metadata.SoftDeleteColumn))
                    {
                        columnDefinitions.Add($"    [{_metadata.SoftDeleteColumn}] INTEGER NOT NULL DEFAULT 0");
                    }

                    if (!existingColumns.Contains(_metadata.SoftDeleteDateColumn))
                    {
                        columnDefinitions.Add($"    [{_metadata.SoftDeleteDateColumn}] TEXT NULL");
                    }

                    if (!existingColumns.Contains(_metadata.SoftDeleteByColumn))
                    {
                        columnDefinitions.Add($"    [{_metadata.SoftDeleteByColumn}] TEXT NULL");
                    }
                }
                        else
                        {
                            if (!existingColumns.Contains(_metadata.SoftDeleteColumn))
                    {
                        columnDefinitions.Add($"    [{_metadata.SoftDeleteColumn}] BIT NOT NULL DEFAULT 0");
                    }

                    if (!existingColumns.Contains(_metadata.SoftDeleteDateColumn))
                    {
                        columnDefinitions.Add($"    [{_metadata.SoftDeleteDateColumn}] DATETIME2 NULL");
                    }

                    if (!existingColumns.Contains(_metadata.SoftDeleteByColumn))
                    {
                        columnDefinitions.Add($"    [{_metadata.SoftDeleteByColumn}] NVARCHAR(256) NULL");
                    }
                }
                    }

            // Ensure column definitions are unique (preserve first occurrence) to avoid duplicate column errors
            HashSet<string> seenColumns = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            List<string> uniqueColumnDefinitions = [];
        
                    foreach (var col in columnDefinitions)
                    {
                        var trimmed = col.TrimStart();
                        string colName;
                        if (trimmed.StartsWith("["))
                        {
                            var end = trimmed.IndexOf(']');
                            colName = end > 1 ? trimmed.Substring(1, end - 1) : trimmed;
                        }
                        else
                        {
                            // fallback: use the full trimmed line as identifier
                            colName = trimmed;
                        }
        
                        if (seenColumns.Add(colName))
                        {
                            uniqueColumnDefinitions.Add(col);
                        }
                        // else: duplicate column definition detected; skip additional entries
                    }
        
                    sql.AppendLine(string.Join(",\n", uniqueColumnDefinitions));
        
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
                            // SQL Server: IDENTITY is only valid for numeric types. For GUID primary keys
                            // we use a DEFAULT NEWID() to populate values generated by the database.
                            if (sqlType.Equals("UNIQUEIDENTIFIER", System.StringComparison.OrdinalIgnoreCase))
                            {
                                columnDef.Append(" NOT NULL DEFAULT NEWID()");
                            }
                            else
                            {
                                // SQL Server: IDENTITY is only valid for numeric types. For GUID primary keys, use DEFAULT NEWID()
                                if (sqlType.Equals("UNIQUEIDENTIFIER", System.StringComparison.OrdinalIgnoreCase))
                                {
                                    columnDef.Append(" NOT NULL DEFAULT NEWID()");
                                }
                                else
                                {
                                    columnDef.Append(" IDENTITY(1,1)");
                                }
                            }
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

            return isSqlite
                ? $"CREATE {uniqueKeyword}INDEX IF NOT EXISTS [{index.IndexName}] ON [{_metadata.TableName}] ({columns});"
                        : $"CREATE {uniqueKeyword}NONCLUSTERED INDEX [{index.IndexName}] ON [{_metadata.TableName}] ({columns});";
        }
        
                public string GenerateInsertSql()
                {
                    bool isSqlite = _provider == "Sqlite";
                    List<PropertyMetadata> insertableProperties = _metadata.Properties
                        .Where(p => !p.IsPrimaryKey || !p.IsAutoIncrement)
                        .ToList();
        
                    List<string> columnNames = [];
                    List<string> parameterNames = [];
        
                    foreach (PropertyMetadata property in insertableProperties)
                    {
                        columnNames.Add($"[{property.ColumnName}]");
                        parameterNames.Add($"@{property.PropertyName}");
                    }
        
                    // Add audit columns
                    if (_metadata.IsAuditable)
                    {
                // Avoid adding audit columns that are already present in the entity properties
                HashSet<string> existing = new HashSet<string>(insertableProperties.Select(p => p.ColumnName), System.StringComparer.OrdinalIgnoreCase);
                        if (!existing.Contains(_metadata.CreatedByColumn))
                        {
                            columnNames.Add($"[{_metadata.CreatedByColumn}]");
                            parameterNames.Add($"@{_metadata.CreatedByColumn}");
                        }
                        if (!existing.Contains(_metadata.CreatedDateColumn))
                        {
                            columnNames.Add($"[{_metadata.CreatedDateColumn}]");
                            parameterNames.Add($"@{_metadata.CreatedDateColumn}");
                        }
                        if (!existing.Contains(_metadata.ModifiedByColumn))
                        {
                            columnNames.Add($"[{_metadata.ModifiedByColumn}]");
                            parameterNames.Add($"@{_metadata.ModifiedByColumn}");
                        }
                        if (!existing.Contains(_metadata.ModifiedDateColumn))
                        {
                            columnNames.Add($"[{_metadata.ModifiedDateColumn}]");
                            parameterNames.Add($"@{_metadata.ModifiedDateColumn}");
                        }
                    }
        
                    StringBuilder sql = new StringBuilder();
                    sql.AppendLine($"INSERT INTO [{_metadata.TableName}]");
                    sql.AppendLine($"({string.Join(", ", columnNames)})");
        
                    // If the primary key is DB-generated and is a GUID, use OUTPUT INSERTED.[Id] to return the generated GUID.
                    string? pkSqlType = null;
                    if (_metadata.PrimaryKeyProperty != null)
                    {
                        pkSqlType = (_metadata.PrimaryKeyProperty.CustomSqlType ?? InferSqlType(_metadata.PrimaryKeyProperty))?.ToUpperInvariant();
                    }
        
                    var useOutputInsertedForGuidPk = !isSqlite && _metadata.PrimaryKeyProperty?.IsAutoIncrement == true && pkSqlType == "UNIQUEIDENTIFIER";
        
                    if (useOutputInsertedForGuidPk)
                    {
                        sql.AppendLine($"OUTPUT INSERTED.[{_metadata.PrimaryKeyProperty!.ColumnName}]");
                    }
        
                    sql.AppendLine($"VALUES");
                    sql.AppendLine($"({string.Join(", ", parameterNames)});");
        
                    if (_metadata.PrimaryKeyProperty?.IsAutoIncrement == true && !useOutputInsertedForGuidPk)
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
                    // Exclude audit and soft-delete properties - they're handled separately
                    List<PropertyMetadata> updatableProperties = _metadata.Properties
                        .Where(p => !p.IsPrimaryKey
                                    && p.PropertyName != "CreatedDate"
                                    && p.PropertyName != "CreatedBy"
                                    && p.PropertyName != "ModifiedDate"
                                    && p.PropertyName != "ModifiedBy"
                                    && p.PropertyName != "IsDeleted"
                                    && p.PropertyName != "DeletedDate"
                                    && p.PropertyName != "DeletedBy"
                                    && p.PropertyName != "RowVersion")
                        .ToList();
        
                    List<string> setStatements = [];
        
                    foreach (PropertyMetadata property in updatableProperties)
                    {
                        setStatements.Add($"[{property.ColumnName}] = @{property.PropertyName}");
                    }
        
                    // Add audit columns
                    if (_metadata.IsAuditable)
                    {
                        setStatements.Add($"[{_metadata.ModifiedByColumn}] = @{_metadata.ModifiedByColumn}");
                        setStatements.Add($"[{_metadata.ModifiedDateColumn}] = @{_metadata.ModifiedDateColumn}");
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
                        sql.AppendLine($"SET [{_metadata.SoftDeleteColumn}] = 1, [{_metadata.SoftDeleteDateColumn}] = GETUTCDATE()");
                    }
                    else
                    {
                        // Hard delete
                        sql.AppendLine($"DELETE FROM [{_metadata.TableName}]");
                    }
        
                    sql.Append($"WHERE [{_metadata.PrimaryKeyProperty?.ColumnName ?? "Id"}] = @Id");
        
                    if (_metadata.HasSoftDelete)
                    {
                        sql.Append($" AND [{_metadata.SoftDeleteColumn}] = 0");
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
                        sql.Append($" AND [{_metadata.SoftDeleteColumn}] = 0");
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
                        sql.Append($" WHERE [{_metadata.SoftDeleteColumn}] = 0");
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
                        sql.Append($" WHERE [{_metadata.SoftDeleteColumn}] = 0");
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
                        sql.Append($" AND [{_metadata.SoftDeleteColumn}] = 0");
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
                        sql.Append($" AND [{_metadata.SoftDeleteColumn}] = 0");
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
                        sql.AppendLine($"SET [{_metadata.SoftDeleteColumn}] = 1, [{_metadata.SoftDeleteDateColumn}] = GETUTCDATE()");
                        sql.AppendLine($"WHERE [{_metadata.SoftDeleteColumn}] = 0;");
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
                        sql.Append($" AND [{_metadata.SoftDeleteColumn}] = 0");
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
                        sql.AppendLine($"SET [{_metadata.SoftDeleteColumn}] = 1, [{_metadata.SoftDeleteDateColumn}] = GETUTCDATE()");
                        sql.Append($"WHERE [{_metadata.PrimaryKeyProperty?.ColumnName ?? "Id"}] IN (SELECT value FROM STRING_SPLIT(@Ids, ','))");
                        sql.AppendLine($" AND [{_metadata.SoftDeleteColumn}] = 0;");
                    }
                    else
                    {
                        sql.AppendLine($"DELETE FROM [{_metadata.TableName}]");
                        sql.AppendLine($"WHERE [{_metadata.PrimaryKeyProperty?.ColumnName ?? "Id"}] IN (SELECT value FROM STRING_SPLIT(@Ids, ','));");
                    }

                    return sql.ToString();
                }

                /// <summary>
                /// Generates SQL for a named query based on specified property names
                /// </summary>
                /// <param name="namedQuery">The named query metadata</param>
                /// <returns>SQL SELECT statement with WHERE clause for the specified properties</returns>
                public string GenerateNamedQuerySql(NamedQueryMetadata namedQuery)
                {
                    bool isSqlite = _provider == "Sqlite";
                    StringBuilder sql = new StringBuilder();

                    // Build the SELECT clause
                    if (namedQuery.IsSingle && !isSqlite)
                    {
                        sql.AppendLine($"SELECT TOP 1 * FROM [{_metadata.TableName}]");
                    }
                    else
                    {
                        sql.Append($"SELECT * FROM [{_metadata.TableName}]");
                    }

                    // Build WHERE clause from property names
                    List<string> conditions = [];

                    foreach (string propName in namedQuery.PropertyNames)
                    {
                        // Find the property to get column name
                        PropertyMetadata? prop = _metadata.Properties.FirstOrDefault(p =>
                            p.PropertyName.Equals(propName, System.StringComparison.OrdinalIgnoreCase));

                        string columnName = prop?.ColumnName ?? propName;
                        conditions.Add($"[{columnName}] = @{propName}");
                    }

                    // Add soft delete filter if enabled and entity has soft delete
                    if (namedQuery.AutoFilterDeleted && _metadata.HasSoftDelete)
                    {
                        // Don't add if IsDeleted is already in the query properties
                        bool hasIsDeletedParam = namedQuery.PropertyNames.Any(p =>
                            p.Equals("IsDeleted", System.StringComparison.OrdinalIgnoreCase));

                        if (!hasIsDeletedParam)
                        {
                            conditions.Add($"[{_metadata.SoftDeleteColumn}] = 0");
                        }
                    }

                    if (conditions.Count > 0)
                    {
                        sql.AppendLine();
                        sql.Append($"WHERE {string.Join(" AND ", conditions)}");
                    }

                    // For SQLite single result, use LIMIT 1
                    if (namedQuery.IsSingle && isSqlite)
                    {
                        sql.AppendLine();
                        sql.Append("LIMIT 1");
                    }

                    sql.AppendLine(";");

                    return sql.ToString();
                }

                /// <summary>
                /// Generates SQL for a named query that explicitly filters by IsDeleted=0
                /// (helper version for soft-delete entities that auto-filters deleted records)
                /// </summary>
                /// <param name="namedQuery">The named query metadata (will have AutoFilterDeleted=true)</param>
                /// <returns>SQL SELECT statement with automatic IsDeleted=0 filter</returns>
                public string GenerateNamedQueryActiveSql(NamedQueryMetadata namedQuery)
                {
            // Create a copy with AutoFilterDeleted forced to true
            NamedQueryMetadata activeQuery = new NamedQueryMetadata
                    {
                        Name = namedQuery.Name,
                        PropertyNames = namedQuery.PropertyNames.ToList(),
                        IsSingle = namedQuery.IsSingle,
                        AutoFilterDeleted = true, // Force filter for active records only
                        EnableCache = namedQuery.EnableCache
                    };

                    return GenerateNamedQuerySql(activeQuery);
                }
            }
}
