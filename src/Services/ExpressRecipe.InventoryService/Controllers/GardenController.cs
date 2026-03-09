using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ExpressRecipe.InventoryService.Data;
using ExpressRecipe.MealPlanningService.Services;

namespace ExpressRecipe.InventoryService.Controllers;

[Authorize]
[ApiController]
[Route("api/garden")]
public sealed class GardenController : ControllerBase
{
    private readonly IGardenRepository _garden;
    private readonly IInventoryRepository _inventory;
    private readonly ISeasonalProduceService _seasonal;

    public GardenController(IGardenRepository garden, IInventoryRepository inventory, ISeasonalProduceService seasonal)
    {
        _garden    = garden;
        _inventory = inventory;
        _seasonal  = seasonal;
    }

    private Guid GetHouseholdId()
        => Guid.Parse(User.FindFirstValue("household_id") ?? Guid.Empty.ToString());

    private Guid? GetUserId()
    {
        string? claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out Guid id) ? id : null;
    }

    [HttpGet]
    public async Task<IActionResult> GetPlantings(CancellationToken ct)
        => Ok(await _garden.GetPlantingsAsync(GetHouseholdId(), ct));

    [HttpPost]
    public async Task<IActionResult> AddPlanting([FromBody] AddPlantingRequest request, CancellationToken ct)
    {
        DateOnly? expectedRipe = request.ExpectedRipeDate;
        if (!expectedRipe.HasValue && PlantTypeDaysToHarvest.TryGetValue(request.PlantType, out int days))
        {
            expectedRipe = request.PlantedDate.AddDays(days);
        }
        Guid id = await _garden.AddPlantingAsync(GetHouseholdId(), request.PlantName, request.VarietyNotes,
            request.PlantType, request.PlantedDate, expectedRipe, request.QuantityPlanted, ct);
        return Ok(new { id });
    }

    [HttpPut("{plantingId}")]
    public async Task<IActionResult> UpdatePlanting(Guid plantingId, [FromBody] UpdatePlantingRequest request, CancellationToken ct)
    {
        await _garden.UpdatePlantingAsync(plantingId, request.ExpectedRipeDate, request.IsActive, request.RipeCheckReminderEnabled, ct);
        return NoContent();
    }

    [HttpGet("{plantingId}/harvests")]
    public async Task<IActionResult> GetHarvests(Guid plantingId, CancellationToken ct)
        => Ok(await _garden.GetHarvestsAsync(plantingId, ct));

    [HttpPost("{plantingId}/harvest")]
    public async Task<IActionResult> RecordHarvest(Guid plantingId, [FromBody] RecordHarvestRequest request, CancellationToken ct)
    {
        Guid harvestId = await _garden.RecordHarvestAsync(plantingId, request.Quantity, request.Unit, request.Notes, ct);
        Guid? inventoryItemId = null;
        if (request.AddToInventory)
        {
            Guid? userId = GetUserId();
            if (!userId.HasValue) { return Unauthorized(); }
            inventoryItemId = await _inventory.CreateFromGardenHarvestAsync(userId.Value, GetHouseholdId(),
                request.PlantName, request.Quantity, request.Unit,
                SeasonalProduceService.GetFreshnessDays(request.PlantName), ct);
            await _garden.LinkHarvestToInventoryAsync(harvestId, inventoryItemId.Value, ct);
        }
        return Ok(new { harvestId, inventoryItemId });
    }

    [HttpGet("seasonal")]
    public IActionResult GetSeasonalProduce([FromQuery] string region, [FromQuery] int? month)
    {
        DateOnly date = month.HasValue
            ? new DateOnly(DateTime.Today.Year, month.Value, 1)
            : DateOnly.FromDateTime(DateTime.Today);
        return Ok(_seasonal.GetInSeasonProduce(region, date));
    }

    private static readonly Dictionary<string, int> PlantTypeDaysToHarvest = new(StringComparer.OrdinalIgnoreCase)
    {
        {"Tomato",70}, {"Pepper",80}, {"Zucchini",57}, {"Cucumber",60}, {"Lettuce",37},
        {"Kale",65}, {"Basil",37}, {"Cilantro",24}, {"Carrot",75}, {"Beet",62},
        {"Bean",55}, {"Squash (Winter)",105}, {"Pumpkin",105}, {"Corn",80}, {"Eggplant",72},
    };
}

public class AddPlantingRequest
{
    public string PlantName { get; set; } = string.Empty;
    public string? VarietyNotes { get; set; }
    public string PlantType { get; set; } = string.Empty;
    public DateOnly PlantedDate { get; set; }
    public DateOnly? ExpectedRipeDate { get; set; }
    public int QuantityPlanted { get; set; } = 1;
}

public class UpdatePlantingRequest
{
    public DateOnly? ExpectedRipeDate { get; set; }
    public bool IsActive { get; set; } = true;
    public bool RipeCheckReminderEnabled { get; set; } = true;
}

public class RecordHarvestRequest
{
    public string PlantName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool AddToInventory { get; set; }
}
