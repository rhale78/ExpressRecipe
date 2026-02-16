using Microsoft.Data.SqlClient;

namespace ExpressRecipe.ShoppingService.Data;

// Partial class for recipe and inventory integration
public partial class ShoppingRepository
{
    public async Task<Guid> AddItemsFromRecipeAsync(Guid listId, Guid userId, Guid recipeId, int servings)
    {
        // TODO: Integrate with RecipeService to get ingredients
        // For now, return a placeholder GUID
        _logger.LogInformation("Adding items from recipe {RecipeId} to list {ListId} (servings: {Servings})", recipeId, listId, servings);
        
        // This will need to:
        // 1. Call RecipeService to get recipe ingredients
        // 2. Adjust quantities based on servings
        // 3. Add items to shopping list
        // 4. Mark items with AddedFromRecipeId
        
        return Guid.NewGuid(); // Placeholder
    }

    public async Task<List<ShoppingListItemDto>> GetRecipeIngredientsAsItemsAsync(Guid recipeId, int servings)
    {
        // TODO: Integrate with RecipeService
        _logger.LogInformation("Getting ingredients from recipe {RecipeId} for {Servings} servings", recipeId, servings);
        
        // This will need to:
        // 1. Call RecipeService API
        // 2. Convert ingredients to ShoppingListItemDto
        // 3. Adjust quantities for servings
        
        return new List<ShoppingListItemDto>(); // Placeholder
    }

    public async Task<Guid> AddLowStockItemsAsync(Guid listId, Guid userId, decimal threshold = 2.0m)
    {
        // TODO: Integrate with InventoryService to get low stock items
        _logger.LogInformation("Adding low stock items to list {ListId} (threshold: {Threshold})", listId, threshold);
        
        // This will need to:
        // 1. Call InventoryService API to get low stock items
        // 2. Convert to shopping list items
        // 3. Add to list with AddedFromInventory flag
        
        return Guid.NewGuid(); // Placeholder
    }

    public async Task<List<ShoppingListItemDto>> GetLowStockItemsFromInventoryAsync(Guid userId, decimal threshold)
    {
        // TODO: Integrate with InventoryService
        _logger.LogInformation("Getting low stock items for user {UserId} (threshold: {Threshold})", userId, threshold);
        
        // This will need to:
        // 1. Call InventoryService API
        // 2. Convert inventory items to shopping list items format
        
        return new List<ShoppingListItemDto>(); // Placeholder
    }

    public async Task AddPurchasedItemsToInventoryAsync(Guid listId)
    {
        // Get all checked items from the list that should be added to inventory
        const string sql = @"
            SELECT ProductId, CustomName, Quantity, Unit, ActualPrice, PurchasedAt
            FROM ShoppingListItem
            WHERE ShoppingListId = @ListId 
              AND IsChecked = 1 
              AND AddToInventoryOnPurchase = 1
              AND IsDeleted = 0";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ListId", listId);

        var itemsToAdd = new List<(Guid? ProductId, string? CustomName, decimal Quantity, string? Unit, decimal? Price, DateTime? PurchasedAt)>();
        await using var reader = await command.ExecuteReaderAsync();
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
        
        // TODO: For each item, call InventoryService API to add to inventory
        // This will need proper API integration
    }
}
