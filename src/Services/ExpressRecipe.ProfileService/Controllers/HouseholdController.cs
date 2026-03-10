using ExpressRecipe.ProfileService.Contracts.Requests;
using ExpressRecipe.ProfileService.Contracts.Responses;
using ExpressRecipe.ProfileService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.ProfileService.Controllers;

[ApiController]
[Route("api/household")]
[Authorize]
public class HouseholdController : ControllerBase
{
    private readonly IHouseholdMemberService _service;
    private readonly ILogger<HouseholdController> _logger;

    public HouseholdController(IHouseholdMemberService service, ILogger<HouseholdController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("{householdId:guid}/members")]
    public async Task<IActionResult> GetMembers(Guid householdId, CancellationToken ct)
    {
        List<HouseholdMemberDto> members = await _service.GetMembersAsync(householdId, ct);
        return Ok(members);
    }

    [HttpPost("{householdId:guid}/members")]
    public async Task<IActionResult> AddMember(Guid householdId, [FromBody] AddMemberRequest request, CancellationToken ct)
    {
        Guid? requestingUserId = GetRequestingUserId();
        Guid memberId = await _service.AddMemberAsync(householdId, request, requestingUserId, ct);
        return CreatedAtAction(nameof(GetMembers), new { householdId }, new { memberId });
    }

    [HttpPut("{householdId:guid}/members/{memberId:guid}")]
    public async Task<IActionResult> UpdateMember(Guid householdId, Guid memberId, [FromBody] UpdateMemberRequest request, CancellationToken ct)
    {
        Guid? requestingUserId = GetRequestingUserId();

        try
        {
            bool updated = await _service.UpdateMemberAsync(householdId, memberId, request, requestingUserId, ct);

            if (!updated)
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{householdId:guid}/members/{memberId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid householdId, Guid memberId, CancellationToken ct)
    {
        Guid? requestingUserId = GetRequestingUserId();
        bool removed = await _service.RemoveMemberAsync(householdId, memberId, requestingUserId, ct);

        if (!removed)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPost("{householdId:guid}/guests/temporary")]
    public async Task<IActionResult> AddTemporaryVisitor(Guid householdId, [FromBody] AddTemporaryVisitorRequest request, CancellationToken ct)
    {
        Guid? requestingUserId = GetRequestingUserId();
        Guid memberId = await _service.AddTemporaryVisitorAsync(householdId, request, requestingUserId, ct);
        return CreatedAtAction(nameof(GetMembers), new { householdId }, new { memberId });
    }

    [HttpPost("{householdId:guid}/guests/cross-household")]
    public async Task<IActionResult> AddCrossHouseholdGuest(Guid householdId, [FromBody] AddCrossHouseholdGuestRequest request, CancellationToken ct)
    {
        Guid? requestingUserId = GetRequestingUserId();
        Guid memberId = await _service.AddCrossHouseholdGuestAsync(householdId, request, requestingUserId, ct);
        return CreatedAtAction(nameof(GetMembers), new { householdId }, new { memberId });
    }

    private Guid? GetRequestingUserId()
    {
        string? sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub");

        if (Guid.TryParse(sub, out Guid userId))
        {
            return userId;
        }

        return null;
    }
}
