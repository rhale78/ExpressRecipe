using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
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
    private readonly IMealPlanHistoryService _history;
    private readonly IMealVotingRepository _voting;

    public MealPlanningController(ILogger<MealPlanningController> logger, IMealPlanningRepository repository,
        IMealPlanHistoryService history, IMealVotingRepository voting)
    {
        _logger = logger;
        _repository = repository;
        _history = history;
        _voting = voting;
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
    public async Task<IActionResult> CompleteMeal(Guid id)
    {
        await _repository.MarkMealAsCompletedAsync(id);
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
        var userId = GetUserId();
        var summary = await _repository.GetNutritionSummaryAsync(userId, date);
        return Ok(summary);
    }

    // ── Snapshots / History ───────────────────────────────────────────────────────

    [HttpGet("plans/{id}/history")]
    public async Task<IActionResult> GetPlanHistory(Guid id, [FromQuery] string? scope, CancellationToken ct)
    {
        Guid userId = GetUserId();
        if (await _repository.GetMealPlanAsync(id, userId) is null) { return NotFound(); }
        return Ok(await _history.GetSnapshotsAsync(id, scope, ct));
    }

    [HttpPost("plans/{id}/snapshot")]
    public async Task<IActionResult> TakeSnapshot(Guid id, [FromBody] TakeSnapshotRequest request, CancellationToken ct)
    {
        Guid userId = GetUserId();
        if (await _repository.GetMealPlanAsync(id, userId) is null) { return NotFound(); }
        return Ok(new { id = await _history.TakePlanSnapshotAsync(id, userId, "Manual", request.Label, ct) });
    }

    [HttpPost("snapshots/{id}/restore")]
    public async Task<IActionResult> RestoreSnapshot(Guid id, CancellationToken ct)
    {
        await _history.RestoreSnapshotAsync(id, GetUserId(), ct);
        return NoContent();
    }

    [HttpGet("meals/{id}/history")]
    public async Task<IActionResult> GetMealHistory(Guid id, CancellationToken ct)
    {
        Guid userId = GetUserId();
        if (!await _repository.UserCanAccessPlannedMealAsync(id, userId, ct)) { return NotFound(); }
        return Ok(await _history.GetMealHistoryAsync(id, ct));
    }

    // ── Voting ────────────────────────────────────────────────────────────────────

    [HttpPost("meals/{id}/vote")]
    public async Task<IActionResult> Vote(Guid id, [FromBody] VoteRequest request, CancellationToken ct)
    {
        Guid userId = GetUserId();
        if (!await _repository.UserCanAccessPlannedMealAsync(id, userId, ct)) { return NotFound(); }
        await _voting.UpsertVoteAsync(id, userId, request.Reaction, request.Comment, ct);
        return NoContent();
    }

    [HttpDelete("meals/{id}/vote")]
    public async Task<IActionResult> RemoveVote(Guid id, CancellationToken ct)
    {
        Guid userId = GetUserId();
        if (!await _repository.UserCanAccessPlannedMealAsync(id, userId, ct)) { return NotFound(); }
        await _voting.DeleteVoteAsync(id, userId, ct);
        return NoContent();
    }

    [HttpGet("meals/{id}/votes")]
    public async Task<IActionResult> GetVotes(Guid id, CancellationToken ct)
    {
        Guid userId = GetUserId();
        if (!await _repository.UserCanAccessPlannedMealAsync(id, userId, ct)) { return NotFound(); }
        return Ok(await _voting.GetVoteSummaryAsync(id, ct));
    }

    [HttpPost("meals/{id}/review")]
    public async Task<IActionResult> PostReview(Guid id, [FromBody] PostMealReviewRequest request, CancellationToken ct)
    {
        Guid userId = GetUserId();
        if (!await _repository.UserCanAccessPlannedMealAsync(id, userId, ct)) { return NotFound(); }
        await _voting.UpsertPostMealReviewAsync(id, userId, request.MealRating, request.Comment, request.WouldHaveAgain, ct);
        return NoContent();
    }

    [HttpPost("meals/{id}/course-reviews")]
    public async Task<IActionResult> PostCourseReviews(Guid id, [FromBody] List<CourseReviewRequest> reviews, CancellationToken ct)
    {
        Guid userId = GetUserId();
        if (!await _repository.UserCanAccessPlannedMealAsync(id, userId, ct)) { return NotFound(); }
        foreach (CourseReviewRequest r in reviews)
        {
            await _voting.UpsertCourseReviewAsync(id, r.RecipeId, r.CourseType, userId, r.Rating, r.Comment, ct);
        }
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

public class SetGoalRequest
{
    public string GoalType { get; set; } = string.Empty;
    public decimal TargetValue { get; set; }
    public string? Unit { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class TakeSnapshotRequest
{
    public string? Label { get; set; }
}

public class VoteRequest
{
    [Required]
    [RegularExpression("^(Love|Like|Neutral|Dislike|Veto)$", ErrorMessage = "Reaction must be one of: Love, Like, Neutral, Dislike, Veto")]
    public string Reaction { get; set; } = string.Empty;
    [MaxLength(300)]
    public string? Comment { get; set; }
}

public class PostMealReviewRequest
{
    [Range(1, 5)]
    public byte MealRating { get; set; }
    [MaxLength(500)]
    public string? Comment { get; set; }
    public bool? WouldHaveAgain { get; set; }
}

public class CourseReviewRequest
{
    [Required]
    public Guid RecipeId { get; set; }
    [MaxLength(50)]
    public string? CourseType { get; set; }
    [Range(1, 5)]
    public byte Rating { get; set; }
    [MaxLength(500)]
    public string? Comment { get; set; }
}
