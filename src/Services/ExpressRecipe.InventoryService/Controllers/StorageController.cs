using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ExpressRecipe.InventoryService.Data;

namespace ExpressRecipe.InventoryService.Controllers;

[Authorize]
[ApiController]
[Route("api/storage")]
public sealed class StorageController : ControllerBase
{
    private readonly IStorageLocationExtendedRepository _storage;
    private readonly IInventoryRepository _inventoryRepo;

    public StorageController(IStorageLocationExtendedRepository storage, IInventoryRepository inventoryRepo)
    { _storage = storage; _inventoryRepo = inventoryRepo; }

    private Guid? GetHouseholdId()
    {
        string? claim = User.FindFirstValue("household_id");
        if (claim is null || !Guid.TryParse(claim, out Guid id)) { return null; }
        return id;
    }

    [HttpGet]
    public async Task<IActionResult> GetLocations(
        [FromQuery] Guid? addressId, CancellationToken ct = default)
    {
        Guid? householdId = GetHouseholdId();
        if (householdId is null) { return Unauthorized(); }
        return Ok(await _storage.GetLocationsAsync(householdId.Value, addressId, ct));
    }

    [HttpPut("{id}/type")]
    public async Task<IActionResult> SetStorageType(Guid id,
        [FromBody] SetStorageTypeRequest req, CancellationToken ct)
    {
        Guid? householdId = GetHouseholdId();
        if (householdId is null) { return Unauthorized(); }
        StorageLocationExtendedDto? location = await _storage.GetLocationByIdAsync(id, ct);
        if (location is null) { return NotFound(); }
        if (location.HouseholdId != householdId.Value) { return Forbid(); }
        await _storage.UpdateStorageTypeAsync(id, req.StorageType, req.EquipmentInstanceId, ct);
        return NoContent();
    }

    [HttpPost("{id}/categories")]
    public async Task<IActionResult> SetFoodCategories(Guid id,
        [FromBody] SetFoodCategoriesRequest req, CancellationToken ct)
    {
        Guid? householdId = GetHouseholdId();
        if (householdId is null) { return Unauthorized(); }
        StorageLocationExtendedDto? location = await _storage.GetLocationByIdAsync(id, ct);
        if (location is null) { return NotFound(); }
        if (location.HouseholdId != householdId.Value) { return Forbid(); }
        await _storage.SetFoodCategoriesAsync(id, req.Categories, ct);
        return NoContent();
    }

    [HttpGet("suggest")]
    public async Task<IActionResult> Suggest(
        [FromQuery] string foodCategory, CancellationToken ct = default)
    {
        Guid? householdId = GetHouseholdId();
        if (householdId is null) { return Unauthorized(); }
        return Ok(await _storage.SuggestLocationsAsync(householdId.Value, foodCategory, ct));
    }

    [HttpPost("{id}/outage")]
    public async Task<IActionResult> SetOutage(Guid id,
        [FromBody] SetOutageRequest req, CancellationToken ct)
    {
        Guid? householdId = GetHouseholdId();
        if (householdId is null) { return Unauthorized(); }
        StorageLocationExtendedDto? location = await _storage.GetLocationByIdAsync(id, ct);
        if (location is null) { return NotFound(); }
        if (location.HouseholdId != householdId.Value) { return Forbid(); }
        await _storage.SetOutageAsync(id, req.OutageType, req.Notes, ct);
        // Trigger immediate item safety check via notification
        // (StorageReminderWorker handles this on next cycle, or fire-and-forget here)
        return NoContent();
    }

    [HttpDelete("{id}/outage")]
    public async Task<IActionResult> ClearOutage(Guid id, CancellationToken ct)
    {
        Guid? householdId = GetHouseholdId();
        if (householdId is null) { return Unauthorized(); }
        StorageLocationExtendedDto? location = await _storage.GetLocationByIdAsync(id, ct);
        if (location is null) { return NotFound(); }
        if (location.HouseholdId != householdId.Value) { return Forbid(); }
        await _storage.ClearOutageAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id}/items")]
    public async Task<IActionResult> GetItemsInStorage(Guid id, CancellationToken ct)
    {
        // Delegate to existing InventoryRepository — filter by StorageLocationId
        return Ok(await _inventoryRepo.GetInventoryByStorageLocationAsync(id));
    }
}

public sealed class SetStorageTypeRequest
{
    public string? StorageType { get; set; }
    public Guid? EquipmentInstanceId { get; set; }
}

public sealed class SetFoodCategoriesRequest
{
    public List<string> Categories { get; set; } = new();
}

public sealed class SetOutageRequest
{
    public string OutageType { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
