using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.User;

namespace ExpressRecipe.UserService.Data;

public interface IUserFavoritesRepository
{
    // Recipes
    Task<List<UserFavoriteRecipeDto>> GetFavoriteRecipesByUserIdAsync(Guid userId);
    Task<UserFavoriteRecipeDto?> GetFavoriteRecipeAsync(Guid userId, Guid recipeId);
    Task<Guid> AddFavoriteRecipeAsync(Guid userId, Guid recipeId, string? notes = null, Guid? createdBy = null);
    Task<bool> RemoveFavoriteRecipeAsync(Guid userId, Guid recipeId);
    
    // Products
    Task<List<UserFavoriteProductDto>> GetFavoriteProductsByUserIdAsync(Guid userId);
    Task<UserFavoriteProductDto?> GetFavoriteProductAsync(Guid userId, Guid productId);
    Task<Guid> AddFavoriteProductAsync(Guid userId, Guid productId, string? notes = null, Guid? createdBy = null);
    Task<bool> RemoveFavoriteProductAsync(Guid userId, Guid productId);
}

public class UserFavoritesRepository : SqlHelper, IUserFavoritesRepository
{
    public UserFavoritesRepository(string connectionString) : base(connectionString)
    {
    }

    // Recipe Favorites
    public async Task<List<UserFavoriteRecipeDto>> GetFavoriteRecipesByUserIdAsync(Guid userId)
    {
        const string sql = @"
            SELECT Id, UserId, RecipeId, Notes, CreatedAt
            FROM UserFavoriteRecipe
            WHERE UserId = @UserId AND IsDeleted = 0
            ORDER BY CreatedAt DESC";

        return await ExecuteReaderAsync(
            sql,
            reader => new UserFavoriteRecipeDto
            {
                Id = GetGuid(reader, "Id"),
                UserId = GetGuid(reader, "UserId"),
                RecipeId = GetGuid(reader, "RecipeId"),
                Notes = GetString(reader, "Notes"),
                CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow
            },
            CreateParameter("@UserId", userId));
    }

    public async Task<UserFavoriteRecipeDto?> GetFavoriteRecipeAsync(Guid userId, Guid recipeId)
    {
        const string sql = @"
            SELECT Id, UserId, RecipeId, Notes, CreatedAt
            FROM UserFavoriteRecipe
            WHERE UserId = @UserId AND RecipeId = @RecipeId AND IsDeleted = 0";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new UserFavoriteRecipeDto
            {
                Id = GetGuid(reader, "Id"),
                UserId = GetGuid(reader, "UserId"),
                RecipeId = GetGuid(reader, "RecipeId"),
                Notes = GetString(reader, "Notes"),
                CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow
            },
            CreateParameter("@UserId", userId),
            CreateParameter("@RecipeId", recipeId));

        return results.FirstOrDefault();
    }

    public async Task<Guid> AddFavoriteRecipeAsync(Guid userId, Guid recipeId, string? notes = null, Guid? createdBy = null)
    {
        const string sql = @"
            INSERT INTO UserFavoriteRecipe (
                Id, UserId, RecipeId, Notes, CreatedBy, CreatedAt
            )
            VALUES (
                @Id, @UserId, @RecipeId, @Notes, @CreatedBy, GETUTCDATE()
            )";

        var favoriteId = Guid.NewGuid();

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", favoriteId),
            CreateParameter("@UserId", userId),
            CreateParameter("@RecipeId", recipeId),
            CreateParameter("@Notes", notes),
            CreateParameter("@CreatedBy", createdBy ?? userId));

        return favoriteId;
    }

    public async Task<bool> RemoveFavoriteRecipeAsync(Guid userId, Guid recipeId)
    {
        const string sql = @"
            UPDATE UserFavoriteRecipe
            SET IsDeleted = 1, DeletedAt = GETUTCDATE()
            WHERE UserId = @UserId AND RecipeId = @RecipeId AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@RecipeId", recipeId));

        return rowsAffected > 0;
    }

    // Product Favorites
    public async Task<List<UserFavoriteProductDto>> GetFavoriteProductsByUserIdAsync(Guid userId)
    {
        const string sql = @"
            SELECT Id, UserId, ProductId, Notes, CreatedAt
            FROM UserFavoriteProduct
            WHERE UserId = @UserId AND IsDeleted = 0
            ORDER BY CreatedAt DESC";

        return await ExecuteReaderAsync(
            sql,
            reader => new UserFavoriteProductDto
            {
                Id = GetGuid(reader, "Id"),
                UserId = GetGuid(reader, "UserId"),
                ProductId = GetGuid(reader, "ProductId"),
                Notes = GetString(reader, "Notes"),
                CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow
            },
            CreateParameter("@UserId", userId));
    }

    public async Task<UserFavoriteProductDto?> GetFavoriteProductAsync(Guid userId, Guid productId)
    {
        const string sql = @"
            SELECT Id, UserId, ProductId, Notes, CreatedAt
            FROM UserFavoriteProduct
            WHERE UserId = @UserId AND ProductId = @ProductId AND IsDeleted = 0";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new UserFavoriteProductDto
            {
                Id = GetGuid(reader, "Id"),
                UserId = GetGuid(reader, "UserId"),
                ProductId = GetGuid(reader, "ProductId"),
                Notes = GetString(reader, "Notes"),
                CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow
            },
            CreateParameter("@UserId", userId),
            CreateParameter("@ProductId", productId));

        return results.FirstOrDefault();
    }

    public async Task<Guid> AddFavoriteProductAsync(Guid userId, Guid productId, string? notes = null, Guid? createdBy = null)
    {
        const string sql = @"
            INSERT INTO UserFavoriteProduct (
                Id, UserId, ProductId, Notes, CreatedBy, CreatedAt
            )
            VALUES (
                @Id, @UserId, @ProductId, @Notes, @CreatedBy, GETUTCDATE()
            )";

        var favoriteId = Guid.NewGuid();

        await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@Id", favoriteId),
            CreateParameter("@UserId", userId),
            CreateParameter("@ProductId", productId),
            CreateParameter("@Notes", notes),
            CreateParameter("@CreatedBy", createdBy ?? userId));

        return favoriteId;
    }

    public async Task<bool> RemoveFavoriteProductAsync(Guid userId, Guid productId)
    {
        const string sql = @"
            UPDATE UserFavoriteProduct
            SET IsDeleted = 1, DeletedAt = GETUTCDATE()
            WHERE UserId = @UserId AND ProductId = @ProductId AND IsDeleted = 0";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            CreateParameter("@UserId", userId),
            CreateParameter("@ProductId", productId));

        return rowsAffected > 0;
    }
}
