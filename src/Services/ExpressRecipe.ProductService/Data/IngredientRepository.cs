using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.Shared.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.ProductService.Data;

public class IngredientRepository : SqlHelper, IIngredientRepository
{
    private readonly HybridCacheService? _cache;
    private readonly ILogger<IngredientRepository>? _logger;

    public IngredientRepository(string connectionString) : base(connectionString)
    {
    }

    public IngredientRepository(string connectionString, HybridCacheService cache, ILogger<IngredientRepository> logger) : base(connectionString)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<IngredientDto>> GetAllAsync()
    {
        const string sql = @"
            SELECT Id, Name, AlternativeNames, Description, Category, IsCommonAllergen, IngredientListString
            FROM Ingredient
            WHERE IsDeleted = 0
            ORDER BY Name";

        return await ExecuteReaderAsync(
            sql,
            reader => new IngredientDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                AlternativeNames = GetString(reader, "AlternativeNames"),
                Description = GetString(reader, "Description"),
                Category = GetString(reader, "Category"),
                IsCommonAllergen = GetBoolean(reader, "IsCommonAllergen"),
                IngredientListString = GetString(reader, "IngredientListString")
            });
    }

    public async Task<IngredientDto?> GetByIdAsync(Guid id)
    {
        if (_cache != null)
        {
            var cacheKey = CacheKeys.FormatKey("ingredient:id:{0}", id);
            return await _cache.GetOrSetAsync(
                cacheKey,
                async () => await GetByIdFromDbAsync(id),
                memoryExpiry: TimeSpan.FromMinutes(30),
                distributedExpiry: TimeSpan.FromHours(2)
            );
        }

        return await GetByIdFromDbAsync(id);
    }

    private async Task<IngredientDto?> GetByIdFromDbAsync(Guid id)
    {
        const string sql = @"
            SELECT Id, Name, AlternativeNames, Description, Category, IsCommonAllergen, IngredientListString
            FROM Ingredient
            WHERE Id = @Id AND IsDeleted = 0";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new IngredientDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                AlternativeNames = GetString(reader, "AlternativeNames"),
                Description = GetString(reader, "Description"),
                Category = GetString(reader, "Category"),
                IsCommonAllergen = GetBoolean(reader, "IsCommonAllergen"),
                IngredientListString = GetString(reader, "IngredientListString")
            },
            CreateParameter("@Id", id));

        return results.FirstOrDefault();
    }

    public async Task<List<IngredientDto>> SearchByNameAsync(string searchTerm)
    {
        const string sql = @"
            SELECT Id, Name, AlternativeNames, Description, Category, IsCommonAllergen, IngredientListString
            FROM Ingredient
            WHERE (Name LIKE @SearchTerm OR AlternativeNames LIKE @SearchTerm)
                  AND IsDeleted = 0
            ORDER BY Name";

        return await ExecuteReaderAsync(
            sql,
            reader => new IngredientDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                AlternativeNames = GetString(reader, "AlternativeNames"),
                Description = GetString(reader, "Description"),
                Category = GetString(reader, "Category"),
                IsCommonAllergen = GetBoolean(reader, "IsCommonAllergen"),
                IngredientListString = GetString(reader, "IngredientListString")
            },
            CreateParameter("@SearchTerm", $"%{searchTerm}%"));
    }

    public async Task<List<IngredientDto>> GetByCategoryAsync(string category)
    {
        const string sql = @"
            SELECT Id, Name, AlternativeNames, Description, Category, IsCommonAllergen, IngredientListString
            FROM Ingredient
            WHERE Category = @Category AND IsDeleted = 0
            ORDER BY Name";

        return await ExecuteReaderAsync(
            sql,
            reader => new IngredientDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                AlternativeNames = GetString(reader, "AlternativeNames"),
                Description = GetString(reader, "Description"),
                Category = GetString(reader, "Category"),
                IsCommonAllergen = GetBoolean(reader, "IsCommonAllergen"),
                IngredientListString = GetString(reader, "IngredientListString")
            },
            CreateParameter("@Category", category));
    }

    public async Task<Guid> CreateAsync(CreateIngredientRequest request, Guid? createdBy = null)
    {
        const string sql = @"
            INSERT INTO Ingredient (
                Id, Name, AlternativeNames, Description, Category,
                IsCommonAllergen, IngredientListString, CreatedBy, CreatedDate
            )
            VALUES (
                @Id, @Name, @AlternativeNames, @Description, @Category,
                @IsCommonAllergen, @IngredientListString, @CreatedBy, GETUTCDATE()
            )";

        var ingredientId = Guid.NewGuid();

            // Use 120 second timeout for bulk processing operations to avoid timeouts
            await ExecuteNonQueryAsync(
                sql,
                timeoutSeconds: 120,
                CreateParameter("@Id", ingredientId),
                CreateParameter("@Name", request.Name),
                CreateParameter("@AlternativeNames", request.AlternativeNames),
                CreateParameter("@Description", request.Description),
                CreateParameter("@Category", request.Category),
                CreateParameter("@IsCommonAllergen", request.IsCommonAllergen),
                CreateParameter("@IngredientListString", request.IngredientListString),
                CreateParameter("@CreatedBy", createdBy));

            // Invalidate cache for this ingredient name
            if (_cache != null)
            {
                var cacheKey = CacheKeys.FormatKey("ingredient:name:{0}", request.Name.ToLowerInvariant());
                await _cache.RemoveAsync(cacheKey);
            }

            return ingredientId;
        }

    public async Task<bool> UpdateAsync(Guid id, UpdateIngredientRequest request, Guid? updatedBy = null)
    {
        const string sql = @"
            UPDATE Ingredient
            SET Name = @Name,
                AlternativeNames = @AlternativeNames,
                Description = @Description,
                Category = @Category,
                IsCommonAllergen = @IsCommonAllergen,
                IngredientListString = @IngredientListString,
                UpdatedBy = @UpdatedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", id),
            CreateParameter("@Name", request.Name),
            CreateParameter("@AlternativeNames", request.AlternativeNames),
            CreateParameter("@Description", request.Description),
            CreateParameter("@Category", request.Category),
            CreateParameter("@IsCommonAllergen", request.IsCommonAllergen),
            CreateParameter("@IngredientListString", request.IngredientListString),
            CreateParameter("@UpdatedBy", updatedBy));

        if (rowsAffected > 0 && _cache != null)
        {
            // Invalidate caches for this ingredient
            await _cache.RemoveAsync(CacheKeys.FormatKey("ingredient:id:{0}", id));
            await _cache.RemoveAsync(CacheKeys.FormatKey("ingredient:name:{0}", request.Name.ToLowerInvariant()));
        }

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid? deletedBy = null)
    {
        const string sql = @"
            UPDATE Ingredient
            SET IsDeleted = 1,
                DeletedAt = GETUTCDATE(),
                UpdatedBy = @DeletedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", id),
            CreateParameter("@DeletedBy", deletedBy));

        return rowsAffected > 0;
    }

    public async Task<List<ProductIngredientDto>> GetProductIngredientsAsync(Guid productId)
    {
        const string sql = @"
            SELECT pi.Id, pi.ProductId, pi.IngredientId, i.Name as IngredientName,
                   pi.OrderIndex, pi.Quantity, pi.Notes, pi.IngredientListString
            FROM ProductIngredient pi
            INNER JOIN Ingredient i ON pi.IngredientId = i.Id
            WHERE pi.ProductId = @ProductId AND pi.IsDeleted = 0 AND i.IsDeleted = 0
            ORDER BY pi.OrderIndex, i.Name";

        return await ExecuteReaderAsync(
            sql,
            reader => new ProductIngredientDto
            {
                Id = GetGuid(reader, "Id"),
                ProductId = GetGuid(reader, "ProductId"),
                IngredientId = GetGuid(reader, "IngredientId"),
                IngredientName = GetString(reader, "IngredientName") ?? string.Empty,
                OrderIndex = GetInt32(reader, "OrderIndex"),
                Quantity = GetString(reader, "Quantity"),
                Notes = GetString(reader, "Notes"),
                IngredientListString = GetString(reader, "IngredientListString")
            },
            CreateParameter("@ProductId", productId));
    }

    public async Task<Guid> AddProductIngredientAsync(Guid productId, AddProductIngredientRequest request, Guid? createdBy = null)
    {
        // Check if this combination already exists
        const string checkSql = @"
            SELECT Id FROM ProductIngredient
            WHERE ProductId = @ProductId
              AND IngredientId = @IngredientId
              AND IsDeleted = 0";

        var existingId = await ExecuteScalarAsync<Guid?>(
            checkSql,
            CreateParameter("@ProductId", productId),
            CreateParameter("@IngredientId", request.IngredientId));

        if (existingId.HasValue)
        {
            // Already exists, return the existing ID
            return existingId.Value;
        }

        const string sql = @"
            INSERT INTO ProductIngredient (
                Id, ProductId, IngredientId, OrderIndex, Quantity, Notes, IngredientListString,
                CreatedBy, CreatedDate
            )
            VALUES (
                @Id, @ProductId, @IngredientId, @OrderIndex, @Quantity, @Notes, @IngredientListString,
                @CreatedBy, GETUTCDATE()
            )";

        var productIngredientId = Guid.NewGuid();

        try
        {
            // Use 120 second timeout for bulk processing operations to avoid timeouts
            await ExecuteNonQueryAsync(
                sql,
                timeoutSeconds: 120,
                CreateParameter("@Id", productIngredientId),
                CreateParameter("@ProductId", productId),
                CreateParameter("@IngredientId", request.IngredientId),
                CreateParameter("@OrderIndex", request.OrderIndex),
                CreateParameter("@Quantity", request.Quantity),
                CreateParameter("@Notes", request.Notes),
                CreateParameter("@IngredientListString", request.IngredientListString),
                CreateParameter("@CreatedBy", createdBy));
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
        {
            // Unique constraint violation - race condition occurred
            // Fetch the existing record ID
            var existingRecordId = await ExecuteScalarAsync<Guid?>(
                checkSql,
                CreateParameter("@ProductId", productId),
                CreateParameter("@IngredientId", request.IngredientId));

            if (existingRecordId.HasValue)
            {
                return existingRecordId.Value;
            }

            // If we still can't find it, rethrow
            throw;
        }

        return productIngredientId;
    }

    public async Task<bool> RemoveProductIngredientAsync(Guid productIngredientId, Guid? deletedBy = null)
    {
        const string sql = @"
            UPDATE ProductIngredient
            SET IsDeleted = 1,
                DeletedAt = GETUTCDATE(),
                UpdatedBy = @DeletedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", productIngredientId),
            CreateParameter("@DeletedBy", deletedBy));

        return rowsAffected > 0;
    }

    /// <summary>
    /// Bulk lookup of ingredient IDs by names - CRITICAL for performance
    /// Instead of N individual queries, this does 1 query to get all ingredient IDs
    /// CACHED: Individual ingredient name-to-ID mappings cached for 24 hours
    /// </summary>
    public async Task<Dictionary<string, Guid>> GetIngredientIdsByNamesAsync(IEnumerable<string> names)
    {
        var namesList = names.ToList();
        if (!namesList.Any())
            return new Dictionary<string, Guid>();

        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        // If cache available, check cached values first
        var uncachedNames = new List<string>();
        if (_cache != null)
        {
            foreach (var name in namesList)
            {
                var cacheKey = CacheKeys.FormatKey("ingredient:name:{0}", name.ToLowerInvariant());
                var cachedId = await _cache.GetAsync<Guid?>(cacheKey);
                if (cachedId.HasValue)
                {
                    result[name] = cachedId.Value;
                }
                else
                {
                    uncachedNames.Add(name);
                }
            }

                _logger?.LogDebug("Ingredient cache: {CacheHits} hits, {CacheMisses} misses out of {Total} lookups",
                    result.Count, uncachedNames.Count, namesList.Count);

                if (!uncachedNames.Any())
                    return result; // All found in cache!
            }
            else
            {
                uncachedNames = namesList;
            }

            // Process uncached names in chunks of 1000 to avoid parameter limits
            foreach (var chunk in uncachedNames.Chunk(1000))
        {
            // Build dynamic SQL with parameters
            var parameters = new List<Microsoft.Data.SqlClient.SqlParameter>();
            var conditions = new List<string>();

            for (int i = 0; i < chunk.Length; i++)
            {
                var paramName = $"@Name{i}";
                conditions.Add($"Name = {paramName}");
                parameters.Add((Microsoft.Data.SqlClient.SqlParameter)CreateParameter(paramName, chunk[i]));
            }

            var sql = $@"
                SELECT Name, Id
                FROM Ingredient
                WHERE ({string.Join(" OR ", conditions)})
                  AND IsDeleted = 0";

            var chunkResults = await ExecuteReaderAsync(
                sql,
                reader => new
                {
                    Name = GetString(reader, "Name") ?? string.Empty,
                    Id = GetGuid(reader, "Id")
                },
                parameters.ToArray());

            // Cache each ingredient name-to-ID mapping for future lookups
            if (_cache != null)
            {
                foreach (var item in chunkResults)
                {
                    var cacheKey = CacheKeys.FormatKey("ingredient:name:{0}", item.Name.ToLowerInvariant());
                    await _cache.SetAsync(cacheKey, item.Id, 
                        memoryExpiry: TimeSpan.FromHours(12),
                        distributedExpiry: TimeSpan.FromHours(24));
                }
            }

            foreach (var item in chunkResults)
            {
                if (!result.ContainsKey(item.Name))
                {
                    result[item.Name] = item.Id;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Bulk create ingredients - much faster than individual inserts
    /// Uses Table-Valued Parameters for optimal performance
    /// </summary>
    public async Task<int> BulkCreateIngredientsAsync(IEnumerable<string> names, Guid? createdBy = null)
    {
        var namesList = names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (!namesList.Any())
            return 0;

        int createdCount = 0;

        // Process in batches to avoid overwhelming the database
        foreach (var batch in namesList.Chunk(100))
        {
            var valuesClauses = new List<string>();
            var parameters = new List<Microsoft.Data.SqlClient.SqlParameter>();

            for (int i = 0; i < batch.Length; i++)
            {
                var id = Guid.NewGuid();
                var nameParam = $"@Name{i}";
                var idParam = $"@Id{i}";

                valuesClauses.Add($"({idParam}, {nameParam}, 'General', @CreatedBy, GETUTCDATE())");
                parameters.Add((Microsoft.Data.SqlClient.SqlParameter)CreateParameter(nameParam, batch[i]));
                parameters.Add((Microsoft.Data.SqlClient.SqlParameter)CreateParameter(idParam, id));
            }

            parameters.Add((Microsoft.Data.SqlClient.SqlParameter)CreateParameter("@CreatedBy", createdBy));

            var sql = $@"
                INSERT INTO Ingredient (Id, Name, Category, CreatedBy, CreatedDate)
                VALUES {string.Join(", ", valuesClauses)}";

            try
            {
                var inserted = await ExecuteNonQueryAsync(sql, timeoutSeconds: 120, parameters.ToArray());
                createdCount += inserted;
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                // Some duplicates exist - that's OK, they might have been created concurrently
                // We'll just skip them
            }
        }

        return createdCount;
    }

    public Task<int> BulkAddProductIngredientsAsync(IEnumerable<(Guid ProductId, Guid IngredientId, int OrderIndex)> links)
    {
        throw new NotImplementedException("Use IngredientRepositoryAdapter for bulk operations with HighSpeedDAL");
    }
}
