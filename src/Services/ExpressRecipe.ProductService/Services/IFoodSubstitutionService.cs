using ExpressRecipe.Shared.DTOs.Product;
using System.Text.Json;

namespace ExpressRecipe.ProductService.Services;

public interface IFoodSubstitutionService
{
    Task<List<SubstituteOption>> GetSubstitutesAsync(
        Guid ingredientId,
        Guid userId,
        Guid? householdId,
        bool filterByAllergens,
        CancellationToken ct = default);
}
