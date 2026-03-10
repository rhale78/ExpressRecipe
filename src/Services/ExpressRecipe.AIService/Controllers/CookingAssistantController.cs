using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ExpressRecipe.AIService.Services;

namespace ExpressRecipe.AIService.Controllers;

[Authorize]
[ApiController]
[Route("api/ai/recipe")]
public sealed class CookingAssistantController : ControllerBase
{
    private readonly ICookingAssistantService _assistant;

    public CookingAssistantController(ICookingAssistantService assistant)
    {
        _assistant = assistant;
    }

    private Guid? HouseholdId()
    {
        var claim = User.FindFirstValue("household_id");
        return Guid.TryParse(claim, out Guid id) ? id : null;
    }

    [HttpPost("something-off")]
    public async Task<IActionResult> SomethingOff(
        [FromBody] CookingAssistantRequest req, CancellationToken ct)
    {
        Guid? householdId = HouseholdId();
        if (householdId is null) return Unauthorized();
        return Ok(await _assistant.AskSomethingSeemsBrokenAsync(
            req with { HouseholdId = householdId.Value }, ct));
    }

    [HttpPost("pairings")]
    public async Task<IActionResult> GetPairings(
        [FromBody] CookingAssistantRequest req, CancellationToken ct)
    {
        Guid? householdId = HouseholdId();
        if (householdId is null) return Unauthorized();
        return Ok(await _assistant.GetPairingsAsync(
            req with { HouseholdId = householdId.Value }, ct));
    }

    [HttpPost("problem")]
    public async Task<IActionResult> TroubleshootProblem(
        [FromBody] CookingAssistantRequest req, CancellationToken ct)
    {
        Guid? householdId = HouseholdId();
        if (householdId is null) return Unauthorized();
        return Ok(await _assistant.TroubleshootProblemAsync(
            req with { HouseholdId = householdId.Value }, ct));
    }

    [HttpPost("variations")]
    public async Task<IActionResult> GetVariations(
        [FromBody] CookingAssistantRequest req, CancellationToken ct)
    {
        Guid? householdId = HouseholdId();
        if (householdId is null) return Unauthorized();
        return Ok(await _assistant.GetVariationsAsync(
            req with { HouseholdId = householdId.Value }, ct));
    }

    [HttpPost("fix")]
    public async Task<IActionResult> FixIssue(
        [FromBody] CookingAssistantRequest req, CancellationToken ct)
    {
        Guid? householdId = HouseholdId();
        if (householdId is null) return Unauthorized();
        return Ok(await _assistant.FixIssueAsync(
            req with { HouseholdId = householdId.Value }, ct));
    }

    [HttpPost("adapt")]
    public async Task<IActionResult> AdaptRecipe(
        [FromBody] CookingAssistantRequest req,
        [FromQuery] string method = "crockpot",
        CancellationToken ct = default)
    {
        Guid? householdId = HouseholdId();
        if (householdId is null) return Unauthorized();
        return Ok(await _assistant.AdaptRecipeAsync(
            req with { HouseholdId = householdId.Value }, method, ct));
    }
}
