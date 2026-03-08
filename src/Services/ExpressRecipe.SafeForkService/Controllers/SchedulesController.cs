using ExpressRecipe.SafeForkService.Contracts.Requests;
using ExpressRecipe.SafeForkService.Contracts.Responses;
using ExpressRecipe.SafeForkService.Data;
using ExpressRecipe.SafeForkService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpressRecipe.SafeForkService.Controllers;

[ApiController]
[Route("api/schedules")]
[Authorize]
public class SchedulesController : ControllerBase
{
    private readonly IAllergenProfileService _service;
    private readonly ITemporaryScheduleRepository _scheduleRepo;
    private readonly ILogger<SchedulesController> _logger;

    public SchedulesController(
        IAllergenProfileService service,
        ITemporaryScheduleRepository scheduleRepo,
        ILogger<SchedulesController> logger)
    {
        _service = service;
        _scheduleRepo = scheduleRepo;
        _logger = logger;
    }

    [HttpGet("{memberId:guid}")]
    public async Task<IActionResult> GetActiveSchedules(Guid memberId, CancellationToken ct = default)
    {
        List<TemporaryScheduleDto> schedules = await _service.GetActiveSchedulesAsync(memberId, ct);
        return Ok(schedules);
    }

    [HttpPost("{memberId:guid}")]
    public async Task<IActionResult> AddSchedule(Guid memberId, [FromBody] AddTemporaryScheduleRequest request, CancellationToken ct = default)
    {
        Guid scheduleId = await _service.AddTemporaryScheduleAsync(
            memberId,
            request.ScheduleType,
            request.ActiveFrom,
            request.ActiveUntil,
            request.ConfigJson,
            ct);

        return CreatedAtAction(nameof(GetActiveSchedules), new { memberId }, new { scheduleId });
    }

    [HttpDelete("{memberId:guid}/{scheduleId:guid}")]
    public async Task<IActionResult> DeleteSchedule(Guid memberId, Guid scheduleId, CancellationToken ct = default)
    {
        bool deleted = await _scheduleRepo.SoftDeleteAsync(scheduleId, ct);
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }
}
