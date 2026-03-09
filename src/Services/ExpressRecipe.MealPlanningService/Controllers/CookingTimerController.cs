using System.Security.Claims;
using ExpressRecipe.MealPlanningService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpressRecipe.MealPlanningService.Controllers;

[Authorize]
[ApiController]
[Route("api/timers")]
public sealed class CookingTimerController : ControllerBase
{
    private readonly ICookingTimerRepository _timers;

    public CookingTimerController(ICookingTimerRepository timers) { _timers = timers; }

    private Guid GetUserId()
        => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? Guid.Empty.ToString());
    private Guid GetHouseholdId()
        => Guid.Parse(User.FindFirstValue("household_id") ?? Guid.Empty.ToString());

    [HttpGet]
    public async Task<IActionResult> GetActive(CancellationToken ct)
        => Ok(await _timers.GetActiveTimersAsync(GetUserId(), ct));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        CookingTimerDto? timer = await _timers.GetByIdAsync(id, ct);
        return timer is null ? NotFound() : Ok(timer);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTimerRequest req, CancellationToken ct)
    {
        if (req.DurationSeconds <= 0)
            return BadRequest(new { error = "DurationSeconds must be > 0" });
        Guid id = await _timers.CreateTimerAsync(GetUserId(), GetHouseholdId(),
            req.Label, req.DurationSeconds, req.RecipeId, req.PlannedMealId, req.StartImmediately, ct);
        return Ok(new { id });
    }

    [HttpPost("{id}/start")]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct)
    {
        await _timers.StartTimerAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id}/pause")]
    public async Task<IActionResult> Pause(Guid id, CancellationToken ct)
    {
        await _timers.PauseTimerAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id}/resume")]
    public async Task<IActionResult> Resume(Guid id, CancellationToken ct)
    {
        await _timers.ResumeTimerAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        await _timers.CancelTimerAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id}/acknowledge")]
    public async Task<IActionResult> Acknowledge(Guid id, CancellationToken ct)
    {
        await _timers.AcknowledgeTimerAsync(id, ct);
        return NoContent();
    }
}

public sealed record CreateTimerRequest
{
    public string Label { get; init; } = string.Empty;
    public int DurationSeconds { get; init; }
    public Guid? RecipeId { get; init; }
    public Guid? PlannedMealId { get; init; }
    public bool StartImmediately { get; init; }
}
