using Microsoft.Data.SqlClient;

namespace ExpressRecipe.ShoppingService.Data;

// Partial class for shopping scan sessions
public partial class ShoppingRepository
{
    public async Task<Guid> StartShoppingScanSessionAsync(Guid userId, Guid shoppingListId, Guid? storeId)
    {
        const string sql = @"
            INSERT INTO ShoppingScanSession (UserId, ShoppingListId, StoreId, StartedAt, IsActive)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @ShoppingListId, @StoreId, GETUTCDATE(), 1)";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@ShoppingListId", shoppingListId);
        command.Parameters.AddWithValue("@StoreId", storeId.HasValue ? storeId.Value : DBNull.Value);

        var sessionId = (Guid)await command.ExecuteScalarAsync()!;
        _logger.LogInformation("Started shopping scan session {SessionId} for list {ListId}", sessionId, shoppingListId);
        return sessionId;
    }

    public async Task<ShoppingScanSessionDto?> GetActiveShoppingScanSessionAsync(Guid userId)
    {
        const string sql = @"
            SELECT 
                ss.Id, ss.UserId, ss.ShoppingListId, sl.Name AS ListName,
                ss.StoreId, s.Name AS StoreName,
                ss.StartedAt, ss.EndedAt, ss.ItemsScanned, ss.TotalSpent, ss.IsActive
            FROM ShoppingScanSession ss
            INNER JOIN ShoppingList sl ON ss.ShoppingListId = sl.Id
            LEFT JOIN Store s ON ss.StoreId = s.Id
            WHERE ss.UserId = @UserId AND ss.IsActive = 1
            ORDER BY ss.StartedAt DESC";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserId", userId);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new ShoppingScanSessionDto
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                ShoppingListId = reader.GetGuid(2),
                ListName = reader.GetString(3),
                StoreId = reader.IsDBNull(4) ? null : reader.GetGuid(4),
                StoreName = reader.IsDBNull(5) ? null : reader.GetString(5),
                StartedAt = reader.GetDateTime(6),
                EndedAt = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                ItemsScanned = reader.GetInt32(8),
                TotalSpent = reader.IsDBNull(9) ? null : reader.GetDecimal(9),
                IsActive = reader.GetBoolean(10)
            };
        }

        return null;
    }

    public async Task<Guid> ScanPurchaseItemAsync(Guid sessionId, string barcode, decimal quantity, decimal price)
    {
        // Get session details
        var session = await GetActiveShoppingScanSessionAsync(Guid.Empty); // Will need userId
        if (session == null || !session.IsActive)
            throw new InvalidOperationException("Scan session not found or not active");

        // Find item in shopping list by barcode
        const string findSql = @"
            SELECT TOP 1 Id 
            FROM ShoppingListItem sli
            INNER JOIN ExpressRecipe.Products.Product p ON sli.ProductId = p.Id
            WHERE sli.ShoppingListId = @ShoppingListId 
              AND p.UPC = @Barcode
              AND sli.IsDeleted = 0";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        Guid itemId;
        await using (var command = new SqlCommand(findSql, connection))
        {
            command.Parameters.AddWithValue("@ShoppingListId", session.ShoppingListId);
            command.Parameters.AddWithValue("@Barcode", barcode);

            var result = await command.ExecuteScalarAsync();
            if (result == null)
                throw new InvalidOperationException($"No item found with barcode {barcode}");

            itemId = (Guid)result;
        }

        // Mark item as checked and record price
        const string updateSql = @"
            UPDATE ShoppingListItem
            SET IsChecked = 1, ActualPrice = @Price, PurchasedAt = GETUTCDATE()
            WHERE Id = @ItemId";

        await using (var command = new SqlCommand(updateSql, connection))
        {
            command.Parameters.AddWithValue("@ItemId", itemId);
            command.Parameters.AddWithValue("@Price", price);
            await command.ExecuteNonQueryAsync();
        }

        // Update session
        const string updateSessionSql = @"
            UPDATE ShoppingScanSession
            SET ItemsScanned = ItemsScanned + 1, TotalSpent = ISNULL(TotalSpent, 0) + @Price
            WHERE Id = @SessionId";

        await using (var command = new SqlCommand(updateSessionSql, connection))
        {
            command.Parameters.AddWithValue("@SessionId", sessionId);
            command.Parameters.AddWithValue("@Price", price);
            await command.ExecuteNonQueryAsync();
        }

        _logger.LogInformation("Scanned item {ItemId} in session {SessionId}", itemId, sessionId);
        return itemId;
    }

    public async Task EndShoppingScanSessionAsync(Guid sessionId, decimal? totalSpent)
    {
        const string sql = @"
            UPDATE ShoppingScanSession
            SET EndedAt = GETUTCDATE(), TotalSpent = ISNULL(@TotalSpent, TotalSpent), IsActive = 0
            WHERE Id = @SessionId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@SessionId", sessionId);
        command.Parameters.AddWithValue("@TotalSpent", totalSpent.HasValue ? totalSpent.Value : DBNull.Value);

        await command.ExecuteNonQueryAsync();
        _logger.LogInformation("Ended shopping scan session {SessionId}", sessionId);
    }
}
