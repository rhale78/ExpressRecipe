using ExpressRecipe.Messaging.Saga.Abstractions;
using ExpressRecipe.Messaging.Saga.Builder;
using ExpressRecipe.SafeForkService.Saga;
using ExpressRecipe.Shared.Messages;
using FluentAssertions;
using Moq;
using Xunit;

namespace ExpressRecipe.SafeForkService.Tests.Saga;

public class MemberOnboardingWorkflowTests
{
    [Fact]
    public void MemberOnboardingSagaState_ImplementsISagaState()
    {
        // Arrange & Act
        MemberOnboardingSagaState state = new MemberOnboardingSagaState
        {
            CorrelationId = "test-onboarding",
            HouseholdId = Guid.NewGuid(),
            MemberType = "Adult",
            DisplayName = "Jane Doe",
            Status = SagaStatus.Running,
            StartedAt = DateTimeOffset.UtcNow
        };

        // Assert
        state.Should().BeAssignableTo<ISagaState>();
        state.CorrelationId.Should().Be("test-onboarding");
        state.CurrentMask.Should().Be(0);
    }

    [Fact]
    public void MemberOnboardingWorkflow_HasFourSteps()
    {
        // Arrange & Act
        SagaWorkflowDefinition<MemberOnboardingSagaState> workflow = MemberOnboardingWorkflow.Build();

        // Assert
        workflow.Steps.Should().HaveCount(4);
    }

    [Fact]
    public void MemberOnboardingWorkflow_CompletionMask_IsFifteen()
    {
        // 4 steps: bit1=1, bit2=2, bit3=4, bit4=8 → mask = 15
        SagaWorkflowDefinition<MemberOnboardingSagaState> workflow = MemberOnboardingWorkflow.Build();

        workflow.CompletionMask.Should().Be(15);
    }

    [Fact]
    public void MemberOnboardingWorkflow_WorkflowName_IsMemberOnboarding()
    {
        // Arrange & Act
        SagaWorkflowDefinition<MemberOnboardingSagaState> workflow = MemberOnboardingWorkflow.Build();

        // Assert
        workflow.WorkflowName.Should().Be("MemberOnboarding");
    }

    [Fact]
    public async Task MemberOnboardingWorkflow_OnWorkflowCompleted_PublishesSagaNotification()
    {
        // Arrange
        SagaWorkflowDefinition<MemberOnboardingSagaState> workflow = MemberOnboardingWorkflow.Build();
        Guid memberId = Guid.NewGuid();

        MemberOnboardingSagaState state = new MemberOnboardingSagaState
        {
            CorrelationId = "test-complete",
            HouseholdId = Guid.NewGuid(),
            MemberType = "Adult",
            DisplayName = "John Doe",
            MemberId = memberId,
            Status = SagaStatus.Running,
            StartedAt = DateTimeOffset.UtcNow
        };

        Mock<ExpressRecipe.Messaging.Core.Abstractions.IMessageBus> busMock =
            new Mock<ExpressRecipe.Messaging.Core.Abstractions.IMessageBus>();

        // Act
        if (workflow.OnWorkflowCompleted != null)
        {
            await workflow.OnWorkflowCompleted(state, busMock.Object, CancellationToken.None);
        }

        // Assert: SagaCompletedNotification published with Succeeded=true
        busMock.Verify(
            b => b.PublishAsync(
                It.Is<SagaCompletedNotification>(n =>
                    n.WorkflowName == "MemberOnboarding" &&
                    n.Succeeded == true &&
                    n.AffectedEntityId == memberId),
                It.IsAny<ExpressRecipe.Messaging.Core.Options.PublishOptions?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task MemberOnboardingWorkflow_OnWorkflowFailed_PublishesFailedNotification()
    {
        // Arrange
        SagaWorkflowDefinition<MemberOnboardingSagaState> workflow = MemberOnboardingWorkflow.Build();

        MemberOnboardingSagaState state = new MemberOnboardingSagaState
        {
            CorrelationId = "test-fail",
            HouseholdId = Guid.NewGuid(),
            MemberType = "Child",
            DisplayName = "Baby Doe",
            Status = SagaStatus.Running,
            StartedAt = DateTimeOffset.UtcNow
        };

        Mock<ExpressRecipe.Messaging.Core.Abstractions.IMessageBus> busMock =
            new Mock<ExpressRecipe.Messaging.Core.Abstractions.IMessageBus>();

        Exception ex = new InvalidOperationException("Service unavailable");

        // Act
        if (workflow.OnWorkflowFailed != null)
        {
            await workflow.OnWorkflowFailed(state, ex, busMock.Object, CancellationToken.None);
        }

        // Assert: SagaCompletedNotification published with Succeeded=false
        busMock.Verify(
            b => b.PublishAsync(
                It.Is<SagaCompletedNotification>(n =>
                    n.WorkflowName == "MemberOnboarding" &&
                    n.Succeeded == false),
                It.IsAny<ExpressRecipe.Messaging.Core.Options.PublishOptions?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void MemberOnboardingSagaState_BitMaskTracksSteps()
    {
        // Arrange
        MemberOnboardingSagaState state = new MemberOnboardingSagaState
        {
            CorrelationId = "test",
            HouseholdId = Guid.NewGuid(),
            MemberType = "Adult",
            DisplayName = "Test",
            Status = SagaStatus.Running,
            StartedAt = DateTimeOffset.UtcNow
        };

        // Step 1 (bit 1): CreateMemberRecord
        state.CurrentMask |= 1L;
        state.CurrentMask.Should().Be(1);

        // Step 2 (bit 2): InitAllergenProfile
        state.CurrentMask |= 2L;
        state.CurrentMask.Should().Be(3);

        // Step 3 (bit 4): InitCookProfile
        state.CurrentMask |= 4L;
        state.CurrentMask.Should().Be(7);

        // Step 4 (bit 8): SendWelcomeNotification
        state.CurrentMask |= 8L;
        state.CurrentMask.Should().Be(15);
    }
}
