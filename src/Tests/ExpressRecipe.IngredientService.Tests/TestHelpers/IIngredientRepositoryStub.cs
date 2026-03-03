using ExpressRecipe.Shared.DTOs.Product;

namespace ExpressRecipe.IngredientService.Data;

/// <summary>
/// Minimal interface stub for IIngredientRepository used by IngredientParser.
/// The concrete implementation lives in ExpressRecipe.IngredientService (which has gRPC build targets
/// and is not referenced here). This interface-only stub allows the parser to be compiled and tested.
/// </summary>
public interface IIngredientRepository
{
    Task<IngredientDto?> GetIngredientByIdAsync(Guid id);
    Task<IngredientDto?> GetIngredientByNameAsync(string name);
    Task<List<IngredientDto>> GetAllIngredientsAsync(int limit = 100, int offset = 0);
    Task<Guid> CreateIngredientAsync(CreateIngredientRequest request, Guid? createdBy = null);
    Task<bool> UpdateIngredientAsync(Guid id, UpdateIngredientRequest request, Guid? updatedBy = null);
    Task<bool> DeleteIngredientAsync(Guid id, Guid? deletedBy = null);
    Task<Dictionary<string, Guid>> GetIngredientIdsByNamesAsync(IEnumerable<string> names);
    Task<int> BulkCreateIngredientsAsync(IEnumerable<string> names, Guid? createdBy = null);
}
