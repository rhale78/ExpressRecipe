using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.ProductService.Entities;

/// <summary>
/// Partial extension to ProductImageEntityDal with named query methods
/// </summary>
public sealed partial class ProductImageEntityDal
{
    private readonly ILogger? _partialLogger;

    /// <summary>
    /// Gets all images for a specific product
    /// Filters out soft-deleted entities
    /// </summary>
    public async Task<List<ProductImageEntity>> GetByProductIdAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Getting product images for product: {ProductId}", productId);

        try
        {
            // Use WHERE clause to find all matching entities
            var whereClause = $"[ProductId] = '{productId:N}'";
            var result = new List<ProductImageEntity>();

            // Check if InMemoryTable is configured
            if (_inMemoryTable != null)
            {
                await _inMemoryTable.ExecuteQueryAsync(result, whereClause);
                return result.Where(e => !e.IsDeleted).ToList();
            }

            // Fall back to database query
            const string sql = @"
                SELECT * FROM [ProductImage]
                WHERE [ProductId] = @ProductId AND [IsDeleted] = 0
                ORDER BY [DisplayOrder], [CreatedDate]";

            var parameters = new[] { CreateSqlParameter("@ProductId", productId) };
            result = await ExecuteReaderAsync(sql, MapDataReaderToEntity, parameters);
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting product images for product {ProductId}", productId);
            throw;
        }
    }

    /// <summary>
    /// Gets all images for a specific product with optional primary flag filter
    /// Filters out soft-deleted entities
    /// </summary>
    public async Task<List<ProductImageEntity>> GetByProductIdAndPrimaryAsync(
        Guid productId,
        bool isPrimary,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInformation(
            "Getting product images for product {ProductId} with IsPrimary={IsPrimary}",
            productId, isPrimary);

        try
        {
            // Use WHERE clause to find all matching entities
            var isPrimaryInt = isPrimary ? 1 : 0;
            var whereClause = $"[ProductId] = '{productId:N}' AND [IsPrimary] = {isPrimaryInt}";
            var result = new List<ProductImageEntity>();

            // Check if InMemoryTable is configured
            if (_inMemoryTable != null)
            {
                await _inMemoryTable.ExecuteQueryAsync(result, whereClause);
                return result.Where(e => !e.IsDeleted).ToList();
            }

            // Fall back to database query
            const string sql = @"
                SELECT * FROM [ProductImage]
                WHERE [ProductId] = @ProductId AND [IsPrimary] = @IsPrimary AND [IsDeleted] = 0
                ORDER BY [DisplayOrder], [CreatedDate]";

            var parameters = new[]
            {
                CreateSqlParameter("@ProductId", productId),
                CreateSqlParameter("@IsPrimary", isPrimary)
            };
            result = await ExecuteReaderAsync(sql, MapDataReaderToEntity, parameters);
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "Error getting product images for product {ProductId} with IsPrimary={IsPrimary}",
                productId, isPrimary);
            throw;
        }
    }
}
