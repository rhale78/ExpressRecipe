using ExpressRecipe.SafeForkService.Contracts.Requests;
using ExpressRecipe.SafeForkService.Contracts.Responses;

namespace ExpressRecipe.SafeForkService.Data;

public interface ITemporaryScheduleRepository
{
    Task<List<TemporaryScheduleDto>> GetActiveAsync(Guid memberId, CancellationToken ct = default);
    Task<Guid> AddAsync(Guid memberId, AddTemporaryScheduleRequest request, CancellationToken ct = default);
    Task<bool> SoftDeleteAsync(Guid scheduleId, CancellationToken ct = default);
    Task<bool> SoftDeleteForMemberAsync(Guid memberId, Guid scheduleId, CancellationToken ct = default);
}
