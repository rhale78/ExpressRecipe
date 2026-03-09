using ExpressRecipe.InventoryService.Data;
using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.Messaging.Core.Options;
using ExpressRecipe.Shared.Messages;
using Microsoft.Extensions.Caching.Hybrid;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ExpressRecipe.InventoryService.Services;

public sealed class RecipeCookedEventSubscriber : IHostedService
{
    private readonly IMessageBus _bus;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecipeCookedEventSubscriber> _logger;

    public RecipeCookedEventSubscriber(
        IMessageBus bus,
        IServiceScopeFactory scopeFactory,
        ILogger<RecipeCookedEventSubscriber> logger)
    {
        _bus = bus;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        SubscribeOptions opts = new SubscribeOptions { RoutingMode = RoutingMode.Broadcast };
        await _bus.SubscribeAsync<RecipeCookedEvent>(HandleRecipeCookedAsync, opts, cancellationToken);
        _logger.LogInformation("[RecipeCookedEventSubscriber] Subscribed to recipe.cooked events");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task HandleRecipeCookedAsync(
        RecipeCookedEvent evt,
        MessageContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Received RecipeCookedEvent for recipe {RecipeId} by user {UserId}, servings={Servings}",
            evt.RecipeId, evt.UserId, evt.Servings);

        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            IInventoryRepository repository = scope.ServiceProvider.GetRequiredService<IInventoryRepository>();
            IHttpClientFactory httpFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

            // Fetch recipe ingredients (use HybridCache if available)
            List<RecipeIngredientResponse> ingredients = await FetchRecipeIngredientsAsync(
                evt.RecipeId, httpFactory, scope, cancellationToken);

            if (ingredients.Count == 0)
            {
                _logger.LogWarning("No ingredients found for recipe {RecipeId}", evt.RecipeId);
                return;
            }

            // Get user inventory for matching
            List<InventoryItemDto> inventory = await repository.GetUserInventoryAsync(evt.UserId);

            List<string> missingIngredients = new List<string>();

            foreach (RecipeIngredientResponse ingredient in ingredients)
            {
                // Scale quantity by servings ratio (recipe definition is for 1 serving by default)
                decimal scaledQuantity = ingredient.Quantity * evt.Servings;

                // Match inventory: ProductId → IngredientId → name
                InventoryItemDto? match = FindMatchingInventoryItem(inventory, ingredient);

                if (match == null)
                {
                    missingIngredients.Add(ingredient.Name);
                    await repository.WriteInventoryHistoryDirectAsync(
                        Guid.Empty, evt.UserId,
                        "RecipeIngredientNotInInventory",
                        -scaledQuantity, 0, 0,
                        $"Recipe {evt.RecipeId}: {ingredient.Name} not in inventory",
                        evt.RecipeId,
                        cancellationToken);
                    continue;
                }

                decimal before = match.Quantity;
                decimal after = Math.Max(0, before - scaledQuantity);

                await repository.UpdateInventoryQuantityAsync(
                    match.Id, after, "UsedInRecipe", evt.UserId, $"Recipe {evt.RecipeId}");

                await repository.WriteInventoryHistoryDirectAsync(
                    match.Id, evt.UserId,
                    "UsedInRecipe",
                    -(before - after), before, after,
                    $"Cooked recipe {evt.RecipeId} x{evt.Servings} servings",
                    evt.RecipeId,
                    cancellationToken);

                _logger.LogInformation(
                    "Deducted {Amount} {Unit} of {Name} from inventory item {ItemId} (was {Before}, now {After})",
                    scaledQuantity, ingredient.Unit, ingredient.Name, match.Id, before, after);
            }

            // Send shortfall notification if any ingredients were missing
            if (missingIngredients.Count > 0)
            {
                await SendRecipeShortfallNotificationAsync(
                    evt.UserId, evt.RecipeId, missingIngredients, httpFactory, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling RecipeCookedEvent for recipe {RecipeId}", evt.RecipeId);
        }
    }

    private async Task<List<RecipeIngredientResponse>> FetchRecipeIngredientsAsync(
        Guid recipeId,
        IHttpClientFactory httpFactory,
        IServiceScope scope,
        CancellationToken cancellationToken)
    {
        // Try HybridCache first if registered
        HybridCache? cache = scope.ServiceProvider.GetService<HybridCache>();
        string cacheKey = $"recipe-ingredients:{recipeId}";

        if (cache != null)
        {
            return await cache.GetOrCreateAsync(
                cacheKey,
                async ct => await FetchFromRecipeServiceAsync(recipeId, httpFactory, ct),
                new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(10) },
                cancellationToken: cancellationToken) ?? new List<RecipeIngredientResponse>();
        }

        return await FetchFromRecipeServiceAsync(recipeId, httpFactory, cancellationToken);
    }

    private async Task<List<RecipeIngredientResponse>> FetchFromRecipeServiceAsync(
        Guid recipeId,
        IHttpClientFactory httpFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            HttpClient recipeClient = httpFactory.CreateClient("recipeservice");
            HttpResponseMessage response = await recipeClient.GetAsync(
                $"/api/recipes/{recipeId}/ingredients", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                List<RecipeIngredientResponse>? ingredients =
                    await response.Content.ReadFromJsonAsync<List<RecipeIngredientResponse>>(cancellationToken: cancellationToken);
                return ingredients ?? new List<RecipeIngredientResponse>();
            }

            _logger.LogWarning("RecipeService returned {StatusCode} for recipe {RecipeId}",
                response.StatusCode, recipeId);
            return new List<RecipeIngredientResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch ingredients for recipe {RecipeId}", recipeId);
            return new List<RecipeIngredientResponse>();
        }
    }

    private static InventoryItemDto? FindMatchingInventoryItem(
        List<InventoryItemDto> inventory,
        RecipeIngredientResponse ingredient)
    {
        // Priority 1: ProductId match
        if (ingredient.ProductId.HasValue)
        {
            InventoryItemDto? byProduct = inventory.FirstOrDefault(i =>
                i.ProductId == ingredient.ProductId && i.Quantity > 0);
            if (byProduct != null)
            {
                return byProduct;
            }
        }

        // Priority 2: IngredientId match (stored as ProductId for ingredients)
        // Priority 3: Name match (case-insensitive)
        return inventory.FirstOrDefault(i =>
            string.Equals(
                i.CustomName ?? i.ProductName,
                ingredient.Name,
                StringComparison.OrdinalIgnoreCase) && i.Quantity > 0);
    }

    private async Task SendRecipeShortfallNotificationAsync(
        Guid userId,
        Guid recipeId,
        List<string> missingIngredients,
        IHttpClientFactory httpFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            HttpClient notificationClient = httpFactory.CreateClient("notificationservice");
            object payload = new
            {
                UserId = userId,
                Type = "RecipeShortfall",
                Priority = "Normal",
                RecipeId = recipeId,
                MissingIngredients = missingIngredients
            };
            await notificationClient.PostAsJsonAsync("/api/notifications/internal", payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send recipe shortfall notification for user {UserId}", userId);
        }
    }

    private sealed class RecipeIngredientResponse
    {
        [JsonPropertyName("productId")]
        public Guid? ProductId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("quantity")]
        public decimal Quantity { get; set; }

        [JsonPropertyName("unit")]
        public string Unit { get; set; } = string.Empty;

        [JsonPropertyName("isOptional")]
        public bool IsOptional { get; set; }
    }
}
