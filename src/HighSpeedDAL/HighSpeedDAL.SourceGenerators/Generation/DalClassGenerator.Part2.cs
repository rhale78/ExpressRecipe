using System.Collections.Generic;
using System.Linq;
using System.Text;
using HighSpeedDAL.SourceGenerators.Models;

namespace HighSpeedDAL.SourceGenerators.Generation;

/// <summary>
/// DAL Class Generator - Part 2: Insert, Update, Delete, and Bulk operations
/// </summary>
internal sealed partial class DalClassGenerator
{
    private void GenerateInsertMethod(StringBuilder code)
    {
        code.AppendLine("    /// <summary>");
        code.AppendLine($"    /// Inserts a new {_metadata.ClassName} and returns the entity with auto-generated fields populated");
        code.AppendLine("    /// </summary>");

        if (_metadata.IsAuditable)
        {
            code.AppendLine($"    public async Task<{_metadata.ClassName}> InsertAsync({_metadata.ClassName} entity, string userName, CancellationToken cancellationToken = default)");
        }
        else
        {
            code.AppendLine($"    public async Task<{_metadata.ClassName}> InsertAsync({_metadata.ClassName} entity, CancellationToken cancellationToken = default)");
        }

        code.AppendLine("    {");
        code.AppendLine("        if (entity == null)");
        code.AppendLine("        {");
        code.AppendLine("            throw new ArgumentNullException(nameof(entity));");
        code.AppendLine("        }");
        code.AppendLine();
        code.AppendLine($"        Logger.LogDebug(\"Inserting new {_metadata.ClassName}\");");
        code.AppendLine();
        code.AppendLine("        Dictionary<string, object> parameters = MapToParameters(entity);");

        if (_metadata.IsAuditable)
        {
            code.AppendLine();
            code.AppendLine("        // Add audit fields");
            code.AppendLine("        DateTime now = DateTime.UtcNow;");
            code.AppendLine("        parameters[\"CreatedBy\"] = userName;");
            code.AppendLine("        parameters[\"CreatedDate\"] = now;");
            code.AppendLine("        parameters[\"ModifiedBy\"] = userName;");
            code.AppendLine("        parameters[\"ModifiedDate\"] = now;");
            code.AppendLine();
            code.AppendLine("        // Populate audit fields on entity");
            code.AppendLine("        entity.CreatedBy = userName;");
            code.AppendLine("        entity.CreatedDate = now;");
            code.AppendLine("        entity.ModifiedBy = userName;");
            code.AppendLine("        entity.ModifiedDate = now;");
        }

        code.AppendLine();
        code.AppendLine("        int? id = await ExecuteScalarAsync<int>(");
        code.AppendLine("            SQL_INSERT,");
        code.AppendLine("            parameters,");
        code.AppendLine("            transaction: null,");
        code.AppendLine("            cancellationToken);");
        code.AppendLine();

            if (_metadata.PrimaryKeyProperty?.IsAutoIncrement == true)
            {
                code.AppendLine("        if (id.HasValue)");
                code.AppendLine("        {");
                code.AppendLine($"            entity.{_metadata.PrimaryKeyProperty.PropertyName} = id.Value;");
                code.AppendLine($"            Logger.LogInformation(\"{_metadata.ClassName} inserted with ID: {{Id}}\", id.Value);");

                if (_metadata.HasCache)
                {
                    code.AppendLine();
                    code.AppendLine("            // Add to L1/L2 cache");
                    code.AppendLine("            await _cache.SetAsync(id.Value, entity, cancellationToken);");
                }

                if (_metadata.HasMemoryMappedTable)
                {
                    code.AppendLine();
                    code.AppendLine("            // Add to L0 cache (memory-mapped)");
                    code.AppendLine("            await UpdateMemoryMappedCacheAsync(entity, cancellationToken);");
                }

                code.AppendLine();
                code.AppendLine("            return entity;");
                code.AppendLine("        }");
                code.AppendLine();
                code.AppendLine("        throw new InvalidOperationException(\"Failed to retrieve inserted ID\");");
            }
            else
            {
                code.AppendLine($"        Logger.LogInformation(\"{_metadata.ClassName} inserted\");");

                if (_metadata.HasCache)
                {
                    code.AppendLine($"        await _cache.SetAsync(entity.{_metadata.PrimaryKeyProperty?.PropertyName ?? "Id"}, entity, cancellationToken);");
                }

                if (_metadata.HasMemoryMappedTable)
                {
                    code.AppendLine("        await UpdateMemoryMappedCacheAsync(entity, cancellationToken);");
                }

                code.AppendLine($"        return entity;");
            }

            code.AppendLine("    }");
        }

    private void GenerateUpdateMethod(StringBuilder code)
    {
        code.AppendLine("    /// <summary>");
        code.AppendLine($"    /// Updates an existing {_metadata.ClassName} and returns the entity with refreshed auto-generated fields");
        code.AppendLine("    /// </summary>");

        if (_metadata.IsAuditable)
        {
            code.AppendLine($"    public async Task<{_metadata.ClassName}> UpdateAsync({_metadata.ClassName} entity, string userName, CancellationToken cancellationToken = default)");
        }
        else
        {
            code.AppendLine($"    public async Task<{_metadata.ClassName}> UpdateAsync({_metadata.ClassName} entity, CancellationToken cancellationToken = default)");
        }

        code.AppendLine("    {");
        code.AppendLine("        if (entity == null)");
        code.AppendLine("        {");
        code.AppendLine("            throw new ArgumentNullException(nameof(entity));");
        code.AppendLine("        }");
        code.AppendLine();
        code.AppendLine($"        Logger.LogDebug(\"Updating {_metadata.ClassName} ID: {{Id}}\", entity.{_metadata.PrimaryKeyProperty?.PropertyName ?? "Id"});");
        code.AppendLine();
        code.AppendLine("        Dictionary<string, object> parameters = MapToParameters(entity);");

        if (_metadata.IsAuditable)
        {
            code.AppendLine();
            code.AppendLine("        // Update audit fields");
            code.AppendLine("        DateTime now = DateTime.UtcNow;");
            code.AppendLine("        parameters[\"ModifiedBy\"] = userName;");
            code.AppendLine("        parameters[\"ModifiedDate\"] = now;");
            code.AppendLine();
            code.AppendLine("        // Populate audit fields on entity");
            code.AppendLine("        entity.ModifiedBy = userName;");
            code.AppendLine("        entity.ModifiedDate = now;");
        }

        if (_metadata.HasRowVersion)
        {
            code.AppendLine();
            code.AppendLine("        // Add row version for optimistic concurrency");
            code.AppendLine("        parameters[\"RowVersion\"] = entity.RowVersion;");
        }

        code.AppendLine();
        code.AppendLine("        int rowsAffected = await ExecuteNonQueryAsync(");
        code.AppendLine("            SQL_UPDATE,");
        code.AppendLine("            parameters,");
        code.AppendLine("            transaction: null,");
        code.AppendLine("            cancellationToken);");
        code.AppendLine();

        if (_metadata.HasRowVersion)
        {
            code.AppendLine("        if (rowsAffected == 0)");
            code.AppendLine("        {");
            code.AppendLine($"            Logger.LogWarning(\"Concurrency conflict updating {_metadata.ClassName} ID: {{Id}}\", entity.{_metadata.PrimaryKeyProperty?.PropertyName ?? "Id"});");
            code.AppendLine("            throw new DBConcurrencyException(\"The record has been modified by another user. Please refresh and try again.\");");
            code.AppendLine("        }");
            code.AppendLine();
        }

            if (_metadata.HasCache)
            {
                code.AppendLine("        // Update L1/L2 cache");
                code.AppendLine($"        await _cache.SetAsync(entity.{_metadata.PrimaryKeyProperty?.PropertyName ?? "Id"}, entity, cancellationToken);");
                code.AppendLine();
            }

            if (_metadata.HasMemoryMappedTable)
            {
                code.AppendLine("        // Update L0 cache (memory-mapped)");
                code.AppendLine("        await UpdateMemoryMappedCacheAsync(entity, cancellationToken);");
                code.AppendLine();
            }

            code.AppendLine($"        Logger.LogInformation(\"{_metadata.ClassName} updated. Rows affected: {{RowsAffected}}\", rowsAffected);");
            code.AppendLine("        return entity;");
            code.AppendLine("    }");
        }

    private void GenerateDeleteMethod(StringBuilder code)
    {
        code.AppendLine("    /// <summary>");
        code.AppendLine($"    /// Deletes a {_metadata.ClassName} by ID");
        code.AppendLine("    /// </summary>");
        code.AppendLine("    public async Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)");
        code.AppendLine("    {");
        code.AppendLine($"        Logger.LogDebug(\"Deleting {_metadata.ClassName} ID: {{Id}}\", id);");
        code.AppendLine();
        code.AppendLine("        Dictionary<string, object> parameters = new Dictionary<string, object>");
        code.AppendLine("        {");
        code.AppendLine("            { \"Id\", id }");
        code.AppendLine("        };");
        code.AppendLine();
        code.AppendLine("        int rowsAffected = await ExecuteNonQueryAsync(");
        code.AppendLine("            SQL_DELETE,");
        code.AppendLine("            parameters,");
        code.AppendLine("            transaction: null,");
        code.AppendLine("            cancellationToken);");
        code.AppendLine();

                if (_metadata.HasCache)
                {
                    code.AppendLine("        // Remove from L1/L2 cache");
                    code.AppendLine("        await _cache.RemoveAsync(id, cancellationToken);");
                    code.AppendLine();
                }

                if (_metadata.HasMemoryMappedTable)
                {
                    code.AppendLine("        // Remove from L0 cache (memory-mapped backed)");
                    code.AppendLine("        _l0Cache.TryRemove(id, out _);");

                    if (_metadata.MemoryMappedSyncMode == 0) // Immediate
                    {
                        code.AppendLine("        await FlushMemoryMappedCacheAsync(cancellationToken); // Immediate sync mode");
                    }

                    code.AppendLine();
                }

                if (_metadata.HasSoftDelete)
                {
                    code.AppendLine($"        Logger.LogInformation(\"{_metadata.ClassName} soft-deleted. Rows affected: {{RowsAffected}}\", rowsAffected);");
                }
                else
                {
                    code.AppendLine($"        Logger.LogInformation(\"{_metadata.ClassName} deleted. Rows affected: {{RowsAffected}}\", rowsAffected);");
                }

                code.AppendLine("        return rowsAffected;");
                code.AppendLine("    }");
            }

    private void GenerateBulkInsertMethod(StringBuilder code)
    {
        code.AppendLine("    /// <summary>");
        code.AppendLine($"    /// Bulk inserts multiple {_metadata.ClassName} entities using SqlBulkCopy for maximum performance");
        code.AppendLine("    /// </summary>");

        if (_metadata.IsAuditable)
        {
            code.AppendLine($"    public async Task<int> BulkInsertAsync(IEnumerable<{_metadata.ClassName}> entities, string userName, CancellationToken cancellationToken = default)");
        }
        else
        {
            code.AppendLine($"    public async Task<int> BulkInsertAsync(IEnumerable<{_metadata.ClassName}> entities, CancellationToken cancellationToken = default)");
        }

        code.AppendLine("    {");
        code.AppendLine("        if (entities == null)");
        code.AppendLine("        {");
        code.AppendLine("            throw new ArgumentNullException(nameof(entities));");
        code.AppendLine("        }");
        code.AppendLine();
        code.AppendLine($"        List<{_metadata.ClassName}> entityList = entities.ToList();");
        code.AppendLine("        if (entityList.Count == 0)");
        code.AppendLine("        {");
        code.AppendLine("            return 0;");
        code.AppendLine("        }");
        code.AppendLine();

        if (_metadata.IsAuditable)
        {
            code.AppendLine("        // Populate audit fields for all entities");
            code.AppendLine("        DateTime now = DateTime.UtcNow;");
            code.AppendLine("        foreach (var entity in entityList)");
            code.AppendLine("        {");
            code.AppendLine("            entity.CreatedBy = userName;");
            code.AppendLine("            entity.CreatedDate = now;");
            code.AppendLine("            entity.ModifiedBy = userName;");
            code.AppendLine("            entity.ModifiedDate = now;");
            code.AppendLine("        }");
            code.AppendLine();
        }

        code.AppendLine($"        Logger.LogInformation(\"Bulk inserting {{Count}} {_metadata.ClassName} entities\", entityList.Count);");
        code.AppendLine();
        code.AppendLine("        int count = await BulkInsertInternalAsync(");
        code.AppendLine($"            \"{_metadata.TableName}\",");
        code.AppendLine("            entityList,");
        code.AppendLine("            MapToParametersForBulk,");
        code.AppendLine("            cancellationToken);");
        code.AppendLine();
        code.AppendLine($"        Logger.LogInformation(\"Bulk inserted {{Count}} {_metadata.ClassName} entities\", count);");
        code.AppendLine("        return count;");
        code.AppendLine("    }");
    }

    private void GenerateCountMethod(StringBuilder code)
    {
        code.AppendLine("    /// <summary>");
        code.AppendLine($"    /// Gets the count of {_metadata.ClassName} entities");
        code.AppendLine("    /// </summary>");
        code.AppendLine("    public async Task<int> CountAsync(CancellationToken cancellationToken = default)");
        code.AppendLine("    {");
        code.AppendLine("        int? count = await ExecuteScalarAsync<int>(");
        code.AppendLine("            SQL_COUNT,");
        code.AppendLine("            parameters: null,");
        code.AppendLine("            transaction: null,");
        code.AppendLine("            cancellationToken);");
        code.AppendLine();
        code.AppendLine("        return count ?? 0;");
        code.AppendLine("    }");
    }

    private void GenerateExistsMethod(StringBuilder code)
    {
        code.AppendLine("    /// <summary>");
        code.AppendLine($"    /// Checks if a {_metadata.ClassName} exists by ID");
        code.AppendLine("    /// </summary>");
        code.AppendLine("    public async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)");
        code.AppendLine("    {");
        code.AppendLine("        Dictionary<string, object> parameters = new Dictionary<string, object>");
        code.AppendLine("        {");
        code.AppendLine("            { \"Id\", id }");
        code.AppendLine("        };");
        code.AppendLine();
        code.AppendLine("        bool? exists = await ExecuteScalarAsync<bool>(");
        code.AppendLine("            SQL_EXISTS,");
        code.AppendLine("            parameters,");
        code.AppendLine("            transaction: null,");
        code.AppendLine("            cancellationToken);");
        code.AppendLine();
        code.AppendLine("        return exists ?? false;");
        code.AppendLine("    }");
    }

    private void GenerateGetByNameMethod(StringBuilder code)
    {
        code.AppendLine("    /// <summary>");
        code.AppendLine($"    /// Gets a {_metadata.ClassName} by name (reference table)");
        code.AppendLine("    /// </summary>");
        code.AppendLine($"    public async Task<{_metadata.ClassName}?> GetByNameAsync(string name, CancellationToken cancellationToken = default)");
        code.AppendLine("    {");
        code.AppendLine("        if (string.IsNullOrWhiteSpace(name))");
        code.AppendLine("        {");
        code.AppendLine("            throw new ArgumentException(\"Name cannot be null or empty\", nameof(name));");
        code.AppendLine("        }");
        code.AppendLine();
        code.AppendLine("        Dictionary<string, object> parameters = new Dictionary<string, object>");
        code.AppendLine("        {");
        code.AppendLine("            { \"Name\", name }");
        code.AppendLine("        };");
        code.AppendLine();
        code.AppendLine($"        List<{_metadata.ClassName}> results = await ExecuteQueryAsync(");
        code.AppendLine("            SQL_GET_BY_NAME,");
        code.AppendLine("            MapFromReader,");
        code.AppendLine("            parameters,");
        code.AppendLine("            transaction: null,");
        code.AppendLine("            cancellationToken);");
        code.AppendLine();
        code.AppendLine("        return results.FirstOrDefault();");
        code.AppendLine("    }");
    }

    private void GeneratePreloadDataMethod(StringBuilder code)
    {
        code.AppendLine("    /// <summary>");
        code.AppendLine("    /// Preloads reference data into the table");
        code.AppendLine("    /// </summary>");
        code.AppendLine("    public async Task PreloadDataAsync(CancellationToken cancellationToken = default)");
        code.AppendLine("    {");
        code.AppendLine($"        Logger.LogInformation(\"Preloading reference data for {_metadata.ClassName}\");");
        code.AppendLine();

        if (_metadata.ReferenceLoadFromCode)
        {
            code.AppendLine("        // Get reference data from static method");
            code.AppendLine($"        {_metadata.ClassName}[] referenceData = {_metadata.ClassName}.GetReferenceData();");
            code.AppendLine();
            code.AppendLine("        if (referenceData == null || referenceData.Length == 0)");
            code.AppendLine("        {");
            code.AppendLine($"            Logger.LogWarning(\"No reference data returned from {_metadata.ClassName}.GetReferenceData()\");");
            code.AppendLine("            return;");
            code.AppendLine("        }");
            code.AppendLine();
            code.AppendLine("        // Check if data already exists");
            code.AppendLine("        int existingCount = await CountAsync(cancellationToken);");
            code.AppendLine("        if (existingCount > 0)");
            code.AppendLine("        {");
            code.AppendLine($"            Logger.LogInformation(\"Reference data already exists for {_metadata.ClassName}. Skipping preload.\");");
            code.AppendLine("            return;");
            code.AppendLine("        }");
            code.AppendLine();
            code.AppendLine("        // Insert reference data");
            code.AppendLine("        foreach ({_metadata.ClassName} item in referenceData)");
            code.AppendLine("        {");

            if (_metadata.IsAuditable)
            {
                code.AppendLine("            await InsertAsync(item, \"System\", cancellationToken);");
            }
            else
            {
                code.AppendLine("            await InsertAsync(item, cancellationToken);");
            }

            code.AppendLine("        }");
            code.AppendLine();
            code.AppendLine($"        Logger.LogInformation(\"Preloaded {{Count}} reference items for {_metadata.ClassName}\", referenceData.Length);");
        }
        else
        {
            code.AppendLine("        // CSV preload not implemented yet");
            code.AppendLine($"        Logger.LogWarning(\"CSV preload not yet implemented for {_metadata.ClassName}\");");
        }

        code.AppendLine("    }");
    }

    private void GenerateMapFromReaderMethod(StringBuilder code)
    {
        code.AppendLine("    /// <summary>");
        code.AppendLine("    /// Maps a data reader row to an entity");
        code.AppendLine("    /// </summary>");
        code.AppendLine($"    private {_metadata.ClassName} MapFromReader(IDataReader reader)");
        code.AppendLine("    {");
        code.AppendLine("        // Cache ordinals for performance - GetOrdinal is expensive");

        // Define audit and soft-delete property names to exclude
        HashSet<string> auditAndSoftDeleteProps = new HashSet<string>
        {
            "CreatedBy", "CreatedDate", "ModifiedBy", "ModifiedDate",
            "IsDeleted", "DeletedDate", "DeletedBy"
        };

        // Generate ordinal caching - exclude audit/soft-delete properties to avoid duplicates
        foreach (PropertyMetadata property in _metadata.Properties)
        {
            if (!auditAndSoftDeleteProps.Contains(property.PropertyName))
            {
                code.AppendLine($"        int ord{property.PropertyName} = reader.GetOrdinal(\"{property.ColumnName}\");");
            }
        }

        if (_metadata.IsAuditable)
        {
            code.AppendLine("        int ordCreatedBy = reader.GetOrdinal(\"CreatedBy\");");
            code.AppendLine("        int ordCreatedDate = reader.GetOrdinal(\"CreatedDate\");");
            code.AppendLine("        int ordModifiedBy = reader.GetOrdinal(\"ModifiedBy\");");
            code.AppendLine("        int ordModifiedDate = reader.GetOrdinal(\"ModifiedDate\");");
        }

        if (_metadata.HasSoftDelete)
        {
            code.AppendLine("        int ordIsDeleted = reader.GetOrdinal(\"IsDeleted\");");
            code.AppendLine("        int ordDeletedDate = reader.GetOrdinal(\"DeletedDate\");");
            code.AppendLine("        int ordDeletedBy = reader.GetOrdinal(\"DeletedBy\");");
        }

        code.AppendLine();
        code.AppendLine($"        {_metadata.ClassName} entity = new {_metadata.ClassName}();");
        code.AppendLine();

        // Generate property assignments using cached ordinals - exclude audit/soft-delete properties
        foreach (PropertyMetadata property in _metadata.Properties)
        {
            if (!auditAndSoftDeleteProps.Contains(property.PropertyName))
            {
                string readerMethod = GetReaderMethod(property.PropertyType);
                string ordinalVar = $"ord{property.PropertyName}";
                string nullCheck = property.IsNullable ? $"reader.IsDBNull({ordinalVar}) ? null : " : "";

                code.AppendLine($"        entity.{property.PropertyName} = {nullCheck}reader.{readerMethod}({ordinalVar});");
            }
        }

        if (_metadata.IsAuditable)
        {
            code.AppendLine();
            code.AppendLine("        // Map audit columns (properties are auto-generated)");
            code.AppendLine("        entity.CreatedBy = reader.GetString(ordCreatedBy);");
            code.AppendLine("        entity.CreatedDate = reader.GetDateTime(ordCreatedDate);");
            code.AppendLine("        entity.ModifiedBy = reader.GetString(ordModifiedBy);");
            code.AppendLine("        entity.ModifiedDate = reader.GetDateTime(ordModifiedDate);");
        }

        if (_metadata.HasSoftDelete)
        {
            code.AppendLine();
            code.AppendLine("        // Map soft delete columns (properties are auto-generated)");
            code.AppendLine("        entity.IsDeleted = reader.GetBoolean(ordIsDeleted);");
            code.AppendLine("        entity.DeletedDate = reader.IsDBNull(ordDeletedDate) ? null : reader.GetDateTime(ordDeletedDate);");
            code.AppendLine("        entity.DeletedBy = reader.IsDBNull(ordDeletedBy) ? null : reader.GetString(ordDeletedBy);");
        }

        code.AppendLine();
        code.AppendLine("        return entity;");
        code.AppendLine("    }");
    }

    private void GenerateMapToParametersMethod(StringBuilder code)
    {
        code.AppendLine("    /// <summary>");
        code.AppendLine("    /// Maps an entity to SQL parameters");
        code.AppendLine("    /// </summary>");
        code.AppendLine($"    private Dictionary<string, object> MapToParameters({_metadata.ClassName} entity)");
        code.AppendLine("    {");
        code.AppendLine("        Dictionary<string, object> parameters = new Dictionary<string, object>();");
        code.AppendLine();

        // Define audit and soft-delete property names to exclude
        HashSet<string> auditAndSoftDeleteProps = new HashSet<string>
        {
            "CreatedBy", "CreatedDate", "ModifiedBy", "ModifiedDate",
            "IsDeleted", "DeletedDate", "DeletedBy"
        };

        foreach (PropertyMetadata property in _metadata.Properties)
        {
            // Skip audit and soft-delete properties - they're handled separately
            if (!auditAndSoftDeleteProps.Contains(property.PropertyName))
            {
                string nullableConversion = property.IsNullable ? $" ?? (object)DBNull.Value" : "";
                code.AppendLine($"        parameters[\"{property.PropertyName}\"] = entity.{property.PropertyName}{nullableConversion};");
            }
        }

        code.AppendLine();
        code.AppendLine("        return parameters;");
        code.AppendLine("    }");
        code.AppendLine();
        code.AppendLine("    /// <summary>");
        code.AppendLine("    /// Maps an entity to dictionary for bulk operations");
        code.AppendLine("    /// </summary>");
        code.AppendLine($"    private Dictionary<string, object> MapToParametersForBulk({_metadata.ClassName} entity)");
        code.AppendLine("    {");
        code.AppendLine("        // Same as MapToParameters for now");
        code.AppendLine("        return MapToParameters(entity);");
        code.AppendLine("    }");
    }

    private string GetReaderMethod(string propertyType)
    {
        string baseType = propertyType.Replace("?", "").Trim();

        // Remove System. prefix if present
        if (baseType.StartsWith("System."))
        {
            baseType = baseType.Substring(7);
        }

        return baseType switch
        {
            "int" => "GetInt32",
            "Int32" => "GetInt32",
            "long" => "GetInt64",
            "Int64" => "GetInt64",
            "short" => "GetInt16",
            "Int16" => "GetInt16",
            "byte" => "GetByte",
            "Byte" => "GetByte",
            "bool" => "GetBoolean",
            "Boolean" => "GetBoolean",
            "decimal" => "GetDecimal",
            "Decimal" => "GetDecimal",
            "double" => "GetDouble",
            "Double" => "GetDouble",
            "float" => "GetFloat",
            "Single" => "GetFloat",
                        "DateTime" => "GetDateTime",
                        "Guid" => "GetGuid",
                        "string" => "GetString",
                        "String" => "GetString",
                        _ => "GetValue"
                    };
                }

                    private void GenerateMemoryMappedHelperMethods(StringBuilder code)
                    {
                        string pkProp = _metadata.PrimaryKeyProperty?.PropertyName ?? "Id";
                        int syncMode = _metadata.MemoryMappedSyncMode;

                        code.AppendLine("    /// <summary>");
                        code.AppendLine("    /// Updates the L0 memory-mapped cache with an entity");
                        code.AppendLine("    /// </summary>");
                        code.AppendLine($"    private async Task UpdateMemoryMappedCacheAsync({_metadata.ClassName} entity, CancellationToken cancellationToken = default)");
                        code.AppendLine("    {");
                        code.AppendLine("        if (_memoryMappedStore == null) return;");
                        code.AppendLine();
                        code.AppendLine("        // Update in-memory L0 cache");
                        code.AppendLine($"        _l0Cache[entity.{pkProp}] = entity;");
                        code.AppendLine();

                        if (syncMode == 0) // Immediate
                        {
                            code.AppendLine("        // Immediate sync mode - flush to file now");
                            code.AppendLine("        await FlushMemoryMappedCacheAsync(cancellationToken);");
                        }
                        else if (syncMode == 1) // Batched
                        {
                            code.AppendLine("        // Batched sync mode - flush happens on timer");
                            code.AppendLine($"        Logger.LogTrace(\"Updated L0 cache for {_metadata.ClassName} ID: {{Id}}, will flush on timer\", entity.{pkProp});");
                        }
                        else // Manual
                        {
                            code.AppendLine("        // Manual sync mode - application must call FlushMemoryMappedCacheAsync explicitly");
                            code.AppendLine($"        Logger.LogTrace(\"Updated L0 cache for {_metadata.ClassName} ID: {{Id}}, flush required\", entity.{pkProp});");
                        }

                        code.AppendLine("    }");
                        code.AppendLine();

                        code.AppendLine("    /// <summary>");
                        code.AppendLine("    /// Flushes the L0 memory-mapped cache to disk");
                        code.AppendLine("    /// </summary>");
                        code.AppendLine("    public async Task FlushMemoryMappedCacheAsync(CancellationToken cancellationToken = default)");
                        code.AppendLine("    {");
                        code.AppendLine("        if (_memoryMappedStore == null) return;");
                        code.AppendLine();
                        code.AppendLine("        try");
                        code.AppendLine("        {");
                        code.AppendLine("            var rows = _l0Cache.Values.ToList();");
                        code.AppendLine("            await _memoryMappedStore.SaveAsync(rows, cancellationToken);");
                        code.AppendLine($"            Logger.LogDebug(\"Flushed {{Count}} rows to memory-mapped file for {_metadata.ClassName}\", rows.Count);");
                        code.AppendLine("        }");
                        code.AppendLine("        catch (Exception ex)");
                        code.AppendLine("        {");
                        code.AppendLine($"            Logger.LogError(ex, \"Failed to flush memory-mapped cache for {_metadata.ClassName}\");");
                        code.AppendLine("            throw;");
                        code.AppendLine("        }");
                        code.AppendLine("    }");
                    }
                }
