using ExpressRecipe.PreferencesService.Contracts.Requests;
using ExpressRecipe.PreferencesService.Contracts.Responses;
using ExpressRecipe.PreferencesService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpressRecipe.PreferencesService.Controllers;

[ApiController]
[Route("api/cookprofile")]
[Authorize]
public class CookProfileController : ControllerBase
{
    private readonly ICookProfileService _service;
    private readonly ILogger<CookProfileController> _logger;

    public CookProfileController(
        ICookProfileService service,
        ILogger<CookProfileController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("{memberId:guid}")]
    public async Task<IActionResult> GetCookProfile(Guid memberId, CancellationToken ct)
    {
        CookProfileDto? profile = await _service.GetCookProfileAsync(memberId, ct);

        if (profile is null)
        {
            return NotFound();
        }

        return Ok(profile);
    }

    [HttpPut("{memberId:guid}")]
    public async Task<IActionResult> UpsertCookProfile(
        Guid memberId,
        [FromBody] UpsertCookProfileRequest request,
        CancellationToken ct)
    {
        Guid id = await _service.UpsertCookProfileAsync(memberId, request, ct);
        return Ok(new { Id = id });
    }

    [HttpGet("{memberId:guid}/techniques/{code}")]
    public async Task<IActionResult> GetTechniqueComfort(Guid memberId, string code, CancellationToken ct)
    {
        TechniqueComfortDto? comfort = await _service.GetTechniqueComfortAsync(memberId, code, ct);

        if (comfort is null)
        {
            return NotFound();
        }

        return Ok(comfort);
    }

    [HttpPut("{memberId:guid}/techniques/{code}")]
    public async Task<IActionResult> SetTechniqueComfort(
        Guid memberId,
        string code,
        [FromBody] SetTechniqueComfortRequest request,
        CancellationToken ct)
    {
        await _service.SetTechniqueComfortAsync(memberId, code, request, ct);
        return NoContent();
    }

    [HttpGet("{memberId:guid}/dismissedtips")]
    public async Task<IActionResult> GetDismissedTips(Guid memberId, CancellationToken ct)
    {
        List<DismissedTipDto> tips = await _service.GetDismissedTipsAsync(memberId, ct);
        return Ok(tips);
    }

    [HttpPost("{memberId:guid}/dismissedtips/{tipId:guid}")]
    public async Task<IActionResult> DismissTip(Guid memberId, Guid tipId, CancellationToken ct)
    {
        await _service.DismissTipAsync(memberId, tipId, ct);
        return NoContent();
    }

    [HttpDelete("{memberId:guid}/dismissedtips/{tipId:guid}")]
    public async Task<IActionResult> RestoreTip(Guid memberId, Guid tipId, CancellationToken ct)
    {
        await _service.RestoreTipAsync(memberId, tipId, ct);
        return NoContent();
    }

    [HttpGet("{memberId:guid}/tips/{techniqueCode}")]
    public async Task<IActionResult> GetTipsForMember(Guid memberId, string techniqueCode, CancellationToken ct)
    {
        List<CookingTipDto> tips = await _service.GetTipsForMemberAsync(memberId, techniqueCode, ct);
        return Ok(tips);
    }
}
