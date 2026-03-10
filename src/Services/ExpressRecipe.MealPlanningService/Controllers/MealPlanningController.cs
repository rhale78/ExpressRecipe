using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ExpressRecipe.MealPlanningService.Data;

namespace ExpressRecipe.MealPlanningService.Controllers;

[Authorize]
[ApiController]
[Route("api/mealplan")]
public class MealPlanningController : ControllerBase
{
    private readonly ILogger<MealPlanningController> _logger;
    private readonly IMealPlanningRepository _repository;

    public MealPlanningController(ILogger<MealPlanningController> logger, IMealPlanningRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    // ──────────── Meal Plan CRUD ────────────

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetMealPlan(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var plan = await _repository.GetMealPlanAsync(id, userId.Value);
        if (plan == null) return NotFound();
        return Ok(plan);
    }

    [HttpPost("search")]
    public async Task<IActionResult> SearchMealPlans([FromBody] SearchMealPlansRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var plans = await _repository.GetUserMealPlansAsync(userId.Value, request.Status);
        var total = plans.Count;
        var page = request.Page > 0 ? request.Page : 1;
        var pageSize = request.PageSize > 0 ? request.PageSize : 20;
        var paged = plans.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Ok(new { Plans = paged, TotalCount = total, Page = page, PageSize = pageSize });
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetMealPlanSummary()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var summary = await _repository.GetMealPlanSummaryAsync(userId.Value);
        return Ok(summary);
    }

    [HttpPost]
    public async Task<IActionResult> CreateMealPlan([FromBody] CreateMealPlanApiRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Name is required" });

        var planId = await _repository.CreateMealPlanAsync(
            userId.Value, request.StartDate, request.EndDate, request.Name);
        return Ok(new { Id = planId });
    }

    [HttpPost("quick-plan")]
    public async Task<IActionResult> CreateQuickMealPlan([FromBody] QuickMealPlanApiRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var startDate = request.StartDate;
        var endDate = startDate.AddDays(request.DurationDays - 1);
        var name = $"Week of {startDate:MMM d, yyyy}";

        var planId = await _repository.CreateMealPlanAsync(userId.Value, startDate, endDate, name);
        return Ok(new { Id = planId });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateMealPlan(Guid id, [FromBody] UpdateMealPlanApiRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var existing = await _repository.GetMealPlanAsync(id, userId.Value);
        if (existing == null) return NotFound();

        await _repository.UpdateMealPlanAsync(
            id, userId.Value, request.Name, request.StartDate, request.EndDate, request.Status ?? "Active");
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteMealPlan(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var existing = await _repository.GetMealPlanAsync(id, userId.Value);
        if (existing == null) return NotFound();

        await _repository.DeleteMealPlanAsync(id, userId.Value);
        return NoContent();
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> CompleteMealPlan(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var existing = await _repository.GetMealPlanAsync(id, userId.Value);
        if (existing == null) return NotFound();

        await _repository.SetMealPlanStatusAsync(id, userId.Value, "Completed");
        return NoContent();
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> ArchiveMealPlan(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var existing = await _repository.GetMealPlanAsync(id, userId.Value);
        if (existing == null) return NotFound();

        await _repository.SetMealPlanStatusAsync(id, userId.Value, "Archived");
        return NoContent();
    }

    // ──────────── Calendar / Week Views ────────────

    [HttpGet("{id:guid}/calendar")]
    public async Task<IActionResult> GetMealPlanCalendar(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var plan = await _repository.GetMealPlanAsync(id, userId.Value);
        if (plan == null) return NotFound();

        var meals = await _repository.GetPlannedMealsAsync(id, plan.StartDate, plan.EndDate);
        var days = BuildDays(plan.StartDate, plan.EndDate, meals);
        return Ok(new { MealPlanId = id, MealPlanName = plan.Name, plan.StartDate, plan.EndDate, Days = days });
    }

    [HttpGet("week/{weekStart}")]
    public async Task<IActionResult> GetWeekView(DateTime weekStart)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var weekEnd = weekStart.AddDays(6);
        var meals = await _repository.GetPlannedMealsByDateRangeAsync(userId.Value, weekStart, weekEnd);
        var days = BuildDays(weekStart, weekEnd, meals);
        return Ok(new { WeekStartDate = weekStart, WeekEndDate = weekEnd, Days = days });
    }

    private static List<object> BuildDays(DateTime startDate, DateTime endDate, List<PlannedMealDto> meals)
    {
        var days = new List<object>();
        for (var d = startDate.Date; d <= endDate.Date; d = d.AddDays(1))
        {
            var dayMeals = meals.Where(m => m.PlannedFor.Date == d).ToList();
            days.Add(new { Date = d, Meals = dayMeals });
        }
        return days;
    }

    [HttpGet("{id:guid}/nutrition")]
    public async Task<IActionResult> GetNutritionSummary(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var plan = await _repository.GetMealPlanAsync(id, userId.Value);
        if (plan == null) return NotFound();

        // Nutrition calculation pending recipe integration
        return Ok(new
        {
            MealPlanId = id,
            plan.StartDate,
            plan.EndDate,
            TotalCalories = 0,
            TotalProtein = 0m,
            TotalCarbohydrates = 0m,
            TotalFat = 0m,
            DaysInPlan = (int)(plan.EndDate - plan.StartDate).TotalDays + 1
        });
    }

    // ──────────── Meal Entries ────────────

    [HttpPost("entries")]
    public async Task<IActionResult> AddMealEntry([FromBody] AddMealEntryApiRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var plan = await _repository.GetMealPlanAsync(request.MealPlanId, userId.Value);
        if (plan == null) return NotFound(new { message = "Meal plan not found" });

        var entryId = await _repository.AddPlannedMealAsync(
            request.MealPlanId, userId.Value, request.RecipeId, request.Date, request.MealType,
            request.Servings, request.CustomMealName, request.Notes);
        return Ok(new { EntryId = entryId });
    }

    [HttpPut("entries/{entryId:guid}")]
    public async Task<IActionResult> UpdateMealEntry(Guid entryId, [FromBody] UpdateMealEntryApiRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var entry = await _repository.GetPlannedMealAsync(entryId);
        if (entry == null) return NotFound();

        // Verify ownership via the plan
        var plan = await _repository.GetMealPlanAsync(entry.MealPlanId, userId.Value);
        if (plan == null) return Forbid();

        await _repository.UpdatePlannedMealAsync(
            entryId, request.Date, request.MealType, request.Servings,
            request.RecipeId, request.CustomMealName, request.Notes);
        return NoContent();
    }

    [HttpDelete("entries/{entryId:guid}")]
    public async Task<IActionResult> DeleteMealEntry(Guid entryId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var entry = await _repository.GetPlannedMealAsync(entryId);
        if (entry == null) return NotFound();

        var plan = await _repository.GetMealPlanAsync(entry.MealPlanId, userId.Value);
        if (plan == null) return Forbid();

        await _repository.RemovePlannedMealAsync(entryId);
        return NoContent();
    }

    [HttpPost("entries/mark-prepared")]
    public async Task<IActionResult> MarkMealPrepared([FromBody] MarkMealPreparedApiRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var entry = await _repository.GetPlannedMealAsync(request.EntryId);
        if (entry == null) return NotFound();

        var plan = await _repository.GetMealPlanAsync(entry.MealPlanId, userId.Value);
        if (plan == null) return Forbid();

        await _repository.MarkMealAsPreparedAsync(request.EntryId, request.IsPrepared);
        return NoContent();
    }

    // ──────────── Shopping list generation (stub - requires ShoppingService) ────────────

    [HttpPost("generate-shopping-list")]
    public async Task<IActionResult> GenerateShoppingList([FromBody] GenerateShoppingListApiRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var plan = await _repository.GetMealPlanAsync(request.MealPlanId, userId.Value);
        if (plan == null) return NotFound(new { message = "Meal plan not found" });

        // Shopping list generation from meal plan entries requires cross-service call to ShoppingService
        // This is a placeholder; the ShoppingService integration will complete this flow
        _logger.LogWarning("GenerateShoppingList called but ShoppingService integration is pending for plan {PlanId}", request.MealPlanId);
        return StatusCode(501, new { message = "Shopping list generation from meal plans is not yet implemented" });
    }

    // ──────────── Nutritional Goals ────────────

    [HttpPost("goals")]
    public async Task<IActionResult> SetGoal([FromBody] SetGoalRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var goalId = await _repository.SetNutritionalGoalAsync(
            userId.Value, request.GoalType, request.TargetValue, request.Unit, request.StartDate, request.EndDate);
        return Ok(new { id = goalId });
    }

    [HttpGet("goals")]
    public async Task<IActionResult> GetGoals()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var goals = await _repository.GetUserGoalsAsync(userId.Value);
        return Ok(goals);
    }

    [HttpGet("nutrition/summary")]
    public async Task<IActionResult> GetNutritionSummaryByDate([FromQuery] DateTime date)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var summary = await _repository.GetNutritionSummaryAsync(userId.Value, date);
        return Ok(summary);
    }
}

// ──────────── Request models ────────────

public class CreateMealPlanApiRequest
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class UpdateMealPlanApiRequest : CreateMealPlanApiRequest
{
    public string? Status { get; set; }
}

public class QuickMealPlanApiRequest
{
    public DateTime StartDate { get; set; }
    public int DurationDays { get; set; } = 7;
    public List<string> MealTypes { get; set; } = new();
    public int DefaultServings { get; set; } = 4;
}

public class SearchMealPlansRequest
{
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class AddMealEntryApiRequest
{
    public Guid MealPlanId { get; set; }
    public DateTime Date { get; set; }
    public string MealType { get; set; } = "Dinner";
    public Guid? RecipeId { get; set; }
    public string? CustomMealName { get; set; }
    public int Servings { get; set; } = 4;
    public string? Notes { get; set; }
}

public class UpdateMealEntryApiRequest
{
    public DateTime Date { get; set; }
    public string MealType { get; set; } = "Dinner";
    public Guid? RecipeId { get; set; }
    public string? CustomMealName { get; set; }
    public int Servings { get; set; } = 4;
    public string? Notes { get; set; }
}

public class MarkMealPreparedApiRequest
{
    public Guid EntryId { get; set; }
    public bool IsPrepared { get; set; }
}

public class GenerateShoppingListApiRequest
{
    public Guid MealPlanId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool SubtractInventory { get; set; } = true;
    public string? ShoppingListName { get; set; }
}

public class SetGoalRequest
{
    public string GoalType { get; set; } = string.Empty;
    public decimal TargetValue { get; set; }
    public string? Unit { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
