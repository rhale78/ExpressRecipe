using ExpressRecipe.CommunityService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.CommunityService.Controllers;

/// <summary>
/// Admin content-moderation queue: approve, reject, or send items for AI assessment.
/// Accessible by Admin, CS, and Moderator roles.
/// </summary>
[Authorize(Roles = "Admin,CS,Moderator")]
[ApiController]
[Route("api/admin/approvals")]
public sealed class ApprovalQueueController : ControllerBase
{
    private readonly IApprovalQueueRepository _queue;
    private readonly ILogger<ApprovalQueueController> _logger;

    public ApprovalQueueController(IApprovalQueueRepository queue, ILogger<ApprovalQueueController> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    private Guid? ReviewerId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    /// <summary>
    /// List items in the approval queue.
    /// </summary>
    /// <param name="entityType">Optional filter: Recipe | Product | Review</param>
    /// <param name="status">Queue status to filter (default: Pending)</param>
    /// <param name="limit">Maximum number of items to return (default: 50)</param>
    [HttpGet]
    public async Task<IActionResult> GetQueue(
        [FromQuery] string? entityType,
        [FromQuery] string status = "Pending",
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var items = await _queue.GetItemsAsync(entityType, status, limit, ct);
        return Ok(items);
    }

    /// <summary>
    /// Approve a queued item.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(
        Guid id,
        [FromBody] ApprovalDecisionRequest req,
        CancellationToken ct)
    {
        var reviewerId = ReviewerId();
        if (reviewerId is null) return Unauthorized();

        var item = await _queue.GetByIdAsync(id, ct);
        if (item is null) return NotFound(new { message = "Approval queue item not found." });

        await _queue.ApproveAsync(id, reviewerId.Value, req.Notes, ct);

        _logger.LogInformation("Reviewer {ReviewerId} approved {EntityType}/{EntityId}",
            reviewerId, item.EntityType, item.EntityId);

        return NoContent();
    }

    /// <summary>
    /// Reject a queued item.
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(
        Guid id,
        [FromBody] ApprovalDecisionRequest req,
        CancellationToken ct)
    {
        var reviewerId = ReviewerId();
        if (reviewerId is null) return Unauthorized();

        var item = await _queue.GetByIdAsync(id, ct);
        if (item is null) return NotFound(new { message = "Approval queue item not found." });

        await _queue.RejectAsync(id, reviewerId.Value, req.Reason ?? "No reason given", ct);

        _logger.LogInformation("Reviewer {ReviewerId} rejected {EntityType}/{EntityId}: {Reason}",
            reviewerId, item.EntityType, item.EntityId, req.Reason);

        return NoContent();
    }

    /// <summary>
    /// Trigger AI assessment for a queued item (human explicitly requests AI review).
    /// </summary>
    [HttpPost("{id:guid}/send-to-ai")]
    public async Task<IActionResult> SendToAI(Guid id, CancellationToken ct)
    {
        var item = await _queue.GetByIdAsync(id, ct);
        if (item is null) return NotFound(new { message = "Approval queue item not found." });

        await _queue.SetStatusAsync(id, "AiReview", ct);

        _logger.LogInformation("Item {Id} sent to AI review by {User}", id, User.Identity?.Name);

        return NoContent();
    }
}

public sealed record ApprovalDecisionRequest
{
    public string? Notes { get; init; }
    public string? Reason { get; init; }
}
