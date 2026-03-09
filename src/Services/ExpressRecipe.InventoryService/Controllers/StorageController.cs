using ExpressRecipe.InventoryService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpressRecipe.InventoryService.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class StorageController : ControllerBase
{
    private readonly IStorageLocationExtendedRepository _repository;
    private readonly ILogger<StorageController> _logger;

    public StorageController(
        IStorageLocationExtendedRepository repository,
        ILogger<StorageController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>GET /api/storage/suggest?foodCategory=Meat — suggest storage locations</summary>
    [HttpGet("suggest")]
    public async Task<IActionResult> SuggestLocations([FromQuery] string foodCategory, CancellationToken ct)
    {
        try
        {
            Guid? householdId = GetHouseholdId();
            if (householdId is null)
            {
                return BadRequest("HouseholdId claim required");
            }

            string storageType = MapFoodCategoryToStorageType(foodCategory);

            List<StorageLocationSuggestionDto> suggestions =
                await _repository.SuggestLocationsAsync(householdId.Value, storageType, ct);
            return Ok(suggestions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error suggesting storage locations for foodCategory {FoodCategory}", foodCategory);
            return StatusCode(500, "Failed to suggest storage locations");
        }
    }

    /// <summary>GET /api/storage/outages — get active outages for dashboard banner</summary>
    [HttpGet("outages")]
    public async Task<IActionResult> GetOutages(CancellationToken ct)
    {
        try
        {
            Guid? householdId = GetHouseholdId();
            if (householdId is null)
            {
                return BadRequest("HouseholdId claim required");
            }

            List<StorageLocationDto> outages =
                await _repository.GetLocationsWithOutageAsync(householdId.Value, ct);
            return Ok(outages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active outages");
            return StatusCode(500, "Failed to retrieve active outages");
        }
    }

    /// <summary>POST /api/storage/{locationId}/outage — mark an outage</summary>
    [HttpPost("{locationId:guid}/outage")]
    public async Task<IActionResult> MarkOutage(Guid locationId, [FromBody] MarkOutageRequest request, CancellationToken ct)
    {
        try
        {
            await _repository.MarkOutageAsync(locationId, request.OutageType, request.Notes, ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking outage for location {LocationId}", locationId);
            return StatusCode(500, "Failed to mark outage");
        }
    }

    /// <summary>DELETE /api/storage/{locationId}/outage — clear an outage</summary>
    [HttpDelete("{locationId:guid}/outage")]
    public async Task<IActionResult> ClearOutage(Guid locationId, CancellationToken ct)
    {
        try
        {
            await _repository.ClearOutageAsync(locationId, ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing outage for location {LocationId}", locationId);
            return StatusCode(500, "Failed to clear outage");
        }
    }

    private Guid? GetHouseholdId()
    {
        string? claim = User.FindFirst("household_id")?.Value
            ?? User.FindFirst("HouseholdId")?.Value;
        return Guid.TryParse(claim, out Guid id) ? id : null;
    }

    private static string MapFoodCategoryToStorageType(string foodCategory)
    {
        return foodCategory switch
        {
            "Frozen"    => "Freezer",
            "Meat"      => "Refrigerator",
            "Poultry"   => "Refrigerator",
            "Seafood"   => "Refrigerator",
            "Dairy"     => "Refrigerator",
            "Produce"   => "Refrigerator",
            "Eggs"      => "Refrigerator",
            "Canned"    => "Pantry",
            "DryGoods"  => "Pantry",
            "Beverages" => "Pantry",
            "Spices"    => "Pantry",
            _           => "Pantry"
        };
    }
}

public sealed class MarkOutageRequest
{
    public string OutageType { get; set; } = string.Empty; // PowerOutage|EquipmentFailure|MaintenanceDown
    public string? Notes { get; set; }
}
