using ExpressRecipe.Messaging.Saga.Builder;
using ExpressRecipe.Shared.Messages;

namespace ExpressRecipe.SafeForkService.Saga;

public static class MemberOnboardingWorkflow
{
    public const string WorkflowName = "MemberOnboarding";

    public static SagaWorkflowDefinition<MemberOnboardingSagaState> Build()
    {
        return new SagaWorkflowBuilder<MemberOnboardingSagaState>(WorkflowName)
            .AddStep("CreateMemberRecord")
                .Sends(s => new RequestCreateMember(
                    s.CorrelationId,
                    s.HouseholdId,
                    s.MemberType,
                    s.DisplayName))
                .SendsTo(MemberOnboardingKeys.CreateMember)
                .OnResult<MemberRecordCreated>((state, result, ct) =>
                {
                    state.MemberId = result.MemberId;
                    return Task.FromResult(state);
                })
            .And()
            .AddStep("InitAllergenProfile")
                .DependsOn("CreateMemberRecord")
                .Sends(s => s.MemberId == null
                    ? throw new InvalidOperationException("MemberId was not set by CreateMemberRecord step.")
                    : new RequestInitAllergenProfile(
                        s.CorrelationId,
                        s.MemberId.Value))
                .SendsTo(MemberOnboardingKeys.InitAllergenProfile)
                .OnResult<AllergenProfileInitialized>()
            .And()
            .AddStep("InitCookProfile")
                .DependsOn("CreateMemberRecord")
                .Sends(s => s.MemberId == null
                    ? throw new InvalidOperationException("MemberId was not set by CreateMemberRecord step.")
                    : new RequestInitCookProfile(
                        s.CorrelationId,
                        s.MemberId.Value))
                .SendsTo(MemberOnboardingKeys.InitCookProfile)
                .OnResult<CookProfileInitialized>()
            .And()
            .AddStep("SendWelcomeNotification")
                .DependsOn("InitAllergenProfile")
                .DependsOn("InitCookProfile")
                .Sends(s => s.MemberId == null
                    ? throw new InvalidOperationException("MemberId was not set by CreateMemberRecord step.")
                    : new RequestSendWelcome(
                        s.CorrelationId,
                        s.MemberId.Value))
                .SendsTo(MemberOnboardingKeys.SendWelcome)
                .OnResult<WelcomeNotificationSent>()
            .And()
            .OnWorkflowCompleted(async (state, bus, ct) =>
            {
                string summary = $"Member '{state.DisplayName}' onboarded successfully.";
                await bus.PublishAsync(new SagaCompletedNotification(
                    WorkflowName,
                    state.CorrelationId,
                    Succeeded: true,
                    summary,
                    AffectedEntityId: state.MemberId,
                    DateTimeOffset.UtcNow), cancellationToken: ct);
            })
            .OnWorkflowFailed(async (state, ex, bus, ct) =>
            {
                state.LastError = ex.Message;

                await bus.PublishAsync(new SagaCompletedNotification(
                    WorkflowName,
                    state.CorrelationId,
                    Succeeded: false,
                    Summary: $"Member onboarding failed: {ex.Message}",
                    AffectedEntityId: state.MemberId,
                    DateTimeOffset.UtcNow), cancellationToken: ct);
            })
            .Build();
    }
}
