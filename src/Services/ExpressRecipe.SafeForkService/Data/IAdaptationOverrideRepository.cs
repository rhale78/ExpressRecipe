using ExpressRecipe.SafeForkService.Models;

namespace ExpressRecipe.SafeForkService.Data;

public interface IAdaptationOverrideRepository
{
    Task<List<AdaptationOverrideEntry>> GetAsync(Guid householdId, Guid? recipeInstanceId, Guid? memberId, CancellationToken ct = default);
    Task<Guid> AddAsync(Guid householdId, Guid? recipeInstanceId, Guid? memberId, string strategyCode, Guid? createdBy, CancellationToken ct = default);
}
