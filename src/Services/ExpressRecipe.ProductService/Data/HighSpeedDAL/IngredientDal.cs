using ExpressRecipe.Data.Common.HighSpeedDAL;
using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.Shared.Services;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.ProductService.Data.HighSpeedDAL;

/// <summary>
/// Ultra-minimal HighSpeedDAL for Ingredient - zero manual SQL, all delegated to base.
/// Follows HighSpeedDAL simple CRUD pattern with generic operations.
/// </summary>
public interface IIngredientDal
{
    Task<IngredientDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<IngredientDto>> GetAllAsync(CancellationToken ct = default);
    Task<Guid> SaveAsync(IngredientDto ingredient, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<int> BulkSaveAsync(IEnumerable<IngredientDto> ingredients, CancellationToken ct = default);
}

public class IngredientDal : DalOperationsBase<IngredientDto, ProductConnection>, IIngredientDal
{
    private const string TableName = "Ingredient";
    private readonly HybridCacheService? _cache;

    public IngredientDal(ProductConnection connection, ILogger<IngredientDal> logger, HybridCacheService? cache = null)
        : base(connection, logger)
    {
        _cache = cache;
    }

    public async Task<IngredientDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var cacheKey = $"ingredient:{id}";
        if (_cache != null)
        {
            var cached = await _cache.GetAsync<IngredientDto>(cacheKey);
            if (cached != null) return cached;
        }

        var result = await GetByIdGenericAsync(TableName, id, MapFromReader, ct);
        
        if (result != null && _cache != null)
        {
            await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(30), TimeSpan.FromHours(2));
        }

        return result;
    }

    public Task<List<IngredientDto>> GetAllAsync(CancellationToken ct = default) 
        => GetAllGenericAsync(TableName, MapFromReader, ct);

    public async Task<Guid> SaveAsync(IngredientDto ingredient, CancellationToken ct = default)
    {
        if (ingredient.Id == Guid.Empty)
        {
            ingredient.Id = Guid.NewGuid();
            await InsertGenericAsync(TableName, ingredient, ct);
        }
        else
        {
            await UpdateGenericAsync(TableName, ingredient, ct);
        }
        
        if (_cache != null) await _cache.RemoveAsync($"ingredient:{ingredient.Id}");
        return ingredient.Id;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var success = await SoftDeleteGenericAsync(TableName, id, ct);
        if (success && _cache != null) await _cache.RemoveAsync($"ingredient:{id}");
        return success;
    }

    public async Task<int> BulkSaveAsync(IEnumerable<IngredientDto> ingredients, CancellationToken ct = default)
    {
        var list = ingredients.ToList();
        foreach (var i in list.Where(i => i.Id == Guid.Empty))
        {
            i.Id = Guid.NewGuid();
        }
        return await BulkInsertAsync(TableName, list, MapForBulk, ct);
    }

    private static IngredientDto MapFromReader(System.Data.IDataReader r) => new()
    {
        Id = r.GetGuid(r.GetOrdinal("Id")),
        Name = r.GetString(r.GetOrdinal("Name")),
        AlternativeNames = r.IsDBNull(r.GetOrdinal("AlternativeNames")) ? null : r.GetString(r.GetOrdinal("AlternativeNames")),
        Description = r.IsDBNull(r.GetOrdinal("Description")) ? null : r.GetString(r.GetOrdinal("Description")),
        Category = r.IsDBNull(r.GetOrdinal("Category")) ? null : r.GetString(r.GetOrdinal("Category")),
        IsCommonAllergen = r.GetBoolean(r.GetOrdinal("IsCommonAllergen")),
        IngredientListString = r.IsDBNull(r.GetOrdinal("IngredientListString")) ? null : r.GetString(r.GetOrdinal("IngredientListString"))
    };

    private static Dictionary<string, object> MapForBulk(IngredientDto i) => new()
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
