using ExpressRecipe.ProfileService.Contracts.Requests;
using ExpressRecipe.ProfileService.Contracts.Responses;

namespace ExpressRecipe.ProfileService.Services;

public interface IHouseholdMemberService
{
    Task<List<HouseholdMemberDto>> GetMembersAsync(Guid householdId, CancellationToken ct);
    Task<Guid> AddMemberAsync(Guid householdId, AddMemberRequest request, Guid? requestingUserId, CancellationToken ct);
    Task<bool> UpdateMemberAsync(Guid householdId, Guid memberId, UpdateMemberRequest request, Guid? requestingUserId, CancellationToken ct);
    Task<bool> RemoveMemberAsync(Guid householdId, Guid memberId, Guid? requestingUserId, CancellationToken ct);
    Task<Guid> AddTemporaryVisitorAsync(Guid householdId, AddTemporaryVisitorRequest request, Guid? requestingUserId, CancellationToken ct);
    Task<Guid> AddCrossHouseholdGuestAsync(Guid householdId, AddCrossHouseholdGuestRequest request, Guid? requestingUserId, CancellationToken ct);
}
