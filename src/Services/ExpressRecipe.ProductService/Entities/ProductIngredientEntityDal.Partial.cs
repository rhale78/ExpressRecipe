using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.ProductService.Entities;

/// <summary>
/// Partial extension to ProductIngredientEntityDal with named query methods
/// </summary>
public sealed partial class ProductIngredientEntityDal
{
    private readonly ILogger? _partialLogger;

    /// <summary>
    /// Gets all ingredient links for a specific product
    /// Filters out soft-deleted entities
    /// </summary>
    public async Task<List<ProductIngredientEntity>> GetByProductIdAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Getting product ingredients for product: {ProductId}", productId);

        try
        {
            // Use WHERE clause to find all matching entities
            var whereClause = $"[ProductId] = '{productId:N}'";
            var result = new List<ProductIngredientEntity>();

            // Check if InMemoryTable is configured
            if (_inMemoryTable != null)
            {
                await _inMemoryTable.ExecuteQueryAsync(result, whereClause);
                return result.Where(e => !e.IsDeleted).ToList();
            }

            // Fall back to database query
            const string sql = @"
                SELECT * FROM [ProductIngredient]
                WHERE [ProductId] = @ProductId AND [IsDeleted] = 0
                ORDER BY [SequenceOrder]";

            var parameters = new[] { CreateSqlParameter("@ProductId", productId) };
            result = await ExecuteReaderAsync(sql, MapDataReaderToEntity, parameters);
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting product ingredients for product {ProductId}", productId);
            throw;
        }
    }
}
