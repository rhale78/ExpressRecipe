using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ExpressRecipe.ProductService.Entities;
using ExpressRecipe.Shared.DTOs.Product;
using HighSpeedDAL.Core;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.ProductService.Data
{
    /// <summary>
    /// Adapter that implements IIngredientRepository using HighSpeedDAL generated DALs.
    /// All operations use the DAL's caching and in-memory table features.
    /// </summary>
    public class IngredientRepositoryAdapter : IIngredientRepository
    {
        private readonly IngredientEntityDal _dal;
        private readonly ProductIngredientEntityDal _productIngredientDal;
        private readonly ILogger<IngredientRepositoryAdapter> _logger;
        private readonly DalMetricsCollector? _metrics;

        public IngredientRepositoryAdapter(
            IngredientEntityDal dal,
            ProductIngredientEntityDal productIngredientDal,
            DalMetricsCollector? metrics,
            ILogger<IngredientRepositoryAdapter> logger)
        {
            _dal = dal ?? throw new ArgumentNullException(nameof(dal));
            _productIngredientDal = productIngredientDal ?? throw new ArgumentNullException(nameof(productIngredientDal));
            _metrics = metrics;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<IngredientDto>> GetAllAsync()
        {
            Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                _metrics?.RecordOperation("Ingredient", "GetAll");
                List<IngredientEntity> entities = await _dal.GetAllAsync();
                return entities.Select(MapEntityToDto).ToList();
            }
            finally
            {
                sw.Stop();
                _metrics?.RecordOperationDuration("Ingredient", "GetAll", sw.ElapsedMilliseconds);
            }
        }

        public async Task<IngredientDto?> GetByIdAsync(Guid id)
        {
            Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                _metrics?.RecordOperation("Ingredient", "GetById");
                IngredientEntity? e = await _dal.GetByIdAsync(id);
                return e is null ? null : MapEntityToDto(e);
            }
            finally
            {
                sw.Stop();
                _metrics?.RecordOperationDuration("Ingredient", "GetById", sw.ElapsedMilliseconds);
            }
        }

        public async Task<List<IngredientDto>> SearchByNameAsync(string searchTerm)
        {
            Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                _metrics?.RecordOperation("Ingredient", "SearchByName");
                // IngredientEntity has [ReferenceTable] + [Cache] - fetch all from cache and filter in memory
                // This is efficient because ingredients are a reference table with limited size
                List<IngredientEntity> allEntities = await _dal.GetAllAsync();
                var searchLower = searchTerm.ToLowerInvariant();

                List<IngredientDto> filtered = allEntities
                    .Where(e => (e.Name?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true) ||
                               (e.AlternativeNames?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true))
                    .OrderBy(e => e.Name)
                    .Select(MapEntityToDto)
                    .ToList();

                return filtered;
            }
            finally
            {
                sw.Stop();
                _metrics?.RecordOperationDuration("Ingredient", "SearchByName", sw.ElapsedMilliseconds);
            }
        }

        public async Task<List<IngredientDto>> GetByCategoryAsync(string category)
        {
            Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                _metrics?.RecordOperation("Ingredient", "GetByCategory");
                // Uses generated [NamedQuery("ByCategory", nameof(Category))] method
                // SQL: SELECT * FROM [Ingredient] WHERE [Category] = @Category AND [IsDeleted] = 0
                List<IngredientEntity> entities = await _dal.GetByCategoryAsync(category);

                return entities
                    .OrderBy(e => e.Name)
                    .Select(MapEntityToDto)
                    .ToList();
            }
            finally
            {
                sw.Stop();
                _metrics?.RecordOperationDuration("Ingredient", "GetByCategory", sw.ElapsedMilliseconds);
            }
        }

        public async Task<Guid> CreateAsync(CreateIngredientRequest request, Guid? createdBy = null)
        {
            Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                _metrics?.RecordOperation("Ingredient", "Insert");
                string userName = createdBy?.ToString() ?? "System";
                
                IngredientEntity entity = new IngredientEntity
                {
                    Id = Guid.NewGuid(),
                    Name = request.Name,
                    Description = request.Description,
                    Category = request.Category,
                    IsCommonAllergen = request.IsCommonAllergen,
                    // Note: CreatedDate/ModifiedDate are auto-populated by HighSpeedDAL's InsertAsync
                    IsDeleted = false
                };
                
                // Generated DAL InsertAsync requires userName and CancellationToken parameters
                await _dal.InsertAsync(entity, userName, System.Threading.CancellationToken.None);
                return entity.Id;
            }
            finally
            {
                sw.Stop();
                _metrics?.RecordOperationDuration("Ingredient", "Insert", sw.ElapsedMilliseconds);
            }
        }

        public async Task<bool> UpdateAsync(Guid id, UpdateIngredientRequest request, Guid? updatedBy = null)
        {
            Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                _metrics?.RecordOperation("Ingredient", "Update");
                IngredientEntity? existing = await _dal.GetByIdAsync(id);
                if (existing == null)
                {
                    return false;
                }

                string userName = updatedBy?.ToString() ?? "System";

                existing.Name = request.Name;
                existing.Description = request.Description;
                existing.Category = request.Category;
                existing.IsCommonAllergen = request.IsCommonAllergen;
                existing.ModifiedBy = userName; // Use standard auto-generated property
                
                // Generated DAL UpdateAsync requires userName and CancellationToken parameters
                await _dal.UpdateAsync(existing, userName, System.Threading.CancellationToken.None);
                return true;
            }
            finally
            {
                sw.Stop();
                _metrics?.RecordOperationDuration("Ingredient", "Update", sw.ElapsedMilliseconds);
            }
        }

        public async Task<bool> DeleteAsync(Guid id, Guid? deletedBy = null)
        {
            Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                _metrics?.RecordOperation("Ingredient", "Delete");
                // If generated DAL doesn't expose a SoftDelete, perform soft-delete via entity update
                IngredientEntity? existing = await _dal.GetByIdAsync(id);
                if (existing == null)
                {
                    return false;
                }

                // If DAL supports soft delete natively via DeleteAsync, we can use that.
                // But since we want to set 'UpdatedBy' (deletedBy), updating is explicit.
                string userName = deletedBy?.ToString() ?? "System";
                
                existing.IsDeleted = true;
                existing.ModifiedBy = userName; // Set ModifiedBy instead of UpdatedBy
                existing.DeletedDate = DateTime.UtcNow; // Set standard DeletedDate
                
                await _dal.UpdateAsync(existing, userName, System.Threading.CancellationToken.None);
                return true;
            }
            finally
            {
                sw.Stop();
                _metrics?.RecordOperationDuration("Ingredient", "Delete", sw.ElapsedMilliseconds);
            }
        }

        public async Task<List<ProductIngredientDto>> GetProductIngredientsAsync(Guid productId)
        {
            Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                _metrics?.RecordOperation("ProductIngredient", "GetByProductId");
                // Uses generated [NamedQuery("ByProductId", nameof(ProductId))] method
                // Named query already filters IsDeleted=0
                List<ProductIngredientEntity> productIngredients = await _productIngredientDal.GetByProductIdAsync(productId);

                if (productIngredients.Count == 0)
                {
                    return [];
                }

                // Batch-fetch Ingredient entities to get names (uses DAL cache)
                List<Guid> ingredientIds = productIngredients.Select(pi => pi.IngredientId).Distinct().ToList();
                List<IngredientEntity> ingredients = await _dal.GetByIdsAsync(ingredientIds);
                Dictionary<Guid, string> ingredientLookup = ingredients.ToDictionary(i => i.Id, i => i.Name ?? string.Empty);

                // Map to DTOs with ingredient names
                return productIngredients
                    .OrderBy(pi => pi.OrderIndex)
                    .ThenBy(pi => ingredientLookup.GetValueOrDefault(pi.IngredientId, string.Empty))
                    .Select(pi => new ProductIngredientDto
                    {
                        Id = pi.Id,
                        ProductId = pi.ProductId,
                        IngredientId = pi.IngredientId,
                        IngredientName = ingredientLookup.GetValueOrDefault(pi.IngredientId, string.Empty),
                        OrderIndex = pi.OrderIndex,
                        Quantity = pi.Quantity,
                        Notes = pi.Notes,
                        IngredientListString = null // Not stored in ProductIngredientEntity
                    })
                    .ToList();
            }
            finally
            {
                sw.Stop();
                _metrics?.RecordOperationDuration("ProductIngredient", "GetByProductId", sw.ElapsedMilliseconds);
            }
        }

        public async Task<Guid> AddProductIngredientAsync(Guid productId, AddProductIngredientRequest request, Guid? createdBy = null)
        {
            Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                _metrics?.RecordOperation("ProductIngredient", "Insert");
                // Uses generated [NamedQuery("ByProductId", nameof(ProductId))] method
                // Named query already filters IsDeleted=0
                List<ProductIngredientEntity> existingLinks = await _productIngredientDal.GetByProductIdAsync(productId);
                ProductIngredientEntity? existing = existingLinks.FirstOrDefault(pi => pi.IngredientId == request.IngredientId);
                if (existing != null)
                {
                    return existing.Id;
                }

                string userName = createdBy?.ToString() ?? "System";

                // Create new ProductIngredient entity
                ProductIngredientEntity newEntity = new ProductIngredientEntity
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    IngredientId = request.IngredientId,
                    OrderIndex = request.OrderIndex,
                    Quantity = request.Quantity,
                    Notes = request.Notes,
                    IsDeleted = false
                };

                try
                {
                    await _productIngredientDal.InsertAsync(newEntity, userName, System.Threading.CancellationToken.None);
                    return newEntity.Id;
                }
                catch (Exception ex) when (ex.Message.Contains("duplicate") || ex.Message.Contains("unique"))
                {
                    // Handle race condition - re-check using named query
                    List<ProductIngredientEntity> refreshedLinks = await _productIngredientDal.GetByProductIdAsync(productId);
                    ProductIngredientEntity? refreshedExisting = refreshedLinks.FirstOrDefault(pi => pi.IngredientId == request.IngredientId);
                    if (refreshedExisting != null)
                    {
                        return refreshedExisting.Id;
                    }

                    throw;
                }
            }
            finally
            {
                sw.Stop();
                _metrics?.RecordOperationDuration("ProductIngredient", "Insert", sw.ElapsedMilliseconds);
            }
        }

        public async Task<bool> RemoveProductIngredientAsync(Guid productIngredientId, Guid? deletedBy = null)
        {
            Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                _metrics?.RecordOperation("ProductIngredient", "Delete");
                // Fetch the entity and soft-delete using DAL
                ProductIngredientEntity? entity = await _productIngredientDal.GetByIdAsync(productIngredientId);
                if (entity == null || entity.IsDeleted)
                {
                    return false;
                }

                string userName = deletedBy?.ToString() ?? "System";
                entity.IsDeleted = true;
                
                await _productIngredientDal.UpdateAsync(entity, userName, System.Threading.CancellationToken.None);
                return true;
            }
            finally
            {
                sw.Stop();
                _metrics?.RecordOperationDuration("ProductIngredient", "Delete", sw.ElapsedMilliseconds);
            }
        }

        public async Task<Dictionary<string, Guid>> GetIngredientIdsByNamesAsync(IEnumerable<string> names)
        {
            List<string> namesList = names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (namesList.Count == 0)
            {
                return [];
            }

            Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                _metrics?.RecordOperation("Ingredient", "GetIdsByNames", namesList.Count);

                // PERFORMANCE: For better performance with many names, batch the lookups
                // instead of individual database queries. Limit batch size to 50 to avoid
                // creating too many SQL queries, but still much better than 100+ individual lookups.
                Dictionary<string, Guid> result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
                const int BATCH_SIZE = 50;

                for (int i = 0; i < namesList.Count; i += BATCH_SIZE)
                {
                    List<string> nameBatch = namesList.Skip(i).Take(BATCH_SIZE).ToList();

                    // Process this batch of names in parallel for better throughput
                    IEnumerable<Task<IngredientEntity?>> tasks = nameBatch.Select(async name =>
                    {
                        try
                        {
                            IngredientEntity? ingredient = await _dal.GetByNameAsync(name);
                            return ingredient;
                        }
                        catch
                        {
                            // Ingredient not found, will be created on demand
                            return null;
                        }
                    });

                    IngredientEntity?[] results = await Task.WhenAll(tasks);
                    foreach (IngredientEntity? ingredient in results)
                    {
                        if (ingredient?.Name != null)
                        {
                            result[ingredient.Name] = ingredient.Id;
                        }
                    }
                }

                return result;
            }
            finally
            {
                sw.Stop();
                _metrics?.RecordBatchOperation("Ingredient", "GetIdsByNames", namesList.Count, sw.ElapsedMilliseconds);
            }
        }

        public async Task<int> BulkCreateIngredientsAsync(IEnumerable<string> names, Guid? createdBy = null)
        {
            List<string> namesList = names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (namesList.Count == 0)
            {
                return 0;
            }

            Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                string userName = createdBy?.ToString() ?? "System";
                
                // Create entities from names
                List<IngredientEntity> entities = namesList.Select(name => new IngredientEntity
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Category = "General",
                    IsCommonAllergen = false,
                    CreatedBy = userName // This will use the generated property
                }).ToList();

                // Use duplicate-handling bulk insert - extracts duplicates gracefully
                BulkInsertResult<IngredientEntity> result = await _dal.BulkInsertWithDuplicatesAsync(entities, userName, System.Threading.CancellationToken.None);

                if (result.HasDuplicates)
                {
                    _logger.LogDebug("Skipped {Count} duplicate ingredients (already exist)", result.DuplicateEntities.Count);
                }

                return result.InsertedCount;
            }
            finally
            {
                sw.Stop();
                _metrics?.RecordBatchOperation("Ingredient", "BulkInsert", namesList.Count, sw.ElapsedMilliseconds);
            }
        }

        public async Task<int> BulkAddProductIngredientsAsync(IEnumerable<(Guid ProductId, Guid IngredientId, int OrderIndex)> links)
        {
            List<(Guid ProductId, Guid IngredientId, int OrderIndex)> linkList = links.ToList();
            if (linkList.Count == 0)
            {
                return 0;
            }

            Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                List<ProductIngredientEntity> entities = linkList.Select(l => new ProductIngredientEntity
                {
                    Id = Guid.NewGuid(),
                    ProductId = l.ProductId,
                    IngredientId = l.IngredientId,
                    OrderIndex = l.OrderIndex,
                    IsDeleted = false
                }).ToList();

                string userName = "System";

                // Use duplicate-handling bulk insert
                BulkInsertResult<ProductIngredientEntity> result = await _productIngredientDal.BulkInsertWithDuplicatesAsync(entities, userName, System.Threading.CancellationToken.None);

                if (result.HasDuplicates)
                {
                    _logger.LogDebug("Skipped {Count} duplicate product-ingredient links", result.DuplicateEntities.Count);
                }

                return result.InsertedCount;
            }
            finally
            {
                sw.Stop();
                _metrics?.RecordBatchOperation("ProductIngredient", "BulkInsert", linkList.Count, sw.ElapsedMilliseconds);
            }
        }

        private static IngredientDto MapEntityToDto(IngredientEntity e)
        {
            return new IngredientDto
            {
                Id = e.Id,
                Name = e.Name ?? string.Empty,
                Description = e.Description,
                Category = e.Category,
                IsCommonAllergen = e.IsCommonAllergen,
                IngredientListString = null
            };
        }
    }
}
