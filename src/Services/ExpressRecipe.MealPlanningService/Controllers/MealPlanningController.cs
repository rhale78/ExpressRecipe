using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    private readonly INutritionLogRepository _nutritionLogRepo;
    private readonly INutritionLoggingService _nutritionLoggingService;
    private readonly ICookingHistoryRepository _cookingHistoryRepo;

    public MealPlanningController(
        ILogger<MealPlanningController> logger,
        IMealPlanningRepository repository,
        INutritionLogRepository nutritionLogRepo,
        INutritionLoggingService nutritionLoggingService,
        ICookingHistoryRepository cookingHistoryRepo)
    {
        _logger = logger;
        _repository = repository;
        _nutritionLogRepo = nutritionLogRepo;
        _nutritionLoggingService = nutritionLoggingService;
        _cookingHistoryRepo = cookingHistoryRepo;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("plans")]
    public async Task<IActionResult> CreateMealPlan([FromBody] CreatePlanRequest request)
    {
        var userId = GetUserId();
        var planId = await _repository.CreateMealPlanAsync(userId, request.StartDate, request.EndDate, request.Name);
        var plan = await _repository.GetMealPlanAsync(planId, userId);
        return CreatedAtAction(nameof(GetMealPlan), new { id = planId }, plan);
    }

    [HttpGet("plans")]
    public async Task<IActionResult> GetMealPlans()
    {
        var userId = GetUserId();
        var plans = await _repository.GetUserMealPlansAsync(userId);
        return Ok(plans);
    }

    [HttpGet("plans/{id}")]
    public async Task<IActionResult> GetMealPlan(Guid id)
    {
        var userId = GetUserId();
        var plan = await _repository.GetMealPlanAsync(id, userId);
        if (plan == null) return NotFound();
        return Ok(plan);
    }

    [HttpDelete("plans/{id}")]
    public async Task<IActionResult> DeletePlan(Guid id)
    {
        var userId = GetUserId();
        await _repository.DeleteMealPlanAsync(id, userId);
        return NoContent();
    }

    [HttpPost("plans/{id}/meals")]
    public async Task<IActionResult> AddPlannedMeal(Guid id, [FromBody] AddMealRequest request)
    {
        var userId = GetUserId();
        var mealId = await _repository.AddPlannedMealAsync(
            id, userId, request.RecipeId, request.PlannedFor, request.MealType, request.Servings);
        return Ok(new { id = mealId });
    }

    [HttpGet("plans/{id}/meals")]
    public async Task<IActionResult> GetPlannedMeals(Guid id, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        var meals = await _repository.GetPlannedMealsAsync(id, startDate, endDate);
        return Ok(meals);
    }

    [HttpPut("meals/{id}/complete")]
    public async Task<IActionResult> CompleteMeal(Guid id, [FromBody] CompleteMealRequest? request = null)
    {
        Guid userId = GetUserId();
        PlannedMealDto? plannedMeal = await _repository.GetPlannedMealByIdAsync(id);

        await _repository.MarkMealAsCompletedAsync(id);

        if (plannedMeal?.RecipeId.HasValue == true)
        {
            decimal servingsEaten = request?.ServingsEaten ?? (decimal)(plannedMeal.Servings ?? 1);
            Guid historyId = await _cookingHistoryRepo.CreateAsync(new CookingHistoryRow
            {
                Id             = Guid.NewGuid(),
                UserId         = userId,
                RecipeId       = plannedMeal.RecipeId.Value,
                PlannedMealId  = plannedMeal.Id,
                CookedAt       = DateTime.UtcNow,
                ServingsCooked = (decimal)(plannedMeal.Servings ?? 1),
                ServingsEaten  = servingsEaten
            });

            _ = _nutritionLoggingService.LogCookingEventAsync(
                userId,
                plannedMeal.RecipeId.Value,
                request?.RecipeName ?? string.Empty,
                plannedMeal.MealType,
                servingsEaten,
                historyId,
                plannedMeal.Id,
                CancellationToken.None);
        }

        return NoContent();
    }

    [HttpPost("goals")]
    public async Task<IActionResult> SetGoal([FromBody] SetGoalRequest request)
    {
        var userId = GetUserId();
        var goalId = await _repository.SetNutritionalGoalAsync(
            userId, request.GoalType, request.TargetValue, request.Unit, request.StartDate, request.EndDate);
        return Ok(new { id = goalId });
    }

    [HttpGet("goals")]
    public async Task<IActionResult> GetGoals()
    {
        var userId = GetUserId();
        var goals = await _repository.GetUserGoalsAsync(userId);
        return Ok(goals);
    }

    [HttpGet("nutrition/summary")]
    public async Task<IActionResult> GetNutritionSummary([FromQuery] DateTime date)
    {
        Guid userId = GetUserId();
        NutritionSummaryDto summary = await _repository.GetNutritionSummaryAsync(userId, date);
        return Ok(summary);
    }

    [HttpGet("/api/nutrition/trend")]
    public async Task<IActionResult> GetTrend([FromQuery] int days = 7, CancellationToken ct = default)
    {
        if (days is < 1 or > 365) { return BadRequest("days must be between 1 and 365"); }
        return Ok(await _nutritionLogRepo.GetTrendAsync(GetUserId(), days, ct));
    }

    [HttpGet("/api/nutrition/log")]
    public async Task<IActionResult> GetDayDetail([FromQuery] DateTime date, CancellationToken ct = default) =>
        Ok(await _nutritionLogRepo.GetDayDetailAsync(GetUserId(), DateOnly.FromDateTime(date), ct));

    [HttpPost("/api/nutrition/log")]
    public async Task<IActionResult> LogManual([FromBody] ManualNutritionLogRequest req, CancellationToken ct = default)
    {
        await _nutritionLoggingService.LogManualEntryAsync(GetUserId(), req.RecipeName, req.MealType,
            req.ServingsEaten, req.Calories, req.Protein, req.Carbohydrates, req.Fat, req.Fiber, req.Sodium, ct);
        return NoContent();
    }

    [HttpGet("/api/nutrition/goals")]
    public async Task<IActionResult> GetActiveGoal(CancellationToken ct = default) =>
        Ok(await _nutritionLogRepo.GetActiveGoalAsync(GetUserId(), ct));

    [HttpGet("/api/nutrition/goals/history")]
    public async Task<IActionResult> GetGoalHistory(CancellationToken ct = default) =>
        Ok(await _nutritionLogRepo.GetGoalHistoryAsync(GetUserId(), ct));

    [HttpPost("/api/nutrition/goals")]
    public async Task<IActionResult> UpsertGoal([FromBody] UpsertGoalRequest req, CancellationToken ct = default)
    {
        if (req.EndDate.HasValue && req.EndDate.Value <= req.StartDate)
        {
            return BadRequest("EndDate must be after StartDate");
        }

        // Validate no overlapping active goal (excluding same goal)
        NutritionalGoalRow? active = await _nutritionLogRepo.GetActiveGoalAsync(GetUserId(), ct);
        if (active is not null && active.Id != req.Id)
        {
            return BadRequest("An active goal already exists. End it before creating a new one.");
        }

        NutritionalGoalRow row = new()
        {
            Id             = req.Id ?? Guid.Empty,
            GoalType       = req.GoalType,
            StartDate      = DateOnly.FromDateTime(req.StartDate),
            EndDate        = req.EndDate.HasValue ? DateOnly.FromDateTime(req.EndDate.Value) : null,
            TargetCalories = req.TargetCalories,
            TargetProtein  = req.TargetProtein,
            TargetCarbs    = req.TargetCarbs,
            TargetFat      = req.TargetFat,
            TargetFiber    = req.TargetFiber,
            TargetSodium   = req.TargetSodium,
            Notes          = req.Notes
        };

        Guid id = await _nutritionLogRepo.UpsertGoalAsync(GetUserId(), row, ct);
        return Ok(new { id });
    }

    [HttpDelete("/api/nutrition/goals/{id}")]
    public async Task<IActionResult> EndGoal(Guid id, CancellationToken ct = default)
    {
        await _nutritionLogRepo.EndGoalAsync(id, ct);
        return NoContent();
    }
}

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

public class CompleteMealRequest
{
    public decimal? ServingsEaten { get; set; }
    public string? RecipeName { get; set; }
}

public class SetGoalRequest
{
    public string GoalType { get; set; } = string.Empty;
    public decimal TargetValue { get; set; }
    public string? Unit { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public sealed record ManualNutritionLogRequest
{
    public string RecipeName { get; init; } = string.Empty;
    public string? MealType { get; init; }
    public decimal ServingsEaten { get; init; } = 1m;
    public decimal? Calories { get; init; }
    public decimal? Protein { get; init; }
    public decimal? Carbohydrates { get; init; }
    public decimal? Fat { get; init; }
    public decimal? Fiber { get; init; }
    public decimal? Sodium { get; init; }
}

public sealed record UpsertGoalRequest
{
    public Guid? Id { get; init; }
    public string GoalType { get; init; } = "Daily";
    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public decimal? TargetCalories { get; init; }
    public decimal? TargetProtein { get; init; }
    public decimal? TargetCarbs { get; init; }
    public decimal? TargetFat { get; init; }
    public decimal? TargetFiber { get; init; }
    public decimal? TargetSodium { get; init; }
    public string? Notes { get; init; }
}
