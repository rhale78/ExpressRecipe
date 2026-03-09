using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ExpressRecipe.InventoryService.Data;

namespace ExpressRecipe.InventoryService.Controllers;

/// <summary>
/// Manages livestock/flock animals, daily production logging, and harvest/processing events.
/// All routes are household-scoped. The caller must be a member of the household.
/// </summary>
[Authorize]
[ApiController]
[Route("api/livestock")]
public class LivestockController : ControllerBase
{
    private readonly ILogger<LivestockController> _logger;
    private readonly ILivestockRepository _livestock;
    private readonly IInventoryRepository _inventory;

    public LivestockController(
        ILogger<LivestockController> logger,
        ILivestockRepository livestock,
        IInventoryRepository inventory)
    {
        _logger = logger;
        _livestock = livestock;
        _inventory = inventory;
    }

    private Guid? GetUserId()
    {
        string? claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out Guid id) ? id : null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Animals / Flocks
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Get all animals/flocks for the household.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAnimals(
        [FromQuery] Guid householdId,
        [FromQuery] bool activeOnly = true)
    {
        try
        {
            Guid? userId = GetUserId();
            if (userId == null) { return Unauthorized(); }

            if (!await _inventory.IsUserMemberOfHouseholdAsync(householdId, userId.Value))
            {
                return Forbid();
            }

            _logger.LogInformation("Getting livestock for household {HouseholdId}", householdId);
            List<LivestockAnimalDto> animals = await _livestock.GetAnimalsAsync(householdId, activeOnly);
            return Ok(animals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving livestock for household {HouseholdId}", householdId);
            return StatusCode(500, new { message = "An error occurred while retrieving livestock." });
        }
    }

    /// <summary>Add an animal or flock to the household.</summary>
    [HttpPost]
    public async Task<IActionResult> AddAnimal([FromBody] AddAnimalRequest request)
    {
        try
        {
            Guid? userId = GetUserId();
            if (userId == null) { return Unauthorized(); }

            if (!await _inventory.IsUserMemberOfHouseholdAsync(request.HouseholdId, userId.Value))
            {
                return Forbid();
            }

            Guid animalId = await _livestock.AddAnimalAsync(
                request.HouseholdId, request.Name, request.AnimalType, request.ProductionCategory,
                request.IsFlockOrHerd, request.Count, request.AcquiredDate, request.BreedNotes, request.Notes);

            LivestockAnimalDto? created = await _livestock.GetAnimalByIdAsync(animalId);
            return CreatedAtAction(nameof(GetAnimal), new { id = animalId }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding livestock animal");
            return StatusCode(500, new { message = "An error occurred while adding the animal." });
        }
    }

    /// <summary>Get a single animal or flock by ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAnimal(Guid id)
    {
        try
        {
            Guid? userId = GetUserId();
            if (userId == null) { return Unauthorized(); }

            LivestockAnimalDto? animal = await _livestock.GetAnimalByIdAsync(id);
            if (animal == null) { return NotFound(); }

            if (!await _inventory.IsUserMemberOfHouseholdAsync(animal.HouseholdId, userId.Value))
            {
                return Forbid();
            }

            return Ok(animal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving livestock animal {AnimalId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the animal." });
        }
    }

    /// <summary>Update count, notes, or active status of an animal.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAnimal(Guid id, [FromBody] UpdateAnimalRequest request)
    {
        try
        {
            Guid? userId = GetUserId();
            if (userId == null) { return Unauthorized(); }

            LivestockAnimalDto? existing = await _livestock.GetAnimalByIdAsync(id);
            if (existing == null) { return NotFound(); }

            if (!await _inventory.IsUserMemberOfHouseholdAsync(existing.HouseholdId, userId.Value))
            {
                return Forbid();
            }

            await _livestock.UpdateAnimalAsync(
                id, request.Name, request.Count, request.IsActive, request.BreedNotes, request.Notes);

            LivestockAnimalDto? updated = await _livestock.GetAnimalByIdAsync(id);
            return Ok(updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating livestock animal {AnimalId}", id);
            return StatusCode(500, new { message = "An error occurred while updating the animal." });
        }
    }

    /// <summary>Soft-delete an animal (sets IsActive = false).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAnimal(Guid id)
    {
        try
        {
            Guid? userId = GetUserId();
            if (userId == null) { return Unauthorized(); }

            LivestockAnimalDto? existing = await _livestock.GetAnimalByIdAsync(id);
            if (existing == null) { return NotFound(); }

            if (!await _inventory.IsUserMemberOfHouseholdAsync(existing.HouseholdId, userId.Value))
            {
                return Forbid();
            }

            await _livestock.SoftDeleteAnimalAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting livestock animal {AnimalId}", id);
            return StatusCode(500, new { message = "An error occurred while deleting the animal." });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Production
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Log daily production (eggs, milk, honey, etc.).</summary>
    [HttpPost("{id:guid}/production")]
    public async Task<IActionResult> LogProduction(Guid id, [FromBody] LogProductionRequest request)
    {
        try
        {
            Guid? userId = GetUserId();
            if (userId == null) { return Unauthorized(); }

            LivestockAnimalDto? animal = await _livestock.GetAnimalByIdAsync(id);
            if (animal == null) { return NotFound(); }

            if (!await _inventory.IsUserMemberOfHouseholdAsync(animal.HouseholdId, userId.Value))
            {
                return Forbid();
            }

            Guid productionId = await _livestock.LogProductionAsync(
                id,
                request.ProductionDate,
                request.ProductType,
                request.Quantity,
                request.Unit,
                request.AddToInventory,
                request.StorageLocationId?.ToString(),
                request.Notes);

            _logger.LogInformation(
                "Logged production {ProductionId} for animal {AnimalId}", productionId, id);

            return Ok(new { id = productionId });
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("No storage location"))
        {
            return UnprocessableEntity(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging production for animal {AnimalId}", id);
            return StatusCode(500, new { message = "An error occurred while logging production." });
        }
    }

    /// <summary>Get production history for an animal within a date range.</summary>
    [HttpGet("{id:guid}/production")]
    public async Task<IActionResult> GetProduction(
        Guid id,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null)
    {
        try
        {
            Guid? userId = GetUserId();
            if (userId == null) { return Unauthorized(); }

            LivestockAnimalDto? animal = await _livestock.GetAnimalByIdAsync(id);
            if (animal == null) { return NotFound(); }

            if (!await _inventory.IsUserMemberOfHouseholdAsync(animal.HouseholdId, userId.Value))
            {
                return Forbid();
            }

            DateOnly effectiveFrom = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
            DateOnly effectiveTo   = to   ?? DateOnly.FromDateTime(DateTime.UtcNow);

            List<LivestockProductionDto> production =
                await _livestock.GetProductionAsync(id, effectiveFrom, effectiveTo);

            return Ok(production);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving production for animal {AnimalId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving production." });
        }
    }

    /// <summary>Aggregated production summary across all animals in the household.</summary>
    [HttpGet("production/summary")]
    public async Task<IActionResult> GetProductionSummary(
        [FromQuery] Guid householdId,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null)
    {
        try
        {
            Guid? userId = GetUserId();
            if (userId == null) { return Unauthorized(); }

            if (!await _inventory.IsUserMemberOfHouseholdAsync(householdId, userId.Value))
            {
                return Forbid();
            }

            DateOnly effectiveFrom = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
            DateOnly effectiveTo   = to   ?? DateOnly.FromDateTime(DateTime.UtcNow);

            List<LivestockProductionSummaryDto> summary =
                await _livestock.GetProductionSummaryAsync(householdId, effectiveFrom, effectiveTo);

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving production summary for household {HouseholdId}", householdId);
            return StatusCode(500, new { message = "An error occurred while retrieving the production summary." });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Harvest / Processing
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Record a slaughter or processing event.</summary>
    [HttpPost("{id:guid}/harvest")]
    public async Task<IActionResult> RecordHarvest(Guid id, [FromBody] RecordHarvestRequest request)
    {
        try
        {
            Guid? userId = GetUserId();
            if (userId == null) { return Unauthorized(); }

            LivestockAnimalDto? animal = await _livestock.GetAnimalByIdAsync(id);
            if (animal == null) { return NotFound(); }

            if (!await _inventory.IsUserMemberOfHouseholdAsync(animal.HouseholdId, userId.Value))
            {
                return Forbid();
            }

            Guid harvestId = await _livestock.RecordHarvestAsync(
                id,
                request.HarvestDate,
                request.CountHarvested,
                request.LiveWeightLbs,
                request.ProcessedWeightLbs,
                request.ProcessedBy,
                request.AddToInventory,
                request.YieldItems,
                request.StorageLocationId?.ToString(),
                request.Notes);

            return Ok(new { id = harvestId });
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("No storage location"))
        {
            return UnprocessableEntity(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording harvest for animal {AnimalId}", id);
            return StatusCode(500, new { message = "An error occurred while recording the harvest." });
        }
    }

    /// <summary>Get harvest history for an animal.</summary>
    [HttpGet("{id:guid}/harvests")]
    public async Task<IActionResult> GetHarvests(Guid id)
    {
        try
        {
            Guid? userId = GetUserId();
            if (userId == null) { return Unauthorized(); }

            LivestockAnimalDto? animal = await _livestock.GetAnimalByIdAsync(id);
            if (animal == null) { return NotFound(); }

            if (!await _inventory.IsUserMemberOfHouseholdAsync(animal.HouseholdId, userId.Value))
            {
                return Forbid();
            }

            List<LivestockHarvestDto> harvests = await _livestock.GetHarvestsAsync(id);
            return Ok(harvests);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving harvests for animal {AnimalId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving harvests." });
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Request DTOs
// ─────────────────────────────────────────────────────────────────────────────

public class AddAnimalRequest
{
    public Guid HouseholdId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AnimalType { get; set; } = string.Empty;
    public string ProductionCategory { get; set; } = string.Empty;
    public bool IsFlockOrHerd { get; set; }
    public int Count { get; set; } = 1;
    public DateOnly? AcquiredDate { get; set; }
    public string? BreedNotes { get; set; }
    public string? Notes { get; set; }
}

public class UpdateAnimalRequest
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public string? BreedNotes { get; set; }
    public string? Notes { get; set; }
}

public class LogProductionRequest
{
    public DateOnly ProductionDate { get; set; }
    public string ProductType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public bool AddToInventory { get; set; }
    public Guid? StorageLocationId { get; set; }
    public string? Notes { get; set; }
}

public class RecordHarvestRequest
{
    public DateOnly HarvestDate { get; set; }
    public int CountHarvested { get; set; } = 1;
    public decimal? LiveWeightLbs { get; set; }
    public decimal? ProcessedWeightLbs { get; set; }
    public string? ProcessedBy { get; set; }
    public bool AddToInventory { get; set; }
    public List<HarvestYieldItem> YieldItems { get; set; } = new();
    public Guid? StorageLocationId { get; set; }
    public string? Notes { get; set; }
}
