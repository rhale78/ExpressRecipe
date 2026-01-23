using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HighSpeedDAL.SourceGenerators.Models;

namespace HighSpeedDAL.SourceGenerators.Generation;

/// <summary>
/// DAL Class Generator - Part 4: Named Query methods generation
/// </summary>
internal sealed partial class DalClassGenerator
{
    /// <summary>
    /// Generates SQL constants for all named queries
    /// </summary>
    private void GenerateNamedQuerySqlConstants(StringBuilder code)
    {
        if (_metadata.NamedQueries == null || _metadata.NamedQueries.Count == 0)
        {
            return;
        }

        code.AppendLine();
        code.AppendLine("    // Named Query SQL Statements");

        foreach (NamedQueryMetadata namedQuery in _metadata.NamedQueries)
        {
            string constantName = $"SQL_GET_{ToUpperSnakeCase(namedQuery.Name)}";
            string sql = _sqlGenerator.GenerateNamedQuerySql(namedQuery).Replace("\"", "\"\"");
            code.AppendLine($"    private const string {constantName} = @\"{sql}\";");
        }
    }

    /// <summary>
    /// Generates all named query methods
    /// </summary>
    private void GenerateNamedQueryMethods(StringBuilder code)
    {
        if (_metadata.NamedQueries == null || _metadata.NamedQueries.Count == 0)
        {
            return;
        }

        foreach (NamedQueryMetadata namedQuery in _metadata.NamedQueries)
        {
            code.AppendLine();
            GenerateNamedQueryMethod(code, namedQuery);

            // For soft-delete entities with AutoFilterDeleted=false, also generate an "Active" helper
            if (_metadata.HasSoftDelete && !namedQuery.AutoFilterDeleted)
            {
                code.AppendLine();
                GenerateNamedQueryActiveHelperMethod(code, namedQuery);
            }
        }

        // For soft-delete entities, generate a general "GetActive{Name}" helper for queries with AutoFilterDeleted=true
        // that explicitly makes it clear the query filters by IsDeleted=0
        if (_metadata.HasSoftDelete)
        {
            foreach (NamedQueryMetadata namedQuery in _metadata.NamedQueries.Where(nq => nq.AutoFilterDeleted))
            {
                // Don't generate helper if IsDeleted is already a parameter (user is explicitly controlling it)
                bool hasIsDeletedParam = namedQuery.PropertyNames.Any(p =>
                    p.Equals("IsDeleted", System.StringComparison.OrdinalIgnoreCase));

                if (!hasIsDeletedParam)
                {
                    code.AppendLine();
                    GenerateNamedQueryActiveDocHelperMethod(code, namedQuery);
                }
            }
        }
    }

    /// <summary>
    /// Generates a single named query method
    /// </summary>
    private void GenerateNamedQueryMethod(StringBuilder code, NamedQueryMetadata namedQuery)
    {
        string methodName = $"Get{namedQuery.Name}Async";
        string constantName = $"SQL_GET_{ToUpperSnakeCase(namedQuery.Name)}";
        string returnType = namedQuery.IsSingle
            ? $"{_metadata.ClassName}?"
            : $"List<{_metadata.ClassName}>";

        // Build parameter list
        var parameters = BuildMethodParameters(namedQuery);

        code.AppendLine("    /// <summary>");
        code.AppendLine($"    /// Gets {_metadata.ClassName} entities by {string.Join(" and ", namedQuery.PropertyNames)}");
        if (namedQuery.IsSingle)
        {
            code.AppendLine("    /// Returns a single entity or null if not found");
        }
        if (namedQuery.AutoFilterDeleted && _metadata.HasSoftDelete)
        {
            code.AppendLine("    /// Note: Automatically filters out soft-deleted records (IsDeleted = 0)");
        }
        if (_metadata.HasInMemoryTable)
        {
            code.AppendLine("    /// Note: Filters from in-memory table (0.01-0.1ms), no database round-trip");
        }
        else if (_metadata.IsReferenceTable && _metadata.HasCache)
        {
            code.AppendLine("    /// Note: Filters from cached data (no database round-trip for reference tables)");
        }
        else if (_metadata.HasMemoryMappedTable)
        {
            code.AppendLine("    /// Note: Filters from L0 in-memory cache when available (no database round-trip)");
        }
        else if (_metadata.HasCache)
        {
            code.AppendLine("    /// Note: Filters from cached data when available");
        }
        code.AppendLine("    /// </summary>");
        code.AppendLine($"    public async Task<{returnType}> {methodName}({parameters}, CancellationToken cancellationToken = default)");
        code.AppendLine("    {");

        // Log the query
        code.AppendLine($"        Logger.LogDebug(\"Executing named query {namedQuery.Name} for {_metadata.ClassName}\");");
        code.AppendLine();

        // Priority order for query execution:
        // 1. In-memory tables - filter from _inMemoryTable (fastest, 0.01-0.1ms)
        // 2. Reference tables with cache - filter from GetAllAsync (fully cached)
        // 3. Memory-mapped tables - filter from L0 cache
        // 4. Entities with cache - filter from GetAllAsync (leverages cache)
        // 5. Standard SQL query

        if (_metadata.HasInMemoryTable)
        {
            // In-memory tables - highest priority for queries
            GenerateInMemoryTableFilter(code, namedQuery);
        }
        else if (_metadata.IsReferenceTable && _metadata.HasCache)
        {
            // Reference tables are fully cached - filter from cache
            GenerateCachedGetAllFilter(code, namedQuery);
        }
        else if (_metadata.HasMemoryMappedTable)
        {
            // Memory-mapped tables have L0 cache
            GenerateL0CacheFilter(code, namedQuery);
        }
        else if (_metadata.HasCache)
        {
            // Entities with cache - use GetAllAsync which leverages cache
            GenerateCachedGetAllFilter(code, namedQuery);
        }
        else
        {
            // Standard SQL query path
            GenerateSqlQueryPath(code, namedQuery, constantName);
        }

        code.AppendLine("    }");
    }

    /// <summary>
    /// Generates in-memory table filtering code for named queries
    /// </summary>
    private void GenerateInMemoryTableFilter(StringBuilder code, NamedQueryMetadata namedQuery)
    {
        code.AppendLine("        // PERFORMANCE: Filter from in-memory table using optimized property lookup (O(1) instead of O(n) LINQ scan)");
        code.AppendLine("        if (_inMemoryTable != null)");
        code.AppendLine("        {");
        code.AppendLine("            try");
        code.AppendLine("            {");

        // OPTIMIZATION: For single-property queries, use GetByPropertyAsync which only scans once
        // For multi-property queries, we still use LINQ but it's typically rare
        if (namedQuery.PropertyNames.Count == 1 && namedQuery.AutoFilterDeleted)
        {
            string propName = namedQuery.PropertyNames[0];
            string paramVarName = ToCamelCase(propName);

            if (namedQuery.IsSingle)
            {
                code.AppendLine($"                // PERFORMANCE: Direct property lookup instead of full table scan");
                code.AppendLine($"                var inMemoryResult = await _inMemoryTable.GetByPropertyAsync(\"{propName}\", {paramVarName}, cancellationToken);");
                code.AppendLine($"                if (inMemoryResult != null)");
                code.AppendLine($"                {{");
                code.AppendLine($"                    Logger.LogDebug(\"Named query {namedQuery.Name} (in-memory) returned 1 result\");");
                code.AppendLine($"                    DataSourceLogger.LogMemoryRead(Logger, \"{_metadata.TableName}\", \"{namedQuery.Name}\", null, 1);");
                code.AppendLine($"                    return inMemoryResult;");
                code.AppendLine($"                }}");
            }
            else
            {
                code.AppendLine($"                // PERFORMANCE: Direct property lookup for list query");
                code.AppendLine($"                var inMemoryResults = await _inMemoryTable.GetByPropertyAsync(\"{propName}\", {paramVarName}, true, cancellationToken);");
                code.AppendLine($"                if (inMemoryResults.Count > 0)");
                code.AppendLine($"                {{");
                code.AppendLine($"                    Logger.LogDebug(\"Named query {namedQuery.Name} (in-memory) returned {{Count}} results\", inMemoryResults.Count);");
                code.AppendLine($"                    DataSourceLogger.LogMemoryRead(Logger, \"{_metadata.TableName}\", \"{namedQuery.Name}\", null, inMemoryResults.Count);");
                code.AppendLine($"                    return inMemoryResults;");
                code.AppendLine($"                }}");
            }
        }
        else
        {
            // Multi-property query - use LINQ predicate (less common path)
            string predicate = BuildLinqPredicate(namedQuery);

            if (namedQuery.IsSingle)
            {
                code.AppendLine($"                // Multi-property query: using LINQ filter");
                code.AppendLine($"                var inMemoryResult = _inMemoryTable.Select().FirstOrDefault(e => {predicate});");
                code.AppendLine($"                if (inMemoryResult != null)");
                code.AppendLine($"                {{");
                code.AppendLine($"                    Logger.LogDebug(\"Named query {namedQuery.Name} (in-memory) returned 1 result\");");
                code.AppendLine($"                    DataSourceLogger.LogMemoryRead(Logger, \"{_metadata.TableName}\", \"{namedQuery.Name}\", null, 1);");
                code.AppendLine($"                    return inMemoryResult;");
                code.AppendLine($"                }}");
            }
            else
            {
                code.AppendLine($"                // Multi-property query: using LINQ filter");
                code.AppendLine($"                var inMemoryResults = _inMemoryTable.Select().Where(e => {predicate}).ToList();");
                code.AppendLine($"                if (inMemoryResults.Count > 0)");
                code.AppendLine($"                {{");
                code.AppendLine($"                    Logger.LogDebug(\"Named query {namedQuery.Name} (in-memory) returned {{Count}} results\", inMemoryResults.Count);");
                code.AppendLine($"                    DataSourceLogger.LogMemoryRead(Logger, \"{_metadata.TableName}\", \"{namedQuery.Name}\", null, inMemoryResults.Count);");
                code.AppendLine($"                    return inMemoryResults;");
                code.AppendLine($"                }}");
            }
        }

        code.AppendLine("            }");
        code.AppendLine("            catch (Exception ex)");
        code.AppendLine("            {");
        code.AppendLine($"                Logger.LogWarning(ex, \"In-memory query failed for {namedQuery.Name}, falling back to database\");");
        code.AppendLine("            }");
        code.AppendLine("        }");
        code.AppendLine();

        // Fallback to next priority
        code.AppendLine("        // In-memory empty or failed, fall back to next priority");

        // If we have in-memory table, skip expensive GetAllAsync - go straight to SQL query
        if (_metadata.HasInMemoryTable)
        {
            // Don't use GenerateCachedGetAllFilter as it calls GetAllAsync
            // Go straight to SQL to avoid repeated full table scans
            string constantName = $"SQL_GET_{ToUpperSnakeCase(namedQuery.Name)}";
            GenerateSqlQueryPath(code, namedQuery, constantName);
        }
        else if (_metadata.HasMemoryMappedTable)
        {
            GenerateL0CacheFilter(code, namedQuery);
        }
        else if (_metadata.HasCache)
        {
            GenerateCachedGetAllFilter(code, namedQuery);
        }
        else
        {
            string constantName = $"SQL_GET_{ToUpperSnakeCase(namedQuery.Name)}";
            GenerateSqlQueryPath(code, namedQuery, constantName);
        }
    }

    /// <summary>
    /// Generates L0 cache (in-memory) filtering code for named queries
    /// </summary>
    private void GenerateL0CacheFilter(StringBuilder code, NamedQueryMetadata namedQuery)
    {
        // Build the predicate
        string predicate = BuildLinqPredicate(namedQuery);

        code.AppendLine("        // Filter from L0 in-memory cache (no database round-trip)");
        code.AppendLine("        if (_l0Cache.Count > 0)");
        code.AppendLine("        {");

        if (namedQuery.IsSingle)
        {
            code.AppendLine($"            var cachedResult = _l0Cache.Values.FirstOrDefault(e => {predicate});");
            code.AppendLine($"            Logger.LogDebug(\"Named query {namedQuery.Name} (L0 cache) returned {{Found}}\", cachedResult != null ? \"1 result\" : \"no results\");");
            code.AppendLine("            return cachedResult;");
        }
        else
        {
            code.AppendLine($"            var cachedResults = _l0Cache.Values.Where(e => {predicate}).ToList();");
            code.AppendLine($"            Logger.LogDebug(\"Named query {namedQuery.Name} (L0 cache) returned {{Count}} results\", cachedResults.Count);");
            code.AppendLine("            return cachedResults;");
        }

        code.AppendLine("        }");
        code.AppendLine();

        // Fallback to SQL if L0 cache is empty
        code.AppendLine("        // L0 cache empty, fall back to database query");
        string constantName = $"SQL_GET_{ToUpperSnakeCase(namedQuery.Name)}";
        GenerateSqlQueryPath(code, namedQuery, constantName);
    }

    /// <summary>
    /// Generates cached GetAllAsync filtering for reference tables
    /// </summary>
    private void GenerateCachedGetAllFilter(StringBuilder code, NamedQueryMetadata namedQuery)
    {
        code.AppendLine("        // Filter from cached reference data (uses GetAllAsync cache)");
        code.AppendLine($"        var allEntities = await GetAllAsync(cancellationToken);");
        code.AppendLine();

        // Build the predicate
        string predicate = BuildLinqPredicate(namedQuery);

        if (namedQuery.IsSingle)
        {
            code.AppendLine($"        var result = allEntities.FirstOrDefault(e => {predicate});");
            code.AppendLine($"        Logger.LogDebug(\"Named query {namedQuery.Name} (cached) returned {{Found}}\", result != null ? \"1 result\" : \"no results\");");
            code.AppendLine("        return result;");
        }
        else
        {
            code.AppendLine($"        var results = allEntities.Where(e => {predicate}).ToList();");
            code.AppendLine($"        Logger.LogDebug(\"Named query {namedQuery.Name} (cached) returned {{Count}} results\", results.Count);");
            code.AppendLine("        return results;");
        }
    }

    /// <summary>
    /// Generates standard SQL query path
    /// </summary>
    private void GenerateSqlQueryPath(StringBuilder code, NamedQueryMetadata namedQuery, string constantName)
    {
        // Build parameters dictionary
        code.AppendLine("        Dictionary<string, object> parameters = new Dictionary<string, object>");
        code.AppendLine("        {");

        foreach (string propName in namedQuery.PropertyNames)
        {
            PropertyMetadata? prop = _metadata.Properties.FirstOrDefault(p =>
                p.PropertyName.Equals(propName, System.StringComparison.OrdinalIgnoreCase));

            bool isNullable = prop?.IsNullable ?? false;
            string paramVarName = ToCamelCase(propName);

            if (isNullable)
            {
                code.AppendLine($"            {{ \"{propName}\", {paramVarName} ?? (object)DBNull.Value }},");
            }
            else
            {
                code.AppendLine($"            {{ \"{propName}\", {paramVarName} }},");
            }
        }

        code.AppendLine("        };");
        code.AppendLine();

        // Execute query
        code.AppendLine($"        List<{_metadata.ClassName}> results = await ExecuteQueryAsync(");
        code.AppendLine($"            {constantName},");
        code.AppendLine("            MapFromReader,");
        code.AppendLine("            parameters,");
        code.AppendLine("            transaction: null,");
        code.AppendLine("            cancellationToken);");
        code.AppendLine();

        if (namedQuery.IsSingle)
        {
            code.AppendLine($"        {_metadata.ClassName}? result = results.FirstOrDefault();");
            code.AppendLine($"        Logger.LogDebug(\"Named query {namedQuery.Name} returned {{Found}}\", result != null ? \"1 result\" : \"no results\");");
            code.AppendLine("        return result;");
        }
        else
        {
            code.AppendLine($"        Logger.LogDebug(\"Named query {namedQuery.Name} returned {{Count}} results\", results.Count);");
            code.AppendLine("        return results;");
        }
    }

    /// <summary>
    /// Builds a LINQ predicate expression for in-memory filtering
    /// </summary>
    private string BuildLinqPredicate(NamedQueryMetadata namedQuery)
    {
        var conditions = new List<string>();

        foreach (string propName in namedQuery.PropertyNames)
        {
            PropertyMetadata? prop = _metadata.Properties.FirstOrDefault(p =>
                p.PropertyName.Equals(propName, System.StringComparison.OrdinalIgnoreCase));

            string paramVarName = ToCamelCase(propName);
            bool isNullable = prop?.IsNullable ?? false;
            // Check if type is string (handles string, string?, String, String?)
            string propType = prop?.PropertyType ?? "";
            bool isString = propType.StartsWith("string", StringComparison.OrdinalIgnoreCase) ||
                           propType.StartsWith("String", StringComparison.Ordinal) ||
                           propType.Contains("System.String", StringComparison.Ordinal);

            if (isString)
            {
                // Case-insensitive string comparison
                if (isNullable)
                {
                    conditions.Add($"string.Equals(e.{propName}, {paramVarName}, StringComparison.OrdinalIgnoreCase)");
                }
                else
                {
                    conditions.Add($"string.Equals(e.{propName}, {paramVarName}, StringComparison.OrdinalIgnoreCase)");
                }
            }
            else
            {
                // Direct equality comparison
                conditions.Add($"e.{propName} == {paramVarName}");
            }
        }

        // Add soft delete filter if enabled
        if (namedQuery.AutoFilterDeleted && _metadata.HasSoftDelete)
        {
            bool hasIsDeletedParam = namedQuery.PropertyNames.Any(p =>
                p.Equals("IsDeleted", System.StringComparison.OrdinalIgnoreCase));

            if (!hasIsDeletedParam)
            {
                conditions.Add("!e.IsDeleted");
            }
        }

        return string.Join(" && ", conditions);
    }

    /// <summary>
    /// Generates an "Active" helper method for queries that don't auto-filter deleted records
    /// </summary>
    private void GenerateNamedQueryActiveHelperMethod(StringBuilder code, NamedQueryMetadata namedQuery)
    {
        string methodName = $"GetActive{namedQuery.Name}Async";
        string baseMethodName = $"Get{namedQuery.Name}Async";
        string returnType = namedQuery.IsSingle
            ? $"{_metadata.ClassName}?"
            : $"List<{_metadata.ClassName}>";

        // Build parameter list (without IsDeleted)
        var parametersWithoutIsDeleted = namedQuery.PropertyNames
            .Where(p => !p.Equals("IsDeleted", System.StringComparison.OrdinalIgnoreCase))
            .ToList();

        var parameters = BuildMethodParametersFromList(parametersWithoutIsDeleted);

        code.AppendLine("    /// <summary>");
        code.AppendLine($"    /// Gets active (non-deleted) {_metadata.ClassName} entities by {string.Join(" and ", parametersWithoutIsDeleted)}");
        code.AppendLine("    /// This is a helper that calls the base query with IsDeleted = false");
        code.AppendLine("    /// </summary>");
        code.AppendLine($"    public async Task<{returnType}> {methodName}({parameters}, CancellationToken cancellationToken = default)");
        code.AppendLine("    {");

        // Call the base method with IsDeleted = false
        var callParams = parametersWithoutIsDeleted
            .Select(p => ToCamelCase(p))
            .ToList();

        // Add IsDeleted = false if the original query includes it
        if (namedQuery.PropertyNames.Any(p => p.Equals("IsDeleted", System.StringComparison.OrdinalIgnoreCase)))
        {
            // Find the position of IsDeleted in the original params
            var allParams = new List<string>();
            foreach (string propName in namedQuery.PropertyNames)
            {
                if (propName.Equals("IsDeleted", System.StringComparison.OrdinalIgnoreCase))
                {
                    allParams.Add("false");
                }
                else
                {
                    allParams.Add(ToCamelCase(propName));
                }
            }
            code.AppendLine($"        return await {baseMethodName}({string.Join(", ", allParams)}, cancellationToken);");
        }
        else
        {
            // The base query doesn't have IsDeleted param, but this helper should filter
            // This case shouldn't normally happen since AutoFilterDeleted=false means the query doesn't auto-filter
            code.AppendLine($"        return await {baseMethodName}({string.Join(", ", callParams)}, cancellationToken);");
        }

        code.AppendLine("    }");
    }

    /// <summary>
    /// Generates a documentation helper that just calls the base method (for clarity that it filters deleted)
    /// </summary>
    private void GenerateNamedQueryActiveDocHelperMethod(StringBuilder code, NamedQueryMetadata namedQuery)
    {
        string methodName = $"GetActive{namedQuery.Name}Async";
        string baseMethodName = $"Get{namedQuery.Name}Async";
        string returnType = namedQuery.IsSingle
            ? $"{_metadata.ClassName}?"
            : $"List<{_metadata.ClassName}>";

        var parameters = BuildMethodParameters(namedQuery);

        code.AppendLine("    /// <summary>");
        code.AppendLine($"    /// Gets active (non-deleted) {_metadata.ClassName} entities by {string.Join(" and ", namedQuery.PropertyNames)}");
        code.AppendLine("    /// This is an alias for the base query which already filters out deleted records");
        code.AppendLine("    /// </summary>");
        code.AppendLine($"    public Task<{returnType}> {methodName}({parameters}, CancellationToken cancellationToken = default)");
        code.AppendLine("    {");

        var callParams = namedQuery.PropertyNames.Select(p => ToCamelCase(p)).ToList();
        code.AppendLine($"        return {baseMethodName}({string.Join(", ", callParams)}, cancellationToken);");

        code.AppendLine("    }");
    }

    /// <summary>
    /// Builds method parameter string from a named query
    /// </summary>
    private string BuildMethodParameters(NamedQueryMetadata namedQuery)
    {
        return BuildMethodParametersFromList(namedQuery.PropertyNames);
    }

    /// <summary>
    /// Builds method parameter string from a list of property names
    /// </summary>
    private string BuildMethodParametersFromList(List<string> propertyNames)
    {
        var paramParts = new List<string>();

        foreach (string propName in propertyNames)
        {
            PropertyMetadata? prop = _metadata.Properties.FirstOrDefault(p =>
                p.PropertyName.Equals(propName, System.StringComparison.OrdinalIgnoreCase));

            string paramType;
            if (prop != null)
            {
                paramType = prop.PropertyType;
                // Normalize the type for method signature
                paramType = NormalizeTypeName(paramType);
            }
            else
            {
                // Fallback: assume string for unknown properties
                paramType = "string?";
            }

            string paramVarName = ToCamelCase(propName);
            paramParts.Add($"{paramType} {paramVarName}");
        }

        return string.Join(", ", paramParts);
    }

    /// <summary>
    /// Converts a name to UPPER_SNAKE_CASE for SQL constant names
    /// </summary>
    private static string ToUpperSnakeCase(string name)
    {
        var result = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (char.IsUpper(c) && i > 0)
            {
                result.Append('_');
            }
            result.Append(char.ToUpperInvariant(c));
        }
        return result.ToString();
    }

    /// <summary>
    /// Converts a name to camelCase for parameter names
    /// </summary>
    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;
        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    /// <summary>
    /// Normalizes a type name for method signatures
    /// </summary>
    private static string NormalizeTypeName(string typeName)
    {
        // Remove System. prefix if present
        if (typeName.StartsWith("System."))
        {
            typeName = typeName.Substring(7);
        }

        // Common type mappings
        return typeName switch
        {
            "Int32" => "int",
            "Int64" => "long",
            "Int16" => "short",
            "Boolean" => "bool",
            "String" => "string",
            "Decimal" => "decimal",
            "Double" => "double",
            "Single" => "float",
            _ => typeName
        };
    }
}
