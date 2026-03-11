using ExpressRecipe.SafeForkService.Contracts.Requests;
using ExpressRecipe.SafeForkService.Contracts.Responses;
using ExpressRecipe.SafeForkService.Services;
using ExpressRecipe.Shared.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpressRecipe.SafeForkService.Controllers;

[ApiController]
[Route("api/allergenprofile")]
[Authorize]
[RequiresFeature("allergy-engine")]
public class AllergenProfileController : ControllerBase
{
    private readonly IAllergenProfileService _service;
    private readonly ILogger<AllergenProfileController> _logger;

    public AllergenProfileController(
        IAllergenProfileService service,
        ILogger<AllergenProfileController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("{memberId:guid}")]
    public async Task<IActionResult> GetEffectiveProfile(Guid memberId, [FromQuery] bool includeSchedules = true, CancellationToken ct = default)
    {
        AllergenProfileDto? profile = await _service.GetEffectiveProfileAsync(memberId, includeSchedules, ct);
        if (profile == null)
        {
            return NotFound();
        }

        return Ok(profile);
    }

    [HttpPost("{memberId:guid}/curated")]
    public async Task<IActionResult> AddCuratedAllergen(Guid memberId, [FromBody] AddCuratedAllergenRequest request, CancellationToken ct = default)
    {
        Guid entryId = await _service.AddCuratedAllergenAsync(memberId, request, ct);
        return CreatedAtAction(nameof(GetEffectiveProfile), new { memberId }, new { entryId });
    }

    [HttpPost("{memberId:guid}/freeform")]
    public async Task<IActionResult> AddFreeformAllergen(Guid memberId, [FromBody] AddFreeformAllergenRequest request, CancellationToken ct = default)
    {
        Guid entryId = await _service.AddFreeformAllergenAsync(memberId, request.FreeFormText, request.Brand, ct);
        return CreatedAtAction(nameof(GetEffectiveProfile), new { memberId }, new { entryId });
    }

    [HttpDelete("{memberId:guid}/entry/{entryId:guid}")]
    public async Task<IActionResult> DeleteEntry(Guid memberId, Guid entryId, CancellationToken ct = default)
    {
        bool deleted = await _service.DeleteEntryForMemberAsync(memberId, entryId, ct);
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpGet("union/{householdId:guid}")]
    public async Task<IActionResult> GetUnionProfile(Guid householdId, [FromQuery] List<Guid> memberIds, CancellationToken ct = default)
    {
        if (memberIds == null || memberIds.Count == 0)
        {
            return BadRequest("At least one memberId is required.");
        }

        UnionProfileDto union = await _service.ComputeUnionProfileAsync(memberIds, ct);
        union.HouseholdId = householdId;
        return Ok(union);
    }

    [HttpPost("evaluate")]
    public async Task<IActionResult> EvaluateRecipe([FromBody] EvaluateRecipeRequest request, CancellationToken ct = default)
    {
        RecipeEvaluationResult result = await _service.EvaluateRecipeAsync(request.Ingredients, request.Profile, ct);
        return Ok(result);
    }

    [HttpPost("adapt")]
    public async Task<IActionResult> ResolveAdaptation([FromBody] ResolveAdaptationRequest request, CancellationToken ct = default)
    {
        string strategy = await _service.ResolveAdaptationStrategyAsync(
            request.ConflictReport, request.HouseholdId, request.RecipeInstanceId, ct);
        return Ok(new { strategy });
    }
}
