using ExpressRecipe.ProfileService.Contracts.Requests;
using ExpressRecipe.ProfileService.Contracts.Responses;

namespace ExpressRecipe.ProfileService.Data;

public interface IHouseholdMemberRepository
{
    Task<List<HouseholdMemberDto>> GetByHouseholdIdAsync(Guid householdId, CancellationToken ct = default);
    Task<HouseholdMemberDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Guid> AddMemberAsync(Guid householdId, AddMemberRequest request, Guid? createdBy = null, CancellationToken ct = default);
    Task<bool> UpdateMemberAsync(Guid memberId, UpdateMemberRequest request, Guid? updatedBy = null, CancellationToken ct = default);
    Task<bool> SoftDeleteMemberAsync(Guid memberId, Guid? deletedBy = null, CancellationToken ct = default);
    Task<Guid> AddTemporaryVisitorAsync(Guid householdId, AddTemporaryVisitorRequest request, Guid? createdBy = null, CancellationToken ct = default);
    Task<Guid> AddCrossHouseholdGuestAsync(Guid householdId, AddCrossHouseholdGuestRequest request, Guid? createdBy = null, CancellationToken ct = default);
    Task<List<HouseholdMemberDto>> GetExpiredTemporaryVisitorsAsync(CancellationToken ct = default);
    Task PurgeExpiredTemporaryVisitorsAsync(CancellationToken ct = default);
    Task<bool> UpdateMemberTypeAsync(Guid memberId, string memberType, Guid? sourceHouseholdId, CancellationToken ct = default);

    // GDPR
    Task<IReadOnlyList<Guid>> DeleteUserDataAsync(Guid userId, CancellationToken ct = default);
}
