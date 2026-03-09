using System.Text.Json;

namespace ExpressRecipe.InventoryService.Data;

public interface ILivestockRepository
{
    // Animals / Flocks
    Task<List<LivestockAnimalDto>> GetAnimalsAsync(Guid householdId, bool activeOnly = true);
    Task<LivestockAnimalDto?> GetAnimalByIdAsync(Guid animalId);
    Task<Guid> AddAnimalAsync(Guid householdId, string name, string animalType, string productionCategory,
        bool isFlockOrHerd, int count, DateOnly? acquiredDate, string? breedNotes, string? notes);
    Task UpdateAnimalAsync(Guid animalId, string name, int count, bool isActive, string? breedNotes, string? notes);
    Task SoftDeleteAnimalAsync(Guid animalId);

    // Production Logging
    Task<Guid> LogProductionAsync(Guid animalId, DateOnly productionDate, string productType,
        decimal quantity, string unit, bool addToInventory, string? storageLocationId, string? notes);
    Task<List<LivestockProductionDto>> GetProductionAsync(Guid animalId, DateOnly from, DateOnly to);
    Task<List<LivestockProductionSummaryDto>> GetProductionSummaryAsync(Guid householdId, DateOnly from, DateOnly to);

    // Harvest / Processing
    Task<Guid> RecordHarvestAsync(Guid animalId, DateOnly harvestDate, int countHarvested,
        decimal? liveWeightLbs, decimal? processedWeightLbs, string? processedBy,
        bool addToInventory, List<HarvestYieldItem> yieldItems, string? storageLocationId, string? notes);
    Task<List<LivestockHarvestDto>> GetHarvestsAsync(Guid animalId);
    Task LinkHarvestToInventoryAsync(Guid harvestId, string yieldItemsJson);
}

public interface IInventorySaleRepository
{
    Task<Guid> RecordSaleAsync(Guid householdId, Guid? inventoryItemId, string productName,
        decimal quantity, string unit, DateOnly saleDate, string? buyer, string? notes,
        bool autoRemoveOnZero = true);
    Task<List<InventorySaleDto>> GetSalesAsync(Guid householdId, DateOnly from, DateOnly to);
    Task<List<InventorySaleDto>> GetSalesByItemAsync(Guid inventoryItemId);
}

// ─────────────────────────────────────────────────────────────────────────────
// DTOs
// ─────────────────────────────────────────────────────────────────────────────

public sealed record LivestockAnimalDto
{
    public Guid Id { get; init; }
    public Guid HouseholdId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string AnimalType { get; init; } = string.Empty;
    public string ProductionCategory { get; init; } = string.Empty;
    public bool IsFlockOrHerd { get; init; }
    public int Count { get; init; }
    public DateOnly? AcquiredDate { get; init; }
    public string? BreedNotes { get; init; }
    public bool IsActive { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed record LivestockProductionDto
{
    public Guid Id { get; init; }
    public Guid AnimalId { get; init; }
    public string AnimalName { get; init; } = string.Empty;
    public DateOnly ProductionDate { get; init; }
    public string ProductType { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string Unit { get; init; } = string.Empty;
    public bool AddedToInventory { get; init; }
    public Guid? InventoryItemId { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record LivestockProductionSummaryDto
{
    public string ProductType { get; init; } = string.Empty;
    public decimal TotalQuantity { get; init; }
    public string Unit { get; init; } = string.Empty;
    public int DaysRecorded { get; init; }
    public decimal DailyAverage { get; init; }
}

public sealed record LivestockHarvestDto
{
    public Guid Id { get; init; }
    public Guid AnimalId { get; init; }
    public string AnimalName { get; init; } = string.Empty;
    public DateOnly HarvestDate { get; init; }
    public int CountHarvested { get; init; }
    public decimal? LiveWeightLbs { get; init; }
    public decimal? ProcessedWeightLbs { get; init; }
    public List<HarvestYieldItem> YieldItems { get; init; } = new();
    public bool AddedToInventory { get; init; }
    public string? ProcessedBy { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record HarvestYieldItem
{
    public string Cut { get; init; } = string.Empty;
    public decimal WeightLbs { get; init; }
    public string Unit { get; init; } = "lb";
    public Guid? InventoryItemId { get; init; }
}

public sealed record InventorySaleDto
{
    public Guid Id { get; init; }
    public Guid HouseholdId { get; init; }
    public Guid? InventoryItemId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string Unit { get; init; } = string.Empty;
    public DateOnly SaleDate { get; init; }
    public string? Buyer { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
}
