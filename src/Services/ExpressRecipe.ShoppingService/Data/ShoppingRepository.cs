using Microsoft.Data.SqlClient;

namespace ExpressRecipe.ShoppingService.Data;

public partial class ShoppingRepository : IShoppingRepository
{
    private readonly string _connectionString;
    private readonly ILogger<ShoppingRepository> _logger;

    public ShoppingRepository(string connectionString, ILogger<ShoppingRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<Guid> CreateShoppingListAsync(Guid userId, Guid? householdId, string name, string? description, string listType = "Standard", Guid? storeId = null)
    {
        const string sql = @"
            INSERT INTO ShoppingList (UserId, HouseholdId, Name, Description, ListType, StoreId, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @HouseholdId, @Name, @Description, @ListType, @StoreId, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@HouseholdId", householdId.HasValue ? householdId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@Description", description ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ListType", listType);
        command.Parameters.AddWithValue("@StoreId", storeId.HasValue ? storeId.Value : DBNull.Value);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<List<ShoppingListDto>> GetUserListsAsync(Guid userId)
    {
        const string sql = @"
            SELECT 
                sl.Id, sl.UserId, sl.Name, sl.Description, sl.CreatedAt, sl.UpdatedAt,
                (SELECT COUNT(*) FROM ShoppingListItem WHERE ShoppingListId = sl.Id AND IsDeleted = 0) AS ItemCount,
                (SELECT COUNT(*) FROM ShoppingListItem WHERE ShoppingListId = sl.Id AND IsChecked = 1 AND IsDeleted = 0) AS CheckedCount
            FROM ShoppingList sl
            WHERE sl.UserId = @UserId AND sl.IsDeleted = 0
            ORDER BY sl.CreatedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        var lists = new List<ShoppingListDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            lists.Add(new ShoppingListDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                Name = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                CreatedAt = reader.GetDateTime(4),
                UpdatedAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                ItemCount = reader.GetInt32(6),
                CheckedCount = reader.GetInt32(7)
            });
        }

        return lists;
    }

    public async Task<ShoppingListDto?> GetShoppingListAsync(Guid listId, Guid userId)
    {
        const string sql = @"
            SELECT 
                sl.Id, sl.UserId, sl.Name, sl.Description, sl.CreatedAt, sl.UpdatedAt,
                (SELECT COUNT(*) FROM ShoppingListItem WHERE ShoppingListId = sl.Id AND IsDeleted = 0) AS ItemCount,
                (SELECT COUNT(*) FROM ShoppingListItem WHERE ShoppingListId = sl.Id AND IsChecked = 1 AND IsDeleted = 0) AS CheckedCount
            FROM ShoppingList sl
            WHERE sl.Id = @ListId AND (sl.UserId = @UserId OR EXISTS (
                SELECT 1 FROM ListShare WHERE ShoppingListId = sl.Id AND SharedWithUserId = @UserId
            )) AND sl.IsDeleted = 0";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ListId", listId);
        command.Parameters.AddWithValue("@UserId", userId);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new ShoppingListDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                Name = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                CreatedAt = reader.GetDateTime(4),
                UpdatedAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                ItemCount = reader.GetInt32(6),
                CheckedCount = reader.GetInt32(7)
            };
        }

        return null;
    }

    public async Task UpdateShoppingListAsync(Guid listId, string name, string? description, Guid? storeId = null)
    {
        const string sql = @"
            UPDATE ShoppingList
            SET Name = @Name, Description = @Description, StoreId = @StoreId, UpdatedAt = GETUTCDATE()
            WHERE Id = @ListId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ListId", listId);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@Description", description ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@StoreId", storeId.HasValue ? storeId.Value : DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteShoppingListAsync(Guid listId, Guid userId)
    {
        const string sql = @"
            UPDATE ShoppingList
            SET IsDeleted = 1, UpdatedAt = GETUTCDATE()
            WHERE Id = @ListId AND UserId = @UserId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ListId", listId);
        command.Parameters.AddWithValue("@UserId", userId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<Guid> AddItemToListAsync(Guid listId, Guid userId, Guid? productId, string? customName, decimal quantity, string? unit, string? category, 
        bool isFavorite = false, bool isGeneric = false, string? preferredBrand = null, Guid? storeId = null)
    {
        const string sql = @"
            INSERT INTO ShoppingListItem (ShoppingListId, ProductId, CustomName, Quantity, Unit, Category, IsFavorite, IsGeneric, PreferredBrand, StoreId, AddedAt)
            OUTPUT INSERTED.Id
            VALUES (@ListId, @ProductId, @CustomName, @Quantity, @Unit, @Category, @IsFavorite, @IsGeneric, @PreferredBrand, @StoreId, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ListId", listId);
        command.Parameters.AddWithValue("@ProductId", productId.HasValue ? productId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@CustomName", customName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Quantity", quantity);
        command.Parameters.AddWithValue("@Unit", unit ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Category", category ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@IsFavorite", isFavorite);
        command.Parameters.AddWithValue("@IsGeneric", isGeneric);
        command.Parameters.AddWithValue("@PreferredBrand", preferredBrand ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@StoreId", storeId.HasValue ? storeId.Value : DBNull.Value);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<List<ShoppingListItemDto>> GetListItemsAsync(Guid listId, Guid userId)
    {
        const string sql = @"
            SELECT Id, ShoppingListId, ProductId, CustomName, Quantity, Unit, Category, IsChecked, SortOrder, AddedAt
            FROM ShoppingListItem
            WHERE ShoppingListId = @ListId AND IsDeleted = 0
            ORDER BY SortOrder ASC, AddedAt ASC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ListId", listId);

        var items = new List<ShoppingListItemDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new ShoppingListItemDto
            {
                Id = reader.GetGuid(0),
                ShoppingListId = reader.GetGuid(1),
                ProductId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                CustomName = reader.IsDBNull(3) ? null : reader.GetString(3),
                Quantity = reader.GetDecimal(4),
                Unit = reader.IsDBNull(5) ? null : reader.GetString(5),
                Category = reader.IsDBNull(6) ? null : reader.GetString(6),
                IsChecked = reader.GetBoolean(7),
                OrderIndex = reader.GetInt32(8),
                AddedAt = reader.GetDateTime(9)
            });
        }

        return items;
    }

    public async Task UpdateItemQuantityAsync(Guid itemId, decimal quantity)
    {
        const string sql = "UPDATE ShoppingListItem SET Quantity = @Quantity WHERE Id = @ItemId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ItemId", itemId);
        command.Parameters.AddWithValue("@Quantity", quantity);

        await command.ExecuteNonQueryAsync();
    }

    public async Task ToggleItemCheckedAsync(Guid itemId)
    {
        const string sql = "UPDATE ShoppingListItem SET IsChecked = ~IsChecked WHERE Id = @ItemId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ItemId", itemId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task RemoveItemFromListAsync(Guid itemId)
    {
        const string sql = "UPDATE ShoppingListItem SET IsDeleted = 1 WHERE Id = @ItemId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ItemId", itemId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<Guid> ShareListAsync(Guid listId, Guid ownerId, Guid sharedWithUserId, string permission)
    {
        const string sql = @"
            INSERT INTO ListShare (ShoppingListId, OwnerId, SharedWithUserId, Permission, SharedAt)
            OUTPUT INSERTED.Id
            VALUES (@ListId, @OwnerId, @SharedWithUserId, @Permission, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ListId", listId);
        command.Parameters.AddWithValue("@OwnerId", ownerId);
        command.Parameters.AddWithValue("@SharedWithUserId", sharedWithUserId);
        command.Parameters.AddWithValue("@Permission", permission);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<List<ListShareDto>> GetListSharesAsync(Guid listId)
    {
        const string sql = @"
            SELECT Id, ShoppingListId, OwnerId, SharedWithUserId, Permission, SharedAt
            FROM ListShare
            WHERE ShoppingListId = @ListId AND IsRevoked = 0
            ORDER BY SharedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ListId", listId);

        var shares = new List<ListShareDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            shares.Add(new ListShareDto
            {
                Id = reader.GetGuid(0),
                ShoppingListId = reader.GetGuid(1),
                OwnerId = reader.GetGuid(2),
                SharedWithUserId = reader.GetGuid(3),
                Permission = reader.GetString(4),
                SharedAt = reader.GetDateTime(5)
            });
        }

        return shares;
    }

    public async Task<List<ShoppingListDto>> GetSharedListsAsync(Guid userId)
    {
        const string sql = @"
            SELECT 
                sl.Id, sl.UserId, sl.Name, sl.Description, sl.CreatedAt, sl.UpdatedAt,
                (SELECT COUNT(*) FROM ShoppingListItem WHERE ShoppingListId = sl.Id AND IsDeleted = 0) AS ItemCount,
                (SELECT COUNT(*) FROM ShoppingListItem WHERE ShoppingListId = sl.Id AND IsChecked = 1 AND IsDeleted = 0) AS CheckedCount
            FROM ShoppingList sl
            INNER JOIN ListShare ls ON sl.Id = ls.ShoppingListId
            WHERE ls.SharedWithUserId = @UserId AND ls.IsRevoked = 0 AND sl.IsDeleted = 0
            ORDER BY ls.SharedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        var lists = new List<ShoppingListDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            lists.Add(new ShoppingListDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                Name = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                CreatedAt = reader.GetDateTime(4),
                UpdatedAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                ItemCount = reader.GetInt32(6),
                CheckedCount = reader.GetInt32(7)
            });
        }

        return lists;
    }

    public async Task RevokeListAccessAsync(Guid shareId)
    {
        const string sql = "UPDATE ListShare SET IsRevoked = 1, RevokedAt = GETUTCDATE() WHERE Id = @ShareId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ShareId", shareId);

        await command.ExecuteNonQueryAsync();
    }
}
