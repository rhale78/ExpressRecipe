using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.Product;

namespace ExpressRecipe.ProductService.Data;

public interface IIngredientRepository
{
    Task<List<IngredientDto>> GetAllAsync();
    Task<IngredientDto?> GetByIdAsync(Guid id);
    Task<List<IngredientDto>> SearchByNameAsync(string searchTerm);
    Task<List<IngredientDto>> GetByCategoryAsync(string category);
    Task<Guid> CreateAsync(CreateIngredientRequest request, Guid? createdBy = null);
    Task<bool> UpdateAsync(Guid id, UpdateIngredientRequest request, Guid? updatedBy = null);
    Task<bool> DeleteAsync(Guid id, Guid? deletedBy = null);
    Task<List<ProductIngredientDto>> GetProductIngredientsAsync(Guid productId);
    Task<Guid> AddProductIngredientAsync(Guid productId, AddProductIngredientRequest request, Guid? createdBy = null);
    Task<bool> RemoveProductIngredientAsync(Guid productIngredientId, Guid? deletedBy = null);
}

public class IngredientRepository : SqlHelper, IIngredientRepository
{
    public IngredientRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task<List<IngredientDto>> GetAllAsync()
    {
        const string sql = @"
            SELECT Id, Name, AlternativeNames, Description, Category, IsCommonAllergen, IngredientListString
            FROM Ingredient
            WHERE IsDeleted = 0
            ORDER BY Name";

        return await ExecuteReaderAsync(
            sql,
            reader => new IngredientDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                AlternativeNames = GetString(reader, "AlternativeNames"),
                Description = GetString(reader, "Description"),
                Category = GetString(reader, "Category"),
                IsCommonAllergen = GetBoolean(reader, "IsCommonAllergen"),
                IngredientListString = GetString(reader, "IngredientListString")
            });
    }

    public async Task<IngredientDto?> GetByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT Id, Name, AlternativeNames, Description, Category, IsCommonAllergen, IngredientListString
            FROM Ingredient
            WHERE Id = @Id AND IsDeleted = 0";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new IngredientDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                AlternativeNames = GetString(reader, "AlternativeNames"),
                Description = GetString(reader, "Description"),
                Category = GetString(reader, "Category"),
                IsCommonAllergen = GetBoolean(reader, "IsCommonAllergen"),
                IngredientListString = GetString(reader, "IngredientListString")
            },
            CreateParameter("@Id", id));

        return results.FirstOrDefault();
    }

    public async Task<List<IngredientDto>> SearchByNameAsync(string searchTerm)
    {
        const string sql = @"
            SELECT Id, Name, AlternativeNames, Description, Category, IsCommonAllergen, IngredientListString
            FROM Ingredient
            WHERE (Name LIKE @SearchTerm OR AlternativeNames LIKE @SearchTerm)
                  AND IsDeleted = 0
            ORDER BY Name";

        return await ExecuteReaderAsync(
            sql,
            reader => new IngredientDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                AlternativeNames = GetString(reader, "AlternativeNames"),
                Description = GetString(reader, "Description"),
                Category = GetString(reader, "Category"),
                IsCommonAllergen = GetBoolean(reader, "IsCommonAllergen"),
                IngredientListString = GetString(reader, "IngredientListString")
            },
            CreateParameter("@SearchTerm", $"%{searchTerm}%"));
    }

    public async Task<List<IngredientDto>> GetByCategoryAsync(string category)
    {
        const string sql = @"
            SELECT Id, Name, AlternativeNames, Description, Category, IsCommonAllergen, IngredientListString
            FROM Ingredient
            WHERE Category = @Category AND IsDeleted = 0
            ORDER BY Name";

        return await ExecuteReaderAsync(
            sql,
            reader => new IngredientDto
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name") ?? string.Empty,
                AlternativeNames = GetString(reader, "AlternativeNames"),
                Description = GetString(reader, "Description"),
                Category = GetString(reader, "Category"),
                IsCommonAllergen = GetBoolean(reader, "IsCommonAllergen"),
                IngredientListString = GetString(reader, "IngredientListString")
            },
            CreateParameter("@Category", category));
    }

    public async Task<Guid> CreateAsync(CreateIngredientRequest request, Guid? createdBy = null)
    {
        const string sql = @"
            INSERT INTO Ingredient (
                Id, Name, AlternativeNames, Description, Category,
                IsCommonAllergen, IngredientListString, CreatedBy, CreatedAt
            )
            VALUES (
                @Id, @Name, @AlternativeNames, @Description, @Category,
                @IsCommonAllergen, @IngredientListString, @CreatedBy, GETUTCDATE()
            )";

        var ingredientId = Guid.NewGuid();

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", ingredientId),
            CreateParameter("@Name", request.Name),
            CreateParameter("@AlternativeNames", request.AlternativeNames),
            CreateParameter("@Description", request.Description),
            CreateParameter("@Category", request.Category),
            CreateParameter("@IsCommonAllergen", request.IsCommonAllergen),
            CreateParameter("@IngredientListString", request.IngredientListString),
            CreateParameter("@CreatedBy", createdBy));

        return ingredientId;
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateIngredientRequest request, Guid? updatedBy = null)
    {
        const string sql = @"
            UPDATE Ingredient
            SET Name = @Name,
                AlternativeNames = @AlternativeNames,
                Description = @Description,
                Category = @Category,
                IsCommonAllergen = @IsCommonAllergen,
                IngredientListString = @IngredientListString,
                UpdatedBy = @UpdatedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", id),
            CreateParameter("@Name", request.Name),
            CreateParameter("@AlternativeNames", request.AlternativeNames),
            CreateParameter("@Description", request.Description),
            CreateParameter("@Category", request.Category),
            CreateParameter("@IsCommonAllergen", request.IsCommonAllergen),
            CreateParameter("@IngredientListString", request.IngredientListString),
            CreateParameter("@UpdatedBy", updatedBy));

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid? deletedBy = null)
    {
        const string sql = @"
            UPDATE Ingredient
            SET IsDeleted = 1,
                DeletedAt = GETUTCDATE(),
                UpdatedBy = @DeletedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", id),
            CreateParameter("@DeletedBy", deletedBy));

        return rowsAffected > 0;
    }

    public async Task<List<ProductIngredientDto>> GetProductIngredientsAsync(Guid productId)
    {
        const string sql = @"
            SELECT pi.Id, pi.ProductId, pi.IngredientId, i.Name as IngredientName,
                   pi.OrderIndex, pi.Quantity, pi.Notes, pi.IngredientListString
            FROM ProductIngredient pi
            INNER JOIN Ingredient i ON pi.IngredientId = i.Id
            WHERE pi.ProductId = @ProductId AND pi.IsDeleted = 0 AND i.IsDeleted = 0
            ORDER BY pi.OrderIndex, i.Name";

        return await ExecuteReaderAsync(
            sql,
            reader => new ProductIngredientDto
            {
                Id = GetGuid(reader, "Id"),
                ProductId = GetGuid(reader, "ProductId"),
                IngredientId = GetGuid(reader, "IngredientId"),
                IngredientName = GetString(reader, "IngredientName") ?? string.Empty,
                OrderIndex = GetInt32(reader, "OrderIndex"),
                Quantity = GetString(reader, "Quantity"),
                Notes = GetString(reader, "Notes"),
                IngredientListString = GetString(reader, "IngredientListString")
            },
            CreateParameter("@ProductId", productId));
    }

    public async Task<Guid> AddProductIngredientAsync(Guid productId, AddProductIngredientRequest request, Guid? createdBy = null)
    {
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
