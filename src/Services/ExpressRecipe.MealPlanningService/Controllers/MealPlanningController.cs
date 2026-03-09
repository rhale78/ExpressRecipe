using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using System.Security.Claims;
using ExpressRecipe.MealPlanningService.Data;
using ExpressRecipe.MealPlanningService.Services;

namespace ExpressRecipe.MealPlanningService.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class MealPlanningController : ControllerBase
{
    private readonly ILogger<MealPlanningController> _logger;
    private readonly IMealPlanningRepository _repository;
    private readonly IMealSuggestionService _suggestionService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public MealPlanningController(
        ILogger<MealPlanningController> logger,
        IMealPlanningRepository repository,
        IMealSuggestionService suggestionService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _repository = repository;
        _suggestionService = suggestionService;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ── Meal Plans ────────────────────────────────────────────────────────────

    [HttpPost("plans")]
    public async Task<IActionResult> CreateMealPlan([FromBody] CreatePlanRequest request)
    {
        Guid userId = GetUserId();
        Guid planId = await _repository.CreateMealPlanAsync(userId, request.StartDate, request.EndDate, request.Name);
        MealPlanDto? plan = await _repository.GetMealPlanAsync(planId, userId);
        return CreatedAtAction(nameof(GetMealPlan), new { id = planId }, plan);
    }

    [HttpGet("plans")]
    public async Task<IActionResult> GetMealPlans()
    {
        Guid userId = GetUserId();
        List<MealPlanDto> plans = await _repository.GetUserMealPlansAsync(userId);
        return Ok(plans);
    }

    [HttpGet("plans/{id}")]
    public async Task<IActionResult> GetMealPlan(Guid id)
    {
        Guid userId = GetUserId();
        MealPlanDto? plan = await _repository.GetMealPlanAsync(id, userId);
        if (plan == null) return NotFound();
        return Ok(plan);
    }

    [HttpDelete("plans/{id}")]
    public async Task<IActionResult> DeletePlan(Guid id)
    {
        Guid userId = GetUserId();
        await _repository.DeleteMealPlanAsync(id, userId);
        return NoContent();
    }

    // ── Planned Meals ─────────────────────────────────────────────────────────

    [HttpPost("plans/{id}/meals")]
    public async Task<IActionResult> AddPlannedMeal(Guid id, [FromBody] AddMealRequest request)
    {
        Guid userId = GetUserId();
        Guid mealId = await _repository.AddPlannedMealAsync(
            id, userId, request.RecipeId, request.PlannedFor, request.MealType, request.Servings);
        return Ok(new { id = mealId });
    }

    [HttpGet("plans/{id}/meals")]
    public async Task<IActionResult> GetPlannedMeals(Guid id, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        List<PlannedMealDto> meals = await _repository.GetPlannedMealsAsync(id, startDate, endDate);
        return Ok(meals);
    }

    [HttpPost("plans/{id}/meals/{mealId}/complete")]
    public async Task<IActionResult> CompletePlannedMeal(Guid id, Guid mealId)
    {
        PlannedMealDto? meal = await _repository.GetPlannedMealAsync(mealId);
        if (meal == null) return NotFound();

        await _repository.MarkMealAsCompletedAsync(mealId);

        // Create a cooking history row from the planned meal
        CookingHistoryRecord record = new()
        {
            UserId      = GetUserId(),
            RecipeId    = meal.RecipeId,
            RecipeName  = meal.RecipeName,
            CookedAt    = DateTime.UtcNow,
            Servings    = meal.Servings,
            MealType    = meal.MealType,
            Source      = "PlannedMeal",
            PlannedMealId = mealId
        };

        Guid historyId = await _repository.RecordCookingHistoryAsync(record);

        return Ok(new { historyId });
    }

    // Keep old route for backwards compatibility
    [HttpPut("meals/{id}/complete")]
    public async Task<IActionResult> CompleteMeal(Guid id)
    {
        await _repository.MarkMealAsCompletedAsync(id);
        return NoContent();
    }

    // ── Goals ─────────────────────────────────────────────────────────────────

    [HttpPost("goals")]
    public async Task<IActionResult> SetGoal([FromBody] SetGoalRequest request)
    {
        Guid userId = GetUserId();
        Guid goalId = await _repository.SetNutritionalGoalAsync(
            userId, request.GoalType, request.TargetValue, request.Unit, request.StartDate, request.EndDate);
        return Ok(new { id = goalId });
    }

    [HttpGet("goals")]
    public async Task<IActionResult> GetGoals()
    {
        Guid userId = GetUserId();
        List<NutritionalGoalDto> goals = await _repository.GetUserGoalsAsync(userId);
        return Ok(goals);
    }

    [HttpGet("nutrition/summary")]
    public async Task<IActionResult> GetNutritionSummary([FromQuery] DateTime date)
    {
        Guid userId = GetUserId();
        NutritionSummaryDto summary = await _repository.GetNutritionSummaryAsync(userId, date);
        return Ok(summary);
    }

    // ── Cooking History ───────────────────────────────────────────────────────

    [HttpPost("history")]
    public async Task<IActionResult> RecordCookingHistory([FromBody] RecordCookingHistoryRequest request)
    {
        Guid userId = GetUserId();
        CookingHistoryRecord record = new()
        {
            UserId      = userId,
            HouseholdId = request.HouseholdId,
            RecipeId    = request.RecipeId,
            RecipeName  = request.RecipeName,
            CookedAt    = request.CookedAt ?? DateTime.UtcNow,
            Servings    = request.Servings,
            MealType    = request.MealType,
            Source      = "Spontaneous"
        };

        Guid historyId = await _repository.RecordCookingHistoryAsync(record);
        return Ok(new { id = historyId });
    }

    [HttpPut("history/{id}/rating")]
    public async Task<IActionResult> UpdateCookingRating(Guid id, [FromBody] UpdateRatingRequest request)
    {
        await _repository.UpdateCookingRatingAsync(id, request.Rating, request.WouldCookAgain, request.Notes);
        return NoContent();
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetCookingHistory([FromQuery] int daysBack = 90)
    {
        Guid userId = GetUserId();
        List<CookingHistoryDto> history = await _repository.GetCookingHistoryAsync(userId, daysBack);
        return Ok(history);
    }

    [HttpGet("history/most-cooked")]
    public async Task<IActionResult> GetMostCooked([FromQuery] int limit = 10, [FromQuery] int daysBack = 365)
    {
        Guid userId = GetUserId();
        List<CookingHistorySummaryDto> summary = await _repository.GetMostCookedAsync(userId, limit, daysBack);
        return Ok(summary);
    }

    // ── Suggestions ───────────────────────────────────────────────────────────

    [HttpPost("suggest")]
    public async Task<IActionResult> GetSuggestions([FromBody] SuggestionRequest request)
    {
        Guid userId = GetUserId();
        SuggestionRequest requestWithUserId = new()
        {
            UserId           = userId,
            HouseholdId      = request.HouseholdId,
            MealType         = request.MealType,
            SuggestionMode   = request.SuggestionMode,
            InventorySlider  = request.InventorySlider,
            MaxCookMinutes   = request.MaxCookMinutes,
            Count            = request.Count,
            ExcludeRecentDays = request.ExcludeRecentDays,
            RecentDaysCutoff  = request.RecentDaysCutoff,
            ExcludeRecipeIds  = request.ExcludeRecipeIds
        };

        List<MealSuggestion> suggestions = await _suggestionService.SuggestAsync(requestWithUserId);
        return Ok(suggestions);
    }

    [HttpPost("suggest/week")]
    public async Task<IActionResult> GetWeekSuggestions([FromBody] SuggestionRequest request)
    {
        Guid userId = GetUserId();
        List<MealSuggestion> suggestions = await _suggestionService.SuggestForWeekAsync(
            userId, request.HouseholdId, request);
        return Ok(suggestions);
    }

    // ── Shopping List Generation ──────────────────────────────────────────────

    [HttpPost("plans/{id}/generate-shopping-list")]
    public async Task<IActionResult> GenerateShoppingList(Guid id, [FromBody] GenerateShoppingListRequest request)
    {
        Guid userId = GetUserId();

        // 1. Load all non-completed planned meals for this plan
        List<PlannedMealDto> meals = await _repository.GetPlannedMealsAsync(id, null, null);
        meals = meals.Where(m => !m.IsCompleted).ToList();

        if (meals.Count == 0)
        {
            return Ok(new { itemsAdded = 0, message = "No upcoming meals in this plan." });
        }

        string recipeServiceUrl    = _configuration["Services:RecipeService"] ?? "http://recipeservice";
        string inventoryServiceUrl = _configuration["Services:InventoryService"] ?? "http://inventoryservice";
        string shoppingServiceUrl  = _configuration["Services:ShoppingService"] ?? "http://shoppingservice";

        using HttpClient httpClient = _httpClientFactory.CreateClient("MealPlanningService");

        // 2. Per meal: fetch ingredients and aggregate by ingredient name
        Dictionary<string, AggregatedIngredient> aggregated = new(StringComparer.OrdinalIgnoreCase);

        foreach (PlannedMealDto meal in meals)
        {
            try
            {
                List<RecipeIngredientItem>? ingredients = await httpClient.GetFromJsonAsync<List<RecipeIngredientItem>>(
                    $"{recipeServiceUrl}/api/recipes/{meal.RecipeId}/ingredients");

                if (ingredients == null) continue;

                foreach (RecipeIngredientItem ing in ingredients)
                {
                    string key = ing.Name.ToLowerInvariant();
                    if (aggregated.TryGetValue(key, out AggregatedIngredient? existing))
                    {
                        aggregated[key] = existing with { TotalQuantity = existing.TotalQuantity + (ing.Quantity * meal.Servings) };
                    }
                    else
                    {
                        aggregated[key] = new AggregatedIngredient(
                            ing.IngredientId, ing.Name, ing.Quantity * meal.Servings, ing.Unit);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch ingredients for recipe {RecipeId}", meal.RecipeId);
            }
        }

        // 3. Fetch on-hand inventory
        Dictionary<string, decimal> onHand = new(StringComparer.OrdinalIgnoreCase);
        try
        {
            List<InventoryItemDto>? inventory = await httpClient.GetFromJsonAsync<List<InventoryItemDto>>(
                $"{inventoryServiceUrl}/api/inventory?userId={userId}");

            if (inventory != null)
            {
                foreach (InventoryItemDto item in inventory)
                {
                    onHand[item.Name.ToLowerInvariant()] = item.QuantityOnHand;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch inventory for user {UserId} — proceeding without", userId);
        }

        // 4. Determine net quantities needed, respecting the inventory slider
        int slider = request.InventorySlider ?? 50;
        Guid? targetListId = request.TargetListId;
        int itemsAdded = 0;

        foreach (AggregatedIngredient agg in aggregated.Values)
        {
            decimal onHandQty = onHand.TryGetValue(agg.Name.ToLowerInvariant(), out decimal q) ? q : 0m;
            decimal usableOnHand = onHandQty * (1m - slider / 100m);
            decimal netQty = agg.TotalQuantity - usableOnHand;

            if (netQty <= 0m) continue;

            try
            {
                if (targetListId.HasValue)
                {
                    await httpClient.PostAsJsonAsync(
                        $"{shoppingServiceUrl}/api/shopping/{targetListId.Value}/items",
                        new
                        {
                            Name     = agg.Name,
                            Quantity = netQty,
                            Unit     = agg.Unit
                        });
                }

                itemsAdded++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to add {Ingredient} to shopping list", agg.Name);
            }
        }

        return Ok(new { itemsAdded });
    }
}

// ── Request/Response DTOs ─────────────────────────────────────────────────────

public class CreatePlanRequest
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Name { get; set; }
}

public class AddMealRequest
{
    public Guid RecipeId { get; set; }
    public DateTime PlannedFor { get; set; }
    public string MealType { get; set; } = "Dinner";
    public int Servings { get; set; } = 1;
}

public class SetGoalRequest
{
    public string GoalType { get; set; } = string.Empty;
    public decimal TargetValue { get; set; }
    public string? Unit { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class RecordCookingHistoryRequest
{
    public Guid? HouseholdId { get; set; }
    public Guid RecipeId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public DateTime? CookedAt { get; set; }
    public int Servings { get; set; } = 1;
    public string MealType { get; set; } = "Dinner";
}

public class UpdateRatingRequest
{
    public byte Rating { get; set; }
    public bool? WouldCookAgain { get; set; }
    public string? Notes { get; set; }
}

public class GenerateShoppingListRequest
{
    public Guid? TargetListId { get; set; }
    public Guid? HouseholdId { get; set; }
    public int? InventorySlider { get; set; }
}

// Internal helper records for shopping list generation
internal record AggregatedIngredient(Guid? IngredientId, string Name, decimal TotalQuantity, string? Unit);

internal class RecipeIngredientItem
{
    public Guid? IngredientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
}

internal class InventoryItemDto
{
    public Guid? IngredientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal QuantityOnHand { get; set; }
    public string? Unit { get; set; }
}
