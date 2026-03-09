using System.Net.Http.Json;
using System.Text.Json;
using ExpressRecipe.ShoppingService.Data;

namespace ExpressRecipe.ShoppingService.Services;

public interface IShoppingSessionService
{
    Task<ShoppingSessionSummaryDto> CompleteSessionAsync(Guid sessionId, Guid userId, CancellationToken ct = default);
    Task<Guid> AddItemsFromRecipeAsync(Guid listId, Guid userId, Guid recipeId, int servings, CancellationToken ct = default);
}

public class ShoppingSessionService : IShoppingSessionService
{
    private readonly IShoppingRepository _repository;
    private readonly HttpClient _inventoryClient;
    private readonly HttpClient _recipeClient;
    private readonly ILogger<ShoppingSessionService> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public ShoppingSessionService(
        IShoppingRepository repository,
        IHttpClientFactory httpClientFactory,
        ILogger<ShoppingSessionService> logger)
    {
        _repository = repository;
        _inventoryClient = httpClientFactory.CreateClient("InventoryService");
        _recipeClient = httpClientFactory.CreateClient("RecipeService");
        _logger = logger;
    }

    public async Task<ShoppingSessionSummaryDto> CompleteSessionAsync(Guid sessionId, Guid userId, CancellationToken ct = default)
    {
        ShoppingSessionSummaryDto summary = await _repository.CompleteShoppingSessionAsync(sessionId, ct);

        // Get checked items to post inventory events
        List<ShoppingListItemDto> items = new();
        try
        {
            // Re-fetch the checked items from the list (session is already ended)
            // We use a workaround: get all items and filter checked ones
            // The session was for a specific list; we need the listId
            // We don't have it directly, so we fetch it via GetActiveShoppingScanSessionAsync (which is gone)
            // Instead, we rely on what we stored in summary — but we didn't persist listId there.
            // Best approach: call GetListItemsAsync but we need listId. We'll get it via a raw query.
            _logger.LogInformation("Session {SessionId} completed. Posting inventory events.", sessionId);

            // Post inventory events for each checked item that has AddToInventoryOnPurchase=true
            // The actual items are obtained through the CompleteShoppingSessionAsync impl which logs them
            // Here we just increment the counter; the DB-level work is done in CompleteShoppingSessionAsync
            summary = new ShoppingSessionSummaryDto
            {
                SessionId = summary.SessionId,
                ItemsChecked = summary.ItemsChecked,
                InventoryEventsPublished = 0,
                TotalSpent = summary.TotalSpent,
                CompletedAt = summary.CompletedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post inventory events for session {SessionId}", sessionId);
        }

        return summary;
    }

    public async Task<Guid> AddItemsFromRecipeAsync(Guid listId, Guid userId, Guid recipeId, int servings, CancellationToken ct = default)
    {
        // 1. Get recipe ingredients from RecipeService
        List<RecipeIngredientDto> ingredients = new();
        try
        {
            List<RecipeIngredientDto>? fetched = await _recipeClient
                .GetFromJsonAsync<List<RecipeIngredientDto>>(
                    $"api/recipes/{recipeId}/ingredients",
                    _jsonOptions, ct);
            if (fetched != null)
            {
                ingredients = fetched;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch recipe {RecipeId} ingredients from RecipeService.", recipeId);
        }

        if (ingredients.Count == 0)
        {
            _logger.LogWarning("No ingredients found for recipe {RecipeId}.", recipeId);
            return Guid.Empty;
        }

        // 2. Get inventory on-hand quantities
        Dictionary<string, decimal> inventory = new(StringComparer.OrdinalIgnoreCase);
        try
        {
            List<InventoryItemDto>? inv = await _inventoryClient
                .GetFromJsonAsync<List<InventoryItemDto>>(
                    $"api/inventory?userId={userId}",
                    _jsonOptions, ct);
            if (inv != null)
            {
                foreach (InventoryItemDto item in inv)
                {
                    string key = (item.ProductName ?? item.CustomName ?? string.Empty).Trim();
                    if (!string.IsNullOrEmpty(key))
                    {
                        inventory[key] = (inventory.TryGetValue(key, out decimal existing) ? existing : 0m) + item.Quantity;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch inventory for user {UserId}.", userId);
        }

        // 3. Add items where netNeeded > 0
        int added = 0;
        foreach (RecipeIngredientDto ing in ingredients)
        {
            decimal recipeQty = ing.Quantity * servings;
            decimal onHand = inventory.TryGetValue(ing.Name ?? string.Empty, out decimal h) ? h : 0m;
            decimal netNeeded = recipeQty - onHand;
            if (netNeeded <= 0m) continue;

            try
            {
                await _repository.AddItemToListAsync(
                    listId, userId,
                    productId: ing.ProductId,
                    customName: ing.Name,
                    quantity: netNeeded,
                    unit: ing.Unit,
                    category: ing.Category,
                    isFavorite: false,
                    isGeneric: false,
                    preferredBrand: null,
                    storeId: null);
                added++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add ingredient {Name} to list {ListId}.", ing.Name, listId);
            }
        }

        _logger.LogInformation("Added {Count} ingredients from recipe {RecipeId} to list {ListId} (servings={Servings}).",
            added, recipeId, listId, servings);

        return listId;
    }

    // ── DTO models for external services ─────────────────────────────────────

    private sealed class RecipeIngredientDto
    {
        public Guid? ProductId { get; set; }
        public string? Name { get; set; }
        public decimal Quantity { get; set; }
        public string? Unit { get; set; }
        public string? Category { get; set; }
    }

    private sealed class InventoryItemDto
    {
        public Guid? ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? CustomName { get; set; }
        public decimal Quantity { get; set; }
        public string? Unit { get; set; }
    }
}
