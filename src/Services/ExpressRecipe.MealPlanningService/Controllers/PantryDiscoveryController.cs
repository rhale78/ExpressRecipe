using ExpressRecipe.MealPlanningService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.MealPlanningService.Controllers;

[Authorize]
[ApiController]
[Route("api/discover")]
public sealed class PantryDiscoveryController : ControllerBase
{
    private readonly IPantryDiscoveryService _discovery;

    public PantryDiscoveryController(IPantryDiscoveryService discovery)
    {
        _discovery = discovery;
    }

    private Guid? GetUserId()
    {
        string? claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out Guid id) ? id : null;
    }

    private Guid? GetHouseholdId()
    {
        string? claim = User.FindFirstValue("household_id");
        return Guid.TryParse(claim, out Guid id) ? id : null;
    }

    /// <summary>
    /// Discover what recipes can be made from the household's current pantry inventory.
    /// Results are cached per household for 30 minutes.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Discover(
        [FromQuery] decimal minMatch = 0.80m,
        [FromQuery] string sortBy = "match",
        [FromQuery] int limit = 24,
        [FromQuery] bool respectDiet = true,
        CancellationToken ct = default)
    {
        Guid? userId = GetUserId();
        if (userId is null) { return Unauthorized(); }

        Guid? householdId = GetHouseholdId();
        if (householdId is null) { return BadRequest(new { message = "household_id claim is missing from the token." }); }

        minMatch = Math.Clamp(minMatch, 0.40m, 1.00m);
        limit    = Math.Clamp(limit, 1, 100);

        PantryDiscoveryOptions options = new()
        {
            MinMatchPercent            = minMatch,
            SortBy                     = sortBy,
            Limit                      = limit,
            RespectDietaryRestrictions = respectDiet
        };

        PantryDiscoveryResult result =
            await _discovery.DiscoverAsync(householdId.Value, userId.Value, options, ct);

        return Ok(result);
    }
}
