using ExpressRecipe.MealPlanningService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.MealPlanningService.Controllers;

/// <summary>
/// Exposes the household work-queue. Authenticated users can view and manage queue items
/// for their household. The internal upsert endpoint is service-to-service only.
/// </summary>
[ApiController]
[Route("api/work-queue")]
[Authorize]
public class WorkQueueController : ControllerBase
{
    private readonly IWorkQueueRepository _repo;
    private readonly ILogger<WorkQueueController> _logger;

    public WorkQueueController(IWorkQueueRepository repo, ILogger<WorkQueueController> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    /// <summary>
    /// Get pending work-queue items for a household.
    /// </summary>
    [HttpGet("{householdId:guid}")]
    public async Task<IActionResult> GetPendingItems(Guid householdId, CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        try
        {
            var items = await _repo.GetPendingItemsAsync(householdId, ct);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work-queue items for household {HouseholdId}", householdId);
            return StatusCode(500, new { message = "An error occurred while retrieving work-queue items" });
        }
    }

    /// <summary>
    /// Mark a work-queue item as done.
    /// </summary>
    [HttpPost("{id:guid}/done")]
    public async Task<IActionResult> MarkDone(Guid id, CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        try
        {
            await _repo.MarkDoneAsync(id, ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking work-queue item {ItemId} as done", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Snooze a work-queue item until a future time.
    /// </summary>
    [HttpPost("{id:guid}/snooze")]
    public async Task<IActionResult> Snooze(Guid id, [FromBody] SnoozeWorkQueueItemRequest request, CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        try
        {
            await _repo.SnoozeAsync(id, userId.Value, request.ResumeAt, request.Notes, ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error snoozing work-queue item {ItemId}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Internal service-to-service endpoint: create or update a work-queue item.
    /// Callers should pass <c>X-Internal-Api-Key</c> when InternalApi:Key is configured.
    /// The <c>DeduplicationKey</c> field is used to avoid creating duplicate items for
    /// the same logical event (e.g. "low stock on chicken").
    /// </summary>
    [AllowAnonymous]
    [HttpPost("internal/upsert")]
    public async Task<IActionResult> InternalUpsert(
        [FromBody] UpsertWorkQueueItemRequest request,
        CancellationToken ct = default)
    {
        try
        {
            if (request.HouseholdId == Guid.Empty)
                return BadRequest(new { message = "HouseholdId is required" });

            if (string.IsNullOrWhiteSpace(request.Title))
                return BadRequest(new { message = "Title is required" });

            if (string.IsNullOrWhiteSpace(request.ItemType))
                return BadRequest(new { message = "ItemType is required" });

            var id = await _repo.UpsertAsync(request, ct);
            _logger.LogInformation(
                "WorkQueueItem upserted: {Id} type={Type} household={HouseholdId}",
                id, request.ItemType, request.HouseholdId);

            return Ok(new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting work-queue item for household {HouseholdId}", request.HouseholdId);
            return StatusCode(500, new { message = "An error occurred while upserting work-queue item" });
        }
    }
}

public sealed record SnoozeWorkQueueItemRequest
{
    public DateTime ResumeAt { get; init; }
    public string? Notes { get; init; }
}
