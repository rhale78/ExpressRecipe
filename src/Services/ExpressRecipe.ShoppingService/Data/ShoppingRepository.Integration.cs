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
            return Guid.NewGuid();
        }

        const string sql = @"
            INSERT INTO ShoppingListItem (ShoppingListId, ProductId, CustomName, Quantity, Unit, AddedFromRecipeId, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@ListId, @ProductId, @CustomName, @Quantity, @Unit, @RecipeId, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var lastId = Guid.NewGuid();
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
            var response = await client.GetAsync($"/api/recipes/{recipeId}/ingredients?servings={servings}");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("RecipeService returned {StatusCode} for recipe {RecipeId}", response.StatusCode, recipeId);
                return new List<ShoppingListItemDto>();
            }

            var ingredients = await response.Content.ReadFromJsonAsync<List<RecipeIngredientResult>>();
            if (ingredients == null) return new List<ShoppingListItemDto>();

            return ingredients.Select(i => new ShoppingListItemDto
            {
                Id = Guid.NewGuid(),
                ProductName = i.IngredientName,
                Quantity = i.Quantity * servings,
                Unit = i.Unit,
                Category = i.Category
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
            return Guid.NewGuid();
        }

        const string sql = @"
            INSERT INTO ShoppingListItem (ShoppingListId, ProductId, CustomName, Quantity, Unit, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@ListId, @ProductId, @CustomName, @Quantity, @Unit, GETUTCDATE())";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var lastId = Guid.NewGuid();
        foreach (var item in items)
        {
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@ListId", listId);
            command.Parameters.AddWithValue("@ProductId", (object?)item.ProductId ?? DBNull.Value);
            command.Parameters.AddWithValue("@CustomName", (object?)item.ProductName ?? (object?)item.CustomName ?? DBNull.Value);
            command.Parameters.AddWithValue("@Quantity", 1m);
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
            var response = await client.GetAsync($"/api/inventory/low-stock?userId={userId}&threshold={threshold}");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("InventoryService returned {StatusCode} for low-stock query", response.StatusCode);
                return new List<ShoppingListItemDto>();
            }

            var inventoryItems = await response.Content.ReadFromJsonAsync<List<LowStockInventoryItem>>();
            if (inventoryItems == null) return new List<ShoppingListItemDto>();

            return inventoryItems.Select(i => new ShoppingListItemDto
            {
                Id = Guid.NewGuid(),
                ProductName = i.Name,
                Quantity = threshold - (decimal)(i.CurrentQuantity ?? 0),
                Unit = i.Unit,
                Category = i.Category
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

        var itemsToAdd = new List<InventoryAddRequest>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            itemsToAdd.Add(new InventoryAddRequest
            {
                ProductId = reader.IsDBNull(0) ? null : reader.GetGuid(0),
                Name = reader.IsDBNull(1) ? null : reader.GetString(1),
                Quantity = reader.GetDecimal(2),
                Unit = reader.IsDBNull(3) ? null : reader.GetString(3),
                PurchasePrice = reader.IsDBNull(4) ? null : reader.GetDecimal(4),
                PurchasedAt = reader.IsDBNull(5) ? DateTime.UtcNow : reader.GetDateTime(5)
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
            var response = await client.PostAsJsonAsync("/api/inventory/bulk-add", itemsToAdd);
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
    private class RecipeIngredientResult
    {
        public string IngredientName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string? Unit { get; set; }
        public string? Category { get; set; }
    }

    private class LowStockInventoryItem
    {
        public Guid? ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public double? CurrentQuantity { get; set; }
        public string? Unit { get; set; }
        public string? Category { get; set; }
    }

    private class InventoryAddRequest
    {
        public Guid? ProductId { get; set; }
        public string? Name { get; set; }
        public decimal Quantity { get; set; }
        public string? Unit { get; set; }
        public decimal? PurchasePrice { get; set; }
        public DateTime PurchasedAt { get; set; }
    }
}
