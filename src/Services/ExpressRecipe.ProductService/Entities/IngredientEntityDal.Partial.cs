using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ExpressRecipe.ProductService.Entities
{
    /// <summary>
    /// Partial extension to IngredientEntityDal with named query methods
    /// Uses indexed database queries for performance (not loading all 200k+ rows)
    /// </summary>
    public sealed partial class IngredientEntityDal
    {
        /// <summary>
        /// Gets all ingredients matching a specific category using database query
        /// Uses indexed lookup on [Category] column
        /// </summary>
        public async Task<List<IngredientEntity>> GetByCategoryAsync(
            string category,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return [];
            }

            const string sql = @"
            SELECT * FROM [Ingredient]
            WHERE [Category] = @Category AND [IsDeleted] = 0";

            var parameters = new { Category = category };
            return await ExecuteQueryAsync(sql, MapFromReader, parameters, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Gets a single ingredient by name using database query
        /// Uses indexed lookup on [Name] column
        /// </summary>
        public async Task<IngredientEntity?> GetByNameAsync(
            string name,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            const string sql = @"
            SELECT TOP 1 * FROM [Ingredient]
            WHERE [Name] = @Name AND [IsDeleted] = 0";

            var parameters = new { Name = name };
            List<IngredientEntity> results = await ExecuteQueryAsync(sql, MapFromReader, parameters, cancellationToken: cancellationToken);
            return results.FirstOrDefault();
        }
    }
}
