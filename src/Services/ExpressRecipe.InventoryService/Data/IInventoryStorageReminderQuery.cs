namespace ExpressRecipe.InventoryService.Data;

public interface IInventoryStorageReminderQuery
{
    Task<List<FreezerBurnRiskItem>> GetFreezerBurnRiskItemsAsync(CancellationToken ct = default);
    Task<List<OutageStorageLocation>> GetActiveOutagesAsync(CancellationToken ct = default);
    Task<int> GetItemCountInStorageAsync(Guid locationId, CancellationToken ct = default);
    Task MarkOutageWarningSentAsync(Guid locationId, CancellationToken ct = default);
    Task<List<PerishableInventoryItem>> GetPerishableItemsForRecipeAsync(
        Guid householdId, Guid recipeId, CancellationToken ct = default);
}

public sealed record FreezerBurnRiskItem
{
    public Guid HouseholdId { get; init; }
    public string ItemName { get; init; } = string.Empty;
    public string LocationName { get; init; } = string.Empty;
    public int DaysInFreezer { get; init; }
}

public sealed record OutageStorageLocation
{
    public Guid LocationId { get; init; }
    public Guid HouseholdId { get; init; }
    public string LocationName { get; init; } = string.Empty;
    public string StorageType { get; init; } = string.Empty;
    public string OutageType { get; init; } = string.Empty;
    public DateTime OutageStartedAt { get; init; }
    public bool WarningSent { get; init; }
}

public sealed record PerishableInventoryItem
{
    public Guid Id { get; init; }
    public string ItemName { get; init; } = string.Empty;
    public string? StorageType { get; init; }
    public string? FoodCategory { get; init; }
}
