using ExpressRecipe.Data.Common.HighSpeedDAL;
using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.Shared.Services;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.ProductService.Data.HighSpeedDAL;

/// <summary>
/// Ultra-minimal HighSpeedDAL for Product - zero manual SQL, all delegated to base.
/// Follows HighSpeedDAL simple CRUD pattern with generic operations.
/// </summary>
public interface IProductDal
{
    Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<ProductDto>> GetAllAsync(CancellationToken ct = default);
    Task<Guid> SaveAsync(ProductDto product, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<int> BulkSaveAsync(IEnumerable<ProductDto> products, CancellationToken ct = default);
}

public class ProductDal : DalOperationsBase<ProductDto, ProductConnection>, IProductDal
{
    private const string TableName = "Product";
    private readonly HybridCacheService? _cache;

    public ProductDal(ProductConnection connection, ILogger<ProductDal> logger, HybridCacheService? cache = null)
        : base(connection, logger)
    {
        _cache = cache;
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var cacheKey = $"product:{id}";
        if (_cache != null)
        {
            var cached = await _cache.GetAsync<ProductDto>(cacheKey);
            if (cached != null) return cached;
        }

        var result = await GetByIdGenericAsync(TableName, id, MapFromReader, ct);
        
        if (result != null && _cache != null)
        {
            await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(15), TimeSpan.FromHours(1));
        }

        return result;
    }

    public Task<List<ProductDto>> GetAllAsync(CancellationToken ct = default) 
        => GetAllGenericAsync(TableName, MapFromReader, ct);

    public async Task<Guid> SaveAsync(ProductDto product, CancellationToken ct = default)
    {
        if (product.Id == Guid.Empty)
        {
            product.Id = Guid.NewGuid();
            product.CreatedAt = DateTime.UtcNow;
            await InsertGenericAsync(TableName, product, ct);
        }
        else
        {
            await UpdateGenericAsync(TableName, product, ct);
        }
        
        if (_cache != null) await _cache.RemoveAsync($"product:{product.Id}");
        return product.Id;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var success = await SoftDeleteGenericAsync(TableName, id, ct);
        if (success && _cache != null) await _cache.RemoveAsync($"product:{id}");
        return success;
    }

    public async Task<int> BulkSaveAsync(IEnumerable<ProductDto> products, CancellationToken ct = default)
    {
        var list = products.ToList();
        foreach (var p in list.Where(p => p.Id == Guid.Empty))
        {
            p.Id = Guid.NewGuid();
            p.CreatedAt = DateTime.UtcNow;
        }
        return await BulkInsertAsync(TableName, list, MapForBulk, ct);
    }

    private static ProductDto MapFromReader(System.Data.IDataReader r) => new()
    {
        Id = r.GetGuid(r.GetOrdinal("Id")),
        Name = r.GetString(r.GetOrdinal("Name")),
        Brand = r.IsDBNull(r.GetOrdinal("Brand")) ? null : r.GetString(r.GetOrdinal("Brand")),
        Barcode = r.IsDBNull(r.GetOrdinal("Barcode")) ? null : r.GetString(r.GetOrdinal("Barcode")),
        BarcodeType = r.IsDBNull(r.GetOrdinal("BarcodeType")) ? null : r.GetString(r.GetOrdinal("BarcodeType")),
        Description = r.IsDBNull(r.GetOrdinal("Description")) ? null : r.GetString(r.GetOrdinal("Description")),
        Category = r.IsDBNull(r.GetOrdinal("Category")) ? null : r.GetString(r.GetOrdinal("Category")),
        ServingSize = r.IsDBNull(r.GetOrdinal("ServingSize")) ? null : r.GetString(r.GetOrdinal("ServingSize")),
        ServingUnit = r.IsDBNull(r.GetOrdinal("ServingUnit")) ? null : r.GetString(r.GetOrdinal("ServingUnit")),
        ImageUrl = r.IsDBNull(r.GetOrdinal("ImageUrl")) ? null : r.GetString(r.GetOrdinal("ImageUrl")),
        ApprovalStatus = r.GetString(r.GetOrdinal("ApprovalStatus")),
        CreatedAt = r.GetDateTime(r.GetOrdinal("CreatedAt"))
    };

    private static Dictionary<string, object> MapForBulk(ProductDto p) => new()
    {
        ["Id"] = p.Id,
        ["Name"] = p.Name,
        ["Brand"] = (object?)p.Brand ?? DBNull.Value,
        ["Barcode"] = (object?)p.Barcode ?? DBNull.Value,
        ["BarcodeType"] = (object?)p.BarcodeType ?? DBNull.Value,
        ["Description"] = (object?)p.Description ?? DBNull.Value,
        ["Category"] = (object?)p.Category ?? DBNull.Value,
        ["ServingSize"] = (object?)p.ServingSize ?? DBNull.Value,
        ["ServingUnit"] = (object?)p.ServingUnit ?? DBNull.Value,
        ["ImageUrl"] = (object?)p.ImageUrl ?? DBNull.Value,
        ["ApprovalStatus"] = p.ApprovalStatus,
        ["CreatedAt"] = p.CreatedAt,
        ["IsDeleted"] = false
    };
}
