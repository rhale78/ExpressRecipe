using ExpressRecipe.MealPlanningService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Security.Cryptography;

namespace ExpressRecipe.MealPlanningService.Controllers;

/// <summary>
/// Exposes the household work-queue. Authenticated users can view and manage queue items
/// for their own household (resolved from JWT claims). The internal upsert endpoint is
/// service-to-service only.
/// </summary>
[ApiController]
[Route("api/work-queue")]
[Authorize]
public class WorkQueueController : ControllerBase
{
    private readonly IWorkQueueRepository _repo;
    private readonly ILogger<WorkQueueController> _logger;
    private readonly IConfiguration? _configuration;

    public WorkQueueController(
        IWorkQueueRepository repo,
        ILogger<WorkQueueController> logger,
        IConfiguration? configuration = null)
    {
        _repo = repo;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>Returns the authenticated user's ID from the JWT, or throws if missing.</summary>
    private Guid GetUserId()
    {
        string? claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(claim) || !Guid.TryParse(claim, out Guid id) || id == Guid.Empty)
            throw new InvalidOperationException("User identity claim is missing or invalid.");
        return id;
    }

    /// <summary>Returns the authenticated user's household ID from the JWT, or throws if missing.</summary>
    private Guid GetHouseholdId()
    {
        string? claim = User.FindFirstValue("household_id");
        if (string.IsNullOrEmpty(claim) || !Guid.TryParse(claim, out Guid id) || id == Guid.Empty)
            throw new InvalidOperationException("household_id claim is missing or invalid.");
        return id;
    }

    /// <summary>
    /// Get pending work-queue items for the caller's household (from JWT claims).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPendingItems(CancellationToken ct = default)
    {
        Guid householdId;
        try { householdId = GetHouseholdId(); }
        catch (InvalidOperationException) { return Unauthorized(); }

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
    /// Mark a work-queue item as done. The item must belong to the caller's household.
    /// </summary>
    [HttpPost("{id:guid}/done")]
    public async Task<IActionResult> MarkDone(Guid id, CancellationToken ct = default)
    {
        Guid householdId;
        try { householdId = GetHouseholdId(); }
        catch (InvalidOperationException) { return Unauthorized(); }

        try
        {
            var item = await _repo.GetByIdAsync(id, ct);
            if (item == null) return NotFound();
            if (item.HouseholdId != householdId) return Forbid();

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
    /// Action a work-queue item (legacy endpoint from PR #58).
    /// </summary>
    [HttpPost("{id:guid}/action")]
    public async Task<IActionResult> ActionItem(Guid id,
        [FromBody] ActionQueueItemRequest req, CancellationToken ct = default)
    {
        Guid householdId;
        try { householdId = GetHouseholdId(); }
        catch (InvalidOperationException) { return Unauthorized(); }

        try
        {
            var item = await _repo.GetByIdAsync(id, ct);
            if (item == null) return NotFound();
            if (item.HouseholdId != householdId) return Forbid();

            await _repo.ActionItemAsync(id, GetUserId(), req.ActionTaken, req.ActionData, ct);
            return NoContent();
        }
        catch (InvalidOperationException) { return Unauthorized(); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actioning work-queue item {ItemId}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Dismiss a work-queue item. The item must belong to the caller's household.
    /// </summary>
    [HttpPost("{id:guid}/dismiss")]
    public async Task<IActionResult> Dismiss(Guid id, CancellationToken ct = default)
    {
        Guid householdId;
        try { householdId = GetHouseholdId(); }
        catch (InvalidOperationException) { return Unauthorized(); }

        try
        {
            var item = await _repo.GetByIdAsync(id, ct);
            if (item == null) return NotFound();
            if (item.HouseholdId != householdId) return Forbid();

            await _repo.DismissItemAsync(id, GetUserId(), ct);
            return NoContent();
        }
        catch (InvalidOperationException) { return Unauthorized(); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dismissing work-queue item {ItemId}", id);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    /// <summary>
    /// Snooze a work-queue item until a future time. The item must belong to the caller's household.
    /// </summary>
    [HttpPost("{id:guid}/snooze")]
    public async Task<IActionResult> Snooze(Guid id, [FromBody] SnoozeWorkQueueItemRequest request, CancellationToken ct = default)
    {
        Guid userId;
        Guid householdId;
        try { userId = GetUserId(); householdId = GetHouseholdId(); }
        catch (InvalidOperationException) { return Unauthorized(); }

        try
        {
            var item = await _repo.GetByIdAsync(id, ct);
            if (item == null) return NotFound();
            if (item.HouseholdId != householdId) return Forbid();

            // Enforce a minimum snooze: if ResumeAt is in the past or present, snooze until tomorrow.
            DateTime resumeAt = request.ResumeAt > DateTime.UtcNow
                ? request.ResumeAt
                : DateTime.UtcNow.Date.AddDays(1);

            await _repo.SnoozeAsync(id, userId, resumeAt, request.Notes, ct);
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
        string? configuredKey = _configuration?["InternalApi:Key"];
        if (!string.IsNullOrEmpty(configuredKey))
        {
            string? providedKey = Request.Headers["X-Internal-Api-Key"].FirstOrDefault();
            if (!IsValidApiKey(providedKey, configuredKey))
                return Unauthorized(new { error = "Invalid or missing X-Internal-Api-Key header" });
        }

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

    private static bool IsValidApiKey(string? provided, string configured)
    {
        if (provided is null) return false;
        byte[] a = System.Text.Encoding.UTF8.GetBytes(provided);
        byte[] b = System.Text.Encoding.UTF8.GetBytes(configured);
        if (a.Length != b.Length)
        {
            byte[] padded = new byte[Math.Max(a.Length, b.Length)];
            Buffer.BlockCopy(a.Length < b.Length ? a : b, 0, padded, 0, Math.Min(a.Length, b.Length));
            if (a.Length < b.Length) { a = padded; } else { b = padded; }
        }
        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}

public sealed record SnoozeWorkQueueItemRequest
{
    public DateTime ResumeAt { get; init; }
    public string? Notes { get; init; }
}
