using System.Security.Claims;
using ExpressRecipe.MealPlanningService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpressRecipe.MealPlanningService.Controllers;

[Authorize]
[ApiController]
[Route("api/work-queue")]
public sealed class WorkQueueController : ControllerBase
{
    private readonly IWorkQueueRepository _repo;

    public WorkQueueController(IWorkQueueRepository repo) { _repo = repo; }

    private Guid GetUserId()
    {
        string? claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(claim) || !Guid.TryParse(claim, out Guid id) || id == Guid.Empty)
            throw new InvalidOperationException("User identity claim is missing or invalid.");
        return id;
    }

    private Guid GetHouseholdId()
    {
        string? claim = User.FindFirstValue("household_id");
        if (string.IsNullOrEmpty(claim) || !Guid.TryParse(claim, out Guid id) || id == Guid.Empty)
            throw new InvalidOperationException("household_id claim is missing or invalid.");
        return id;
    }

    [HttpGet]
    public async Task<IActionResult> GetQueue(
        [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        try { return Ok(await _repo.GetPendingItemsAsync(GetHouseholdId(), GetUserId(), limit, ct)); }
        catch (InvalidOperationException) { return Unauthorized(); }
    }

    [HttpPost("{id}/action")]
    public async Task<IActionResult> ActionItem(Guid id,
        [FromBody] ActionQueueItemRequest req, CancellationToken ct = default)
    {
        try
        {
            await _repo.ActionItemAsync(id, GetUserId(), req.ActionTaken, req.ActionData, ct);
            return NoContent();
        }
        catch (InvalidOperationException) { return Unauthorized(); }
    }

    [HttpPost("{id}/dismiss")]
    public async Task<IActionResult> Dismiss(Guid id, CancellationToken ct = default)
    {
        try
        {
            await _repo.DismissItemAsync(id, GetUserId(), ct);
            return NoContent();
        }
        catch (InvalidOperationException) { return Unauthorized(); }
    }

    [HttpPost("{id}/snooze")]
    public async Task<IActionResult> Snooze(Guid id,
        [FromBody] SnoozeRequest req, CancellationToken ct = default)
    {
        try
        {
            DateTime until = req.Hours > 0
                ? DateTime.UtcNow.AddHours(req.Hours)
                : DateTime.UtcNow.Date.AddDays(1);  // default: snooze until tomorrow
            await _repo.SnoozeItemAsync(id, GetUserId(), until, ct);
            return NoContent();
        }
        catch (InvalidOperationException) { return Unauthorized(); }
    }
}

public sealed record SnoozeRequest { public int Hours { get; init; } = 24; }
