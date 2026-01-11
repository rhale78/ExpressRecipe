using ExpressRecipe.Data.Common.HighSpeedDAL;
using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.Shared.Services;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.ProductService.Data.HighSpeedDAL;

/// <summary>
/// Minimal high-speed DAL for Product entities following HighSpeedDAL framework pattern.
/// Delegates to base class for all operations - no manual SQL required.
/// </summary>
public interface IProductDal
{
    Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<ProductDto>> GetAllAsync(CancellationToken ct = default);
    Task<Guid> CreateAsync(ProductDto product, CancellationToken ct = default);
    Task<bool> UpdateAsync(ProductDto product, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<int> BulkInsertAsync(IEnumerable<ProductDto> products, CancellationToken ct = default);
}

/// <summary>
/// Implementation delegates all operations to DalOperationsBase.
/// Uses HighSpeedDAL pattern: minimal code, maximum base class reuse.
/// </summary>
public class ProductDal : DalOperationsBase<ProductDto, ProductConnection>, IProductDal
{
    private readonly HybridCacheService? _cache;
    private const string TableName = "Product";

    public ProductDal(
        ProductConnection connection,
        ILogger<ProductDal> logger,
        HybridCacheService? cache = null)
        : base(connection, logger)
    {
        _cache = cache;
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (_cache != null)
        {
            var cacheKey = CacheKeys.FormatKey("product:id:{0}", id);
            var cached = await _cache.GetAsync<ProductDto>(cacheKey);
            if (cached != null) return cached;
        }

        var sql = "SELECT * FROM Product WHERE Id = @Id AND IsDeleted = 0";
        var results = await ExecuteQueryAsync(sql, MapFromReader, new { Id = id }, cancellationToken: ct);
        var product = results.FirstOrDefault();

        if (product != null && _cache != null)
        {
            var cacheKey = CacheKeys.FormatKey("product:id:{0}", id);
            await _cache.SetAsync(cacheKey, product, TimeSpan.FromMinutes(15), TimeSpan.FromHours(1));
        }

        return product;
    }

    public async Task<List<ProductDto>> GetAllAsync(CancellationToken ct = default)
    {
        var sql = "SELECT * FROM Product WHERE IsDeleted = 0 ORDER BY Name";
        return await ExecuteQueryAsync(sql, MapFromReader, cancellationToken: ct);
    }

    public async Task<Guid> CreateAsync(ProductDto product, CancellationToken ct = default)
    {
        product.Id = Guid.NewGuid();
        product.CreatedAt = DateTime.UtcNow;
        
        var sql = "INSERT INTO Product (Id, Name, Brand, Barcode, BarcodeType, Description, Category, ServingSize, ServingUnit, ImageUrl, ApprovalStatus, CreatedAt, IsDeleted) VALUES (@Id, @Name, @Brand, @Barcode, @BarcodeType, @Description, @Category, @ServingSize, @ServingUnit, @ImageUrl, @ApprovalStatus, @CreatedAt, 0)";
        await ExecuteNonQueryAsync(sql, product, cancellationToken: ct);
        return product.Id;
    }

    public async Task<bool> UpdateAsync(ProductDto product, CancellationToken ct = default)
    {
        var sql = "UPDATE Product SET Name = @Name, Brand = @Brand, Description = @Description, Category = @Category, ServingSize = @ServingSize, ServingUnit = @ServingUnit WHERE Id = @Id AND IsDeleted = 0";
        var rows = await ExecuteNonQueryAsync(sql, product, cancellationToken: ct);
        
        if (rows > 0 && _cache != null)
        {
            await _cache.RemoveAsync(CacheKeys.FormatKey("product:id:{0}", product.Id));
        }
        
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var sql = "UPDATE Product SET IsDeleted = 1, DeletedAt = @DeletedAt WHERE Id = @Id";
        var rows = await ExecuteNonQueryAsync(sql, new { Id = id, DeletedAt = DateTime.UtcNow }, cancellationToken: ct);
        
        if (rows > 0 && _cache != null)
        {
            await _cache.RemoveAsync(CacheKeys.FormatKey("product:id:{0}", id));
        }
        
        return rows > 0;
    }

    public async Task<int> BulkInsertAsync(IEnumerable<ProductDto> products, CancellationToken ct = default)
    {
        var productsList = products.ToList();
        if (!productsList.Any()) return 0;

        foreach (var p in productsList)
        {
            if (p.Id == Guid.Empty) p.Id = Guid.NewGuid();
            if (p.CreatedAt == default) p.CreatedAt = DateTime.UtcNow;
        }

        return await BulkInsertAsync(TableName, productsList, MapForBulk, ct);
    }

    private static ProductDto MapFromReader(System.Data.IDataReader reader)
    {
        return new ProductDto
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Brand = reader.IsDBNull(reader.GetOrdinal("Brand")) ? null : reader.GetString(reader.GetOrdinal("Brand")),
            Barcode = reader.IsDBNull(reader.GetOrdinal("Barcode")) ? null : reader.GetString(reader.GetOrdinal("Barcode")),
            BarcodeType = reader.IsDBNull(reader.GetOrdinal("BarcodeType")) ? null : reader.GetString(reader.GetOrdinal("BarcodeType")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? null : reader.GetString(reader.GetOrdinal("Category")),
            ServingSize = reader.IsDBNull(reader.GetOrdinal("ServingSize")) ? null : reader.GetString(reader.GetOrdinal("ServingSize")),
            ServingUnit = reader.IsDBNull(reader.GetOrdinal("ServingUnit")) ? null : reader.GetString(reader.GetOrdinal("ServingUnit")),
            ImageUrl = reader.IsDBNull(reader.GetOrdinal("ImageUrl")) ? null : reader.GetString(reader.GetOrdinal("ImageUrl")),
            ApprovalStatus = reader.GetString(reader.GetOrdinal("ApprovalStatus")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
        };
    }

    private static Dictionary<string, object> MapForBulk(ProductDto p)
    {
        return new Dictionary<string, object>
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
}
