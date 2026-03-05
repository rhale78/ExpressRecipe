using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.Shared.Services;
using ExpressRecipe.Client.Shared.Services;
using Microsoft.Data.SqlClient;
using System.Data;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.ProductService.Data;

public interface IIngredientRepository
{
    // These now proxy to the Ingredient Microservice
    Task<List<IngredientDto>> GetAllAsync();
    Task<IngredientDto?> GetByIdAsync(Guid id);
    Task<List<IngredientDto>> SearchByNameAsync(string searchTerm);
    Task<List<IngredientDto>> GetByCategoryAsync(string category);
    Task<Guid> CreateAsync(CreateIngredientRequest request, Guid? createdBy = null);
    Task<bool> UpdateAsync(Guid id, UpdateIngredientRequest request, Guid? updatedBy = null);
    Task<bool> DeleteAsync(Guid id, Guid? deletedBy = null);
    
    // These remain local as they manage the product-ingredient relationship
    Task<List<ProductIngredientDto>> GetProductIngredientsAsync(Guid productId);
    Task<Guid> AddProductIngredientAsync(Guid productId, AddProductIngredientRequest request, Guid? createdBy = null);
    Task<bool> RemoveProductIngredientAsync(Guid productIngredientId, Guid? deletedBy = null);

    // Bulk operations proxy to microservice
    Task<Dictionary<string, Guid>> GetIngredientIdsByNamesAsync(IEnumerable<string> names);
    Task<int> BulkCreateIngredientsAsync(IEnumerable<string> names, Guid? createdBy = null);
    Task<Dictionary<string, Guid>> GetAllIngredientNamesAndIdsAsync();
}

/// <summary>
/// Refactored IngredientRepository that acts as a gateway to the Ingredient Microservice.
/// Relationship data (ProductIngredient) remains local, but core Ingredient data is remote.
/// </summary>
public class IngredientRepository : SqlHelper, IIngredientRepository
{
    private readonly IIngredientServiceClient _ingredientClient;
    private readonly HybridCacheService? _cache;
    private readonly ILogger<IngredientRepository>? _logger;

    public IngredientRepository(
        string connectionString, 
        IIngredientServiceClient ingredientClient,
        HybridCacheService? cache = null, 
        ILogger<IngredientRepository>? logger = null) : base(connectionString)
    {
        _ingredientClient = ingredientClient;
        _cache = cache;
        _logger = logger;
    }

    // Proxy methods to Microservice
    
    public async Task<List<IngredientDto>> GetAllAsync()
    {
        // For "GetAll", the microservice might not have a perfect match if it's too many
        // For now, we'll assume a reasonable limit or use search with empty term
        return await SearchByNameAsync("");
    }

    public async Task<IngredientDto?> GetByIdAsync(Guid id)
    {
        return await _ingredientClient.GetIngredientAsync(id);
    }

    public async Task<List<IngredientDto>> SearchByNameAsync(string searchTerm)
    {
        // Assuming IngredientService has a search endpoint, if not we use name lookup
        // For now, let's use the REST client's capabilities
        // If the client doesn't have SearchByName, we might need to add it or use GET by name
        var result = await _ingredientClient.GetIngredientIdByNameAsync(searchTerm);
        if (result.HasValue)
        {
            var ingredient = await _ingredientClient.GetIngredientAsync(result.Value);
            return ingredient != null ? new List<IngredientDto> { ingredient } : new List<IngredientDto>();
        }
        return new List<IngredientDto>();
    }

    public async Task<List<IngredientDto>> GetByCategoryAsync(string category)
    {
        // Proxy to microservice
        // TODO: Add GetByCategory to IIngredientServiceClient if needed
        return new List<IngredientDto>();
    }

    public async Task<Guid> CreateAsync(CreateIngredientRequest request, Guid? createdBy = null)
    {
        var id = await _ingredientClient.CreateIngredientAsync(request);
        return id ?? Guid.Empty;
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateIngredientRequest request, Guid? updatedBy = null)
    {
        // TODO: Add Update to IIngredientServiceClient
        return true; 
    }

    public async Task<bool> DeleteAsync(Guid id, Guid? deletedBy = null)
    {
        // TODO: Add Delete to IIngredientServiceClient
        return true;
    }

    public async Task<Dictionary<string, Guid>> GetIngredientIdsByNamesAsync(IEnumerable<string> names)
    {
        return await _ingredientClient.LookupIngredientIdsAsync(names.ToList());
    }

    public async Task<int> BulkCreateIngredientsAsync(IEnumerable<string> names, Guid? createdBy = null)
    {
        return await _ingredientClient.BulkCreateIngredientsAsync(names.ToList());
    }

    public async Task<Dictionary<string, Guid>> GetAllIngredientNamesAndIdsAsync()
    {
        var ingredients = await _ingredientClient.GetAllIngredientsAsync();
        return ingredients.ToDictionary(i => i.Name, i => i.Id, StringComparer.OrdinalIgnoreCase);
    }

    // Local Relationship Methods (ProductIngredient table)

    public async Task<List<ProductIngredientDto>> GetProductIngredientsAsync(Guid productId)
    {
        const string sql = @"
            SELECT pi.Id, pi.ProductId, pi.IngredientId,
                   pi.OrderIndex, pi.Quantity, pi.Notes, pi.IngredientListString
            FROM ProductIngredient pi
            WHERE pi.ProductId = @ProductId AND pi.IsDeleted = 0
            ORDER BY pi.OrderIndex";

        var productIngredients = await ExecuteReaderAsync(
            sql,
            reader => new ProductIngredientDto
            {
                Id = GetGuid(reader, "Id"),
                ProductId = GetGuid(reader, "ProductId"),
                IngredientId = GetGuid(reader, "IngredientId"),
                OrderIndex = GetInt32(reader, "OrderIndex"),
                Quantity = GetString(reader, "Quantity"),
                Notes = GetString(reader, "Notes"),
                IngredientListString = GetString(reader, "IngredientListString")
            },
            CreateParameter("@ProductId", productId));

        // Enrich all ingredients in parallel by batching calls to the Ingredient microservice
        var ingredientIds = productIngredients
            .Select(pi => pi.IngredientId)
            .Distinct()
            .ToList();

        // Fetch all unique ingredient details in parallel
        var ingredientTasks = ingredientIds
            .Select(ingredientId => _ingredientClient.GetIngredientAsync(ingredientId))
            .ToArray();

        var ingredientResults = await Task.WhenAll(ingredientTasks);

        // Build lookup dictionary from results
        var ingredientMap = ingredientIds
            .Zip(ingredientResults, (id, dto) => (id, dto))
            .Where(x => x.dto != null)
            .ToDictionary(x => x.id, x => x.dto!.Name);

        // Apply names from the lookup
        foreach (var pi in productIngredients)
        {
            pi.IngredientName = ingredientMap.TryGetValue(pi.IngredientId, out var name)
                ? name
                : "Unknown Ingredient";
        }

        return productIngredients;
    }

    public async Task<Guid> AddProductIngredientAsync(Guid productId, AddProductIngredientRequest request, Guid? createdBy = null)
    {
        const string checkSql = @"
            SELECT Id FROM ProductIngredient
            WHERE ProductId = @ProductId
              AND IngredientId = @IngredientId
              AND IsDeleted = 0";

        var existingId = await ExecuteScalarAsync<Guid?>(
            checkSql,
            CreateParameter("@ProductId", productId),
            CreateParameter("@IngredientId", request.IngredientId));

        if (existingId.HasValue) return existingId.Value;

        const string sql = @"
            INSERT INTO ProductIngredient (
                Id, ProductId, IngredientId, OrderIndex, Quantity, Notes, IngredientListString,
                CreatedBy, CreatedAt
            )
            VALUES (
                @Id, @ProductId, @IngredientId, @OrderIndex, @Quantity, @Notes, @IngredientListString,
                @CreatedBy, GETUTCDATE()
            )";

        var productIngredientId = Guid.NewGuid();
        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", productIngredientId),
            CreateParameter("@ProductId", productId),
            CreateParameter("@IngredientId", request.IngredientId),
            CreateParameter("@OrderIndex", request.OrderIndex),
            CreateParameter("@Quantity", request.Quantity),
            CreateParameter("@Notes", request.Notes),
            CreateParameter("@IngredientListString", request.IngredientListString),
            CreateParameter("@CreatedBy", createdBy));

        return productIngredientId;
    }

    public async Task<bool> RemoveProductIngredientAsync(Guid productIngredientId, Guid? deletedBy = null)
    {
        const string sql = @"
            UPDATE ProductIngredient
            SET IsDeleted = 1,
                DeletedAt = GETUTCDATE(),
                UpdatedBy = @DeletedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", productIngredientId),
            CreateParameter("@DeletedBy", deletedBy));

        return rowsAffected > 0;
    }
}
