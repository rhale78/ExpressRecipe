using System;
using System.Linq;
using ExpressRecipe.ProductService.Entities;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.ProductService.Data;

/// <summary>
/// Adapter that implements IProductStagingRepository using HighSpeedDAL generated DAL.
/// All operations use the DAL's in-memory table and caching features.
/// </summary>
public class ProductStagingRepositoryAdapter : IProductStagingRepository
{
    private readonly ProductStagingEntityDal _dal;
    private readonly ILogger<ProductStagingRepositoryAdapter> _logger;

    public ProductStagingRepositoryAdapter(ProductStagingEntityDal dal, ILogger<ProductStagingRepositoryAdapter> logger)
    {
        _dal = dal ?? throw new ArgumentNullException(nameof(dal));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> InsertStagingProductAsync(StagedProduct product)
    {
        var entity = MapStagedProductToEntity(product);
        await _dal.InsertAsync(entity, "System", System.Threading.CancellationToken.None);
        return entity.Id;
    }

    public async Task<int> BulkInsertStagingProductsAsync(IEnumerable<StagedProduct> products)
    {
        var productList = products.ToList();
        if (!productList.Any()) return 0;

        // 1. Identify which products already exist to avoid Unique Key violations
        // Use DAL's indexed GetByExternalIdAsync in batches
        var externalIds = productList.Select(p => p.ExternalId).Distinct().ToList();
        var existingIds = await GetExistingExternalIdsAsync(externalIds);

        // 2. Filter out duplicates
        var newProducts = productList
            .Where(p => !existingIds.Contains(p.ExternalId))
            .DistinctBy(p => p.ExternalId)
            .ToList();

        if (!newProducts.Any()) return 0;

        // 3. Map StagedProduct POCOs to ProductStagingEntity for HighSpeedDAL
        var entities = newProducts.Select(MapStagedProductToEntity).ToList();
        // Ensure IsDeleted is set to false for all entities (required for NOT NULL constraint)
        foreach (var entity in entities)
        {
            entity.IsDeleted = false;
        }
        // 4. Use duplicate-handling bulk insert (handles race conditions)
        var result = await _dal.BulkInsertWithDuplicatesAsync(entities, "System", System.Threading.CancellationToken.None);
        return result.InsertedCount;
    }

    private async Task<HashSet<string>> GetExistingExternalIdsAsync(List<string> externalIds)
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!externalIds.Any()) return existing;

        // PERFORMANCE: Batch lookup by ExternalId using named query
        // Instead of loading all 2.6M rows, lookup only the IDs we care about
        // Batch size of 100 to avoid too many queries, but way better than GetAllAsync
        const int BATCH_SIZE = 100;
        for (int i = 0; i < externalIds.Count; i += BATCH_SIZE)
        {
            var batch = externalIds.Skip(i).Take(BATCH_SIZE).ToList();

            foreach (var extId in batch)
            {
                try
                {
                    // Use single-result named query: GetByExternalIdAsync(externalId)
                    var entity = await _dal.GetByExternalIdAsync(extId);
                    if (entity != null && !entity.IsDeleted)
                    {
                        existing.Add(entity.ExternalId);
                    }
                }
                catch
                {
                    // If named query fails, skip and continue (item will be inserted as new)
                }
            }
        }

        return existing;
    }

    private static string? SanitizeScore(string? score)
    {
        if (string.IsNullOrWhiteSpace(score)) return null;
        if (score.Length > 10) return score.Substring(0, 10);
        return score;
    }

    public async Task<int> BulkAugmentStagingProductsAsync(IEnumerable<StagedProduct> products, string sourceLabel)
    {
        var productList = products.ToList();
        if (!productList.Any()) return 0;

        int augmentedCount = 0;
        var entitiesToUpdate = new List<ProductStagingEntity>();

        // PERFORMANCE: Batch lookup by barcode instead of loading all 2.6M rows
        // Get only the barcodes we need to check
        var barcodes = productList
            .Where(p => !string.IsNullOrWhiteSpace(p.Barcode))
            .Select(p => p.Barcode!)
            .Distinct()
            .ToList();

        // Batch lookup existing products by barcode (use named query: GetByBarcodeAsync)
        var byBarcode = new Dictionary<string, ProductStagingEntity>(StringComparer.OrdinalIgnoreCase);
        const int BATCH_SIZE = 50;
        for (int i = 0; i < barcodes.Count; i += BATCH_SIZE)
        {
            var batch = barcodes.Skip(i).Take(BATCH_SIZE).ToList();
            foreach (var barcode in batch)
            {
                try
                {
                    var entity = await _dal.GetByBarcodeAsync(barcode);
                    if (entity != null && !entity.IsDeleted)
                    {
                        byBarcode[barcode] = entity;
                    }
                }
                catch
                {
                    // If lookup fails, skip (entity won't be in dict and will be inserted as new)
                }
            }
        }

        // Apply COALESCE logic for each product
        foreach (var product in productList)
        {
            if (string.IsNullOrWhiteSpace(product.Barcode)) continue;

            if (!byBarcode.TryGetValue(product.Barcode, out var existing)) continue;

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
        if (entitiesToUpdate.Any())
        {
            await _dal.BulkUpdateAsync(entitiesToUpdate, sourceLabel, System.Threading.CancellationToken.None);
        }

        _logger.LogInformation("Augmented {Count} staging products from source {Source}", augmentedCount, sourceLabel);
        return augmentedCount;
    }

    public async Task<List<StagedProduct>> GetPendingProductsAsync(int limit = 100)
    {
        // PERFORMANCE: Use named query GetByProcessingStatusAsync instead of GetAllAsync
        // This avoids loading 2.6M rows when only needing pending items (~100 at a time)
        // Named query uses indexed database lookup and in-memory table partial load
        try
        {
            // Try using generated named query for Pending status
            var pendingEntities = await _dal.GetByProcessingStatusAsync("Pending");

            // Filter in memory for additional criteria and apply limit
            var filtered = pendingEntities
                .Where(e => !e.IsDeleted && e.ProcessingAttempts < 3)
                .OrderBy(e => e.CreatedDate)
                .Take(limit)
                .Select(MapEntityToStagedProduct)
                .ToList();

            return filtered;
        }
        catch
        {
            // Fallback if named query not available - filter in-memory using GetAllAsync
            // Only reached if GetByProcessingStatusAsync is not generated
            _logger.LogWarning("Named query GetByProcessingStatusAsync not available, falling back to GetAllAsync");
            var allEntities = await _dal.GetAllAsync();

            var filtered = allEntities
                .Where(e => !e.IsDeleted && e.ProcessingStatus == "Pending" && e.ProcessingAttempts < 3)
                .OrderBy(e => e.CreatedDate)
                .Take(limit)
                .Select(MapEntityToStagedProduct)
                .ToList();

            return filtered;
        }
    }

    public async Task UpdateProcessingStatusAsync(Guid id, string status, string? error = null)
    {
        var entity = await _dal.GetByIdAsync(id);
        if (entity == null) return;

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
        var idList = ids.ToList();
        if (!idList.Any()) return;

        // Fetch entities using DAL (may use in-memory cache if available)
        var entities = await _dal.GetByIdsAsync(idList);

        // Update the status fields
        var now = DateTime.UtcNow;
        foreach (var entity in entities)
        {
            entity.ProcessingStatus = status;
            entity.ProcessingAttempts = entity.ProcessingAttempts + 1;
            entity.ProcessingError = error;
            if (status == "Completed")
            {
                entity.ProcessedAt = now;
            }
        }

        // Use HighSpeedDAL's BulkUpdateAsync which leverages in-memory table if configured
        await _dal.BulkUpdateAsync(entities, "System", System.Threading.CancellationToken.None);
    }

    public async Task<int> GetPendingCountAsync()
    {
        // PERFORMANCE: Use named query GetByProcessingStatusAsync instead of GetAllAsync
        // Avoids loading 2.6M rows when only need to count pending items
        try
        {
            var pendingEntities = await _dal.GetByProcessingStatusAsync("Pending");
            return pendingEntities.Count(e => !e.IsDeleted && e.ProcessingAttempts < 3);
        }
        catch
        {
            // Fallback if named query not available
            _logger.LogWarning("Named query GetByProcessingStatusAsync not available, falling back to GetAllAsync");
            var allEntities = await _dal.GetAllAsync();
            return allEntities.Count(e => !e.IsDeleted && e.ProcessingStatus == "Pending" && e.ProcessingAttempts < 3);
        }
    }

    /// <summary>
    /// Cleans up old completed/failed ProductStaging records to prevent in-memory overflow.
    /// Keeps only completed records from the last 7 days, deletes older ones.
    /// This prevents the 2.6M row overflow issue when MaxRowCount is configured for 50K rows.
    /// </summary>
    public async Task<int> CleanupOldStagingRecordsAsync(int daysToKeep = 7)
    {
        _logger.LogInformation("Starting cleanup of ProductStaging records older than {DaysToKeep} days", daysToKeep);

        // Fetch all entities from in-memory table
        var allEntities = await _dal.GetAllAsync();
        var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);

        // Find old completed/failed records
        var oldRecords = allEntities
            .Where(e => !e.IsDeleted &&
                       (e.ProcessingStatus == "Completed" || e.ProcessingStatus == "Failed") &&
                       e.ProcessedAt.HasValue &&
                       e.ProcessedAt.Value < cutoffDate)
            .ToList();

        if (!oldRecords.Any())
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
            var batch = oldRecords.Skip(i).Take(BATCH_SIZE).ToList();
            var idsToDelete = batch.Select(r => r.Id).ToList();

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
            ModifiedDate = e.ModifiedDate
        };
    }
}
