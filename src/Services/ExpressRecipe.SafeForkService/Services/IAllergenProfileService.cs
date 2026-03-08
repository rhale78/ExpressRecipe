using ExpressRecipe.SafeForkService.Contracts.Requests;
using ExpressRecipe.SafeForkService.Contracts.Responses;

namespace ExpressRecipe.SafeForkService.Services;

public interface IAllergenProfileService
{
    Task<AllergenProfileDto?> GetEffectiveProfileAsync(Guid memberId, bool includeSchedules, CancellationToken ct);
    Task<UnionProfileDto> ComputeUnionProfileAsync(IReadOnlyList<Guid> memberIds, CancellationToken ct);
    Task<RecipeEvaluationResult> EvaluateRecipeAsync(IReadOnlyList<RecipeIngredientDto> ingredients, UnionProfileDto profile, CancellationToken ct);
    Task<string> ResolveAdaptationStrategyAsync(ConflictReport report, Guid householdId, Guid? recipeInstanceId, CancellationToken ct);
    Task<List<SubstituteDto>> GetSubstitutesAsync(RecipeIngredientDto ingredient, Guid allergenId, RecipeContextDto context, CancellationToken ct);
    Task<Guid> AddFreeformAllergenAsync(Guid memberId, string freeFormText, string? brand, CancellationToken ct);
    Task<Guid> AddTemporaryScheduleAsync(Guid memberId, string scheduleType, DateTimeOffset start, DateTimeOffset end, string? configJson, CancellationToken ct);
    Task<List<TemporaryScheduleDto>> GetActiveSchedulesAsync(Guid memberId, CancellationToken ct);
    Task<List<AllergenProfileEntryDto>> GetHouseholdHardExcludesAsync(Guid householdId, CancellationToken ct);
}
