using Microsoft.Data.SqlClient;

namespace ExpressRecipe.ShoppingService.Data;

public class ShoppingRepository : IShoppingRepository
{
    private readonly string _connectionString;
    private readonly ILogger<ShoppingRepository> _logger;

    public ShoppingRepository(string connectionString, ILogger<ShoppingRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<Guid> CreateShoppingListAsync(Guid userId, string name, string? description)
    {
        const string sql = @"
            INSERT INTO ShoppingList (UserId, Name, Description, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @Name, @Description, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@Description", description ?? (object)DBNull.Value);

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

    public async Task UpdateShoppingListAsync(Guid listId, string name, string? description)
    {
        const string sql = @"
            UPDATE ShoppingList
            SET Name = @Name, Description = @Description, UpdatedAt = GETUTCDATE()
            WHERE Id = @ListId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ListId", listId);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@Description", description ?? (object)DBNull.Value);

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

    public async Task<Guid> AddItemToListAsync(Guid listId, Guid userId, Guid? productId, string? customName, decimal quantity, string? unit, string? category)
    {
        const string sql = @"
            INSERT INTO ShoppingListItem (ShoppingListId, ProductId, CustomName, Quantity, Unit, Category, AddedAt)
            OUTPUT INSERTED.Id
            VALUES (@ListId, @ProductId, @CustomName, @Quantity, @Unit, @Category, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ListId", listId);
        command.Parameters.AddWithValue("@ProductId", productId.HasValue ? productId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@CustomName", customName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Quantity", quantity);
        command.Parameters.AddWithValue("@Unit", unit ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Category", category ?? (object)DBNull.Value);

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
                SortOrder = reader.GetInt32(8),
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

    public async Task<Guid> CreateStoreLayoutAsync(Guid userId, string storeName, string? address)
    {
        const string sql = @"
            INSERT INTO StoreLayout (UserId, StoreName, Address, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @StoreName, @Address, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@StoreName", storeName);
        command.Parameters.AddWithValue("@Address", address ?? (object)DBNull.Value);

        return (Guid)await command.ExecuteScalarAsync()!;
    }

    public async Task<List<StoreLayoutDto>> GetUserStoresAsync(Guid userId)
    {
        const string sql = @"
            SELECT Id, UserId, StoreName, Address
            FROM StoreLayout
            WHERE UserId = @UserId AND IsDeleted = 0
            ORDER BY StoreName";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        var stores = new List<StoreLayoutDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            stores.Add(new StoreLayoutDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                StoreName = reader.GetString(2),
                Address = reader.IsDBNull(3) ? null : reader.GetString(3)
            });
        }

        return stores;
    }

    public async Task AddCategoryToStoreAsync(Guid storeId, string categoryName, int displayOrder)
    {
        const string sql = @"
            INSERT INTO StoreCategory (StoreLayoutId, CategoryName, DisplayOrder)
            VALUES (@StoreId, @CategoryName, @DisplayOrder)";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@StoreId", storeId);
        command.Parameters.AddWithValue("@CategoryName", categoryName);
        command.Parameters.AddWithValue("@DisplayOrder", displayOrder);

        await command.ExecuteNonQueryAsync();
    }
}
