using System.Linq;
using Microsoft.Extensions.Logging;
using ExpressRecipe.ProductService.Entities;

namespace ExpressRecipe.ProductService.Data;

/// <summary>
/// Adapter that implements IProductImageRepository using HighSpeedDAL generated DALs.
/// All operations use the DAL's caching and in-memory table features.
/// </summary>
public class ProductImageRepositoryAdapter : IProductImageRepository
{
    private readonly ProductImageEntityDal _dal;
    private readonly ProductEntityDal _productDal;
    private readonly ILogger<ProductImageRepositoryAdapter> _logger;

    public ProductImageRepositoryAdapter(
        ProductImageEntityDal dal,
        ProductEntityDal productDal,
        ILogger<ProductImageRepositoryAdapter> logger)
    {
        _dal = dal ?? throw new ArgumentNullException(nameof(dal));
        _productDal = productDal ?? throw new ArgumentNullException(nameof(productDal));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> AddImageAsync(Guid productId, string imageType, string? imageUrl, string? localFilePath,
        string? fileName, long? fileSize, string? mimeType, int? width, int? height,
        bool isPrimary, int displayOrder, bool isUserUploaded, string? sourceSystem, string? sourceId, Guid? userId)
    {
        try
        {
            // If this is a primary image, clear other primary flags first
            if (isPrimary)
            {
                // Uses generated [NamedQuery("ByProductIdAndPrimary", nameof(ProductId), nameof(IsPrimary))] method
                // Gets only primary images for this product (already filters IsDeleted=0)
                var imagesToUpdate = await _dal.GetByProductIdAndPrimaryAsync(productId, true);
                if (imagesToUpdate.Any())
                {
                    foreach (var img in imagesToUpdate)
                    {
                        img.IsPrimary = false;
                    }
                    await _dal.BulkUpdateAsync(imagesToUpdate, "System", System.Threading.CancellationToken.None);
                }
            }

            // Create and insert new image entity
            var newImage = new ProductImageEntity
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                ImageType = imageType ?? string.Empty,
                ImageUrl = imageUrl,
                LocalFilePath = localFilePath,
                FileName = fileName,
                FileSize = fileSize,
                MimeType = mimeType,
                Width = width,
                Height = height,
                DisplayOrder = displayOrder,
                IsPrimary = isPrimary,
                IsUserUploaded = isUserUploaded,
                SourceSystem = sourceSystem,
                SourceId = sourceId
            };

            await _dal.InsertAsync(newImage, userId?.ToString(), System.Threading.CancellationToken.None);

            // Sync Product.ImageUrl for primary images
            if (isPrimary && !string.IsNullOrWhiteSpace(imageUrl))
            {
                var product = await _productDal.GetByIdAsync(productId);
                if (product != null)
                {
                    product.ImageUrl = imageUrl;
                    await _productDal.UpdateAsync(product, "System", System.Threading.CancellationToken.None);

                    _logger.LogDebug("Synced primary image to Product.ImageUrl for ProductId: {ProductId}, ImageType: {ImageType}, Source: {Source}",
                        productId, imageType, sourceSystem ?? "Unknown");
                }
            }

            _logger.LogDebug("Added image {ImageId} for product {ProductId}: Type={ImageType}, IsPrimary={IsPrimary}, Source={Source}",
                newImage.Id, productId, imageType, isPrimary, sourceSystem ?? "Unknown");

            return newImage.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add image for product {ProductId}", productId);
            throw;
        }
    }

    public async Task<List<ProductImageModel>> GetImagesByProductIdAsync(Guid productId)
    {
        // Uses generated [NamedQuery("ByProductId", nameof(ProductId))] method
        // Filters from cache - no database round-trip
        var entities = await _dal.GetByProductIdAsync(productId);

        return entities
            .OrderBy(e => e.DisplayOrder)
            .ThenBy(e => e.CreatedDate)
            .Select(MapEntityToModel)
            .ToList();
    }

    public async Task<Dictionary<Guid, List<ProductImageModel>>> GetImagesByProductIdsAsync(IEnumerable<Guid> productIds)
    {
        var productIdSet = productIds.ToHashSet();
        if (!productIdSet.Any()) return new Dictionary<Guid, List<ProductImageModel>>();

        _logger.LogDebug("Loading images for {ProductCount} products", productIdSet.Count);

        // Fetch all images at once (leverages DAL caching - no DB hit if cached)
        // Then group by ProductId to avoid N individual queries
        // This uses the in-memory table or L1/L2 cache depending on DAL configuration
        var allImages = await _dal.GetAllAsync(CancellationToken.None);

        var result = new Dictionary<Guid, List<ProductImageModel>>();

        // Filter to requested product IDs and group
        var groupedByProduct = allImages
            .Where(e => productIdSet.Contains(e.ProductId))
            .GroupBy(e => e.ProductId)
            .ToDictionary(
                g => g.Key,
                g => g
                    .OrderBy(e => e.DisplayOrder)
                    .ThenBy(e => e.CreatedDate)
                    .Select(MapEntityToModel)
                    .ToList()
            );

        _logger.LogDebug("Loaded images for {ProductCount} products from cache/DB", groupedByProduct.Count);
        return groupedByProduct;
    }

    public async Task<ProductImageModel?> GetPrimaryImageAsync(Guid productId)
    {
        // Uses generated [NamedQuery("ByProductIdAndPrimary", nameof(ProductId), nameof(IsPrimary))] method
        // Filters from cache - no database round-trip
        var entities = await _dal.GetByProductIdAndPrimaryAsync(productId, true);

        var primaryEntity = entities
            .OrderBy(e => e.DisplayOrder)
            .FirstOrDefault();

        return primaryEntity != null ? MapEntityToModel(primaryEntity) : null;
    }

    public async Task SetPrimaryImageAsync(Guid productId, Guid imageId)
    {
        try
        {
            // Uses generated [NamedQuery("ByProductId", nameof(ProductId))] method
            // Filters from cache - no database round-trip
            var allImages = await _dal.GetByProductIdAsync(productId);

            // Clear current primary flags and set new primary
            var imagesToUpdate = new List<ProductImageEntity>();
            ProductImageEntity? newPrimaryImage = null;

            // Named query already filters IsDeleted=0
            foreach (var img in allImages)
            {
                if (img.Id == imageId)
                {
                    img.IsPrimary = true;
                    img.DisplayOrder = 0;
                    newPrimaryImage = img;
                    imagesToUpdate.Add(img);
                }
                else if (img.IsPrimary)
                {
                    img.IsPrimary = false;
                    imagesToUpdate.Add(img);
                }
            }

            // Bulk update all changed images
            if (imagesToUpdate.Any())
            {
                await _dal.BulkUpdateAsync(imagesToUpdate, "System", System.Threading.CancellationToken.None);
            }

            // Sync Product.ImageUrl with the new primary image
            if (newPrimaryImage != null)
            {
                var product = await _productDal.GetByIdAsync(productId);
                if (product != null)
                {
                    product.ImageUrl = newPrimaryImage.ImageUrl ?? newPrimaryImage.LocalFilePath;
                    await _productDal.UpdateAsync(product, "System", System.Threading.CancellationToken.None);
                }
            }

            _logger.LogDebug("Set primary image {ImageId} for product {ProductId} and synced to Product.ImageUrl",
                imageId, productId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set primary image for product {ProductId}", productId);
            throw;
        }
    }

    public async Task DeleteImageAsync(Guid imageId)
    {
        var entity = await _dal.GetByIdAsync(imageId);
        if (entity == null || entity.IsDeleted) return;

        entity.IsDeleted = true;
        await _dal.UpdateAsync(entity, "System", System.Threading.CancellationToken.None);
    }

    public async Task DeleteAllProductImagesAsync(Guid productId)
    {
        // Uses generated [NamedQuery("ByProductId", nameof(ProductId))] method
        // Named query already filters IsDeleted=0
        var toDelete = await _dal.GetByProductIdAsync(productId);
        if (!toDelete.Any()) return;

        foreach (var entity in toDelete)
        {
            entity.IsDeleted = true;
        }

        await _dal.BulkUpdateAsync(toDelete, "System", System.Threading.CancellationToken.None);
    }

    public async Task<int> BulkAddImagesAsync(IEnumerable<ProductImageRequest> images)
    {
        var imageList = images.ToList();
        if (!imageList.Any()) return 0;

        // Convert to entities for HighSpeedDAL
        var entities = imageList.Select(img => new ProductImageEntity
        {
            Id = Guid.NewGuid(),
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
            SourceId = img.SourceId
        }).ToList();

        // Use duplicate-handling bulk insert
        var result = await _dal.BulkInsertWithDuplicatesAsync(entities, "System", System.Threading.CancellationToken.None);

        if (result.HasDuplicates)
        {
            _logger.LogDebug("Skipped {Count} duplicate product images", result.DuplicateEntities.Count);
        }

        _logger.LogDebug("Bulk inserted {Count} product images via HighSpeedDAL", result.InsertedCount);
        return result.InsertedCount;
    }

    private static ProductImageModel MapEntityToModel(ProductImageEntity e)
    {
        return new ProductImageModel
        {
            Id = e.Id,
            ProductId = e.ProductId,
            ImageType = e.ImageType,
            ImageUrl = e.ImageUrl,
            LocalFilePath = e.LocalFilePath,
            FileName = e.FileName,
            FileSize = e.FileSize,
            MimeType = e.MimeType,
            Width = e.Width,
            Height = e.Height,
            DisplayOrder = e.DisplayOrder,
            IsPrimary = e.IsPrimary,
            IsUserUploaded = e.IsUserUploaded,
            SourceSystem = e.SourceSystem,
            SourceId = e.SourceId,
            CreatedAt = e.CreatedDate
        };
    }
}
