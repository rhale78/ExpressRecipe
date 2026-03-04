using ExpressRecipe.GroceryStoreLocationService.Data;

namespace ExpressRecipe.GroceryStoreLocationService.Services;

public interface IOpenPricesLocationImportService
{
    Task<(List<UpsertGroceryStoreRequest> Stores, string? ErrorMessage)> FetchStoresAsync(string? pathOrUrl, CancellationToken cancellationToken = default);
    Task<(List<UpsertGroceryStoreRequest> Stores, string? ErrorMessage)> FetchStoresFromFileAsync(string filePath, CancellationToken cancellationToken = default);
    Task<(List<UpsertGroceryStoreRequest> Stores, string? ErrorMessage)> FetchStoresFromUrlAsync(string jsonlUrl, CancellationToken cancellationToken = default);
    bool FileExists(string filePath);
}
