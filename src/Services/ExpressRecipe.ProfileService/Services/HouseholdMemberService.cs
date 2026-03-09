using ExpressRecipe.ProfileService.Contracts.Requests;
using ExpressRecipe.ProfileService.Contracts.Responses;
using ExpressRecipe.ProfileService.Data;

namespace ExpressRecipe.ProfileService.Services;

public class HouseholdMemberService : IHouseholdMemberService
{
    private readonly IHouseholdMemberRepository _repository;
    private readonly IProfileEventPublisher _eventPublisher;
    private readonly ILogger<HouseholdMemberService> _logger;

    public HouseholdMemberService(
        IHouseholdMemberRepository repository,
        IProfileEventPublisher eventPublisher,
        ILogger<HouseholdMemberService> logger)
    {
        _repository = repository;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public Task<List<HouseholdMemberDto>> GetMembersAsync(Guid householdId, CancellationToken ct)
    {
        return _repository.GetByHouseholdIdAsync(householdId, ct);
    }

    public async Task<Guid> AddMemberAsync(Guid householdId, AddMemberRequest request, Guid? requestingUserId, CancellationToken ct)
    {
        // COPPA: Child and Infant members cannot have user accounts
        if (IsMinorMemberType(request.MemberType))
        {
            request = new AddMemberRequest
            {
                MemberType    = request.MemberType,
                DisplayName   = request.DisplayName,
                BirthYear     = request.BirthYear,
                LinkedUserId  = null
            };
        }

        Guid memberId = await _repository.AddMemberAsync(householdId, request, requestingUserId, ct);

        bool hasUserAccount = !IsMinorMemberType(request.MemberType) && request.LinkedUserId.HasValue;

        await _eventPublisher.PublishMemberAddedAsync(
            memberId, householdId, request.MemberType, request.DisplayName, hasUserAccount, ct);

        _logger.LogInformation(
            "Added household member {MemberId} of type {MemberType} to household {HouseholdId}",
            memberId, request.MemberType, householdId);

        return memberId;
    }

    public async Task<bool> UpdateMemberAsync(Guid householdId, Guid memberId, UpdateMemberRequest request, Guid? requestingUserId, CancellationToken ct)
    {
        HouseholdMemberDto? member = await _repository.GetByIdAsync(memberId, ct);
        if (member == null)
        {
            return false;
        }

        // COPPA: Child/Infant members cannot have user accounts
        if (IsMinorMemberType(member.MemberType) && request.HasUserAccount == true)
        {
            throw new ArgumentException("COPPA: Child/Infant members cannot have user accounts");
        }

        bool updated = await _repository.UpdateMemberAsync(memberId, request, requestingUserId, ct);

        // Teen account activation: notify FamilyAdmin
        if (updated
            && member.MemberType == "Teen"
            && request.HasUserAccount == true
            && !member.HasUserAccount)
        {
            await _eventPublisher.PublishFamilyAdminNotificationAsync(
                householdId,
                memberId,
                "TeenAccountActivated",
                $"Teen member '{member.DisplayName}' has activated their user account.",
                ct);
        }

        return updated;
    }

    public async Task<bool> RemoveMemberAsync(Guid householdId, Guid memberId, Guid? requestingUserId, CancellationToken ct)
    {
        HouseholdMemberDto? member = await _repository.GetByIdAsync(memberId, ct);
        if (member == null)
        {
            return false;
        }

        // CrossHouseholdGuest deletion: convert to RecurringGuest snapshot before removing
        if (member.MemberType == "CrossHouseholdGuest")
        {
            await _repository.UpdateMemberTypeAsync(memberId, "RecurringGuest", null, ct);
            _logger.LogInformation(
                "Converted CrossHouseholdGuest {MemberId} to RecurringGuest snapshot in household {HouseholdId}",
                memberId, householdId);
        }

        bool deleted = await _repository.SoftDeleteMemberAsync(memberId, requestingUserId, ct);

        if (deleted)
        {
            await _eventPublisher.PublishMemberRemovedAsync(memberId, householdId, member.MemberType, ct);

            _logger.LogInformation(
                "Removed household member {MemberId} of type {MemberType} from household {HouseholdId}",
                memberId, member.MemberType, householdId);
        }

        return deleted;
    }

    public async Task<Guid> AddTemporaryVisitorAsync(Guid householdId, AddTemporaryVisitorRequest request, Guid? requestingUserId, CancellationToken ct)
    {
        Guid memberId = await _repository.AddTemporaryVisitorAsync(householdId, request, requestingUserId, ct);

        await _eventPublisher.PublishMemberAddedAsync(
            memberId, householdId, "TemporaryVisitor", request.DisplayName, false, ct);

        _logger.LogInformation(
            "Added temporary visitor {MemberId} to household {HouseholdId}, expires {ExpiresAt}",
            memberId, householdId, request.GuestExpiresAt);

        return memberId;
    }

    public async Task<Guid> AddCrossHouseholdGuestAsync(Guid householdId, AddCrossHouseholdGuestRequest request, Guid? requestingUserId, CancellationToken ct)
    {
        Guid memberId = await _repository.AddCrossHouseholdGuestAsync(householdId, request, requestingUserId, ct);

        await _eventPublisher.PublishMemberAddedAsync(
            memberId, householdId, "CrossHouseholdGuest", request.DisplayName, false, ct);

        _logger.LogInformation(
            "Added cross-household guest {MemberId} from household {SourceHouseholdId} to household {HouseholdId}",
            memberId, request.SourceHouseholdId, householdId);

        return memberId;
    }

    private static bool IsMinorMemberType(string memberType)
    {
        return memberType == "Child" || memberType == "Infant";
    }
}
