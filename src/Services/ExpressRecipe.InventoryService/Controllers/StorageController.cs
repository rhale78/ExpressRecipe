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
    private readonly IInventoryRepository _inventoryRepository;

    public StorageController(
        IStorageLocationExtendedRepository repository,
        IInventoryRepository inventoryRepository)
    {
        _repository          = repository;
        _inventoryRepository = inventoryRepository;
    }

    /// <summary>GET /api/storage — list storage locations for the authenticated household</summary>
    [HttpGet]
    public async Task<IActionResult> GetLocations([FromQuery] Guid? addressId, CancellationToken ct)
    {
        Guid? householdId = GetHouseholdId();
        if (householdId is null)
            return Unauthorized();

        try
        {
            List<StorageLocationExtendedDto> locations =
                await _repository.GetLocationsAsync(householdId.Value, addressId, ct);
            return Ok(locations);
        }
        catch (Exception ex)
        {
            // log error
            return StatusCode(500, "Failed to retrieve storage locations");
        }
    }

    /// <summary>GET /api/storage/suggest?foodCategory=Meat — suggest storage locations</summary>
    [HttpGet("suggest")]
    public async Task<IActionResult> Suggest([FromQuery] string foodCategory, CancellationToken ct)
    {
        Guid? householdId = GetHouseholdId();
        if (householdId is null)
            return Unauthorized();

        try
        {
            List<StorageLocationSuggestionDto> suggestions =
                await _repository.SuggestLocationsAsync(householdId.Value, foodCategory, ct);
            return Ok(suggestions);
        }
        catch (Exception ex)
        {
            // log error
            return StatusCode(500, "Failed to suggest storage locations");
        }
    }

    /// <summary>GET /api/storage/outages — get active outages for dashboard banner</summary>
    [HttpGet("outages")]
    public async Task<IActionResult> GetOutages(CancellationToken ct)
    {
        Guid? householdId = GetHouseholdId();
        if (householdId is null)
            return Unauthorized();

        try
        {
            List<StorageLocationDto> outages =
                await _repository.GetLocationsWithOutageAsync(householdId.Value, ct);
            return Ok(outages);
        }
        catch (Exception ex)
        {
            // log error
            return StatusCode(500, "Failed to retrieve active outages");
        }
    }

    /// <summary>PUT /api/storage/{locationId}/storage-type — update the storage type and linked equipment</summary>
    [HttpPut("{locationId:guid}/storage-type")]
    public async Task<IActionResult> SetStorageType(Guid locationId, [FromBody] SetStorageTypeRequest request, CancellationToken ct)
    {
        try
        {
            StorageLocationExtendedDto? location = await _repository.GetLocationByIdAsync(locationId, ct);
            if (location is null) return NotFound();

            Guid? householdId = GetHouseholdId();
            if (householdId is null || location.HouseholdId != householdId.Value) return Forbid();

            await _repository.UpdateStorageTypeAsync(locationId, request.StorageType ?? string.Empty, request.EquipmentInstanceId, ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            // log error
            return StatusCode(500, "Failed to update storage type");
        }
    }

    /// <summary>PUT /api/storage/{locationId}/food-categories — set food categories for a storage location</summary>
    [HttpPut("{locationId:guid}/food-categories")]
    public async Task<IActionResult> SetFoodCategories(Guid locationId, [FromBody] SetFoodCategoriesRequest request, CancellationToken ct)
    {
        try
        {
            StorageLocationExtendedDto? location = await _repository.GetLocationByIdAsync(locationId, ct);
            if (location is null) return NotFound();

            Guid? householdId = GetHouseholdId();
            if (householdId is null || location.HouseholdId != householdId.Value) return Forbid();

            await _repository.SetFoodCategoriesAsync(locationId, request.Categories ?? new List<string>(), ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            // log error
            return StatusCode(500, "Failed to set food categories");
        }
    }

    /// <summary>POST /api/storage/{locationId}/outage — mark an outage</summary>
    [HttpPost("{locationId:guid}/outage")]
    public async Task<IActionResult> SetOutage(Guid locationId, [FromBody] SetOutageRequest request, CancellationToken ct)
    {
        try
        {
            StorageLocationExtendedDto? location = await _repository.GetLocationByIdAsync(locationId, ct);
            if (location is null) return NotFound();

            Guid? householdId = GetHouseholdId();
            if (householdId is null || location.HouseholdId != householdId.Value) return Forbid();

            await _repository.SetOutageAsync(locationId, request.OutageType, request.Notes, ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            // log error
            return StatusCode(500, "Failed to mark outage");
        }
    }

    /// <summary>DELETE /api/storage/{locationId}/outage — clear an outage</summary>
    [HttpDelete("{locationId:guid}/outage")]
    public async Task<IActionResult> ClearOutage(Guid locationId, CancellationToken ct)
    {
        try
        {
            StorageLocationExtendedDto? location = await _repository.GetLocationByIdAsync(locationId, ct);
            if (location is null) return NotFound();

            Guid? householdId = GetHouseholdId();
            if (householdId is null || location.HouseholdId != householdId.Value) return Forbid();

            await _repository.ClearOutageAsync(locationId, ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            // log error
            return StatusCode(500, "Failed to clear outage");
        }
    }

    /// <summary>GET /api/storage/{locationId}/items — get inventory items in a storage location</summary>
    [HttpGet("{locationId:guid}/items")]
    public async Task<IActionResult> GetItemsInStorage(Guid locationId, CancellationToken ct)
    {
        try
        {
            List<InventoryItemDto> items =
                await _inventoryRepository.GetInventoryByStorageLocationAsync(locationId);
            return Ok(items);
        }
        catch (Exception ex)
        {
            // log error
            return StatusCode(500, "Failed to retrieve storage items");
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

public sealed class SetOutageRequest
{
    public string OutageType { get; set; } = string.Empty; // PowerOutage|EquipmentFailure|MaintenanceDown
    public string? Notes { get; set; }
}

public sealed class SetStorageTypeRequest
{
    public string? StorageType { get; set; }
    public Guid? EquipmentInstanceId { get; set; }
}

public sealed class SetFoodCategoriesRequest
{
    public List<string>? Categories { get; set; }
}
