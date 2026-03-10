using ExpressRecipe.MealPlanningService.Data;
using Microsoft.AspNetCore.Mvc;

namespace ExpressRecipe.MealPlanningService.Controllers;

// MealPlanningService — internal endpoint for other services to push items.
// This endpoint is service-to-service only — protected by internal network only.
[ApiController]
[Route("api/work-queue/internal")]
public sealed class WorkQueueInternalController : ControllerBase
{
    private readonly IWorkQueueRepository _repo;

    public WorkQueueInternalController(IWorkQueueRepository repo) { _repo = repo; }

    [HttpPost("upsert")]
    public async Task<IActionResult> UpsertItem(
        [FromBody] UpsertWorkQueueItemRequest req, CancellationToken ct = default)
    {
        await _repo.UpsertItemAsync(req.HouseholdId, req.ItemType, req.Priority,
            req.Title, req.Body, req.ActionPayload,
            req.SourceEntityId, req.SourceService, req.ExpiresAt, ct);
        return NoContent();
    }
}

public sealed record UpsertWorkQueueItemRequest
{
    public Guid HouseholdId { get; init; }
    public string ItemType { get; init; } = string.Empty;
    public int Priority { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Body { get; init; }
    public string? ActionPayload { get; init; }
    public Guid? SourceEntityId { get; init; }
    public string? SourceService { get; init; }
    public DateTime? ExpiresAt { get; init; }
}
