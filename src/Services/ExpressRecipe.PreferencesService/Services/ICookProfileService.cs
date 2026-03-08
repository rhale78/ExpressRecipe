using ExpressRecipe.PreferencesService.Contracts.Requests;
using ExpressRecipe.PreferencesService.Contracts.Responses;

namespace ExpressRecipe.PreferencesService.Services;

public interface ICookProfileService
{
    Task<CookProfileDto?> GetCookProfileAsync(Guid memberId, CancellationToken ct);
    Task<Guid> UpsertCookProfileAsync(Guid memberId, UpsertCookProfileRequest request, CancellationToken ct);
    Task<TechniqueComfortDto?> GetTechniqueComfortAsync(Guid memberId, string techniqueCode, CancellationToken ct);
    Task SetTechniqueComfortAsync(Guid memberId, string techniqueCode, SetTechniqueComfortRequest request, CancellationToken ct);
    Task<List<DismissedTipDto>> GetDismissedTipsAsync(Guid memberId, CancellationToken ct);
    Task DismissTipAsync(Guid memberId, Guid tipId, CancellationToken ct);
    Task RestoreTipAsync(Guid memberId, Guid tipId, CancellationToken ct);
    Task<List<CookingTipDto>> GetTipsForMemberAsync(Guid memberId, string techniqueCode, CancellationToken ct);
}
