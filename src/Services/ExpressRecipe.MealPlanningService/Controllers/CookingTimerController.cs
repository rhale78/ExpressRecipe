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
    public async Task<IActionResult> GetActive(CancellationToken ct)
    {
        try { return Ok(await _timers.GetActiveTimersAsync(GetUserId(), ct)); }
        catch (InvalidOperationException) { return Unauthorized(); }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            CookingTimerDto? timer = await _timers.GetByIdAsync(id, ct);
            if (timer is null) return NotFound();
            if (timer.UserId != GetUserId()) return Forbid();
            return Ok(timer);
        }
        catch (InvalidOperationException) { return Unauthorized(); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTimerRequest req, CancellationToken ct)
    {
        try
        {
            if (req.DurationSeconds <= 0)
                return BadRequest(new { error = "DurationSeconds must be > 0" });
            Guid id = await _timers.CreateTimerAsync(GetUserId(), GetHouseholdId(),
                req.Label, req.DurationSeconds, req.RecipeId, req.PlannedMealId, req.StartImmediately, ct);
            return Ok(new { id });
        }
        catch (InvalidOperationException) { return Unauthorized(); }
    }

    [HttpPost("{id}/start")]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct)
    {
        try
        {
            CookingTimerDto? timer = await _timers.GetByIdAsync(id, ct);
            if (timer is null) return NotFound();
            if (timer.UserId != GetUserId()) return Forbid();
            await _timers.StartTimerAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException) { return Unauthorized(); }
    }

    [HttpPost("{id}/pause")]
    public async Task<IActionResult> Pause(Guid id, CancellationToken ct)
    {
        try
        {
            CookingTimerDto? timer = await _timers.GetByIdAsync(id, ct);
            if (timer is null) return NotFound();
            if (timer.UserId != GetUserId()) return Forbid();
            await _timers.PauseTimerAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException) { return Unauthorized(); }
    }

    [HttpPost("{id}/resume")]
    public async Task<IActionResult> Resume(Guid id, CancellationToken ct)
    {
        try
        {
            CookingTimerDto? timer = await _timers.GetByIdAsync(id, ct);
            if (timer is null) return NotFound();
            if (timer.UserId != GetUserId()) return Forbid();
            await _timers.ResumeTimerAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException) { return Unauthorized(); }
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        try
        {
            CookingTimerDto? timer = await _timers.GetByIdAsync(id, ct);
            if (timer is null) return NotFound();
            if (timer.UserId != GetUserId()) return Forbid();
            await _timers.CancelTimerAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException) { return Unauthorized(); }
    }

    [HttpPost("{id}/acknowledge")]
    public async Task<IActionResult> Acknowledge(Guid id, CancellationToken ct)
    {
        try
        {
            CookingTimerDto? timer = await _timers.GetByIdAsync(id, ct);
            if (timer is null) return NotFound();
            if (timer.UserId != GetUserId()) return Forbid();
            await _timers.AcknowledgeTimerAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException) { return Unauthorized(); }
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
