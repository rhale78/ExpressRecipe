using ExpressRecipe.Data.Common.HighSpeedDAL;
using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.Shared.Services;
using Microsoft.Extensions.Logging;
using System.Data;

namespace ExpressRecipe.ProductService.Data.HighSpeedDAL;

/// <summary>
/// High-speed DAL for Product entities following HighSpeedDAL framework patterns.
/// Provides optimized bulk operations, intelligent caching, and retry logic.
/// </summary>
public interface IProductDal
{
    // Single operations
    Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductDto?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateProductRequest request, Guid? createdBy = null, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Guid id, UpdateProductRequest request, Guid? updatedBy = null, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, Guid? deletedBy = null, CancellationToken cancellationToken = default);

    // Bulk operations (HighSpeedDAL patterns)
    Task<int> BulkInsertAsync(IEnumerable<CreateProductRequest> products, CancellationToken cancellationToken = default);
    Task<Dictionary<Guid, ProductDto>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    Task<Dictionary<string, ProductDto>> GetByBarcodesAsync(IEnumerable<string> barcodes, CancellationToken cancellationToken = default);
    
    // Cache operations
    Task InvalidateCacheAsync(Guid id);
    Task InvalidateCacheByBarcodeAsync(string barcode);
}

public class ProductDal : DalOperationsBase<ProductDto, ProductConnection>, IProductDal
{
    private readonly IProductImageRepository _productImageRepository;
    private readonly HybridCacheService? _cache;

    public ProductDal(
        ProductConnection connection,
        ILogger<ProductDal> logger,
        IProductImageRepository productImageRepository,
        HybridCacheService? cache = null)
        : base(connection, logger)
    {
        _productImageRepository = productImageRepository ?? throw new ArgumentNullException(nameof(productImageRepository));
        _cache = cache;
    }

    #region Single Operations

    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Check cache first
        if (_cache != null)
        {
            var cacheKey = CacheKeys.FormatKey("product:id:{0}", id);
            var cachedProduct = await _cache.GetAsync<ProductDto>(cacheKey);
            if (cachedProduct != null)
            {
                Logger.LogDebug("Product {Id} retrieved from cache", id);
                return cachedProduct;
            }
        }

        // Query database
        var product = await GetByIdFromDbAsync(id, cancellationToken);

        // Cache result
        if (product != null && _cache != null)
        {
            var cacheKey = CacheKeys.FormatKey("product:id:{0}", id);
            await _cache.SetAsync(
                cacheKey,
                product,
                memoryExpiry: TimeSpan.FromMinutes(15),
                distributedExpiry: TimeSpan.FromHours(1));
        }

        return product;
    }

    public async Task<ProductDto?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        // Check cache first
        if (_cache != null)
        {
            var cacheKey = CacheKeys.FormatKey("product:barcode:{0}", barcode);
            var cachedProduct = await _cache.GetAsync<ProductDto>(cacheKey);
            if (cachedProduct != null)
            {
                Logger.LogDebug("Product with barcode {Barcode} retrieved from cache", barcode);
                return cachedProduct;
            }
        }

        const string sql = @"
            SELECT Id, Name, Brand, Barcode, BarcodeType, Description, Category,
                   ServingSize, ServingUnit, ImageUrl, ApprovalStatus,
                   ApprovedBy, ApprovedAt, RejectionReason, SubmittedBy, CreatedAt
            FROM Product
            WHERE Barcode = @Barcode AND IsDeleted = 0";

        var products = await ExecuteQueryAsync(
            sql,
            MapReaderToProductDto,
            new { Barcode = barcode },
            cancellationToken: cancellationToken);

        var product = products.FirstOrDefault();

        if (product != null)
        {
            await LoadImagesAsync(product);

            // Cache result
            if (_cache != null)
            {
                var cacheKey = CacheKeys.FormatKey("product:barcode:{0}", barcode);
                await _cache.SetAsync(
                    cacheKey,
                    product,
                    memoryExpiry: TimeSpan.FromMinutes(15),
                    distributedExpiry: TimeSpan.FromHours(1));
            }
        }

        return product;
    }

    public async Task<Guid> CreateAsync(CreateProductRequest request, Guid? createdBy = null, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
            INSERT INTO Product (Id, Name, Brand, Barcode, BarcodeType, Description, Category,
                                ServingSize, ServingUnit, ImageUrl, ApprovalStatus, SubmittedBy, 
                                CreatedAt, CreatedBy, IsDeleted)
            VALUES (@Id, @Name, @Brand, @Barcode, @BarcodeType, @Description, @Category,
                    @ServingSize, @ServingUnit, @ImageUrl, @ApprovalStatus, @SubmittedBy, 
                    @CreatedAt, @CreatedBy, 0)";

        await ExecuteNonQueryAsync(sql, new
        {
            Id = id,
            request.Name,
            request.Brand,
            request.Barcode,
            request.BarcodeType,
            request.Description,
            request.Category,
            request.ServingSize,
            request.ServingUnit,
            request.ImageUrl,
            ApprovalStatus = "Pending",
            SubmittedBy = (object?)null,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = (object?)createdBy
        }, cancellationToken: cancellationToken);

        Logger.LogInformation("Created product {Id} with name {Name}", id, request.Name);
        return id;
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateProductRequest request, Guid? updatedBy = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE Product 
            SET Name = @Name, Brand = @Brand, Description = @Description, Category = @Category,
                ServingSize = @ServingSize, ServingUnit = @ServingUnit,
                UpdatedAt = @UpdatedAt, UpdatedBy = @UpdatedBy
            WHERE Id = @Id AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(sql, new
        {
            Id = id,
            request.Name,
            request.Brand,
            request.Description,
            request.Category,
            request.ServingSize,
            request.ServingUnit,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = (object?)updatedBy
        }, cancellationToken: cancellationToken);

        if (rowsAffected > 0)
        {
            await InvalidateCacheAsync(id);
            Logger.LogInformation("Updated product {Id}", id);
            return true;
        }

        return false;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid? deletedBy = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE Product 
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
            Logger.LogInformation("Soft deleted product {Id}", id);
            return true;
        }

        return false;
    }

    #endregion

    #region Bulk Operations

    public async Task<int> BulkInsertAsync(IEnumerable<CreateProductRequest> products, CancellationToken cancellationToken = default)
    {
        var productsList = products.ToList();
        if (!productsList.Any()) return 0;

        Logger.LogInformation("Bulk inserting {Count} products using HighSpeedDAL pattern", productsList.Count);

        // Use HighSpeedDAL BulkInsertAsync from base class
        return await BulkInsertAsync(
            "Product",
            productsList.Select(p => new ProductDto
            {
                Id = Guid.NewGuid(),
                Name = p.Name,
                Brand = p.Brand,
                Barcode = p.Barcode,
                BarcodeType = p.BarcodeType,
                Description = p.Description,
                Category = p.Category,
                ServingSize = p.ServingSize,
                ServingUnit = p.ServingUnit,
                ImageUrl = p.ImageUrl,
                ApprovalStatus = "Pending",
                CreatedAt = DateTime.UtcNow
            }),
            product => new Dictionary<string, object>
            {
                ["Id"] = product.Id,
                ["Name"] = product.Name ?? string.Empty,
                ["Brand"] = (object?)product.Brand ?? DBNull.Value,
                ["Barcode"] = (object?)product.Barcode ?? DBNull.Value,
                ["BarcodeType"] = (object?)product.BarcodeType ?? DBNull.Value,
                ["Description"] = (object?)product.Description ?? DBNull.Value,
                ["Category"] = (object?)product.Category ?? DBNull.Value,
                ["ServingSize"] = (object?)product.ServingSize ?? DBNull.Value,
                ["ServingUnit"] = (object?)product.ServingUnit ?? DBNull.Value,
                ["ImageUrl"] = (object?)product.ImageUrl ?? DBNull.Value,
                ["ApprovalStatus"] = product.ApprovalStatus ?? "Pending",
                ["SubmittedBy"] = DBNull.Value,
                ["CreatedAt"] = product.CreatedAt,
                ["CreatedBy"] = DBNull.Value,
                ["IsDeleted"] = false
            },
            cancellationToken);
    }

    public async Task<Dictionary<Guid, ProductDto>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idsList = ids.Distinct().ToList();
        if (!idsList.Any()) return new Dictionary<Guid, ProductDto>();

        // Check cache first for each ID
        var result = new Dictionary<Guid, ProductDto>();
        var uncachedIds = new List<Guid>();

        if (_cache != null)
        {
            foreach (var id in idsList)
            {
                var cacheKey = CacheKeys.FormatKey("product:id:{0}", id);
                var cachedProduct = await _cache.GetAsync<ProductDto>(cacheKey);
                if (cachedProduct != null)
                {
                    result[id] = cachedProduct;
                }
                else
                {
                    uncachedIds.Add(id);
                }
            }

            Logger.LogDebug("Product batch lookup: {CacheHits} hits, {CacheMisses} misses", result.Count, uncachedIds.Count);
        }
        else
        {
            uncachedIds = idsList;
        }

        // Fetch uncached products from DB
        if (uncachedIds.Any())
        {
            var dbProducts = await GetProductsByIdsFromDbAsync(uncachedIds, cancellationToken);

            // Cache and add to result
            foreach (var kvp in dbProducts)
            {
                result[kvp.Key] = kvp.Value;

                if (_cache != null)
                {
                    var cacheKey = CacheKeys.FormatKey("product:id:{0}", kvp.Key);
                    await _cache.SetAsync(
                        cacheKey,
                        kvp.Value,
                        memoryExpiry: TimeSpan.FromMinutes(15),
                        distributedExpiry: TimeSpan.FromHours(1));
                }
            }
        }

        return result;
    }

    public async Task<Dictionary<string, ProductDto>> GetByBarcodesAsync(IEnumerable<string> barcodes, CancellationToken cancellationToken = default)
    {
        var barcodesList = barcodes.Distinct().ToList();
        if (!barcodesList.Any()) return new Dictionary<string, ProductDto>(StringComparer.OrdinalIgnoreCase);

        Logger.LogDebug("Batch fetching {Count} products by barcode", barcodesList.Count);

        // Build parameterized query
        var inClause = string.Join(",", barcodesList.Select((_, i) => $"@Barcode{i}"));
        var sql = $@"
            SELECT Id, Name, Brand, Barcode, BarcodeType, Description, Category,
                   ServingSize, ServingUnit, ImageUrl, ApprovalStatus,
                   ApprovedBy, ApprovedAt, RejectionReason, SubmittedBy, CreatedAt
            FROM Product
            WHERE Barcode IN ({inClause}) AND IsDeleted = 0";

        var parameters = new Dictionary<string, object>();
        for (int i = 0; i < barcodesList.Count; i++)
        {
            parameters[$"Barcode{i}"] = barcodesList[i];
        }

        var products = await ExecuteQueryAsync(sql, MapReaderToProductDto, parameters, cancellationToken: cancellationToken);

        var result = new Dictionary<string, ProductDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var product in products)
        {
            if (product.Barcode != null)
            {
                await LoadImagesAsync(product);
                result[product.Barcode] = product;
            }
        }

        return result;
    }

    #endregion

    #region Cache Operations

    public async Task InvalidateCacheAsync(Guid id)
    {
        if (_cache == null) return;

        var cacheKey = CacheKeys.FormatKey("product:id:{0}", id);
        await _cache.RemoveAsync(cacheKey);
        Logger.LogDebug("Invalidated cache for product {Id}", id);
    }

    public async Task InvalidateCacheByBarcodeAsync(string barcode)
    {
        if (_cache == null) return;

        var cacheKey = CacheKeys.FormatKey("product:barcode:{0}", barcode);
        await _cache.RemoveAsync(cacheKey);
        Logger.LogDebug("Invalidated cache for product barcode {Barcode}", barcode);
    }

    #endregion

    #region Helper Methods

    private async Task<ProductDto?> GetByIdFromDbAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT Id, Name, Brand, Barcode, BarcodeType, Description, Category,
                   ServingSize, ServingUnit, ImageUrl, ApprovalStatus,
                   ApprovedBy, ApprovedAt, RejectionReason, SubmittedBy, CreatedAt
            FROM Product
            WHERE Id = @Id AND IsDeleted = 0";

        var products = await ExecuteQueryAsync(
            sql,
            MapReaderToProductDto,
            new { Id = id },
            cancellationToken: cancellationToken);

        var product = products.FirstOrDefault();
        if (product != null)
        {
            await LoadImagesAsync(product);
        }

        return product;
    }

    private async Task<Dictionary<Guid, ProductDto>> GetProductsByIdsFromDbAsync(List<Guid> ids, CancellationToken cancellationToken)
    {
        // Use IN clause for batch query
        var inClause = string.Join(",", ids.Select((_, i) => $"@Id{i}"));
        var sql = $@"
            SELECT Id, Name, Brand, Barcode, BarcodeType, Description, Category,
                   ServingSize, ServingUnit, ImageUrl, ApprovalStatus,
                   ApprovedBy, ApprovedAt, RejectionReason, SubmittedBy, CreatedAt
            FROM Product
            WHERE Id IN ({inClause}) AND IsDeleted = 0";

        var parameters = new Dictionary<string, object>();
        for (int i = 0; i < ids.Count; i++)
        {
            parameters[$"Id{i}"] = ids[i];
        }

        var products = await ExecuteQueryAsync(sql, MapReaderToProductDto, parameters, cancellationToken: cancellationToken);

        var result = new Dictionary<Guid, ProductDto>();
        foreach (var product in products)
        {
            await LoadImagesAsync(product);
            result[product.Id] = product;
        }

        return result;
    }

    private static ProductDto MapReaderToProductDto(IDataReader reader)
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
            ApprovalStatus = reader.IsDBNull(reader.GetOrdinal("ApprovalStatus")) ? "Pending" : reader.GetString(reader.GetOrdinal("ApprovalStatus")),
            ApprovedBy = reader.IsDBNull(reader.GetOrdinal("ApprovedBy")) ? null : reader.GetGuid(reader.GetOrdinal("ApprovedBy")),
            ApprovedAt = reader.IsDBNull(reader.GetOrdinal("ApprovedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("ApprovedAt")),
            RejectionReason = reader.IsDBNull(reader.GetOrdinal("RejectionReason")) ? null : reader.GetString(reader.GetOrdinal("RejectionReason")),
            SubmittedBy = reader.IsDBNull(reader.GetOrdinal("SubmittedBy")) ? null : reader.GetGuid(reader.GetOrdinal("SubmittedBy")),
            CreatedAt = reader.IsDBNull(reader.GetOrdinal("CreatedAt")) ? DateTime.UtcNow : reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
        };
    }

    private async Task LoadImagesAsync(ProductDto product)
    {
        try
        {
            var images = await _productImageRepository.GetImagesByProductIdAsync(product.Id);
            product.Images = images.Select(img => new ProductImageDto
            {
                Id = img.Id,
                ProductId = img.ProductId,
                ImageType = img.ImageType,
                ImageUrl = img.ImageUrl,
                LocalFilePath = img.LocalFilePath,
                FileName = img.FileName,
                FileSize = img.FileSize,
                MimeType = img.MimeType,
                Width = img.Width,
                Height = img.Height,
                DisplayOrder = img.DisplayOrder,
                IsPrimary = img.IsPrimary,
                IsUserUploaded = img.IsUserUploaded,
                SourceSystem = img.SourceSystem,
                SourceId = img.SourceId,
                CreatedAt = img.CreatedAt
            }).ToList();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to load images for product {ProductId}", product.Id);
            product.Images = new List<ProductImageDto>();
        }
    }

    #endregion
}
