using Microsoft.Data.SqlClient;

namespace ExpressRecipe.ShoppingService.Data;

// Partial class for enhanced list operations
public partial class ShoppingRepository
{
    public async Task<List<ShoppingListDto>> GetHouseholdListsAsync(Guid householdId)
    {
        const string sql = @"
            SELECT 
                sl.Id, sl.UserId, sl.HouseholdId, sl.Name, sl.Description, sl.Status, sl.ListType,
                sl.StoreId, s.Name AS StoreName, sl.ScheduledFor, sl.TotalEstimatedCost, sl.ActualCost,
                sl.CreatedAt, sl.UpdatedAt, sl.CompletedAt,
                (SELECT COUNT(*) FROM ShoppingListItem WHERE ShoppingListId = sl.Id) AS ItemCount,
                (SELECT COUNT(*) FROM ShoppingListItem WHERE ShoppingListId = sl.Id AND IsChecked = 1) AS CheckedCount
            FROM ShoppingList sl
            LEFT JOIN Store s ON sl.StoreId = s.Id
            WHERE sl.HouseholdId = @HouseholdId AND sl.IsDeleted = 0
            ORDER BY sl.CreatedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@HouseholdId", householdId);

        return await ReadShoppingListsAsync(command);
    }

    public async Task CompleteShoppingListAsync(Guid listId)
    {
        const string sql = @"
            UPDATE ShoppingList
            SET Status = 'Completed', CompletedAt = GETUTCDATE(), UpdatedAt = GETUTCDATE()
            WHERE Id = @ListId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ListId", listId);

        await command.ExecuteNonQueryAsync();

        // Evict the active-list cache entry so the next read reflects Completed status.
        if (_cache is not null)
            _ = _cache.RemoveAsync($"shopping:list:{listId}");

        _logger.LogInformation("Completed shopping list {ListId}", listId);
    }

    public async Task ArchiveShoppingListAsync(Guid listId)
    {
        const string sql = @"
            UPDATE ShoppingList
            SET Status = 'Archived', UpdatedAt = GETUTCDATE()
            WHERE Id = @ListId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ListId", listId);

        await command.ExecuteNonQueryAsync();

        // Evict any previous cache entry so the next read re-caches with Archived status.
        if (_cache is not null)
            _ = _cache.RemoveAsync($"shopping:list:{listId}");

        _logger.LogInformation("Archived shopping list {ListId}", listId);
    }

    private async Task<List<ShoppingListDto>> ReadShoppingListsAsync(SqlCommand command)
    {
        var lists = new List<ShoppingListDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            lists.Add(new ShoppingListDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                HouseholdId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                Name = reader.GetString(3),
                Description = reader.IsDBNull(4) ? null : reader.GetString(4),
                Status = reader.GetString(5),
                ListType = reader.GetString(6),
                StoreId = reader.IsDBNull(7) ? null : reader.GetGuid(7),
                StoreName = reader.IsDBNull(8) ? null : reader.GetString(8),
                ScheduledFor = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                TotalEstimatedCost = reader.IsDBNull(10) ? null : reader.GetDecimal(10),
                ActualCost = reader.IsDBNull(11) ? null : reader.GetDecimal(11),
                CreatedAt = reader.GetDateTime(12),
                UpdatedAt = reader.IsDBNull(13) ? null : reader.GetDateTime(13),
                CompletedAt = reader.IsDBNull(14) ? null : reader.GetDateTime(14),
                ItemCount = reader.GetInt32(15),
                CheckedCount = reader.GetInt32(16)
            });
        }

        return lists;
    }
}
