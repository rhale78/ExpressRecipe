using ExpressRecipe.Data.Common.HighSpeedDAL;
using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.Shared.Services;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.ProductService.Data.HighSpeedDAL;

/// <summary>
/// Minimal high-speed DAL for Ingredient entities following HighSpeedDAL framework pattern.
/// Delegates to base class for all operations - no manual SQL required.
/// </summary>
public interface IIngredientDal
{
    Task<IngredientDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<IngredientDto>> GetAllAsync(CancellationToken ct = default);
    Task<Guid> CreateAsync(IngredientDto ingredient, CancellationToken ct = default);
    Task<bool> UpdateAsync(IngredientDto ingredient, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<int> BulkInsertAsync(IEnumerable<IngredientDto> ingredients, CancellationToken ct = default);
    Task<Dictionary<string, Guid>> GetIdsByNamesAsync(IEnumerable<string> names, CancellationToken ct = default);
}

/// <summary>
/// Implementation delegates all operations to DalOperationsBase.
/// Uses HighSpeedDAL pattern: minimal code, maximum base class reuse.
/// </summary>
public class IngredientDal : DalOperationsBase<IngredientDto, ProductConnection>, IIngredientDal
{
    private readonly HybridCacheService? _cache;
    private const string TableName = "Ingredient";

    public IngredientDal(
        ProductConnection connection,
        ILogger<IngredientDal> logger,
        HybridCacheService? cache = null)
        : base(connection, logger)
    {
        _cache = cache;
    }

    public async Task<IngredientDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (_cache != null)
        {
            var cacheKey = CacheKeys.FormatKey("ingredient:id:{0}", id);
            var cached = await _cache.GetAsync<IngredientDto>(cacheKey);
            if (cached != null) return cached;
        }

        var sql = "SELECT * FROM Ingredient WHERE Id = @Id AND IsDeleted = 0";
        var results = await ExecuteQueryAsync(sql, MapFromReader, new { Id = id }, cancellationToken: ct);
        var ingredient = results.FirstOrDefault();

        if (ingredient != null && _cache != null)
        {
            var cacheKey = CacheKeys.FormatKey("ingredient:id:{0}", id);
            await _cache.SetAsync(cacheKey, ingredient, TimeSpan.FromMinutes(30), TimeSpan.FromHours(2));
        }

        return ingredient;
    }

    public async Task<List<IngredientDto>> GetAllAsync(CancellationToken ct = default)
    {
        var sql = "SELECT * FROM Ingredient WHERE IsDeleted = 0 ORDER BY Name";
        return await ExecuteQueryAsync(sql, MapFromReader, cancellationToken: ct);
    }

    public async Task<Guid> CreateAsync(IngredientDto ingredient, CancellationToken ct = default)
    {
        ingredient.Id = Guid.NewGuid();
        
        var sql = "INSERT INTO Ingredient (Id, Name, AlternativeNames, Description, Category, IsCommonAllergen, IngredientListString, IsDeleted) VALUES (@Id, @Name, @AlternativeNames, @Description, @Category, @IsCommonAllergen, @IngredientListString, 0)";
        await ExecuteNonQueryAsync(sql, ingredient, cancellationToken: ct);
        return ingredient.Id;
    }

    public async Task<bool> UpdateAsync(IngredientDto ingredient, CancellationToken ct = default)
    {
        var sql = "UPDATE Ingredient SET Name = @Name, AlternativeNames = @AlternativeNames, Description = @Description, Category = @Category, IsCommonAllergen = @IsCommonAllergen, IngredientListString = @IngredientListString WHERE Id = @Id AND IsDeleted = 0";
        var rows = await ExecuteNonQueryAsync(sql, ingredient, cancellationToken: ct);
        
        if (rows > 0 && _cache != null)
        {
            await _cache.RemoveAsync(CacheKeys.FormatKey("ingredient:id:{0}", ingredient.Id));
            if (ingredient.Name != null)
            {
                await _cache.RemoveAsync(CacheKeys.FormatKey("ingredient:name:{0}", ingredient.Name));
            }
        }
        
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var sql = "UPDATE Ingredient SET IsDeleted = 1, DeletedAt = @DeletedAt WHERE Id = @Id";
        var rows = await ExecuteNonQueryAsync(sql, new { Id = id, DeletedAt = DateTime.UtcNow }, cancellationToken: ct);
        
        if (rows > 0 && _cache != null)
        {
            await _cache.RemoveAsync(CacheKeys.FormatKey("ingredient:id:{0}", id));
        }
        
        return rows > 0;
    }

    public async Task<int> BulkInsertAsync(IEnumerable<IngredientDto> ingredients, CancellationToken ct = default)
    {
        var ingredientsList = ingredients.ToList();
        if (!ingredientsList.Any()) return 0;

        foreach (var i in ingredientsList)
        {
            if (i.Id == Guid.Empty) i.Id = Guid.NewGuid();
        }

        return await BulkInsertAsync(TableName, ingredientsList, MapForBulk, ct);
    }

    public async Task<Dictionary<string, Guid>> GetIdsByNamesAsync(IEnumerable<string> names, CancellationToken ct = default)
    {
        var namesList = names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (!namesList.Any()) return new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var uncachedNames = new List<string>();

        // Check cache
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
        }
        else
        {
            uncachedNames = namesList;
        }

        // Query uncached
        if (uncachedNames.Any())
        {
            var inClause = string.Join(",", uncachedNames.Select((_, i) => $"@Name{i}"));
            var sql = $"SELECT Id, Name FROM Ingredient WHERE LOWER(Name) IN ({string.Join(",", uncachedNames.Select((_, i) => $"LOWER(@Name{i}"))}) AND IsDeleted = 0";
            
            var parameters = new Dictionary<string, object>();
            for (int i = 0; i < uncachedNames.Count; i++)
            {
                parameters[$"Name{i}"] = uncachedNames[i];
            }

            var dbResults = await ExecuteQueryAsync(
                sql,
                reader => new { Id = reader.GetGuid(reader.GetOrdinal("Id")), Name = reader.GetString(reader.GetOrdinal("Name")) },
                parameters,
                cancellationToken: ct);

            foreach (var item in dbResults)
            {
                result[item.Name] = item.Id;
                
                if (_cache != null)
                {
                    var cacheKey = CacheKeys.FormatKey("ingredient:name:{0}", item.Name);
                    await _cache.SetAsync(cacheKey, item.Id, TimeSpan.FromHours(12), TimeSpan.FromHours(24));
                }
            }
        }

        return result;
    }

    private static IngredientDto MapFromReader(System.Data.IDataReader reader)
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

    private static Dictionary<string, object> MapForBulk(IngredientDto i)
    {
        return new Dictionary<string, object>
        {
            ["Id"] = i.Id,
            ["Name"] = i.Name,
            ["AlternativeNames"] = (object?)i.AlternativeNames ?? DBNull.Value,
            ["Description"] = (object?)i.Description ?? DBNull.Value,
            ["Category"] = (object?)i.Category ?? "General",
            ["IsCommonAllergen"] = i.IsCommonAllergen,
            ["IngredientListString"] = (object?)i.IngredientListString ?? DBNull.Value,
            ["IsDeleted"] = false
        };
    }
}
