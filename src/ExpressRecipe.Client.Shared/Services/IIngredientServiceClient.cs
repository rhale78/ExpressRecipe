using ExpressRecipe.Shared.DTOs.Product;

namespace ExpressRecipe.Client.Shared.Services;

/// <summary>
/// Interface for high-performance ingredient microservice communication.
/// </summary>
public interface IIngredientServiceClient
{
    Task<Dictionary<string, Guid>> LookupIngredientIdsAsync(List<string> names);
    Task<Guid?> GetIngredientIdByNameAsync(string name);
    Task<IngredientDto?> GetIngredientAsync(Guid id);
    Task<List<IngredientDto>> GetAllIngredientsAsync();
    Task<Guid?> CreateIngredientAsync(CreateIngredientRequest request);
    Task<int> BulkCreateIngredientsAsync(List<string> names);
}
