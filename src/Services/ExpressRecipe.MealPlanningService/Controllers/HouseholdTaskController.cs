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
    {
        string? claim = User.FindFirstValue("household_id");
        if (string.IsNullOrEmpty(claim) || !Guid.TryParse(claim, out Guid id) || id == Guid.Empty)
            throw new InvalidOperationException("household_id claim is missing or invalid.");
        return id;
    }

    private Guid GetUserId()
    {
        string? claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(claim) || !Guid.TryParse(claim, out Guid id))
            throw new InvalidOperationException("User identity claim is missing or invalid.");
        return id;
    }

    /// <summary>
    /// Get active (Pending + Escalated) tasks for the caller's household.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetActive(CancellationToken ct)
    {
        try { return Ok(await _tasks.GetActiveTasksAsync(GetHouseholdId(), ct)); }
        catch (InvalidOperationException) { return Unauthorized(); }
    }

    /// <summary>
    /// Get task history for a date range (defaults to last 30 days).
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        try
        {
            DateOnly rangeFrom = from ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
            DateOnly rangeTo   = to   ?? DateOnly.FromDateTime(DateTime.Today);
            return Ok(await _tasks.GetTaskHistoryAsync(GetHouseholdId(), rangeFrom, rangeTo, ct));
        }
        catch (InvalidOperationException) { return Unauthorized(); }
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
        try
        {
            bool updated = await _tasks.ActionTaskAsync(id, GetHouseholdId(), GetUserId(), req.ActionTaken, ct);
            if (!updated) { return NotFound(); }
            return NoContent();
        }
        catch (InvalidOperationException) { return Unauthorized(); }
    }

    /// <summary>
    /// Dismiss a task.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Dismiss(Guid id, CancellationToken ct)
    {
        try
        {
            bool updated = await _tasks.DismissTaskAsync(id, GetHouseholdId(), ct);
            if (!updated) { return NotFound(); }
            return NoContent();
        }
        catch (InvalidOperationException) { return Unauthorized(); }
    }
}

public sealed record TaskActionRequest
{
    public string ActionTaken { get; init; } = string.Empty;
}
