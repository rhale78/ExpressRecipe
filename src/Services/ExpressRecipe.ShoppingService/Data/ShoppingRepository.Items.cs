using Microsoft.Data.SqlClient;

namespace ExpressRecipe.ShoppingService.Data;

// Partial class for item management with pricing
public partial class ShoppingRepository
{
    public async Task UpdateItemPriceAsync(Guid itemId, decimal? estimatedPrice, decimal? actualPrice)
    {
        const string sql = @"
            UPDATE ShoppingListItem
            SET EstimatedPrice = @EstimatedPrice, ActualPrice = @ActualPrice
            WHERE Id = @ItemId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ItemId", itemId);
        command.Parameters.AddWithValue("@EstimatedPrice", estimatedPrice.HasValue ? estimatedPrice.Value : DBNull.Value);
        command.Parameters.AddWithValue("@ActualPrice", actualPrice.HasValue ? actualPrice.Value : DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    public async Task BulkAddItemsAsync(Guid listId, Guid userId, List<ShoppingListItemDto> items)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var item in items)
            {
                const string sql = @"
                    INSERT INTO ShoppingListItem 
                    (ShoppingListId, ProductId, CustomName, Quantity, Unit, Category, IsFavorite, IsGeneric, PreferredBrand, OrderIndex)
                    VALUES (@ListId, @ProductId, @CustomName, @Quantity, @Unit, @Category, @IsFavorite, @IsGeneric, @PreferredBrand, @OrderIndex)";

                await using var command = new SqlCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@ListId", listId);
                command.Parameters.AddWithValue("@ProductId", item.ProductId.HasValue ? item.ProductId.Value : DBNull.Value);
                command.Parameters.AddWithValue("@CustomName", item.CustomName ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Quantity", item.Quantity);
                command.Parameters.AddWithValue("@Unit", item.Unit ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Category", item.Category ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@IsFavorite", item.IsFavorite);
                command.Parameters.AddWithValue("@IsGeneric", item.IsGeneric);
                command.Parameters.AddWithValue("@PreferredBrand", item.PreferredBrand ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@OrderIndex", item.OrderIndex);

                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            _logger.LogInformation("Bulk added {Count} items to list {ListId}", items.Count, listId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task MoveItemToListAsync(Guid itemId, Guid targetListId)
    {
        const string sql = @"
            UPDATE ShoppingListItem
            SET ShoppingListId = @TargetListId
            WHERE Id = @ItemId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ItemId", itemId);
        command.Parameters.AddWithValue("@TargetListId", targetListId);

        await command.ExecuteNonQueryAsync();
        _logger.LogInformation("Moved item {ItemId} to list {TargetListId}", itemId, targetListId);
    }

    public async Task UpdateBestPriceForItemAsync(Guid itemId)
    {
        // Get the best price from price comparisons
        const string sql = @"
            UPDATE ShoppingListItem
            SET BestPrice = pc.MinPrice,
                BestPriceStoreId = pc.BestStoreId
            FROM ShoppingListItem sli
            CROSS APPLY (
                SELECT TOP 1 Price AS MinPrice, StoreId AS BestStoreId
                FROM PriceComparison
                WHERE ShoppingListItemId = sli.Id AND IsAvailable = 1
                ORDER BY Price ASC
            ) pc
            WHERE sli.Id = @ItemId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ItemId", itemId);

        await command.ExecuteNonQueryAsync();
    }
}
