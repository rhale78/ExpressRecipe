using ExpressRecipe.Shared.DTOs.Product;
using HighSpeedDAL.Core.Interfaces;
using HighSpeedDAL.SqlServer;
using ExpressRecipe.ProductService.Entities;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.ProductService.Data;

/// <summary>
/// Adapter that implements IProductRepository but delegates CRUD to generated HighSpeedDAL DALs.
/// Keeps legacy DTOs (ProductDto) for compatibility and maps between generated entities and DTOs.
/// Advanced search and aggregated queries are delegated to ProductSearchAdapter.
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
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id)
    {
        var entity = await _dal.GetByIdAsync(id);
        if (entity == null) return null;
        var dto = MapEntityToDto(entity);
        await LoadImagesAsync(dto);
        return dto;
    }

    public async Task<ProductDto?> GetByBarcodeAsync(string barcode)
    {
        _logger.LogDebug("Searching for product by barcode '{Barcode}' using DAL.", barcode);
        // This is not the most efficient way for a large dataset,
        // but it removes raw SQL and leverages the DAL's caching.
        // A future optimization would be to add a GetByBarcodeAsync method to the source generator.
        var allProducts = await _dal.GetAllAsync();
        var entity = allProducts.FirstOrDefault(p => p.Barcode == barcode);

        if (entity != null)
        {
            _logger.LogDebug("Found product with barcode '{Barcode}'.", barcode);
            var dto = MapEntityToDto(entity);
            await LoadImagesAsync(dto);
            return dto;
        }

        _logger.LogWarning("No product found with barcode '{Barcode}'.", barcode);
        return null;
    }

    public Task<ProductDto?> GetProductByBarcodeAsync(string barcode) => GetByBarcodeAsync(barcode);

    public Task<List<ProductDto>> SearchAsync(ProductSearchRequest request)
    {
        return _searchAdapter.SearchAsync(request);
    }

    public Task<int> GetSearchCountAsync(ProductSearchRequest request)
    {
        return _searchAdapter.GetSearchCountAsync(request);
    }

    public Task<Dictionary<string,int>> GetLetterCountsAsync(ProductSearchRequest request)
    {
        return _searchAdapter.GetLetterCountsAsync(request);
    }

    public async Task<Guid> CreateAsync(CreateProductRequest request, Guid? createdBy = null)
    {
        var entity = MapCreateRequestToEntity(request);
        // generated DAL InsertAsync requires userName and CancellationToken parameters
        await _dal.InsertAsync(entity, null, System.Threading.CancellationToken.None);
        return entity.Id;
    }

    public Task<Guid> CreateProductAsync(CreateProductRequest request) => CreateAsync(request, null);

    public async Task<int> BulkCreateAsync(IEnumerable<CreateProductRequest> requests, Guid? createdBy = null)
    {
        var entities = requests.Select(MapCreateRequestToEntity).ToList();
        if (entities.Count == 0) return 0;

        // Uses generated DAL BulkInsertAsync which leverages InMemoryTable for high-speed writes
        return await _dal.BulkInsertAsync(entities, null, System.Threading.CancellationToken.None);
    }

    public async Task AddIngredientToProductAsync(Guid productId, string ingredientName, int orderIndex = 0)
    {
        // Find the ingredient by name. This is inefficient but removes raw SQL.
        // A future optimization is to add [ReferenceTable] to IngredientEntity to get a GetByNameAsync method.
        var allIngredients = await _ingredientDal.GetAllAsync();
        var ingredient = allIngredients.FirstOrDefault(i => i.Name.Equals(ingredientName, StringComparison.OrdinalIgnoreCase));

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
        var productLabel = new ProductLabelEntity
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            LabelName = label
        };

        await _productLabelDal.InsertAsync(productLabel, null, System.Threading.CancellationToken.None);
        _logger.LogInformation("Added label '{Label}' to product '{ProductId}'.", label, productId);
    }

    public async Task AddAllergenToProductAsync(Guid productId, string allergen)
    {
        var productAllergen = new ProductAllergenEntity
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            AllergenName = allergen
        };

        await _productAllergenDal.InsertAsync(productAllergen, null, System.Threading.CancellationToken.None);
        _logger.LogInformation("Added allergen '{Allergen}' to product '{ProductId}'.", allergen, productId);
    }

    public async Task AddExternalLinkAsync(Guid productId, string source, string externalId)
    {
        var link = new ProductExternalLinkEntity
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Source = source,
            ExternalId = externalId
        };

        await _productExternalLinkDal.InsertAsync(link, null, System.Threading.CancellationToken.None);
        _logger.LogInformation("Added external link from source '{Source}' to product '{ProductId}'.", source, productId);
    }

    public async Task UpdateProductMetadataAsync(Guid productId, string key, string value)
    {
        var metadata = new ProductMetadataEntity
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            MetaKey = key,
            MetaValue = value
        };

        await _productMetadataDal.InsertAsync(metadata, null, System.Threading.CancellationToken.None);
        _logger.LogInformation("Added metadata with key '{Key}' to product '{ProductId}'.", key, productId);
    }

    public async Task<ProductDto?> GetProductByExternalIdAsync(string source, string externalId)
    {
        // This is inefficient, but removes the incorrect raw SQL.
        // A future optimization would be a custom query method in the generator for this lookup.
        var allLinks = await _productExternalLinkDal.GetAllAsync();
        var link = allLinks.FirstOrDefault(l => 
            l.Source.Equals(source, StringComparison.OrdinalIgnoreCase) && 
            l.ExternalId.Equals(externalId, StringComparison.OrdinalIgnoreCase));

        if (link == null)
        {
            _logger.LogWarning("Could not find external link for source '{Source}' and external ID '{ExternalId}'.", source, externalId);
            return null;
        }

        // Now get the product using the ProductId from the link
        return await GetByIdAsync(link.ProductId);
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateProductRequest request, Guid? updatedBy = null)
    {
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

    public async Task<bool> DeleteAsync(Guid id, Guid? deletedBy = null)
    {
        // If generated DAL does not expose SoftDeleteAsync, perform soft-delete via update
        var existing = await _dal.GetByIdAsync(id);
        if (existing == null) return false;
        existing.IsDeleted = true;
        existing.ModifiedDate = DateTime.UtcNow;
        await _dal.UpdateAsync(existing, null, System.Threading.CancellationToken.None);
        return true;
    }

    public async Task<bool> ApproveAsync(Guid id, bool approve, Guid approvedBy, string? rejectionReason = null)
    {
        var entity = await _dal.GetByIdAsync(id);
        if (entity == null) return false;
        entity.ApprovalStatus = approve ? "Approved" : "Rejected";
        entity.ApprovedBy = approvedBy;
        entity.ApprovedAt = DateTime.UtcNow;
        entity.RejectionReason = rejectionReason;
        await _dal.UpdateAsync(entity, null, System.Threading.CancellationToken.None);
        return true;
    }

    public async Task<bool> ProductExistsAsync(Guid id)
    {
        var entity = await _dal.GetByIdAsync(id);
        return entity != null && !entity.IsDeleted;
    }

    public async Task<int?> GetProductCountAsync()
    {
        var count = await _dal.CountAsync();
        return count;
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
}
