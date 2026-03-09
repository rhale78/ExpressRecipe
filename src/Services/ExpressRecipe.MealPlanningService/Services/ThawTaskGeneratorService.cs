using System.Net.Http.Json;
using ExpressRecipe.MealPlanningService.Data;

namespace ExpressRecipe.MealPlanningService.Services;

public interface IThawTaskGeneratorService
{
    Task GenerateForMealAsync(Guid householdId, Guid plannedMealId, Guid recipeId,
        DateTime mealDateTime, CancellationToken ct = default);
    Task RemoveForMealAsync(Guid plannedMealId, CancellationToken ct = default);
}

public sealed class ThawTaskGeneratorService : IThawTaskGeneratorService
{
    private readonly IHouseholdTaskRepository _tasks;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<ThawTaskGeneratorService> _logger;

    // Hours before meal to start thawing, by food category
    private static readonly Dictionary<string, int> ThawHoursByCategory =
        new(StringComparer.OrdinalIgnoreCase)
    {
        { "Meat",    24 }, { "Poultry", 24 }, { "Seafood", 12 },
        { "Frozen",   8 }, { "Dairy",    4 }, { "Other",    8 },
    };

    public ThawTaskGeneratorService(
        IHouseholdTaskRepository tasks,
        IHttpClientFactory http,
        ILogger<ThawTaskGeneratorService> logger)
    {
        _tasks  = tasks;
        _http   = http;
        _logger = logger;
    }

    public async Task GenerateForMealAsync(Guid householdId, Guid plannedMealId,
        Guid recipeId, DateTime mealDateTime, CancellationToken ct = default)
    {
        HttpClient client = _http.CreateClient("InventoryService");
        List<FrozenIngredientInfo>? frozen;
        try
        {
            frozen = await client.GetFromJsonAsync<List<FrozenIngredientInfo>>(
                $"/api/inventory/frozen-for-recipe/{householdId}/{recipeId}", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch frozen ingredients for recipe {RecipeId}", recipeId);
            return;
        }

        if (frozen is null || frozen.Count == 0) { return; }

        // Group by category; use the longest thaw time to generate a single task
        int maxThawHours = frozen
            .Select(f => ThawHoursByCategory.TryGetValue(f.FoodCategory, out int h) ? h : 8)
            .DefaultIfEmpty(8)
            .Max();

        DateTime dueAt  = mealDateTime.AddHours(-maxThawHours);
        string itemList = string.Join(", ", frozen.Select(f => f.ItemName));
        string title    = $"Move to fridge: {itemList}";
        string desc     = $"These items are in the freezer and should be moved to the refrigerator " +
                          $"~{maxThawHours}h before your meal on {mealDateTime:ddd MMM d 'at' h:mm tt}.";

        await _tasks.UpsertThawTaskAsync(householdId, plannedMealId, title, desc, dueAt, ct);
    }

    public async Task RemoveForMealAsync(Guid plannedMealId, CancellationToken ct = default)
        => await _tasks.DeleteTasksByRelatedEntityAsync(plannedMealId, ct);
}

public sealed record FrozenIngredientInfo
{
    public string ItemName { get; init; } = string.Empty;
    public string FoodCategory { get; init; } = string.Empty;
    public Guid StorageLocationId { get; init; }
}
