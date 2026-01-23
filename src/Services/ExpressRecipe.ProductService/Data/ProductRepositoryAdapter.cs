using ExpressRecipe.Shared.DTOs.Product;
using HighSpeedDAL.Core;
using HighSpeedDAL.Core.Interfaces;
using HighSpeedDAL.SqlServer;
using ExpressRecipe.ProductService.Entities;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.ProductService.Data;

/// <summary>
/// Adapter that implements IProductRepository but delegates CRUD to generated HighSpeedDAL DALs.
/// Keeps legacy DTOs (ProductDto) for compatibility and maps between generated entities and DTOs.
/// Advanced search and aggregated queries are delegated to ProductSearchAdapter.
/// Records DAL operations to metrics for observability.
/// </summary>
public class ProductRepositoryAdapter : IProductRepository
{
    private readonly ProductEntityDal _dal; // generated DAL
    private readonly IngredientEntityDal _ingredientDal;
    private readonly ProductIngredientEntityDal _productIngredientDal;
    private readonly ProductLabelEntityDal _productLabelDal;
    private readonly ProductAllergenEntityDal _productAllergenDal;
    private readonly ProductExternalLinkEntityDal _productExternalLinkDal;
    private readonly ProductMetadataEntityDal _productMetadataDal;
    private readonly ProductDatabaseConnection _dbConnection;
    private readonly ProductSearchAdapter _searchAdapter;
    private readonly IProductImageRepository _imageRepo;
    private readonly ILogger<ProductRepositoryAdapter> _logger;
    private readonly DalMetricsCollector? _metrics;

    public ProductRepositoryAdapter(
        ProductEntityDal dal,
        IngredientEntityDal ingredientDal,
        ProductIngredientEntityDal productIngredientDal,
        ProductLabelEntityDal productLabelDal,
        ProductAllergenEntityDal productAllergenDal,
        ProductExternalLinkEntityDal productExternalLinkDal,
        ProductMetadataEntityDal productMetadataDal,
        ProductSearchAdapter searchAdapter,
        IProductImageRepository imageRepo,
        ProductDatabaseConnection dbConnection,
        DalMetricsCollector? metrics,
        ILogger<ProductRepositoryAdapter> logger)
    {
        _dal = dal ?? throw new ArgumentNullException(nameof(dal));
        _ingredientDal = ingredientDal ?? throw new ArgumentNullException(nameof(ingredientDal));
        _productIngredientDal = productIngredientDal ?? throw new ArgumentNullException(nameof(productIngredientDal));
        _productLabelDal = productLabelDal ?? throw new ArgumentNullException(nameof(productLabelDal));
        _productAllergenDal = productAllergenDal ?? throw new ArgumentNullException(nameof(productAllergenDal));
        _productExternalLinkDal = productExternalLinkDal ?? throw new ArgumentNullException(nameof(productExternalLinkDal));
        _productMetadataDal = productMetadataDal ?? throw new ArgumentNullException(nameof(productMetadataDal));
        _searchAdapter = searchAdapter ?? throw new ArgumentNullException(nameof(searchAdapter));
        _imageRepo = imageRepo ?? throw new ArgumentNullException(nameof(imageRepo));
        _dbConnection = dbConnection ?? throw new ArgumentNullException(nameof(dbConnection));
        _metrics = metrics;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            _metrics?.RecordOperation("Product", "GetById");
            var entity = await _dal.GetByIdAsync(id);
            if (entity == null) return null;
            var dto = MapEntityToDto(entity);
            await LoadImagesAsync(dto);
            return dto;
        }
        finally
        {
            sw.Stop();
            _metrics?.RecordOperationDuration("Product", "GetById", sw.ElapsedMilliseconds);
        }
    }

    public async Task<ProductDto?> GetByBarcodeAsync(string barcode)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            _metrics?.RecordOperation("Product", "GetByBarcode");
            _logger.LogDebug("Searching for product by barcode '{Barcode}' using ProductSearchAdapter.", barcode);
            var dto = await _searchAdapter.GetByBarcodeAsync(barcode);
            if (dto != null)
            {
                await LoadImagesAsync(dto);
            }
            return dto;
        }
        finally
        {
            sw.Stop();
            _metrics?.RecordOperationDuration("Product", "GetByBarcode", sw.ElapsedMilliseconds);
        }
    }

    public Task<ProductDto?> GetProductByBarcodeAsync(string barcode) => GetByBarcodeAsync(barcode);

    public async Task<List<ProductDto>> SearchAsync(ProductSearchRequest request)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            _metrics?.RecordOperation("Product", "Search");
            var products = await _searchAdapter.SearchAsync(request);

            // Batch load images for all products instead of N+1 queries
            if (products.Any())
            {
                await LoadImagesInBatchAsync(products);
            }

            return products;
        }
        finally
        {
            sw.Stop();
            _metrics?.RecordOperationDuration("Product", "Search", sw.ElapsedMilliseconds);
        }
    }

    public async Task<int> GetSearchCountAsync(ProductSearchRequest request)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            _metrics?.RecordOperation("Product", "GetSearchCount");
            return await _searchAdapter.GetSearchCountAsync(request);
        }
        finally
        {
            sw.Stop();
            _metrics?.RecordOperationDuration("Product", "GetSearchCount", sw.ElapsedMilliseconds);
        }
    }

    public async Task<Dictionary<string,int>> GetLetterCountsAsync(ProductSearchRequest request)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            _metrics?.RecordOperation("Product", "GetLetterCounts");
            return await _searchAdapter.GetLetterCountsAsync(request);
        }
        finally
        {
            sw.Stop();
            _metrics?.RecordOperationDuration("Product", "GetLetterCounts", sw.ElapsedMilliseconds);
        }
    }

    public async Task<Guid> CreateAsync(CreateProductRequest request, Guid? createdBy = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            _metrics?.RecordOperation("Product", "Insert");
            var entity = MapCreateRequestToEntity(request);
            entity.IsDeleted = false;
            // generated DAL InsertAsync requires userName and CancellationToken parameters
            await _dal.InsertAsync(entity, null, System.Threading.CancellationToken.None);
            return entity.Id;
        }
        finally
        {
            sw.Stop();
            _metrics?.RecordOperationDuration("Product", "Insert", sw.ElapsedMilliseconds);
        }
    }

    public Task<Guid> CreateProductAsync(CreateProductRequest request) => CreateAsync(request, null);

    public async Task<int> BulkCreateAsync(IEnumerable<CreateProductRequest> requests, Guid? createdBy = null)
    {
        var entities = requests.Select(MapCreateRequestToEntity).ToList();
        if (entities.Count == 0) return 0;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {

            foreach (var entity in entities)
            {
                entity.IsDeleted = false;
            }

            // Use duplicate-handling bulk insert - extracts duplicates for update processing
            var result = await _dal.BulkInsertWithDuplicatesAsync(entities, null, System.Threading.CancellationToken.None);

            // Handle duplicates by updating them
            if (result.HasDuplicates)
            {
                _logger.LogInformation("Processing {Count} duplicate products for update", result.DuplicateEntities.Count);
                int updated = 0;
                foreach (var duplicate in result.DuplicateEntities)
                {
                    try
                    {
                        // Look up existing product by barcode
                        var existing = await _searchAdapter.GetByBarcodeAsync(duplicate.Barcode ?? string.Empty);
                        if (existing != null)
                        {
                            // Update existing product with new data (skip if identical)
                            var updateRequest = new UpdateProductRequest
                            {
                                Name = duplicate.Name ?? string.Empty,
                                Brand = duplicate.Brand,
                                Barcode = duplicate.Barcode,
                                BarcodeType = duplicate.BarcodeType,
                                Description = duplicate.Description,
                                Category = duplicate.Category,
                                ServingSize = duplicate.ServingSize,
                                ServingUnit = duplicate.ServingUnit,
                                ImageUrl = duplicate.ImageUrl
                            };
                            await UpdateAsync(existing.Id, updateRequest, createdBy);
                            updated++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to update duplicate product with barcode {Barcode}", duplicate.Barcode);
                    }
                }
                _logger.LogInformation("Updated {Updated} of {Total} duplicate products", updated, result.DuplicateEntities.Count);
            }

            return result.InsertedCount + result.DuplicateEntities.Count;
        }
        finally
        {
            sw.Stop();
            _metrics?.RecordBatchOperation("Product", "BulkInsert", entities.Count, sw.ElapsedMilliseconds);
        }
    }

    public async Task AddIngredientToProductAsync(Guid productId, string ingredientName, int orderIndex = 0)
    {
        _metrics?.RecordOperation("ProductIngredient", "Insert");
        var ingredient = await _ingredientDal.GetByNameAsync(ingredientName);

        if (ingredient == null)
        {
            _logger.LogWarning("Could not find ingredient with name '{IngredientName}' while trying to add to product '{ProductId}'.", ingredientName, productId);
            return;
        }

        var productIngredient = new ProductIngredientEntity
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            IngredientId = ingredient.Id,
            OrderIndex = orderIndex
        };

        // The username parameter is not available here, passing null. This should be reviewed.
        await _productIngredientDal.InsertAsync(productIngredient, null, System.Threading.CancellationToken.None);
        _logger.LogInformation("Added ingredient '{IngredientName}' to product '{ProductId}'.", ingredientName, productId);
    }

    public async Task AddLabelToProductAsync(Guid productId, string label)
    {
        _metrics?.RecordOperation("ProductLabel", "Insert");
        var productLabel = new ProductLabelEntity
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            LabelName = label,
            IsDeleted = false
        };
        await _productLabelDal.InsertAsync(productLabel, null, System.Threading.CancellationToken.None);
        _logger.LogInformation("Added label '{Label}' to product '{ProductId}'.", label, productId);
    }

    public async Task AddAllergenToProductAsync(Guid productId, string allergen)
    {
        _metrics?.RecordOperation("ProductAllergen", "Insert");
        var productAllergen = new ProductAllergenEntity
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            AllergenName = allergen,
            IsDeleted = false
        };
        await _productAllergenDal.InsertAsync(productAllergen, null, System.Threading.CancellationToken.None);
        _logger.LogInformation("Added allergen '{Allergen}' to product '{ProductId}'.", allergen, productId);
    }

    public async Task AddExternalLinkAsync(Guid productId, string source, string externalId)
    {
        _metrics?.RecordOperation("ProductExternalLink", "Insert");
        var link = new ProductExternalLinkEntity
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Source = source,
            ExternalId = externalId,
            IsDeleted = false
        };
        await _productExternalLinkDal.InsertAsync(link, null, System.Threading.CancellationToken.None);
        _logger.LogInformation("Added external link from source '{Source}' to product '{ProductId}'.", source, productId);
    }

    public async Task UpdateProductMetadataAsync(Guid productId, string key, string value)
    {
        _metrics?.RecordOperation("ProductMetadata", "Insert");
        var metadata = new ProductMetadataEntity
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            MetaKey = key,
            MetaValue = value,
            IsDeleted = false
        };
        await _productMetadataDal.InsertAsync(metadata, null, System.Threading.CancellationToken.None);
        _logger.LogInformation("Added metadata with key '{Key}' to product '{ProductId}'.", key, productId);
    }

    public async Task<ProductDto?> GetProductByExternalIdAsync(string source, string externalId)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            _metrics?.RecordOperation("Product", "GetByExternalId");
            _logger.LogDebug("Searching for product by external ID '{ExternalId}' from source '{Source}' using ProductSearchAdapter.", externalId, source);
            var dto = await _searchAdapter.GetByExternalIdAsync(source, externalId);
            if (dto != null)
            {
                await LoadImagesAsync(dto);
            }
            return dto;
        }
        finally
        {
            sw.Stop();
            _metrics?.RecordOperationDuration("Product", "GetByExternalId", sw.ElapsedMilliseconds);
        }
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateProductRequest request, Guid? updatedBy = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            _metrics?.RecordOperation("Product", "Update");
            var existing = await _dal.GetByIdAsync(id);
            if (existing == null) return false;

            existing.Name = request.Name;
            existing.Brand = request.Brand;
            existing.Barcode = request.Barcode;
            existing.BarcodeType = request.BarcodeType;
            existing.Description = request.Description;
            existing.Category = request.Category;
            existing.ServingSize = request.ServingSize;
            existing.ServingUnit = request.ServingUnit;
            existing.ImageUrl = request.ImageUrl;

            // generated DAL UpdateAsync requires userName and CancellationToken parameters
            await _dal.UpdateAsync(existing, null, System.Threading.CancellationToken.None);
            return true;
        }
        finally
        {
            sw.Stop();
            _metrics?.RecordOperationDuration("Product", "Update", sw.ElapsedMilliseconds);
        }
    }

    public async Task<bool> DeleteAsync(Guid id, Guid? deletedBy = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            _metrics?.RecordOperation("Product", "Delete");
            // If generated DAL does not expose SoftDeleteAsync, perform soft-delete via update
            var existing = await _dal.GetByIdAsync(id);
            if (existing == null) return false;
            existing.IsDeleted = true;
            existing.ModifiedDate = DateTime.UtcNow;
            await _dal.UpdateAsync(existing, null, System.Threading.CancellationToken.None);
            return true;
        }
        finally
        {
            sw.Stop();
            _metrics?.RecordOperationDuration("Product", "Delete", sw.ElapsedMilliseconds);
        }
    }

    public async Task<bool> ApproveAsync(Guid id, bool approve, Guid approvedBy, string? rejectionReason = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            _metrics?.RecordOperation("Product", "Update");
            var entity = await _dal.GetByIdAsync(id);
            if (entity == null) return false;
            entity.ApprovalStatus = approve ? "Approved" : "Rejected";
            entity.ApprovedBy = approvedBy;
            entity.ApprovedAt = DateTime.UtcNow;
            entity.RejectionReason = rejectionReason;
            await _dal.UpdateAsync(entity, null, System.Threading.CancellationToken.None);
            return true;
        }
        finally
        {
            sw.Stop();
            _metrics?.RecordOperationDuration("Product", "Update", sw.ElapsedMilliseconds);
        }
    }

    public async Task<bool> ProductExistsAsync(Guid id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            _metrics?.RecordOperation("Product", "Exists");
            var entity = await _dal.GetByIdAsync(id);
            return entity != null && !entity.IsDeleted;
        }
        finally
        {
            sw.Stop();
            _metrics?.RecordOperationDuration("Product", "Exists", sw.ElapsedMilliseconds);
        }
    }

    public async Task<int?> GetProductCountAsync()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            _metrics?.RecordOperation("Product", "Count");
            var count = await _dal.CountAsync();
            return count;
        }
        finally
        {
            sw.Stop();
            _metrics?.RecordOperationDuration("Product", "Count", sw.ElapsedMilliseconds);
        }
    }

    private ProductDto MapEntityToDto(ProductEntity entity)
    {
        return new ProductDto
        {
            Id = entity.Id,
            Name = entity.Name ?? string.Empty,
            Brand = entity.Brand,
            Barcode = entity.Barcode,
            BarcodeType = entity.BarcodeType,
            Description = entity.Description,
            Category = entity.Category,
            ServingSize = entity.ServingSize,
            ServingUnit = entity.ServingUnit,
            ImageUrl = entity.ImageUrl,
            ApprovalStatus = entity.ApprovalStatus ?? "Pending",
            ApprovedBy = entity.ApprovedBy,
            ApprovedAt = entity.ApprovedAt,
            RejectionReason = entity.RejectionReason,
            SubmittedBy = entity.SubmittedBy,
            CreatedAt = entity.CreatedDate  // Map CreatedDate to DTO's CreatedAt
        };
    }

    private ProductEntity MapCreateRequestToEntity(CreateProductRequest request)
    {
        return new ProductEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Brand = request.Brand,
            Barcode = request.Barcode,
            BarcodeType = request.BarcodeType,
            Description = request.Description,
            Category = request.Category,
            ServingSize = request.ServingSize,
            ServingUnit = request.ServingUnit,
            ImageUrl = request.ImageUrl,
            ApprovalStatus = "Pending",
            SubmittedBy = null,
            // Note: CreatedDate/ModifiedDate are auto-populated by HighSpeedDAL's InsertAsync
            IsDeleted = false
        };
    }

    private async Task LoadImagesAsync(ProductDto? product)
    {
        if (product == null) return;
        var images = await _imageRepo.GetImagesByProductIdAsync(product.Id);
        product.Images = images.Select(MapImageToDto).ToList();
    }

    private async Task LoadImagesInBatchAsync(List<ProductDto> products)
    {
        if (!products.Any()) return;

        _logger.LogDebug("Batch loading images for {Count} products", products.Count);

        // Get all images for all products at once (single DAL call, cached)
        var imagesByProductId = await _imageRepo.GetImagesByProductIdsAsync(products.Select(p => p.Id));

        // Assign images to products
        foreach (var product in products)
        {
            if (imagesByProductId.TryGetValue(product.Id, out var images))
            {
                product.Images = images.Select(MapImageToDto).ToList();
            }
            else
            {
                product.Images = new List<ProductImageDto>();
            }
        }

        _logger.LogDebug("Batch loaded images for {Count} products", products.Count);
    }

    private static ProductImageDto MapImageToDto(ProductImageModel img)
    {
        return new ProductImageDto
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
        };
    }

    // Bulk operations for batch processing
    public async Task<IEnumerable<string>> GetExistingBarcodesAsync(IEnumerable<string> barcodes)
    {
        var barcodeList = barcodes.ToList();
        if (!barcodeList.Any()) return Enumerable.Empty<string>();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            _metrics?.RecordOperation("Product", "GetExistingBarcodes", barcodeList.Count);
            return await _searchAdapter.GetExistingBarcodesAsync(barcodeList);
        }
        finally
        {
            sw.Stop();
            _metrics?.RecordBatchOperation("Product", "GetExistingBarcodes", barcodeList.Count, sw.ElapsedMilliseconds);
        }
    }

    public async Task<Dictionary<string, Guid>> GetProductIdsByBarcodesAsync(IEnumerable<string> barcodes)
    {
        var barcodeList = barcodes.ToList();
        if (!barcodeList.Any()) return new Dictionary<string, Guid>();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            _metrics?.RecordOperation("Product", "GetProductIdsByBarcodes", barcodeList.Count);
            return await _searchAdapter.GetProductIdsByBarcodesAsync(barcodeList);
        }
        finally
        {
            sw.Stop();
            _metrics?.RecordBatchOperation("Product", "GetProductIdsByBarcodes", barcodeList.Count, sw.ElapsedMilliseconds);
        }
    }

    public async Task BulkAddExternalLinksAsync(IEnumerable<(Guid ProductId, string Source, string ExternalId)> links)
    {
        var linkList = links.ToList();
        if (!linkList.Any()) return;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var entities = linkList.Select(l => new ProductExternalLinkEntity
            {
                Id = Guid.NewGuid(),
                ProductId = l.ProductId,
                Source = l.Source,
                ExternalId = l.ExternalId,
                IsDeleted = false
            }).ToList();

            var result = await _productExternalLinkDal.BulkInsertWithDuplicatesAsync(entities, null, System.Threading.CancellationToken.None);

            if (result.HasDuplicates)
            {
                _logger.LogDebug("Skipped {Count} duplicate external links", result.DuplicateEntities.Count);
            }

            _logger.LogDebug("Bulk inserted {Count} external links", result.InsertedCount);
        }
        finally
        {
            sw.Stop();
            _metrics?.RecordBatchOperation("ProductExternalLink", "BulkInsert", linkList.Count, sw.ElapsedMilliseconds);
        }
    }
}
