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
    private readonly IMealCourseRepository _courseRepo;
    private readonly IMealAttendeeRepository _attendeeRepo;
    private readonly IMealPlanCopyService _copyService;
    private readonly IMealPlanTemplateService _templateService;

    public MealPlanningController(
        ILogger<MealPlanningController> logger,
        IMealPlanningRepository repository,
        IMealCourseRepository courseRepo,
        IMealAttendeeRepository attendeeRepo,
        IMealPlanCopyService copyService,
        IMealPlanTemplateService templateService)
    {
        _logger = logger;
        _repository = repository;
        _courseRepo = courseRepo;
        _attendeeRepo = attendeeRepo;
        _copyService = copyService;
        _templateService = templateService;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ── Meal Plans ─────────────────────────────────────────────────────────────

    [HttpPost("plans")]
    public async Task<IActionResult> CreateMealPlan([FromBody] CreatePlanRequest request, CancellationToken ct)
    {
        Guid userId = GetUserId();
        Guid planId = await _repository.CreateMealPlanAsync(userId, request.StartDate, request.EndDate, request.Name, ct);
        MealPlanDto? plan = await _repository.GetMealPlanAsync(planId, userId, ct);
        return CreatedAtAction(nameof(GetMealPlan), new { id = planId }, plan);
    }

    [HttpGet("plans")]
    public async Task<IActionResult> GetMealPlans(CancellationToken ct)
    {
        Guid userId = GetUserId();
        List<MealPlanDto> plans = await _repository.GetUserMealPlansAsync(userId, ct);
        return Ok(plans);
    }

    [HttpGet("plans/{id}")]
    public async Task<IActionResult> GetMealPlan(Guid id, CancellationToken ct)
    {
        Guid userId = GetUserId();
        MealPlanDto? plan = await _repository.GetMealPlanAsync(id, userId, ct);
        if (plan == null) return NotFound();
        return Ok(plan);
    }

    [HttpDelete("plans/{id}")]
    public async Task<IActionResult> DeletePlan(Guid id, CancellationToken ct)
    {
        Guid userId = GetUserId();
        await _repository.DeleteMealPlanAsync(id, userId, ct);
        return NoContent();
    }

    // ── Planned Meals ──────────────────────────────────────────────────────────

    [HttpPost("plans/{id}/meals")]
    public async Task<IActionResult> AddPlannedMeal(Guid id, [FromBody] AddMealRequest request, CancellationToken ct)
    {
        Guid userId = GetUserId();
        Guid mealId = await _repository.AddPlannedMealAsync(
            id, userId, request.RecipeId, request.PlannedDate, request.MealType, request.Servings, ct);
        return Ok(new { id = mealId });
    }

    [HttpGet("plans/{id}/meals")]
    public async Task<IActionResult> GetPlannedMeals(Guid id, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, CancellationToken ct)
    {
        List<PlannedMealDto> meals = await _repository.GetPlannedMealsAsync(id, startDate, endDate, ct);
        return Ok(meals);
    }

    [HttpPut("meals/{id}/complete")]
    public async Task<IActionResult> CompleteMeal(Guid id, CancellationToken ct)
    {
        await _repository.MarkMealAsCompletedAsync(id, ct);
        return NoContent();
    }

    // ── Move (DnD) ─────────────────────────────────────────────────────────────

    [HttpPut("meals/{id}/move")]
    public async Task<IActionResult> MoveMeal(Guid id, [FromBody] MoveMealRequest req, CancellationToken ct)
    {
        await _repository.UpdatePlannedMealAsync(id, req.NewDate.ToDateTime(TimeOnly.MinValue), req.NewMealType, null, ct);
        return NoContent();
    }

    // ── Multi-course ───────────────────────────────────────────────────────────

    [HttpGet("meals/{id}/courses")]
    public async Task<IActionResult> GetCourses(Guid id, CancellationToken ct)
        => Ok(await _courseRepo.GetCoursesAsync(id, ct));

    [HttpPost("meals/{id}/courses")]
    public async Task<IActionResult> AddCourse(Guid id, [FromBody] AddCourseRequest req, CancellationToken ct)
    {
        // Use max existing sort order + 1 so new courses naturally append to the end
        List<MealCourseDto> existing = await _courseRepo.GetCoursesAsync(id, ct);
        int nextSortOrder = existing.Count > 0 ? existing.Max(c => c.SortOrder) + 1 : 0;
        Guid courseId = await _courseRepo.AddCourseAsync(id, req.CourseType, req.RecipeId, req.CustomName, req.Servings, nextSortOrder, ct);
        return Ok(new { id = courseId });
    }

    [HttpPut("meals/{id}/courses/{courseId}")]
    public async Task<IActionResult> UpdateCourse(Guid id, Guid courseId, [FromBody] UpdateCourseRequest req, CancellationToken ct)
    {
        await _courseRepo.UpdateCourseAsync(courseId, req.RecipeId, req.CustomName, req.Servings, req.SortOrder, ct);
        return NoContent();
    }

    [HttpDelete("meals/{id}/courses/{courseId}")]
    public async Task<IActionResult> DeleteCourse(Guid id, Guid courseId, CancellationToken ct)
    {
        await _courseRepo.DeleteCourseAsync(courseId, ct);
        return NoContent();
    }

    [HttpPut("meals/{id}/courses/reorder")]
    public async Task<IActionResult> ReorderCourses(Guid id, [FromBody] List<CourseOrderItem> order, CancellationToken ct)
    {
        await _courseRepo.ReorderCoursesAsync(order.Select(o => (o.CourseId, o.SortOrder)).ToList(), ct);
        return NoContent();
    }

    // ── Copy / Clone ───────────────────────────────────────────────────────────

    [HttpPost("meals/{id}/clone")]
    public async Task<IActionResult> CloneMeal(Guid id, [FromBody] CloneMealRequest req, CancellationToken ct)
        => Ok(new { id = await _copyService.CloneMealAsync(id, req.TargetDate, req.TargetMealType, ct) });

    [HttpPost("plans/{id}/copy-day")]
    public async Task<IActionResult> CopyDay(Guid id, [FromBody] CopyDayRequest req, CancellationToken ct)
    {
        await _copyService.CopyDayAsync(id, req.SourceDate, req.TargetDate, ct);
        return NoContent();
    }

    [HttpPost("plans/{id}/copy-week")]
    public async Task<IActionResult> CopyWeek(Guid id, [FromBody] CopyWeekRequest req, CancellationToken ct)
    {
        await _copyService.CopyWeekAsync(id, req.SourceWeekStart, req.TargetWeekStart, ct);
        return NoContent();
    }

    [HttpPost("plans/{id}/copy-month")]
    public async Task<IActionResult> CopyMonth(Guid id, [FromBody] CopyMonthRequest req, CancellationToken ct)
    {
        await _copyService.CopyMonthAsync(id, req.SourceYear, req.SourceMonth, req.TargetYear, req.TargetMonth, ct);
        return NoContent();
    }

    [HttpPost("plans/{id}/copy")]
    public async Task<IActionResult> CopyPlan(Guid id, [FromBody] CopyPlanRequest req, CancellationToken ct)
        => Ok(new { id = await _copyService.CopyPlanAsync(id, req.NewName, req.NewStartDate, ct) });

    // ── Calendar ───────────────────────────────────────────────────────────────

    [HttpGet("calendar")]
    public async Task<IActionResult> GetCalendar([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
        => Ok(await _repository.GetCalendarAsync(GetUserId(), year, month, ct));

    // ── Attendees ──────────────────────────────────────────────────────────────

    [HttpGet("meals/{id}/attendees")]
    public async Task<IActionResult> GetAttendees(Guid id, CancellationToken ct)
        => Ok(await _attendeeRepo.GetAttendeesAsync(id, ct));

    [HttpPut("meals/{id}/attendees")]
    public async Task<IActionResult> SetAttendees(Guid id, [FromBody] List<MealAttendeeDto> attendees, CancellationToken ct)
    {
        await _attendeeRepo.SetAttendeesAsync(id, attendees, ct);
        return NoContent();
    }

    // ── Templates ──────────────────────────────────────────────────────────────

    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates([FromQuery] bool includePublic = true, CancellationToken ct = default)
        => Ok(await _templateService.GetTemplatesAsync(GetUserId(), includePublic, ct));

    [HttpPost("templates")]
    public async Task<IActionResult> SaveTemplate([FromBody] SaveTemplateRequest req, CancellationToken ct)
    {
        Guid id = await _templateService.SaveTemplateFromPlanAsync(GetUserId(), req.PlanId,
            req.FromDate, req.ToDate, req.Name, req.Description, req.Category, req.IsPublic, ct);
        return Ok(new { id });
    }

    [HttpPost("templates/{id}/apply")]
    public async Task<IActionResult> ApplyTemplate(Guid id, [FromBody] ApplyTemplateRequest req, CancellationToken ct)
    {
        Guid planId = await _templateService.ApplyTemplateAsync(id, GetUserId(), req.TargetPlanId, req.StartDate, ct);
        return Ok(new { planId });
    }

    // ── Nutritional Goals ──────────────────────────────────────────────────────

    [HttpPost("goals")]
    public async Task<IActionResult> SetGoal([FromBody] SetGoalRequest request, CancellationToken ct)
    {
        Guid userId = GetUserId();
        Guid goalId = await _repository.SetNutritionalGoalAsync(
            userId, request.GoalType, request.TargetValue, request.Unit, request.StartDate, request.EndDate, ct);
        return Ok(new { id = goalId });
    }

    [HttpGet("goals")]
    public async Task<IActionResult> GetGoals(CancellationToken ct)
    {
        Guid userId = GetUserId();
        List<NutritionalGoalDto> goals = await _repository.GetUserGoalsAsync(userId, ct);
        return Ok(goals);
    }

    [HttpGet("nutrition/summary")]
    public async Task<IActionResult> GetNutritionSummary([FromQuery] DateTime date, CancellationToken ct)
    {
        Guid userId = GetUserId();
        NutritionSummaryDto summary = await _repository.GetNutritionSummaryAsync(userId, date, ct);
        return Ok(summary);
    }
}

// ── Request models ─────────────────────────────────────────────────────────────

public class CreatePlanRequest
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Name { get; set; }
}

public class AddMealRequest
{
    public Guid? RecipeId { get; set; }
    public DateTime PlannedDate { get; set; }
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

public class MoveMealRequest
{
    public DateOnly NewDate { get; set; }
    public string NewMealType { get; set; } = "Dinner";
}

public class AddCourseRequest
{
    public string CourseType { get; set; } = "Main";
    public Guid? RecipeId { get; set; }
    public string? CustomName { get; set; }
    public decimal Servings { get; set; } = 1;
}

public class UpdateCourseRequest
{
    public Guid? RecipeId { get; set; }
    public string? CustomName { get; set; }
    public decimal Servings { get; set; } = 1;
    public int SortOrder { get; set; }
}

public class CourseOrderItem
{
    public Guid CourseId { get; set; }
    public int SortOrder { get; set; }
}

public class CloneMealRequest
{
    public DateOnly TargetDate { get; set; }
    public string TargetMealType { get; set; } = "Dinner";
}

public class CopyDayRequest
{
    public DateOnly SourceDate { get; set; }
    public DateOnly TargetDate { get; set; }
}

public class CopyWeekRequest
{
    public DateOnly SourceWeekStart { get; set; }
    public DateOnly TargetWeekStart { get; set; }
}

public class CopyMonthRequest
{
    public int SourceYear { get; set; }
    public int SourceMonth { get; set; }
    public int TargetYear { get; set; }
    public int TargetMonth { get; set; }
}

public class CopyPlanRequest
{
    public string NewName { get; set; } = string.Empty;
    public DateOnly NewStartDate { get; set; }
}

public class SaveTemplateRequest
{
    public Guid PlanId { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool IsPublic { get; set; }
}

public class ApplyTemplateRequest
{
    public Guid TargetPlanId { get; set; }
    public DateOnly StartDate { get; set; }
}
