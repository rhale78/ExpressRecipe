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
    private readonly IAllergyIncidentRepository  _repo;
    private readonly IFamilyMemberRepository     _familyRepo;
    private readonly IEnhancedAllergenRepository _allergenRepo;
    private readonly AllergyDifferentialAnalyzer _analyzer;
    private readonly ILogger<AllergyController>  _logger;

    public AllergyController(
        IAllergyIncidentRepository repo,
        IFamilyMemberRepository familyRepo,
        IEnhancedAllergenRepository allergenRepo,
        AllergyDifferentialAnalyzer analyzer,
        ILogger<AllergyController> logger)
    {
        _repo        = repo;
        _familyRepo  = familyRepo;
        _allergenRepo = allergenRepo;
        _analyzer    = analyzer;
        _logger      = logger;
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

            // Run analysis asynchronously per affected member.
            // Use CancellationToken.None so a client disconnect never cancels analysis.
            _ = Task.Run(async () =>
            {
                try
                {
                    var members = request.Members
                        .Select(m => (m.MemberId, m.MemberName))
                        .Distinct()
                        .ToList();

                    await _analyzer.RunForMembersAsync(householdId.Value, members!, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error running allergy differential analysis for household {HouseholdId}",
                        householdId.Value);
                }
            }, CancellationToken.None);

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
        var userId = UserId();
        if (userId == null) return Unauthorized();

        var suspect = await _repo.GetSuspectedAllergenByIdAsync(id, ct);
        if (suspect == null) return NotFound();
        if (suspect.HouseholdId != userId.Value) return Forbid();

        // Promote the suspect flag
        await _repo.PromoteSuspectedAllergenAsync(id, ct);

        // Also create a UserIngredientAllergy record so SafeFork / other features
        // treat this ingredient as a confirmed allergy going forward.
        try
        {
            await _allergenRepo.CreateIngredientAllergyAsync(userId.Value,
                new CreateUserIngredientAllergyRequest
                {
                    IngredientName = suspect.IngredientName,
                    SeverityLevel  = "Moderate",   // default — user can edit in AllergensController
                    RequiresEpiPen = false
                });
        }
        catch (Exception ex)
        {
            // Log but don't fail the confirmation if the profile upsert fails
            _logger.LogWarning(ex,
                "Could not create ingredient allergy profile entry for {Ingredient}", suspect.IngredientName);
        }

        return NoContent();
    }

    [HttpDelete("suspects/{id:guid}")]
    public async Task<IActionResult> ClearSuspect(Guid id, CancellationToken ct)
    {
        var userId = UserId();
        if (userId == null) return Unauthorized();

        var suspect = await _repo.GetSuspectedAllergenByIdAsync(id, ct);
        if (suspect == null) return NotFound();
        if (suspect.HouseholdId != userId.Value) return Forbid();

        // Atomic operation: insert ClearedIngredient + soft-delete suspect in one transaction
        await _repo.ClearSuspectTransactionalAsync(id, userId.Value, ct);
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
        var userId = UserId();
        if (userId == null) return Unauthorized();

        string memberName = "Me";
        if (memberId.HasValue)
        {
            var member = await _familyRepo.GetByIdAsync(memberId.Value);
            if (member == null) return NotFound(new { message = "Member not found" });
            if (member.PrimaryUserId != userId.Value) return Forbid();
            memberName = member.Name;
        }

        var suspects  = await _repo.GetSuspectedAllergensAsync(userId.Value, memberId, ct);
        var incidents = await _repo.GetIncidentsAsync(userId.Value, memberId, 200, ct);
        var cleared   = await _repo.GetClearedIngredientsAsync(userId.Value, memberId, ct);

        // Confirmed allergens come from the ingredient allergy profile (primary user only)
        List<ConfirmedAllergenDto> confirmed = new();
        if (!memberId.HasValue)
        {
            try
            {
                var ingredientAllergies =
                    await _allergenRepo.GetUserIngredientAllergiesAsync(userId.Value, includeReactions: false);

                confirmed = ingredientAllergies
                    .Select(a => new ConfirmedAllergenDto
                    {
                        AllergenName   = a.IngredientName ?? string.Empty,
                        SeverityLevel  = a.SeverityLevel,
                        RequiresEpiPen = a.RequiresEpiPen,
                        DiagnosisDate  = a.DiagnosisDate
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load confirmed allergens for report");
            }
        }

        var report = new AllergyReportModel
        {
            MemberName         = memberName,
            GeneratedAt        = DateTime.UtcNow,
            ConfirmedAllergens = confirmed,
            SuspectedAllergens = suspects.OrderByDescending(s => s.ConfidenceScore).ToList(),
            Incidents          = incidents,
            ClearedIngredients = cleared
        };

        return Ok(report);
    }
}
