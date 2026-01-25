using System;
using System.Linq;
using ExpressRecipe.ProductService.Entities;

using HighSpeedDAL.Core;
using HighSpeedDAL.Core.InMemoryTable;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.ProductService.Data
{
    /// <summary>
    /// Adapter that implements IProductStagingRepository using HighSpeedDAL generated DAL.
    /// All operations use the DAL's in-memory table and caching features.
    /// </summary>
    public class ProductStagingRepositoryAdapter : IProductStagingRepository
    {
        private readonly ProductStagingEntityDal _dal;
        private readonly InMemoryTable<ProductStagingEntity> _memoryTable;
        private readonly ILogger<ProductStagingRepositoryAdapter> _logger;

        public ProductStagingRepositoryAdapter(
            ProductStagingEntityDal dal,
            InMemoryTableManager tableManager,
            ILogger<ProductStagingRepositoryAdapter> logger)
        {
            _dal = dal ?? throw new ArgumentNullException(nameof(dal));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Resolve InMemoryTable for efficient lookups
            _memoryTable = tableManager.GetTable<ProductStagingEntity>("ProductStaging")
                ?? throw new InvalidOperationException("InMemoryTable for 'ProductStaging' is not registered.");
        }

        public async Task<Guid> InsertStagingProductAsync(StagedProduct product)
        {
            ProductStagingEntity entity = MapStagedProductToEntity(product);
            await _dal.InsertAsync(entity, "System", System.Threading.CancellationToken.None);
            return entity.Id;
        }

        public async Task<int> BulkInsertStagingProductsAsync(IEnumerable<StagedProduct> products)
        {
            List<StagedProduct> productList = products.ToList();
            if (productList.Count == 0)
            {
                return 0;
            }

            // 1. Identify which products already exist to avoid Unique Key violations
            List<string> externalIds = productList.Select(p => p.ExternalId).Distinct().ToList();
            HashSet<string> existingIds = await GetExistingExternalIdsAsync(externalIds);

            // 2. Filter out duplicates
            List<StagedProduct> newProducts = productList
                .Where(p => !existingIds.Contains(p.ExternalId))
                .DistinctBy(p => p.ExternalId)
                .ToList();

            if (newProducts.Count == 0)
            {
                return 0;
            }

            // 3. Map StagedProduct POCOs to ProductStagingEntity for HighSpeedDAL
            List<ProductStagingEntity> entities = newProducts.Select(MapStagedProductToEntity).ToList();
            foreach (ProductStagingEntity entity in entities)
            {
                entity.IsDeleted = false;
            }
            // 4. Use duplicate-handling bulk insert (handles race conditions)
            BulkInsertResult<ProductStagingEntity> result = await _dal.BulkInsertWithDuplicatesAsync(entities, "System", System.Threading.CancellationToken.None);
            return result.InsertedCount;
        }

        private async Task<HashSet<string>> GetExistingExternalIdsAsync(List<string> externalIds)
        {
            HashSet<string> existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (externalIds.Count == 0)
            {
                return existing;
            }

            // PERFORMANCE: Use O(1) InMemory lookup via property cache
            foreach (var extId in externalIds)
            {
                // GetByPropertyAsync builds/uses a dictionary cache on ExternalId
                var entity = await _memoryTable.GetByPropertyAsync(nameof(ProductStagingEntity.ExternalId), extId);
                if (entity != null && !entity.IsDeleted)
                {
                    existing.Add(entity.ExternalId);
                }
            }

            return existing;
        }

        private static string? SanitizeScore(string? score)
        {
            return string.IsNullOrWhiteSpace(score) ? null : score.Length > 10 ? score.Substring(0, 10) : score;
        }

        public async Task<int> BulkAugmentStagingProductsAsync(IEnumerable<StagedProduct> products, string sourceLabel)
        {
            List<StagedProduct> productList = products.ToList();
            if (productList.Count == 0)
            {
                return 0;
            }

            int augmentedCount = 0;
            List<ProductStagingEntity> entitiesToUpdate = [];

            // PERFORMANCE: Batch lookup by barcode using InMemoryTable O(1) cache
            List<string> barcodes = productList
                .Where(p => !string.IsNullOrWhiteSpace(p.Barcode))
                .Select(p => p.Barcode!)
                .Distinct()
                .ToList();

            Dictionary<string, ProductStagingEntity> byBarcode = new Dictionary<string, ProductStagingEntity>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var barcode in barcodes)
            {
                var entity = await _memoryTable.GetByPropertyAsync(nameof(ProductStagingEntity.Barcode), barcode);
                if (entity != null && !entity.IsDeleted)
                {
                    byBarcode[barcode] = entity;
                }
            }

            // Apply COALESCE logic for each product
            foreach (StagedProduct product in productList)
            {
                if (string.IsNullOrWhiteSpace(product.Barcode))
                {
                    continue;
                }

                if (!byBarcode.TryGetValue(product.Barcode, out ProductStagingEntity? existing))
                {
                    continue;
                }

                // Apply COALESCE logic - only fill in null/empty fields
                bool updated = false;
                if (string.IsNullOrEmpty(existing.ProductName) && !string.IsNullOrEmpty(product.ProductName)) { existing.ProductName = product.ProductName; updated = true; }
                if (string.IsNullOrEmpty(existing.GenericName) && !string.IsNullOrEmpty(product.GenericName)) { existing.GenericName = product.GenericName; updated = true; }
                if (string.IsNullOrEmpty(existing.Brands) && !string.IsNullOrEmpty(product.Brands)) { existing.Brands = product.Brands; updated = true; }
                if (string.IsNullOrEmpty(existing.IngredientsText) && !string.IsNullOrEmpty(product.IngredientsText)) { existing.IngredientsText = product.IngredientsText; updated = true; }
                if (string.IsNullOrEmpty(existing.IngredientsTextEn) && !string.IsNullOrEmpty(product.IngredientsTextEn)) { existing.IngredientsTextEn = product.IngredientsTextEn; updated = true; }
                if (string.IsNullOrEmpty(existing.Allergens) && !string.IsNullOrEmpty(product.Allergens)) { existing.Allergens = product.Allergens; updated = true; }
                if (string.IsNullOrEmpty(existing.AllergensHierarchy) && !string.IsNullOrEmpty(product.AllergensHierarchy)) { existing.AllergensHierarchy = product.AllergensHierarchy; updated = true; }
                if (string.IsNullOrEmpty(existing.Categories) && !string.IsNullOrEmpty(product.Categories)) { existing.Categories = product.Categories; updated = true; }
                if (string.IsNullOrEmpty(existing.CategoriesHierarchy) && !string.IsNullOrEmpty(product.CategoriesHierarchy)) { existing.CategoriesHierarchy = product.CategoriesHierarchy; updated = true; }
                if (string.IsNullOrEmpty(existing.NutritionData) && !string.IsNullOrEmpty(product.NutritionData)) { existing.NutritionData = product.NutritionData; updated = true; }
                if (string.IsNullOrEmpty(existing.ImageUrl) && !string.IsNullOrEmpty(product.ImageUrl)) { existing.ImageUrl = product.ImageUrl; updated = true; }
                if (string.IsNullOrEmpty(existing.ImageSmallUrl) && !string.IsNullOrEmpty(product.ImageSmallUrl)) { existing.ImageSmallUrl = product.ImageSmallUrl; updated = true; }
                if (string.IsNullOrEmpty(existing.Countries) && !string.IsNullOrEmpty(product.Countries)) { existing.Countries = product.Countries; updated = true; }
                if (string.IsNullOrEmpty(existing.NutriScore) && !string.IsNullOrEmpty(product.NutriScore)) { existing.NutriScore = SanitizeScore(product.NutriScore); updated = true; }
                if (existing.NovaGroup == null && product.NovaGroup != null) { existing.NovaGroup = product.NovaGroup; updated = true; }
                if (string.IsNullOrEmpty(existing.EcoScore) && !string.IsNullOrEmpty(product.EcoScore)) { existing.EcoScore = SanitizeScore(product.EcoScore); updated = true; }

                if (updated)
                {
                    entitiesToUpdate.Add(existing);
                    augmentedCount++;
                }
            }

            // Bulk update all modified entities using DAL (leverages in-memory table)
            if (entitiesToUpdate.Count != 0)
            {
                await _dal.BulkUpdateAsync(entitiesToUpdate, sourceLabel, System.Threading.CancellationToken.None);
            }

            _logger.LogInformation("Augmented {Count} staging products from source {Source}", augmentedCount, sourceLabel);
            return augmentedCount;
        }

        public async Task<List<StagedProduct>> GetPendingProductsAsync(int limit = 100)
        {
            // PERFORMANCE: Use O(1) lookup for "Pending" status from InMemoryTable property cache
            // GetByPropertyAsync(..., returnMultiple: true)
            List<ProductStagingEntity> pendingEntities = await _memoryTable.GetByPropertyAsync(
                nameof(ProductStagingEntity.ProcessingStatus), 
                "Pending", 
                returnMultiple: true);

            // Filter in memory for additional criteria and apply limit
            List<StagedProduct> filtered = pendingEntities
                .Where(e => !e.IsDeleted && e.ProcessingAttempts < 3)
                .OrderBy(e => e.CreatedDate)
                .Take(limit)
                .Select(MapEntityToStagedProduct)
                .ToList();

            return filtered;
        }

        public async Task UpdateProcessingStatusAsync(Guid id, string status, string? error = null)
        {
            ProductStagingEntity? entity = await _dal.GetByIdAsync(id);
            if (entity == null)
            {
                return;
            }

            entity.ProcessingStatus = status;
            entity.ProcessingAttempts = entity.ProcessingAttempts + 1;
            entity.ProcessingError = error;
            if (status == "Completed")
            {
                entity.ProcessedAt = DateTime.UtcNow;
            }

            await _dal.UpdateAsync(entity, "System", System.Threading.CancellationToken.None);
        }

        public async Task BulkUpdateProcessingStatusAsync(IEnumerable<Guid> ids, string status, string? error = null)
        {
            List<Guid> idList = ids.ToList();
            if (idList.Count == 0)
            {
                return;
            }

            // Call the optimized bulk update method from the partial ProductStagingEntityDal class
            await _dal.BulkUpdateProcessingStatusAsync(idList, status, error, System.Threading.CancellationToken.None);

            _logger.LogInformation("Bulk updated {Count} staging products to status {Status}", idList.Count, status);
        }

        public async Task<int> GetPendingCountAsync()
        {
            // PERFORMANCE: Use O(1) lookup for "Pending" count
            List<ProductStagingEntity> pendingEntities = await _memoryTable.GetByPropertyAsync(
                nameof(ProductStagingEntity.ProcessingStatus),
                "Pending",
                returnMultiple: true);

            return pendingEntities.Count(e => !e.IsDeleted && e.ProcessingAttempts < 3);
        }

        public async Task<int> CleanupOldStagingRecordsAsync(int daysToKeep = 7)
        {
            _logger.LogInformation("Starting cleanup of ProductStaging records older than {DaysToKeep} days", daysToKeep);

            // Fetch all entities from in-memory table (could optimize if we had an index on ProcessedAt, but scan is OK for background task)
            IEnumerable<ProductStagingEntity> allEntities = _memoryTable.Select();
            DateTime cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);

            // Find old completed/failed records
            List<ProductStagingEntity> oldRecords = allEntities
                .Where(e => !e.IsDeleted &&
                           (e.ProcessingStatus == "Completed" || e.ProcessingStatus == "Failed") &&
                           e.ProcessedAt.HasValue &&
                           e.ProcessedAt.Value < cutoffDate)
                .ToList();

            if (oldRecords.Count == 0)
            {
                _logger.LogInformation("No old staging records to clean up");
                return 0;
            }

            _logger.LogInformation("Deleting {Count} old staging records", oldRecords.Count);

            // Soft delete in batches of 100
            int deleted = 0;
            const int BATCH_SIZE = 100;

            for (int i = 0; i < oldRecords.Count; i += BATCH_SIZE)
            {
                List<ProductStagingEntity> batch = oldRecords.Skip(i).Take(BATCH_SIZE).ToList();
                List<Guid> idsToDelete = batch.Select(r => r.Id).ToList();

                try
                {
                    await _dal.BulkDeleteAsync(idsToDelete);
                    deleted += batch.Count;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting batch of {BatchSize} staging records", batch.Count);
                }
            }

            _logger.LogInformation("Cleanup completed: deleted {Count} old staging records", deleted);
            return deleted;
        }

        private static ProductStagingEntity MapStagedProductToEntity(StagedProduct p)
        {
            return new ProductStagingEntity
            {
                Id = p.Id != Guid.Empty ? p.Id : Guid.NewGuid(),
                ExternalId = p.ExternalId,
                Barcode = p.Barcode,
                ProductName = p.ProductName,
                GenericName = p.GenericName,
                Brands = p.Brands,
                IngredientsText = p.IngredientsText,
                IngredientsTextEn = p.IngredientsTextEn,
                Allergens = p.Allergens,
                AllergensHierarchy = p.AllergensHierarchy,
                Categories = p.Categories,
                CategoriesHierarchy = p.CategoriesHierarchy,
                NutritionData = p.NutritionData,
                ImageUrl = p.ImageUrl,
                ImageSmallUrl = p.ImageSmallUrl,
                Lang = p.Lang,
                Countries = p.Countries,
                NutriScore = SanitizeScore(p.NutriScore),
                NovaGroup = p.NovaGroup,
                EcoScore = SanitizeScore(p.EcoScore),
                RawJson = p.RawJson,
                ProcessingStatus = p.ProcessingStatus ?? "Pending",
                ProcessedAt = p.ProcessedAt,
                ProcessingError = p.ProcessingError,
                ProcessingAttempts = p.ProcessingAttempts,
                CreatedDate = p.CreatedDate != default ? p.CreatedDate : DateTime.UtcNow,
                IsDeleted = false
            };
        }

        private static StagedProduct MapEntityToStagedProduct(ProductStagingEntity e)
        {
            return new StagedProduct
            {
                Id = e.Id,
                ExternalId = e.ExternalId,
                Barcode = e.Barcode,
                ProductName = e.ProductName,
                GenericName = e.GenericName,
                Brands = e.Brands,
                IngredientsText = e.IngredientsText,
                IngredientsTextEn = e.IngredientsTextEn,
                Allergens = e.Allergens,
                AllergensHierarchy = e.AllergensHierarchy,
                Categories = e.Categories,
                CategoriesHierarchy = e.CategoriesHierarchy,
                NutritionData = e.NutritionData,
                ImageUrl = e.ImageUrl,
                ImageSmallUrl = e.ImageSmallUrl,
                Lang = e.Lang,
                Countries = e.Countries,
                NutriScore = e.NutriScore,
                NovaGroup = e.NovaGroup,
                EcoScore = e.EcoScore,
                RawJson = e.RawJson,
                ProcessingStatus = e.ProcessingStatus,
                ProcessedAt = e.ProcessedAt,
                ProcessingError = e.ProcessingError,
                ProcessingAttempts = e.ProcessingAttempts,
                CreatedDate = e.CreatedDate,
                ModifiedDate = e.ModifiedDate // Updated to use auto-generated property
            };
        }
    }
}
