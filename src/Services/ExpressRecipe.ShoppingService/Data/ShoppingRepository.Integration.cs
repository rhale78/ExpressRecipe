using System.Net.Http.Json;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.ShoppingService.Data;

// Partial class for recipe and inventory integration
public partial class ShoppingRepository
{
    public async Task<Guid> AddItemsFromRecipeAsync(Guid listId, Guid userId, Guid recipeId, int servings)
    {
        var items = await GetRecipeIngredientsAsItemsAsync(recipeId, servings);
        if (items.Count == 0)
        {
            _logger.LogWarning("No ingredients returned from RecipeService for recipe {RecipeId}", recipeId);
            return Guid.Empty; // distinct from a real inserted ID — caller should check Guid.Empty
        }

        const string sql = @"
            INSERT INTO ShoppingListItem (ShoppingListId, ProductId, CustomName, Quantity, Unit, AddedFromRecipeId, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@ListId, @ProductId, @CustomName, @Quantity, @Unit, @RecipeId, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var lastId = Guid.Empty;
        foreach (var item in items)
        {
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@ListId", listId);
            command.Parameters.AddWithValue("@ProductId", (object?)item.ProductId ?? DBNull.Value);
            command.Parameters.AddWithValue("@CustomName", (object?)item.ProductName ?? (object?)item.CustomName ?? DBNull.Value);
            command.Parameters.AddWithValue("@Quantity", item.Quantity);
            command.Parameters.AddWithValue("@Unit", (object?)item.Unit ?? DBNull.Value);
            command.Parameters.AddWithValue("@RecipeId", recipeId);

            var result = await command.ExecuteScalarAsync();
            if (result is Guid g) lastId = g;
        }

        _logger.LogInformation("Added {Count} ingredients from recipe {RecipeId} to list {ListId}", items.Count, recipeId, listId);
        return lastId;
    }

    public async Task<List<ShoppingListItemDto>> GetRecipeIngredientsAsItemsAsync(Guid recipeId, int servings)
    {
        // Clamp servings to a reasonable range to avoid overflow
        servings = Math.Clamp(servings, 1, 100);

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("http://recipeservice");
            // Use the AllowAnonymous internal endpoint (no user JWT needed for service-to-service calls)
            var response = await client.GetAsync($"/api/recipes/{recipeId}/internal/shopping-ingredients?servings={servings}");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("RecipeService returned {StatusCode} for recipe {RecipeId}", response.StatusCode, recipeId);
                return new List<ShoppingListItemDto>();
            }

            var shoppingList = await response.Content.ReadFromJsonAsync<RecipeShoppingListResult>();
            if (shoppingList?.Items == null) return new List<ShoppingListItemDto>();

            return shoppingList.Items.Select(i => new ShoppingListItemDto
            {
                Id = Guid.NewGuid(),
                ProductName = i.IngredientName,
                // Prefer normalized quantity/unit; fall back to scaled values
                Quantity = i.NormalizedQuantity > 0 ? i.NormalizedQuantity : i.ScaledQuantity,
                Unit = !string.IsNullOrEmpty(i.NormalizedUnit) ? i.NormalizedUnit : i.Unit,
                Category = null
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch ingredients from RecipeService for recipe {RecipeId}", recipeId);
            return new List<ShoppingListItemDto>();
        }
    }

    public async Task<Guid> AddLowStockItemsAsync(Guid listId, Guid userId, decimal threshold = 2.0m)
    {
        var items = await GetLowStockItemsFromInventoryAsync(userId, threshold);
        if (items.Count == 0)
        {
            _logger.LogInformation("No low-stock items found for user {UserId} at threshold {Threshold}", userId, threshold);
            return Guid.Empty; // distinct from a real inserted ID — caller should check Guid.Empty
        }

        const string sql = @"
            INSERT INTO ShoppingListItem (ShoppingListId, ProductId, CustomName, Quantity, Unit, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@ListId, @ProductId, @CustomName, @Quantity, @Unit, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var lastId = Guid.Empty;
        foreach (var item in items)
        {
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@ListId", listId);
            command.Parameters.AddWithValue("@ProductId", (object?)item.ProductId ?? DBNull.Value);
            command.Parameters.AddWithValue("@CustomName", (object?)item.ProductName ?? (object?)item.CustomName ?? DBNull.Value);
            // Use the computed deficit quantity (not a hardcoded 1)
            command.Parameters.AddWithValue("@Quantity", item.Quantity > 0 ? item.Quantity : 1m);
            command.Parameters.AddWithValue("@Unit", (object?)item.Unit ?? DBNull.Value);

            var result = await command.ExecuteScalarAsync();
            if (result is Guid g) lastId = g;
        }

        _logger.LogInformation("Added {Count} low-stock items to list {ListId}", items.Count, listId);
        return lastId;
    }

    public async Task<List<ShoppingListItemDto>> GetLowStockItemsFromInventoryAsync(Guid userId, decimal threshold)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("http://inventoryservice");
            // Use the AllowAnonymous internal endpoint that accepts userId as query param
            var response = await client.GetAsync($"/api/inventory/internal/low-stock?userId={userId}&threshold={threshold}");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("InventoryService returned {StatusCode} for internal low-stock query", response.StatusCode);
                return new List<ShoppingListItemDto>();
            }

            // Response matches InventoryItemDto: ProductName/CustomName + Quantity + Unit
            var inventoryItems = await response.Content.ReadFromJsonAsync<List<InventoryItemResult>>();
            if (inventoryItems == null) return new List<ShoppingListItemDto>();

            return inventoryItems.Select(i => new ShoppingListItemDto
            {
                Id = Guid.NewGuid(),
                ProductId = i.ProductId,
                ProductName = i.ProductName ?? i.CustomName,
                Quantity = Math.Max(0, threshold - i.Quantity), // amount needed to reach threshold
                Unit = i.Unit
            }).Where(i => i.Quantity > 0).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch low-stock items from InventoryService for user {UserId}", userId);
            return new List<ShoppingListItemDto>();
        }
    }

    public async Task AddPurchasedItemsToInventoryAsync(Guid listId)
    {
        // Read purchased items from the shopping list (join to get UserId)
        const string sql = @"
            SELECT sl.UserId, sli.ProductId, sli.CustomName, sli.Quantity, sli.Unit, sli.ActualPrice, sli.PurchasedAt
            FROM ShoppingListItem sli
            JOIN ShoppingList sl ON sl.Id = sli.ShoppingListId
            WHERE sli.ShoppingListId = @ListId 
              AND sli.IsChecked = 1 
              AND sli.AddToInventoryOnPurchase = 1
              AND sli.IsDeleted = 0";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ListId", listId);

        var itemsToAdd = new List<InternalInventoryAddItem>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            itemsToAdd.Add(new InternalInventoryAddItem
            {
                UserId = reader.GetGuid(0),
                ProductId = reader.IsDBNull(1) ? null : reader.GetGuid(1),
                CustomName = reader.IsDBNull(2) ? null : reader.GetString(2),
                Quantity = reader.GetDecimal(3),
                Unit = reader.IsDBNull(4) ? null : reader.GetString(4),
                PurchasePrice = reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                PurchasedAt = reader.IsDBNull(6) ? DateTime.UtcNow : reader.GetDateTime(6)
            });
        }

        if (itemsToAdd.Count == 0)
        {
            _logger.LogInformation("No items to add to inventory from list {ListId}", listId);
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("http://inventoryservice");
            // Use the AllowAnonymous internal bulk-add endpoint
            var response = await client.PostAsJsonAsync("/api/inventory/internal/bulk-add", itemsToAdd);
            if (response.IsSuccessStatusCode)
                _logger.LogInformation("Added {Count} purchased items to inventory from list {ListId}", itemsToAdd.Count, listId);
            else
                _logger.LogWarning("InventoryService returned {StatusCode} when adding purchased items from list {ListId}", response.StatusCode, listId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add purchased items to inventory from list {ListId}", listId);
        }
    }

    // Internal DTOs for cross-service calls
    private class RecipeShoppingListResult
    {
        public Guid RecipeId { get; set; }
        public string RecipeName { get; set; } = string.Empty;
        public int Servings { get; set; }
        public List<RecipeShoppingItem> Items { get; set; } = new();
    }

    private class RecipeShoppingItem
    {
        public string IngredientName { get; set; } = string.Empty;
        public decimal ScaledQuantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public decimal NormalizedQuantity { get; set; }
        public string NormalizedUnit { get; set; } = string.Empty;
    }

    // Maps to InventoryItemDto returned by InventoryService
    private class InventoryItemResult
    {
        public Guid? ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? CustomName { get; set; }
        public decimal Quantity { get; set; }
        public string? Unit { get; set; }
    }

    // Request body for the internal bulk-add endpoint
    private class InternalInventoryAddItem
    {
        public Guid UserId { get; set; }
        public Guid? HouseholdId { get; set; }
        public Guid? ProductId { get; set; }
        public string? CustomName { get; set; }
        public Guid StorageLocationId { get; set; } // default Guid.Empty; InventoryService resolves to user default
        public decimal Quantity { get; set; }
        public string? Unit { get; set; }
        public decimal? PurchasePrice { get; set; }
        public DateTime PurchasedAt { get; set; }
        public string? PreferredStore { get; set; }
    }
}
