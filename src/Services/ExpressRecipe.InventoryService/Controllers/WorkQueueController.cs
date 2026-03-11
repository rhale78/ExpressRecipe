using System.Security.Claims;
using ExpressRecipe.InventoryService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpressRecipe.InventoryService.Controllers;

/// <summary>
/// User work-queue: personal action items surfaced from inventory rules,
/// price alerts, recipe rating prompts, etc.
/// </summary>
[Authorize]
[ApiController]
[Route("api/work-queue")]
public sealed class WorkQueueController : ControllerBase
{
    private readonly IWorkQueueRepository _repo;
    private readonly ILogger<WorkQueueController> _logger;

    public WorkQueueController(IWorkQueueRepository repo, ILogger<WorkQueueController> logger)
    {
        _repo   = repo;
        _logger = logger;
    }

    private Guid? GetUserId()
    {
        string? claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out Guid id) ? id : null;
    }

    private Guid? GetHouseholdId()
    {
        string? claim = User.FindFirstValue("household_id");
        return Guid.TryParse(claim, out Guid id) ? id : null;
    }

    // ── GET /api/work-queue ───────────────────────────────────────────────────

    /// <summary>Get all pending work queue items for the authenticated user.</summary>
    [HttpGet]
    public async Task<IActionResult> GetItems(CancellationToken ct)
    {
        Guid? userId = GetUserId();
        if (userId is null) return Unauthorized();
        Guid? householdId = GetHouseholdId();
        if (householdId is null) return Unauthorized();

        try
        {
            // Wake any snoozed items whose snooze period has expired (scoped to this user).
            await _repo.WakeSnoozedItemsAsync(userId.Value, ct);

            List<WorkQueueItemDto> items = await _repo.GetPendingItemsAsync(userId.Value, householdId.Value, ct);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work queue items for user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    // ── GET /api/work-queue/count ─────────────────────────────────────────────

    /// <summary>Get the pending item count and critical flag (for dashboard badge).</summary>
    [HttpGet("count")]
    public async Task<IActionResult> GetCount(CancellationToken ct)
    {
        Guid? userId = GetUserId();
        if (userId is null) return Unauthorized();
        Guid? householdId = GetHouseholdId();
        if (householdId is null) return Unauthorized();

        try
        {
            // Re-use GetPendingItems to get count + hasCritical in one lightweight query.
            List<WorkQueueItemDto> items = await _repo.GetPendingItemsAsync(userId.Value, householdId.Value, ct);
            int  count       = items.Count;
            bool hasCritical = items.Any(i => i.Priority <= 4);
            return Ok(new { count, hasCritical });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work queue count for user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    // ── POST /api/work-queue/{id}/action ─────────────────────────────────────

    /// <summary>Mark an item as actioned.</summary>
    [HttpPost("{id}/action")]
    public async Task<IActionResult> ActionItem(Guid id,
        [FromBody] WorkQueueActionRequest request, CancellationToken ct)
    {
        Guid? userId = GetUserId();
        if (userId is null) return Unauthorized();

        try
        {
            bool updated = await _repo.ActionItemAsync(id, userId.Value, request.ActionTaken, request.ActionData, ct);
            if (!updated) return NotFound();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actioning work queue item {ItemId}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    // ── DELETE /api/work-queue/{id} ───────────────────────────────────────────

    /// <summary>Dismiss an item for the current user (per-user soft delete).</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DismissItem(Guid id, CancellationToken ct)
    {
        Guid? userId = GetUserId();
        if (userId is null) return Unauthorized();

        try
        {
            bool updated = await _repo.DismissItemAsync(id, userId.Value, ct);
            if (!updated) return NotFound();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dismissing work queue item {ItemId}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    // ── POST /api/work-queue/{id}/snooze ─────────────────────────────────────

    /// <summary>Snooze an item for the given number of hours (default 24).</summary>
    [HttpPost("{id}/snooze")]
    public async Task<IActionResult> SnoozeItem(Guid id,
        [FromBody] WorkQueueSnoozeRequest? request, CancellationToken ct)
    {
        Guid? userId = GetUserId();
        if (userId is null) return Unauthorized();

        int hours = request?.Hours ?? 24;
        if (hours < 1 || hours > 168)
            return BadRequest(new { message = "Hours must be between 1 and 168" });

        try
        {
            bool updated = await _repo.SnoozeItemAsync(id, userId.Value, hours, ct);
            if (!updated) return NotFound();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error snoozing work queue item {ItemId}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }
}

// ── Request models ────────────────────────────────────────────────────────────

public sealed record WorkQueueActionRequest
{
    public string  ActionTaken { get; init; } = string.Empty;
    public string? ActionData  { get; init; }
}

public sealed record WorkQueueSnoozeRequest
{
    public int Hours { get; init; } = 24;
}
