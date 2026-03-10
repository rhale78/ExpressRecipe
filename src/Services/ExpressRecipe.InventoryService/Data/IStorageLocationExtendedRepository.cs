namespace ExpressRecipe.InventoryService.Data;

public sealed record StorageLocationExtendedDto
{
    public Guid Id { get; init; }
    public Guid HouseholdId { get; init; }
    public Guid? AddressId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? StorageType { get; init; }
    public bool OutageActive { get; init; }
    public List<string> FoodCategories { get; init; } = new();
}

public sealed record StorageLocationSuggestionDto
{
    public Guid StorageLocationId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string StorageType { get; init; } = string.Empty;
    public bool OutageActive { get; init; }
    public int MatchScore { get; init; }
    public int ItemCount { get; init; }
}

public interface IStorageLocationExtendedRepository
{
    Task<List<StorageLocationExtendedDto>> GetLocationsAsync(
        Guid householdId, Guid? addressId, CancellationToken ct = default);

    Task<StorageLocationExtendedDto?> GetLocationByIdAsync(
        Guid locationId, CancellationToken ct = default);

    Task<List<StorageLocationSuggestionDto>> SuggestLocationsAsync(
        Guid householdId, string storageType, CancellationToken ct = default);

    Task<List<StorageLocationDto>> GetLocationsWithOutageAsync(
        Guid householdId, CancellationToken ct = default);

    Task SetOutageAsync(Guid locationId, string outageType, string? notes, CancellationToken ct = default);

    Task ClearOutageAsync(Guid locationId, CancellationToken ct = default);

    Task UpdateStorageTypeAsync(Guid locationId, string storageType, Guid? equipmentInstanceId, CancellationToken ct = default);

    Task SetFoodCategoriesAsync(Guid locationId, List<string> categories, CancellationToken ct = default);
}
