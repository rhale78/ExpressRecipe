using Microsoft.Data.SqlClient;

namespace ExpressRecipe.ShoppingService.Data;

// Partial class for favorite items management
public partial class ShoppingRepository
{
    public async Task<Guid> AddFavoriteItemAsync(Guid userId, Guid? householdId, Guid? productId, string? customName, string? preferredBrand, 
        decimal typicalQuantity, string? typicalUnit, string? category, bool isGeneric)
    {
        const string sql = @"
            INSERT INTO FavoriteItem 
            (UserId, HouseholdId, ProductId, CustomName, PreferredBrand, TypicalQuantity, TypicalUnit, Category, IsGeneric, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @HouseholdId, @ProductId, @CustomName, @PreferredBrand, @TypicalQuantity, @TypicalUnit, @Category, @IsGeneric, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@HouseholdId", householdId.HasValue ? householdId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@ProductId", productId.HasValue ? productId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@CustomName", customName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@PreferredBrand", preferredBrand ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@TypicalQuantity", typicalQuantity);
        command.Parameters.AddWithValue("@TypicalUnit", typicalUnit ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Category", category ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@IsGeneric", isGeneric);

        var favoriteId = (Guid)await command.ExecuteScalarAsync()!;
        _logger.LogInformation("Added favorite item {FavoriteId} for user {UserId}", favoriteId, userId);
        return favoriteId;
    }

    public async Task<List<FavoriteItemDto>> GetUserFavoritesAsync(Guid userId)
    {
        const string sql = @"
            SELECT 
                f.Id, f.UserId, f.HouseholdId, f.ProductId, f.CustomName, f.PreferredBrand,
                f.TypicalQuantity, f.TypicalUnit, f.Category, f.IsGeneric, f.UseCount, f.LastUsed, f.CreatedAt,
                p.Name AS ProductName
            FROM FavoriteItem f
            LEFT JOIN ExpressRecipe.Products.Product p ON f.ProductId = p.Id
            WHERE f.UserId = @UserId
            ORDER BY f.UseCount DESC, f.LastUsed DESC, f.CreatedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        return await ReadFavoriteItemsAsync(command);
    }

    public async Task<List<FavoriteItemDto>> GetHouseholdFavoritesAsync(Guid householdId)
    {
        const string sql = @"
            SELECT 
                f.Id, f.UserId, f.HouseholdId, f.ProductId, f.CustomName, f.PreferredBrand,
                f.TypicalQuantity, f.TypicalUnit, f.Category, f.IsGeneric, f.UseCount, f.LastUsed, f.CreatedAt,
                p.Name AS ProductName
            FROM FavoriteItem f
            LEFT JOIN ExpressRecipe.Products.Product p ON f.ProductId = p.Id
            WHERE f.HouseholdId = @HouseholdId
            ORDER BY f.UseCount DESC, f.LastUsed DESC, f.CreatedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@HouseholdId", householdId);

        return await ReadFavoriteItemsAsync(command);
    }

    public async Task UpdateFavoriteUsageAsync(Guid favoriteId)
    {
        const string sql = @"
            UPDATE FavoriteItem
            SET UseCount = UseCount + 1, LastUsed = GETUTCDATE()
            WHERE Id = @FavoriteId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@FavoriteId", favoriteId);

        await command.ExecuteNonQueryAsync();
        _logger.LogInformation("Updated usage for favorite {FavoriteId}", favoriteId);
    }

    public async Task RemoveFavoriteAsync(Guid favoriteId)
    {
        const string sql = "DELETE FROM FavoriteItem WHERE Id = @FavoriteId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@FavoriteId", favoriteId);

        await command.ExecuteNonQueryAsync();
        _logger.LogInformation("Removed favorite {FavoriteId}", favoriteId);
    }

    private async Task<List<FavoriteItemDto>> ReadFavoriteItemsAsync(SqlCommand command)
    {
        var favorites = new List<FavoriteItemDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            favorites.Add(new FavoriteItemDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                HouseholdId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                ProductId = reader.IsDBNull(3) ? null : reader.GetGuid(3),
                CustomName = reader.IsDBNull(4) ? null : reader.GetString(4),
                PreferredBrand = reader.IsDBNull(5) ? null : reader.GetString(5),
                TypicalQuantity = reader.GetDecimal(6),
                TypicalUnit = reader.IsDBNull(7) ? null : reader.GetString(7),
                Category = reader.IsDBNull(8) ? null : reader.GetString(8),
                IsGeneric = reader.GetBoolean(9),
                UseCount = reader.GetInt32(10),
                LastUsed = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
                CreatedAt = reader.GetDateTime(12),
                ProductName = reader.IsDBNull(13) ? null : reader.GetString(13)
            });
        }

        return favorites;
    }
}
