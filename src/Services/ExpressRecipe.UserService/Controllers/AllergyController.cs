using ExpressRecipe.Shared.DTOs.User;
using ExpressRecipe.UserService.Data;
using ExpressRecipe.UserService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExpressRecipe.UserService.Controllers;

[ApiController]
[Route("api/allergy")]
[Authorize]
public class AllergyController : ControllerBase
{
    private readonly IAllergyIncidentRepository    _repo;
    private readonly IFamilyMemberRepository       _familyRepo;
    private readonly AllergyDifferentialAnalyzer   _analyzer;
    private readonly ILogger<AllergyController>    _logger;

    public AllergyController(
        IAllergyIncidentRepository repo,
        IFamilyMemberRepository familyRepo,
        AllergyDifferentialAnalyzer analyzer,
        ILogger<AllergyController> logger)
    {
        _repo       = repo;
        _familyRepo = familyRepo;
        _analyzer   = analyzer;
        _logger     = logger;
    }

    private Guid? UserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    // Household = primary user Id in this service.
    private Guid? HouseholdId() => UserId();

    private string UserName() =>
        User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name") ?? "Me";

    // ─── Incidents ────────────────────────────────────────────────────────────

    [HttpPost("incidents")]
    public async Task<IActionResult> CreateIncident(
        [FromBody] CreateAllergyIncidentV2Request request, CancellationToken ct)
    {
        var householdId = HouseholdId();
        if (householdId == null) return Unauthorized();

        try
        {
            var id = await _repo.CreateIncidentAsync(householdId.Value, request, ct);

            // Run analysis asynchronously per affected member (fire & forget with catch)
            _ = Task.Run(async () =>
            {
                var members = request.Members
                    .Select(m => (m.MemberId, m.MemberName))
                    .Distinct()
                    .ToList();

                await _analyzer.RunForMembersAsync(householdId.Value, members!, ct);
            }, ct);

            return CreatedAtAction(nameof(GetIncident), new { id }, new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating allergy incident");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    [HttpGet("incidents/{id:guid}")]
    public async Task<IActionResult> GetIncident(Guid id, CancellationToken ct)
    {
        var householdId = HouseholdId();
        if (householdId == null) return Unauthorized();

        var incident = await _repo.GetIncidentByIdAsync(id, ct);
        if (incident == null) return NotFound();
        if (incident.HouseholdId != householdId.Value) return Forbid();
        return Ok(incident);
    }

    [HttpGet("incidents")]
    public async Task<IActionResult> GetIncidents(
        [FromQuery] Guid? memberId = null,
        [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var householdId = HouseholdId();
        if (householdId == null) return Unauthorized();
        if (limit is <= 0 or > 200) return BadRequest(new { message = "Limit must be 1–200" });

        var incidents = await _repo.GetIncidentsAsync(householdId.Value, memberId, limit, ct);
        return Ok(incidents);
    }

    // ─── Suspects ─────────────────────────────────────────────────────────────

    [HttpGet("suspects")]
    public async Task<IActionResult> GetSuspects(
        [FromQuery] Guid? memberId = null, CancellationToken ct = default)
    {
        var householdId = HouseholdId();
        if (householdId == null) return Unauthorized();

        var suspects = await _repo.GetSuspectedAllergensAsync(householdId.Value, memberId, ct);
        return Ok(suspects);
    }

    [HttpPost("suspects/{id:guid}/confirm")]
    public async Task<IActionResult> ConfirmSuspect(Guid id, CancellationToken ct)
    {
        var householdId = HouseholdId();
        if (householdId == null) return Unauthorized();

        var suspect = await _repo.GetSuspectedAllergenByIdAsync(id, ct);
        if (suspect == null) return NotFound();
        if (suspect.HouseholdId != householdId.Value) return Forbid();

        await _repo.PromoteSuspectedAllergenAsync(id, ct);
        return NoContent();
    }

    [HttpDelete("suspects/{id:guid}")]
    public async Task<IActionResult> ClearSuspect(Guid id, CancellationToken ct)
    {
        var userId = UserId();
        if (userId == null) return Unauthorized();

        var householdId = HouseholdId();
        var suspect = await _repo.GetSuspectedAllergenByIdAsync(id, ct);
        if (suspect == null) return NotFound();
        if (suspect.HouseholdId != householdId!.Value) return Forbid();

        await _repo.DeleteSuspectedAllergenAsync(id, ct);
        await _repo.InsertUserClearedIngredientAsync(id, userId.Value, ct);
        return NoContent();
    }

    // ─── Cleared Ingredients ──────────────────────────────────────────────────

    [HttpGet("cleared")]
    public async Task<IActionResult> GetClearedIngredients(
        [FromQuery] Guid? memberId = null, CancellationToken ct = default)
    {
        var householdId = HouseholdId();
        if (householdId == null) return Unauthorized();

        var items = await _repo.GetClearedIngredientsAsync(householdId.Value, memberId, ct);
        return Ok(items);
    }

    // ─── Report ───────────────────────────────────────────────────────────────

    [HttpGet("report/{memberId?}")]
    public async Task<IActionResult> GetReport(Guid? memberId, CancellationToken ct)
    {
        var householdId = HouseholdId();
        if (householdId == null) return Unauthorized();

        string memberName = "Me";
        if (memberId.HasValue)
        {
            var member = await _familyRepo.GetByIdAsync(memberId.Value);
            if (member == null) return NotFound(new { message = "Member not found" });
            if (member.PrimaryUserId != householdId.Value) return Forbid();
            memberName = member.Name;
        }

        var suspects  = await _repo.GetSuspectedAllergensAsync(householdId.Value, memberId, ct);
        var incidents = await _repo.GetIncidentsAsync(householdId.Value, memberId, 200, ct);
        var cleared   = await _repo.GetClearedIngredientsAsync(householdId.Value, memberId, ct);

        var report = new AllergyReportModel
        {
            MemberName         = memberName,
            GeneratedAt        = DateTime.UtcNow,
            SuspectedAllergens = suspects.OrderByDescending(s => s.ConfidenceScore).ToList(),
            Incidents          = incidents,
            ClearedIngredients = cleared
        };

        return Ok(report);
    }
}