using ExpressRecipe.Messaging.Saga.Abstractions;

namespace ExpressRecipe.SafeForkService.Saga;

public class MemberOnboardingSagaState : ISagaState
{
    public string CorrelationId { get; set; } = string.Empty;
    public long CurrentMask { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public SagaStatus Status { get; set; }

    public Guid HouseholdId { get; set; }
    public string MemberType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public Guid? MemberId { get; set; }
    public string? LastError { get; set; }
}
