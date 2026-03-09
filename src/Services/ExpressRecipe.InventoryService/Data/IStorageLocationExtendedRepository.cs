namespace ExpressRecipe.InventoryService.Data;

public sealed record StorageLocationSuggestionDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string StorageType { get; init; } = string.Empty;
    public bool OutageActive { get; init; }
    public int Score { get; init; }
    public int ItemCount { get; init; }
}

public interface IStorageLocationExtendedRepository
{
    Task<List<StorageLocationSuggestionDto>> SuggestLocationsAsync(
        Guid householdId, string storageType, CancellationToken ct = default);

    Task<List<StorageLocationDto>> GetLocationsWithOutageAsync(
        Guid householdId, CancellationToken ct = default);

    Task MarkOutageAsync(Guid locationId, string outageType, string? notes, CancellationToken ct = default);

    Task ClearOutageAsync(Guid locationId, CancellationToken ct = default);
}
