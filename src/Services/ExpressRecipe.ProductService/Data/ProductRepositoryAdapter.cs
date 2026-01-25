using ExpressRecipe.Shared.DTOs.Product;
using HighSpeedDAL.Core;
using HighSpeedDAL.Core.Interfaces;
using HighSpeedDAL.SqlServer;
using ExpressRecipe.ProductService.Entities;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ExpressRecipe.ProductService.Data
{
    /// <summary>
    /// Adapter that implements IProductRepository but delegates CRUD to generated HighSpeedDAL DALs.
    /// STRICTLY adheres to "No Manual SQL" policy.
    /// All reads/writes go through ProductEntityDal which manages InMemoryTable -> Staging -> Primary flow.
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
            _metrics = metrics;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ProductDto?> GetByIdAsync(Guid id)
        {
            // Direct InMemory lookup handled by DAL
            ProductEntity? entity = await _dal.GetByIdAsync(id);
            if (entity == null) return null;

            ProductDto dto = MapEntityToDto(entity);
            await LoadImagesAsync(dto);
            return dto;
        }

        public async Task<ProductDto?> GetByBarcodeAsync(string barcode)
        {
            // Use SearchAdapter for optimized in-memory lookup (O(1))
            ProductDto? dto = await _searchAdapter.GetByBarcodeAsync(barcode);
            if (dto == null) return null;

            await LoadImagesAsync(dto);
            return dto;
        }

        public Task<ProductDto?> GetProductByBarcodeAsync(string barcode) => GetByBarcodeAsync(barcode);

        public async Task<List<ProductDto>> SearchAsync(ProductSearchRequest request)
        {
            // For complex search that InMemoryTable simple indices can't handle (wildcards, multiple filters),
            // delegate to SearchAdapter which might use SQL or advanced LINQ on the MemoryTable.
            // Ideally, _dal.MemoryTable.Where(...) should be used if possible.
            List<ProductDto> products = await _searchAdapter.SearchAsync(request);

            if (products.Count != 0)
            {
                await LoadImagesInBatchAsync(products);
            }

            return products;
        }

        public async Task<int> GetSearchCountAsync(ProductSearchRequest request)
        {
            return await _searchAdapter.GetSearchCountAsync(request);
        }

        public async Task<Dictionary<string,int>> GetLetterCountsAsync(ProductSearchRequest request)
        {
            return await _searchAdapter.GetLetterCountsAsync(request);
        }

        public async Task<Guid> CreateAsync(CreateProductRequest request, Guid? createdBy = null)
        {
            ProductEntity entity = MapCreateRequestToEntity(request);
            // Convert Guid to string for HighSpeedDAL compatibility
            string userName = createdBy?.ToString() ?? "System";
            entity.CreatedBy = userName;
            entity.IsDeleted = false;
            
            // Insert into InMemoryTable -> auto-flushes to Staging -> auto-merges to Primary
            await _dal.InsertAsync(entity, userName, CancellationToken.None);
            return entity.Id;
        }

        public Task<Guid> CreateProductAsync(CreateProductRequest request) => CreateAsync(request, null);

        public async Task<int> BulkCreateAsync(IEnumerable<CreateProductRequest> requests, Guid? createdBy = null)
        {
            List<ProductEntity> entities = requests.Select(MapCreateRequestToEntity).ToList();
            if (entities.Count == 0) return 0;

            string userName = createdBy?.ToString() ?? "System";
            foreach (var e in entities) { e.CreatedBy = userName; }

            // HighSpeedDAL handles bulk insert efficiently
            // If InMemoryTable is active: Updates memory, queues for async bulk flush
            // If StagingTable is active: Bulk inserts to staging
            var result = await _dal.BulkInsertWithDuplicatesAsync(entities, userName, CancellationToken.None);
            
            // Handle duplicates if framework reports them
            if (result.HasDuplicates)
            {
                // In a perfect world, we'd bulk update these too.
                // For now, simple iteration or logging is acceptable as fallback.
                _logger.LogWarning("Bulk insert encountered {Count} duplicates", result.DuplicateEntities.Count);
            }

            return result.InsertedCount;
        }

        public async Task AddIngredientToProductAsync(Guid productId, string ingredientName, int orderIndex = 0)
        {
            IngredientEntity? ingredient = await _ingredientDal.GetByNameAsync(ingredientName); // Assumes generated method
            if (ingredient == null) return;

            string userName = "System";

            ProductIngredientEntity productIngredient = new ProductIngredientEntity
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                IngredientId = ingredient.Id,
                OrderIndex = orderIndex
            };

            await _productIngredientDal.InsertAsync(productIngredient, userName, CancellationToken.None);
        }

        public async Task AddLabelToProductAsync(Guid productId, string label)
        {
            string userName = "System";
            ProductLabelEntity productLabel = new ProductLabelEntity
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                LabelName = label,
                IsDeleted = false
            };
            await _productLabelDal.InsertAsync(productLabel, userName, CancellationToken.None);
        }

        public async Task AddAllergenToProductAsync(Guid productId, string allergen)
        {
            string userName = "System";
            ProductAllergenEntity productAllergen = new ProductAllergenEntity
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                AllergenName = allergen,
                IsDeleted = false
            };
            await _productAllergenDal.InsertAsync(productAllergen, userName, CancellationToken.None);
        }

        public async Task AddExternalLinkAsync(Guid productId, string source, string externalId)
        {
            string userName = "System";
            ProductExternalLinkEntity link = new ProductExternalLinkEntity
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                Source = source,
                ExternalId = externalId,
                IsDeleted = false
            };
            await _productExternalLinkDal.InsertAsync(link, userName, CancellationToken.None);
        }

        public async Task UpdateProductMetadataAsync(Guid productId, string key, string value)
        {
            string userName = "System";
            ProductMetadataEntity metadata = new ProductMetadataEntity
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                MetaKey = key,
                MetaValue = value,
                IsDeleted = false
            };
            await _productMetadataDal.InsertAsync(metadata, userName, CancellationToken.None);
        }

        public async Task<ProductDto?> GetProductByExternalIdAsync(string source, string externalId)
        {
            // Assuming ProductEntityDal doesn't have a specific named query for this yet,
            // we delegate to search adapter or use LINQ on memory table if exposed
            return await _searchAdapter.GetByExternalIdAsync(source, externalId);
        }

        public async Task<bool> UpdateAsync(Guid id, UpdateProductRequest request, Guid? updatedBy = null)
        {
            ProductEntity? existing = await _dal.GetByIdAsync(id);
            if (existing == null) return false;

            string userName = updatedBy?.ToString() ?? "System";

            // Map updates
            existing.Name = request.Name;
            existing.Brand = request.Brand;
            existing.Barcode = request.Barcode;
            existing.BarcodeType = request.BarcodeType;
            existing.Description = request.Description;
            existing.Category = request.Category;
            existing.ServingSize = request.ServingSize;
            existing.ServingUnit = request.ServingUnit;
            existing.ImageUrl = request.ImageUrl;
            existing.ModifiedBy = userName; // Use standard auto-generated property

            // DAL handles IsDirty tracking -> Staging -> Primary
            await _dal.UpdateAsync(existing, userName, CancellationToken.None);
            return true;
        }

        public async Task<bool> DeleteAsync(Guid id, Guid? deletedBy = null)
        {
            // SoftDelete is handled automatically by DAL based on [SoftDelete] attribute
            // We just call DeleteAsync. The framework updates IsDeleted=1 instead of removing row.
            // Note: We ignore deletedBy here as the simple DeleteAsync signature might not support it,
            // or we rely on framework context. If supported, we'd pass it.
            await _dal.DeleteAsync(id); 
            return true;
        }

        public async Task<bool> ApproveAsync(Guid id, bool approve, Guid approvedBy, string? rejectionReason = null)
        {
            ProductEntity? entity = await _dal.GetByIdAsync(id);
            if (entity == null) return false;

            string userName = approvedBy.ToString();

            entity.ApprovalStatus = approve ? "Approved" : "Rejected";
            entity.ApprovedBy = userName;
            entity.ApprovedAt = DateTime.UtcNow;
            entity.RejectionReason = rejectionReason;
            entity.ModifiedBy = userName; // Use standard auto-generated property

            await _dal.UpdateAsync(entity, userName, CancellationToken.None);
            return true;
        }

        public async Task<bool> ProductExistsAsync(Guid id)
        {
            // Super fast InMemory check
            return await _dal.ExistsAsync(id); 
        }

        public async Task<int?> GetProductCountAsync()
        {
            return await _dal.CountAsync();
        }

        // Bulk Helpers
        public async Task<IEnumerable<string>> GetExistingBarcodesAsync(IEnumerable<string> barcodes)
        {
            // Optimization: Check InMemoryTable first if possible!
            // _dal.GetByBarcodesAsync(barcodes) should be generated.
            return await _searchAdapter.GetExistingBarcodesAsync(barcodes);
        }

        public async Task<Dictionary<string, Guid>> GetProductIdsByBarcodesAsync(IEnumerable<string> barcodes)
        {
             return await _searchAdapter.GetProductIdsByBarcodesAsync(barcodes);
        }

        public async Task BulkAddExternalLinksAsync(IEnumerable<(Guid ProductId, string Source, string ExternalId)> links)
        {
             List<ProductExternalLinkEntity> entities = links.Select(l => new ProductExternalLinkEntity
             {
                 Id = Guid.NewGuid(),
                 ProductId = l.ProductId,
                 Source = l.Source,
                 ExternalId = l.ExternalId,
                 IsDeleted = false
             }).ToList();

             string userName = "System";
             await _productExternalLinkDal.BulkInsertWithDuplicatesAsync(entities, userName, CancellationToken.None);
        }

        // Mappers
        private ProductDto MapEntityToDto(ProductEntity entity)
        {
            // Parse strings back to Guids if needed for DTO
            Guid.TryParse(entity.ApprovedBy, out Guid approvedBy);
            Guid.TryParse(entity.SubmittedBy, out Guid submittedBy);

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
                ApprovedBy = approvedBy == Guid.Empty ? null : approvedBy,
                ApprovedAt = entity.ApprovedAt,
                RejectionReason = entity.RejectionReason,
                SubmittedBy = submittedBy == Guid.Empty ? null : submittedBy,
                CreatedAt = entity.CreatedDate
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
                SubmittedBy = null, // Will be set by logic if needed, currently string
                IsDeleted = false
            };
        }

        private async Task LoadImagesAsync(ProductDto? product)
        {
            if (product == null) return;
            List<ProductImageModel> images = await _imageRepo.GetImagesByProductIdAsync(product.Id);
            product.Images = images.Select(MapImageToDto).ToList();
        }

        private async Task LoadImagesInBatchAsync(List<ProductDto> products)
        {
            if (products.Count == 0) return;
            Dictionary<Guid, List<ProductImageModel>> imagesByProductId = await _imageRepo.GetImagesByProductIdsAsync(products.Select(p => p.Id));
            foreach (ProductDto product in products)
            {
                if (imagesByProductId.TryGetValue(product.Id, out List<ProductImageModel>? images))
                {
                    product.Images = images.Select(MapImageToDto).ToList();
                }
                else
                {
                    product.Images = [];
                }
            }
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
    }
}