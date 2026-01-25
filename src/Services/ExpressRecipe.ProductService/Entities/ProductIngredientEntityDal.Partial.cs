using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ExpressRecipe.ProductService.Entities
{
    /// <summary>
    /// Partial extension to ProductIngredientEntityDal with named query methods
    /// Uses indexed database queries on ProductId for performance
    /// </summary>
    public sealed partial class ProductIngredientEntityDal
    {
        /// <summary>
        /// Gets all ingredient links for a specific product using indexed database query
        /// </summary>
        public async Task<List<ProductIngredientEntity>> GetByProductIdAsync(
            Guid productId,
            CancellationToken cancellationToken = default)
        {
            const string sql = @"
            SELECT * FROM [ProductIngredient]
            WHERE [ProductId] = @ProductId AND [IsDeleted] = 0
            ORDER BY [OrderIndex]";

            var parameters = new { ProductId = productId };
            return await ExecuteQueryAsync(sql, MapFromReader, parameters, cancellationToken: cancellationToken);
        }
    }
}
