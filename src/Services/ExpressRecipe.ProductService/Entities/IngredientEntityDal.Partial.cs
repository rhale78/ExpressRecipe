using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.ProductService.Entities;

/// <summary>
/// Partial extension to IngredientEntityDal with named query methods
/// </summary>
public sealed partial class IngredientEntityDal
{
    private readonly ILogger? _partialLogger;

    /// <summary>
    /// Gets all ingredients matching a specific category (case-insensitive)
    /// Filters out soft-deleted entities
    /// </summary>
    public async Task<List<IngredientEntity>> GetByCategoryAsync(
        string category,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(category))
            return new List<IngredientEntity>();

        Logger.LogInformation("Getting ingredients by category: '{Category}'", category);

        try
        {
            // Use WHERE clause to find all matching entities
            var whereClause = $"[Category] = '{category.Replace("'", "''")}'";
            var result = new List<IngredientEntity>();

            // Check if InMemoryTable is configured
            if (_inMemoryTable != null)
            {
                await _inMemoryTable.ExecuteQueryAsync(result, whereClause);
                return result.Where(e => !e.IsDeleted).ToList();
            }

            // Fall back to database query
            const string sql = @"
                SELECT * FROM [Ingredient]
                WHERE [Category] = @Category AND [IsDeleted] = 0";

            var parameters = new[] { CreateSqlParameter("@Category", category) };
            result = await ExecuteReaderAsync(sql, MapDataReaderToEntity, parameters);
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting ingredients by category '{Category}'", category);
            throw;
        }
    }

    /// <summary>
    /// Gets a single ingredient by name (case-insensitive)
    /// Returns null if not found or soft-deleted
    /// </summary>
    public async Task<IngredientEntity?> GetByNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        Logger.LogInformation("Getting ingredient by name: '{Name}'", name);

        try
        {
            // Use WHERE clause to find matching entity
            var whereClause = $"[Name] = '{name.Replace("'", "''")}'";
            var result = new List<IngredientEntity>();

            // Check if InMemoryTable is configured
            if (_inMemoryTable != null)
            {
                await _inMemoryTable.ExecuteQueryAsync(result, whereClause);
                var entity = result.FirstOrDefault(e => !e.IsDeleted);
                if (entity != null)
                    return entity;
            }

            // Fall back to database query
            const string sql = @"
                SELECT TOP 1 * FROM [Ingredient]
                WHERE [Name] = @Name AND [IsDeleted] = 0";

            var parameters = new[] { CreateSqlParameter("@Name", name) };
            var results = await ExecuteReaderAsync(sql, MapDataReaderToEntity, parameters);
            return results.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting ingredient by name '{Name}'", name);
            throw;
        }
    }
}
