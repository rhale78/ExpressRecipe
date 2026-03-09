using System.Security.Claims;
using ExpressRecipe.MealPlanningService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpressRecipe.MealPlanningService.Controllers;

[Authorize]
[ApiController]
[Route("api/tasks")]
public sealed class HouseholdTaskController : ControllerBase
{
    private readonly IHouseholdTaskRepository _tasks;

    public HouseholdTaskController(IHouseholdTaskRepository tasks) { _tasks = tasks; }

    private Guid GetHouseholdId()
        => Guid.Parse(User.FindFirstValue("household_id") ?? Guid.Empty.ToString());

    private Guid GetUserId()
        => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? Guid.Empty.ToString());

    /// <summary>
    /// Get active (Pending + Escalated) tasks for the caller's household.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetActive(CancellationToken ct)
        => Ok(await _tasks.GetActiveTasksAsync(GetHouseholdId(), ct));

    /// <summary>
    /// Get task history for a date range (defaults to last 30 days).
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        DateOnly rangeFrom = from ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
        DateOnly rangeTo   = to   ?? DateOnly.FromDateTime(DateTime.Today);
        return Ok(await _tasks.GetTaskHistoryAsync(GetHouseholdId(), rangeFrom, rangeTo, ct));
    }

    /// <summary>
    /// Record an action taken on a task (Moved, AlreadyMoved, Ignored).
    /// </summary>
    [HttpPost("{id}/action")]
    public async Task<IActionResult> TakeAction(Guid id,
        [FromBody] TaskActionRequest req, CancellationToken ct)
    {
        string[] valid = ["Moved", "AlreadyMoved", "Ignored"];
        if (!valid.Contains(req.ActionTaken))
        {
            return BadRequest(new { error = "Invalid action" });
        }
        await _tasks.ActionTaskAsync(id, GetUserId(), req.ActionTaken, ct);
        return NoContent();
    }

    /// <summary>
    /// Dismiss a task.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Dismiss(Guid id, CancellationToken ct)
    {
        await _tasks.DismissTaskAsync(id, ct);
        return NoContent();
    }
}

public sealed record TaskActionRequest
{
    public string ActionTaken { get; init; } = string.Empty;
}
