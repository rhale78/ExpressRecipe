using System.Linq;
using System.Text;
using HighSpeedDAL.SourceGenerators.Models;

namespace HighSpeedDAL.SourceGenerators.Generation;

/// <summary>
/// DAL Class Generator - Part 3: Additional CRUD operations (GetByIds, BulkUpdate, BulkDelete, HardDelete)
/// </summary>
internal sealed partial class DalClassGenerator
{
    private void GenerateGetByIdsMethod(StringBuilder code)
    {
        code.AppendLine("    /// <summary>");
        code.AppendLine($"    /// Gets multiple {_metadata.ClassName} entities by IDs");
        code.AppendLine("    /// </summary>");
        code.AppendLine($"    public async Task<List<{_metadata.ClassName}>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)");
        code.AppendLine("    {");
        code.AppendLine("        if (ids == null)");
        code.AppendLine("        {");
        code.AppendLine("            throw new ArgumentNullException(nameof(ids));");
        code.AppendLine("        }");
        code.AppendLine();
        code.AppendLine("        List<int> idList = ids.ToList();");
        code.AppendLine("        if (idList.Count == 0)");
        code.AppendLine("        {");
        code.AppendLine($"            return new List<{_metadata.ClassName}>();");
        code.AppendLine("        }");
        code.AppendLine();
        code.AppendLine($"        Logger.LogDebug(\"Retrieving {{Count}} {_metadata.ClassName} entities by IDs\", idList.Count);");
        code.AppendLine();
        code.AppendLine("        string idsString = string.Join(\",\", idList);");
        code.AppendLine("        Dictionary<string, object> parameters = new Dictionary<string, object>");
        code.AppendLine("        {");
        code.AppendLine("            { \"Ids\", idsString }");
        code.AppendLine("        };");
        code.AppendLine();
        code.AppendLine($"        List<{_metadata.ClassName}> results = await ExecuteQueryAsync(");
        code.AppendLine("            SQL_GET_BY_IDS,");
        code.AppendLine("            MapFromReader,");
        code.AppendLine("            parameters,");
        code.AppendLine("            transaction: null,");
        code.AppendLine("            cancellationToken);");
        code.AppendLine();
        code.AppendLine($"        Logger.LogDebug(\"Retrieved {{Count}} {_metadata.ClassName} entities\", results.Count);");
        code.AppendLine("        return results;");
        code.AppendLine("    }");
    }

    private void GenerateBulkUpdateMethod(StringBuilder code)
    {
        code.AppendLine("    /// <summary>");
        code.AppendLine($"    /// Bulk updates multiple {_metadata.ClassName} entities");
        code.AppendLine("    /// </summary>");

        if (_metadata.IsAuditable)
        {
            code.AppendLine($"    public async Task<int> BulkUpdateAsync(IEnumerable<{_metadata.ClassName}> entities, string userName, CancellationToken cancellationToken = default)");
        }
        else
        {
            code.AppendLine($"    public async Task<int> BulkUpdateAsync(IEnumerable<{_metadata.ClassName}> entities, CancellationToken cancellationToken = default)");
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
        code.AppendLine($"        Logger.LogInformation(\"Bulk updating {{Count}} {_metadata.ClassName} entities\", entityList.Count);");
        code.AppendLine();
            code.AppendLine("        int totalUpdated = 0;");
            code.AppendLine($"        foreach ({_metadata.ClassName} entity in entityList)");
            code.AppendLine("        {");

            if (_metadata.IsAuditable)
            {
                code.AppendLine("            await UpdateAsync(entity, userName, cancellationToken);");
            }
            else
            {
                code.AppendLine("            await UpdateAsync(entity, cancellationToken);");
            }

            code.AppendLine("            totalUpdated++;");
            code.AppendLine("        }");
            code.AppendLine();
            code.AppendLine($"        Logger.LogInformation(\"Bulk updated {{Count}} {_metadata.ClassName} entities\", totalUpdated);");
            code.AppendLine("        return totalUpdated;");
            code.AppendLine("    }");
        }

    private void GenerateBulkDeleteMethod(StringBuilder code)
    {
        code.AppendLine("    /// <summary>");
        code.AppendLine($"    /// Bulk deletes multiple {_metadata.ClassName} entities by IDs");
        code.AppendLine("    /// </summary>");
        code.AppendLine("    public async Task<int> BulkDeleteAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)");
        code.AppendLine("    {");
        code.AppendLine("        if (ids == null)");
        code.AppendLine("        {");
        code.AppendLine("            throw new ArgumentNullException(nameof(ids));");
        code.AppendLine("        }");
        code.AppendLine();
        code.AppendLine("        List<int> idList = ids.ToList();");
        code.AppendLine("        if (idList.Count == 0)");
        code.AppendLine("        {");
        code.AppendLine("            return 0;");
        code.AppendLine("        }");
        code.AppendLine();
        code.AppendLine($"        Logger.LogInformation(\"Bulk deleting {{Count}} {_metadata.ClassName} entities\", idList.Count);");
        code.AppendLine();
        code.AppendLine("        string idsString = string.Join(\",\", idList);");
        code.AppendLine("        Dictionary<string, object> parameters = new Dictionary<string, object>");
        code.AppendLine("        {");
        code.AppendLine("            { \"Ids\", idsString }");
        code.AppendLine("        };");
        code.AppendLine();
        code.AppendLine("        int rowsAffected = await ExecuteNonQueryAsync(");
        code.AppendLine("            SQL_BULK_DELETE,");
        code.AppendLine("            parameters,");
        code.AppendLine("            transaction: null,");
        code.AppendLine("            cancellationToken);");
        code.AppendLine();

        if (_metadata.HasCache)
        {
            code.AppendLine("        // Remove from cache");
            code.AppendLine("        foreach (int id in idList)");
            code.AppendLine("        {");
            code.AppendLine("            await _cache.RemoveAsync(id, cancellationToken);");
            code.AppendLine("        }");
            code.AppendLine();
        }

        if (_metadata.HasSoftDelete)
        {
            code.AppendLine($"        Logger.LogInformation(\"Bulk soft-deleted {{Count}} {_metadata.ClassName} entities\", rowsAffected);");
        }
        else
        {
            code.AppendLine($"        Logger.LogInformation(\"Bulk deleted {{Count}} {_metadata.ClassName} entities\", rowsAffected);");
        }

        code.AppendLine("        return rowsAffected;");
        code.AppendLine("    }");
    }

    private void GenerateHardDeleteMethod(StringBuilder code)
    {
        code.AppendLine("    /// <summary>");
        code.AppendLine($"    /// Permanently deletes a {_metadata.ClassName} by ID (bypasses soft delete)");
        code.AppendLine("    /// </summary>");
        code.AppendLine("    public async Task<int> HardDeleteAsync(int id, CancellationToken cancellationToken = default)");
        code.AppendLine("    {");
        code.AppendLine($"        Logger.LogWarning(\"Hard deleting {_metadata.ClassName} ID: {{Id}}\", id);");
        code.AppendLine();
        code.AppendLine("        Dictionary<string, object> parameters = new Dictionary<string, object>");
        code.AppendLine("        {");
        code.AppendLine("            { \"Id\", id }");
        code.AppendLine("        };");
        code.AppendLine();
        code.AppendLine("        int rowsAffected = await ExecuteNonQueryAsync(");
        code.AppendLine("            SQL_HARD_DELETE,");
        code.AppendLine("            parameters,");
        code.AppendLine("            transaction: null,");
        code.AppendLine("            cancellationToken);");
        code.AppendLine();

        if (_metadata.HasCache)
        {
            code.AppendLine("        // Remove from cache");
            code.AppendLine("        await _cache.RemoveAsync(id, cancellationToken);");
            code.AppendLine();
        }

        code.AppendLine($"        Logger.LogInformation(\"{_metadata.ClassName} permanently deleted. Rows affected: {{RowsAffected}}\", rowsAffected);");
        code.AppendLine("        return rowsAffected;");
        code.AppendLine("    }");
    }

    private void GenerateGetAllIncludingDeletedMethod(StringBuilder code)
    {
        code.AppendLine("    /// <summary>");
        code.AppendLine($"    /// Gets all {_metadata.ClassName} entities including soft-deleted ones");
        code.AppendLine("    /// </summary>");
        code.AppendLine($"    public async Task<List<{_metadata.ClassName}>> GetAllIncludingDeletedAsync(CancellationToken cancellationToken = default)");
        code.AppendLine("    {");
        code.AppendLine($"        Logger.LogDebug(\"Retrieving all {_metadata.ClassName} entities including deleted\");");
        code.AppendLine();
        code.AppendLine($"        List<{_metadata.ClassName}> results = await ExecuteQueryAsync(");
        code.AppendLine("            SQL_GET_ALL_INCLUDING_DELETED,");
        code.AppendLine("            MapFromReader,");
        code.AppendLine("            parameters: null,");
        code.AppendLine("            cancellationToken: cancellationToken);");
        code.AppendLine();
        code.AppendLine($"        Logger.LogDebug(\"Retrieved {{Count}} {_metadata.ClassName} entities (including deleted)\", results.Count);");
        code.AppendLine("        return results;");
        code.AppendLine("    }");
    }
}
