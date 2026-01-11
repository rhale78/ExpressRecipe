using ExpressRecipe.Data.Common.HighSpeedDAL;
using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.Shared.Services;
using Microsoft.Extensions.Logging;
using System.Data;

namespace ExpressRecipe.ProductService.Data.HighSpeedDAL;

/// <summary>
/// High-speed DAL for Ingredient entities following HighSpeedDAL framework patterns.
/// Provides optimized bulk operations, intelligent caching, and retry logic.
/// </summary>
public interface IIngredientDal
{
    // Single operations
    Task<IngredientDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<IngredientDto>> SearchByNameAsync(string searchTerm, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateIngredientRequest request, Guid? createdBy = null, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Guid id, UpdateIngredientRequest request, Guid? updatedBy = null, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, Guid? deletedBy = null, CancellationToken cancellationToken = default);

    // Bulk operations (HighSpeedDAL patterns)
    Task<int> BulkInsertAsync(IEnumerable<CreateIngredientRequest> ingredients, CancellationToken cancellationToken = default);
    Task<Dictionary<Guid, IngredientDto>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    Task<Dictionary<string, Guid>> GetIdsByNamesAsync(IEnumerable<string> names, CancellationToken cancellationToken = default);
    
    // Cache operations
    Task InvalidateCacheAsync(Guid id);
    Task InvalidateCacheByNameAsync(string name);
}

public class IngredientDal : DalOperationsBase<IngredientDto, ProductConnection>, IIngredientDal
{
    private readonly HybridCacheService? _cache;

    public IngredientDal(
        ProductConnection connection,
        ILogger<IngredientDal> logger,
        HybridCacheService? cache = null)
        : base(connection, logger)
    {
        _cache = cache;
    }

    #region Single Operations

    public async Task<IngredientDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Check cache first
        if (_cache != null)
        {
            var cacheKey = CacheKeys.FormatKey("ingredient:id:{0}", id);
            var cachedIngredient = await _cache.GetAsync<IngredientDto>(cacheKey);
            if (cachedIngredient != null)
            {
                Logger.LogDebug("Ingredient {Id} retrieved from cache", id);
                return cachedIngredient;
            }
        }

        const string sql = @"
            SELECT Id, Name, AlternativeNames, Description, Category, IsCommonAllergen, IngredientListString
            FROM Ingredient
            WHERE Id = @Id AND IsDeleted = 0";

        var ingredients = await ExecuteQueryAsync(
            sql,
            MapReaderToIngredientDto,
            new { Id = id },
            cancellationToken: cancellationToken);

        var ingredient = ingredients.FirstOrDefault();

        // Cache result
        if (ingredient != null && _cache != null)
        {
            var cacheKey = CacheKeys.FormatKey("ingredient:id:{0}", id);
            await _cache.SetAsync(
                cacheKey,
                ingredient,
                memoryExpiry: TimeSpan.FromMinutes(30),
                distributedExpiry: TimeSpan.FromHours(2));
        }

        return ingredient;
    }

    public async Task<List<IngredientDto>> SearchByNameAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT Id, Name, AlternativeNames, Description, Category, IsCommonAllergen, IngredientListString
            FROM Ingredient
            WHERE (Name LIKE @SearchTerm OR AlternativeNames LIKE @SearchTerm) AND IsDeleted = 0
            ORDER BY Name";

        return await ExecuteQueryAsync(
            sql,
            MapReaderToIngredientDto,
            new { SearchTerm = $"%{searchTerm}%" },
            cancellationToken: cancellationToken);
    }

    public async Task<Guid> CreateAsync(CreateIngredientRequest request, Guid? createdBy = null, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
            INSERT INTO Ingredient (Id, Name, AlternativeNames, Description, Category, IsCommonAllergen, 
                                   IngredientListString, CreatedAt, CreatedBy, IsDeleted)
            VALUES (@Id, @Name, @AlternativeNames, @Description, @Category, @IsCommonAllergen, 
                    @IngredientListString, @CreatedAt, @CreatedBy, 0)";

        await ExecuteNonQueryAsync(sql, new
        {
            Id = id,
            request.Name,
            request.AlternativeNames,
            request.Description,
            Category = request.Category ?? "General",
            request.IsCommonAllergen,
            request.IngredientListString,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = (object?)createdBy
        }, cancellationToken: cancellationToken);

        Logger.LogInformation("Created ingredient {Id} with name {Name}", id, request.Name);
        return id;
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateIngredientRequest request, Guid? updatedBy = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE Ingredient 
            SET Name = @Name, AlternativeNames = @AlternativeNames, Description = @Description, 
                Category = @Category, IsCommonAllergen = @IsCommonAllergen,
                IngredientListString = @IngredientListString,
                UpdatedAt = @UpdatedAt, UpdatedBy = @UpdatedBy
            WHERE Id = @Id AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(sql, new
        {
            Id = id,
            request.Name,
            request.AlternativeNames,
            request.Description,
            Category = request.Category ?? "General",
            request.IsCommonAllergen,
            request.IngredientListString,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = (object?)updatedBy
        }, cancellationToken: cancellationToken);

        if (rowsAffected > 0)
        {
            await InvalidateCacheAsync(id);
            if (request.Name != null)
            {
                await InvalidateCacheByNameAsync(request.Name);
            }
            Logger.LogInformation("Updated ingredient {Id}", id);
            return true;
        }

        return false;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid? deletedBy = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE Ingredient 
            SET IsDeleted = 1, DeletedAt = @DeletedAt, UpdatedBy = @DeletedBy
            WHERE Id = @Id AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(sql, new
        {
            Id = id,
            DeletedAt = DateTime.UtcNow,
            DeletedBy = (object?)deletedBy
        }, cancellationToken: cancellationToken);

        if (rowsAffected > 0)
        {
            await InvalidateCacheAsync(id);
            Logger.LogInformation("Soft deleted ingredient {Id}", id);
            return true;
        }

        return false;
    }

    #endregion

    #region Bulk Operations

    public async Task<int> BulkInsertAsync(IEnumerable<CreateIngredientRequest> ingredients, CancellationToken cancellationToken = default)
    {
        var ingredientsList = ingredients.ToList();
        if (!ingredientsList.Any()) return 0;

        Logger.LogInformation("Bulk inserting {Count} ingredients using HighSpeedDAL pattern", ingredientsList.Count);

        // Use HighSpeedDAL BulkInsertAsync from base class
        return await BulkInsertAsync(
            "Ingredient",
            ingredientsList.Select(i => new IngredientDto
            {
                Id = Guid.NewGuid(),
                Name = i.Name,
                AlternativeNames = i.AlternativeNames,
                Description = i.Description,
                Category = i.Category ?? "General",
                IsCommonAllergen = i.IsCommonAllergen,
                IngredientListString = i.IngredientListString
            }),
            ingredient => new Dictionary<string, object>
            {
                ["Id"] = ingredient.Id,
                ["Name"] = ingredient.Name ?? string.Empty,
                ["AlternativeNames"] = (object?)ingredient.AlternativeNames ?? DBNull.Value,
                ["Description"] = (object?)ingredient.Description ?? DBNull.Value,
                ["Category"] = ingredient.Category ?? "General",
                ["IsCommonAllergen"] = ingredient.IsCommonAllergen,
                ["IngredientListString"] = (object?)ingredient.IngredientListString ?? DBNull.Value,
                ["CreatedAt"] = DateTime.UtcNow,
                ["CreatedBy"] = DBNull.Value,
                ["IsDeleted"] = false
            },
            cancellationToken);
    }

    public async Task<Dictionary<Guid, IngredientDto>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idsList = ids.Distinct().ToList();
        if (!idsList.Any()) return new Dictionary<Guid, IngredientDto>();

        // Check cache first for each ID
        var result = new Dictionary<Guid, IngredientDto>();
        var uncachedIds = new List<Guid>();

        if (_cache != null)
        {
            foreach (var id in idsList)
            {
                var cacheKey = CacheKeys.FormatKey("ingredient:id:{0}", id);
                var cachedIngredient = await _cache.GetAsync<IngredientDto>(cacheKey);
                if (cachedIngredient != null)
                {
                    result[id] = cachedIngredient;
                }
                else
                {
                    uncachedIds.Add(id);
                }
            }

            Logger.LogDebug("Ingredient batch lookup: {CacheHits} hits, {CacheMisses} misses", result.Count, uncachedIds.Count);
        }
        else
        {
            uncachedIds = idsList;
        }

        // Fetch uncached ingredients from DB
        if (uncachedIds.Any())
        {
            var dbIngredients = await GetIngredientsByIdsFromDbAsync(uncachedIds, cancellationToken);

            // Cache and add to result
            foreach (var kvp in dbIngredients)
            {
                result[kvp.Key] = kvp.Value;

                if (_cache != null)
                {
                    var cacheKey = CacheKeys.FormatKey("ingredient:id:{0}", kvp.Key);
                    await _cache.SetAsync(
                        cacheKey,
                        kvp.Value,
                        memoryExpiry: TimeSpan.FromMinutes(30),
                        distributedExpiry: TimeSpan.FromHours(2));
                }
            }
        }

        return result;
    }

    public async Task<Dictionary<string, Guid>> GetIdsByNamesAsync(IEnumerable<string> names, CancellationToken cancellationToken = default)
    {
        var namesList = names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (!namesList.Any()) return new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        // Check cache first for each name
        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var uncachedNames = new List<string>();

        if (_cache != null)
        {
            foreach (var name in namesList)
            {
                var cacheKey = CacheKeys.FormatKey("ingredient:name:{0}", name);
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

            Logger.LogDebug("Ingredient name lookup: {CacheHits} hits, {CacheMisses} misses", result.Count, uncachedNames.Count);
        }
        else
        {
            uncachedNames = namesList;
        }

        // Fetch uncached ingredient IDs from DB
        if (uncachedNames.Any())
        {
            var dbIngredientIds = await GetIngredientIdsByNamesFromDbAsync(uncachedNames, cancellationToken);

            // Cache and add to result
            foreach (var kvp in dbIngredientIds)
            {
                result[kvp.Key] = kvp.Value;

                if (_cache != null)
                {
                    var cacheKey = CacheKeys.FormatKey("ingredient:name:{0}", kvp.Key);
                    await _cache.SetAsync(
                        cacheKey,
                        kvp.Value,
                        memoryExpiry: TimeSpan.FromHours(12),
                        distributedExpiry: TimeSpan.FromHours(24));
                }
            }
        }

        return result;
    }

    #endregion

    #region Cache Operations

    public async Task InvalidateCacheAsync(Guid id)
    {
        if (_cache == null) return;

        var cacheKey = CacheKeys.FormatKey("ingredient:id:{0}", id);
        await _cache.RemoveAsync(cacheKey);
        Logger.LogDebug("Invalidated cache for ingredient {Id}", id);
    }

    public async Task InvalidateCacheByNameAsync(string name)
    {
        if (_cache == null) return;

        var cacheKey = CacheKeys.FormatKey("ingredient:name:{0}", name);
        await _cache.RemoveAsync(cacheKey);
        Logger.LogDebug("Invalidated cache for ingredient name {Name}", name);
    }

    #endregion

    #region Helper Methods

    private async Task<Dictionary<Guid, IngredientDto>> GetIngredientsByIdsFromDbAsync(List<Guid> ids, CancellationToken cancellationToken)
    {
        // Use IN clause for batch query
        var inClause = string.Join(",", ids.Select((_, i) => $"@Id{i}"));
        var sql = $@"
            SELECT Id, Name, AlternativeNames, Description, Category, IsCommonAllergen, IngredientListString
            FROM Ingredient
            WHERE Id IN ({inClause}) AND IsDeleted = 0";

        var parameters = new Dictionary<string, object>();
        for (int i = 0; i < ids.Count; i++)
        {
            parameters[$"Id{i}"] = ids[i];
        }

        var ingredients = await ExecuteQueryAsync(sql, MapReaderToIngredientDto, parameters, cancellationToken: cancellationToken);

        var result = new Dictionary<Guid, IngredientDto>();
        foreach (var ingredient in ingredients)
        {
            result[ingredient.Id] = ingredient;
        }

        return result;
    }

    private async Task<Dictionary<string, Guid>> GetIngredientIdsByNamesFromDbAsync(List<string> names, CancellationToken cancellationToken)
    {
        // Use IN clause for batch query
        var inClause = string.Join(",", names.Select((_, i) => $"@Name{i}"));
        var sql = $@"
            SELECT Id, Name
            FROM Ingredient
            WHERE LOWER(Name) IN ({string.Join(",", names.Select((_, i) => $"LOWER(@Name{i})"))}) AND IsDeleted = 0";

        var parameters = new Dictionary<string, object>();
        for (int i = 0; i < names.Count; i++)
        {
            parameters[$"Name{i}"] = names[i];
        }

        var results = await ExecuteQueryAsync(
            sql,
            reader => new
            {
                Id = reader.GetGuid(reader.GetOrdinal("Id")),
                Name = reader.GetString(reader.GetOrdinal("Name"))
            },
            parameters,
            cancellationToken: cancellationToken);

        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in results)
        {
            result[item.Name] = item.Id;
        }

        return result;
    }

    private static IngredientDto MapReaderToIngredientDto(IDataReader reader)
    {
        return new IngredientDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            AlternativeNames = reader.IsDBNull(reader.GetOrdinal("AlternativeNames")) ? null : reader.GetString(reader.GetOrdinal("AlternativeNames")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? null : reader.GetString(reader.GetOrdinal("Category")),
            IsCommonAllergen = reader.GetBoolean(reader.GetOrdinal("IsCommonAllergen")),
            IngredientListString = reader.IsDBNull(reader.GetOrdinal("IngredientListString")) ? null : reader.GetString(reader.GetOrdinal("IngredientListString"))
        };
    }

    #endregion
}
