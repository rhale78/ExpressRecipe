using ExpressRecipe.PreferencesService.Contracts.Requests;
using ExpressRecipe.PreferencesService.Contracts.Responses;

namespace ExpressRecipe.PreferencesService.Data;

public interface ICookProfileRepository
{
    Task<CookProfileDto?> GetByMemberIdAsync(Guid memberId, CancellationToken ct = default);
    Task<Guid> UpsertAsync(Guid memberId, UpsertCookProfileRequest request, CancellationToken ct = default);
    Task<TechniqueComfortDto?> GetTechniqueComfortAsync(Guid memberId, string techniqueCode, CancellationToken ct = default);
    Task UpsertTechniqueComfortAsync(Guid memberId, string techniqueCode, SetTechniqueComfortRequest request, CancellationToken ct = default);
    Task<List<DismissedTipDto>> GetDismissedTipsAsync(Guid memberId, CancellationToken ct = default);
    Task DismissTipAsync(Guid memberId, Guid tipId, CancellationToken ct = default);
    Task RestoreTipAsync(Guid memberId, Guid tipId, CancellationToken ct = default);
    Task<List<TechniqueComfortDto>> GetAllTechniqueComfortsAsync(Guid memberId, CancellationToken ct = default);
    Task InitializeCookProfileAsync(Guid memberId, CancellationToken ct = default);
    Task SoftDeleteCookProfileAsync(Guid memberId, CancellationToken ct = default);

    // GDPR
    Task DeleteMemberDataAsync(Guid memberId, CancellationToken ct = default);
}
