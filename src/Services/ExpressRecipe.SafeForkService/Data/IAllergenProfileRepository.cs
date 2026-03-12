using ExpressRecipe.SafeForkService.Contracts.Requests;
using ExpressRecipe.SafeForkService.Contracts.Responses;

namespace ExpressRecipe.SafeForkService.Data;

public interface IAllergenProfileRepository
{
    Task<List<AllergenProfileEntryDto>> GetByMemberIdAsync(Guid memberId, CancellationToken ct = default);
    Task<Guid> AddCuratedEntryAsync(Guid memberId, AddCuratedAllergenRequest request, Guid? createdBy = null, CancellationToken ct = default);
    Task<Guid> AddFreeformEntryAsync(Guid memberId, string freeFormText, string? brand, Guid? createdBy = null, CancellationToken ct = default);
    Task<bool> SoftDeleteEntryAsync(Guid entryId, CancellationToken ct = default);
    Task<bool> SoftDeleteEntryForMemberAsync(Guid memberId, Guid entryId, CancellationToken ct = default);
    Task<bool> SetHouseholdExcludeAsync(Guid entryId, bool value, CancellationToken ct = default);
    Task<bool> SetUnresolvedAsync(Guid entryId, bool isUnresolved, CancellationToken ct = default);
    Task AddLinkAsync(Guid allergenProfileId, string linkType, Guid linkedId, string matchMethod, decimal confidenceScore = 1.000m, CancellationToken ct = default);
    Task<int> CountLinksByMemberIngredientAsync(Guid memberId, CancellationToken ct = default);
    Task<List<(Guid LinkedIngredientId, int Count)>> GetTopIngredientLinksAsync(Guid memberId, int minCount = 5, CancellationToken ct = default);
    Task<List<AllergenProfileEntryDto>> GetHouseholdHardExcludesAsync(IReadOnlyList<Guid> memberIds, CancellationToken ct = default);
    Task SoftDeleteAllForMemberAsync(Guid memberId, CancellationToken ct = default);

    // GDPR
    Task DeleteMemberDataAsync(Guid memberId, CancellationToken ct = default);
}
