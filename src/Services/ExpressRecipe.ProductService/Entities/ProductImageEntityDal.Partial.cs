using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ExpressRecipe.ProductService.Entities
{
    /// <summary>
    /// Partial extension to ProductImageEntityDal with named query methods
    /// Uses indexed database queries on ProductId for performance
    /// </summary>
    public sealed partial class ProductImageEntityDal
    {
        /// <summary>
        /// Gets all images for a specific product using indexed database query
        /// </summary>
        public async Task<List<ProductImageEntity>> GetByProductIdAsync(
            Guid productId,
            CancellationToken cancellationToken = default)
        {
            const string sql = @"
            SELECT * FROM [ProductImage]
            WHERE [ProductId] = @ProductId AND [IsDeleted] = 0
            ORDER BY [DisplayOrder], [CreatedDate]";

            var parameters = new { ProductId = productId };
            return await ExecuteQueryAsync(sql, MapFromReader, parameters, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Gets all images for a specific product with primary flag filter using indexed database query
        /// </summary>
        public async Task<List<ProductImageEntity>> GetByProductIdAndPrimaryAsync(
            Guid productId,
            bool isPrimary,
            CancellationToken cancellationToken = default)
        {
            const string sql = @"
            SELECT * FROM [ProductImage]
            WHERE [ProductId] = @ProductId AND [IsPrimary] = @IsPrimary AND [IsDeleted] = 0
            ORDER BY [DisplayOrder], [CreatedDate]";

            var parameters = new { ProductId = productId, IsPrimary = isPrimary };
            return await ExecuteQueryAsync(sql, MapFromReader, parameters, cancellationToken: cancellationToken);
        }
    }
}
