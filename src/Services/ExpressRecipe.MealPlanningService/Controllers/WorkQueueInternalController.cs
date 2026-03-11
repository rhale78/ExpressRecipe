using ExpressRecipe.MealPlanningService.Data;
using Microsoft.AspNetCore.Authorization;
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

    [AllowAnonymous]
    [HttpPost("upsert")]
    public async Task<IActionResult> UpsertItem(
        [FromBody] UpsertWorkQueueItemRequest req, CancellationToken ct = default)
    {
        await _repo.UpsertAsync(req, ct);
        return NoContent();
    }
}
