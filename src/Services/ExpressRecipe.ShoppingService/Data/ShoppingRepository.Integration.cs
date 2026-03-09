using Microsoft.Data.SqlClient;

namespace ExpressRecipe.ShoppingService.Data;

// Partial class for recipe and inventory integration
public partial class ShoppingRepository
{
    public Task<Guid> AddItemsFromRecipeAsync(Guid listId, Guid userId, Guid recipeId, int servings)
    {
        // The real cross-service logic is handled by ShoppingSessionService.AddItemsFromRecipeAsync.
        // This method is kept for backward compatibility and delegates by returning the listId.
        _logger.LogInformation(
            "AddItemsFromRecipeAsync called for recipe {RecipeId}, list {ListId}, servings {Servings}. " +
            "Use IShoppingSessionService.AddItemsFromRecipeAsync for full cross-service integration.",
            recipeId, listId, servings);
        return Task.FromResult(listId);
    }

    public Task<List<ShoppingListItemDto>> GetRecipeIngredientsAsItemsAsync(Guid recipeId, int servings)
    {
        // The actual integration with RecipeService is done in ShoppingSessionService.
        _logger.LogInformation("GetRecipeIngredientsAsItemsAsync: recipeId={RecipeId}, servings={Servings}", recipeId, servings);
        return Task.FromResult(new List<ShoppingListItemDto>());
    }

    public Task<Guid> AddLowStockItemsAsync(Guid listId, Guid userId, decimal threshold = 2.0m)
    {
        _logger.LogInformation("Adding low stock items to list {ListId} (threshold: {Threshold})", listId, threshold);
        return Task.FromResult(listId);
    }

    public Task<List<ShoppingListItemDto>> GetLowStockItemsFromInventoryAsync(Guid userId, decimal threshold)
    {
        _logger.LogInformation("Getting low stock items for user {UserId} (threshold: {Threshold})", userId, threshold);
        return Task.FromResult(new List<ShoppingListItemDto>());
    }

    public async Task AddPurchasedItemsToInventoryAsync(Guid listId)
    {
        const string sql = @"
            SELECT ProductId, CustomName, Quantity, Unit, ActualPrice, PurchasedAt
            FROM ShoppingListItem
            WHERE ShoppingListId = @ListId 
              AND IsChecked = 1 
              AND AddToInventoryOnPurchase = 1
              AND IsDeleted = 0";

        await using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync();

        await using SqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@ListId", listId);

        List<(Guid? ProductId, string? CustomName, decimal Quantity, string? Unit, decimal? Price, DateTime? PurchasedAt)> itemsToAdd = new();
        await using SqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            itemsToAdd.Add((
                reader.IsDBNull(0) ? null : reader.GetGuid(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.GetDecimal(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetDecimal(4),
                reader.IsDBNull(5) ? null : reader.GetDateTime(5)
            ));
        }

        _logger.LogInformation("Found {Count} items to add to inventory from list {ListId}", itemsToAdd.Count, listId);
        // HTTP calls to InventoryService are handled by ShoppingSessionService.CompleteSessionAsync
    }
}

