using ExpressRecipe.Messaging.Saga.Abstractions;
using ExpressRecipe.Messaging.Saga.Builder;
using ExpressRecipe.SafeForkService.Saga;
using ExpressRecipe.Shared.Messages;
using FluentAssertions;
using Moq;
using Xunit;

namespace ExpressRecipe.SafeForkService.Tests.Saga;

public class AllergenResolutionWorkflowTests
{
    [Fact]
    public void AllergenResolutionWorkflow_HasFourSteps()
    {
        // Arrange & Act
        SagaWorkflowDefinition<AllergenResolutionSagaState> workflow = AllergenResolutionWorkflow.Build();

        // Assert
        workflow.Steps.Should().HaveCount(4);
    }

    [Fact]
    public void AllergenResolutionWorkflow_CompletionMask_IsFifteen()
    {
        // 4 steps: bit1+bit2+bit4+bit8 = 15
        SagaWorkflowDefinition<AllergenResolutionSagaState> workflow = AllergenResolutionWorkflow.Build();

        workflow.CompletionMask.Should().Be(15);
    }

    [Fact]
    public void AllergenResolutionWorkflow_WorkflowName_IsAllergenResolution()
    {
        // Arrange & Act
        SagaWorkflowDefinition<AllergenResolutionSagaState> workflow = AllergenResolutionWorkflow.Build();

        // Assert
        workflow.WorkflowName.Should().Be("AllergenResolution");
    }

    [Fact]
    public void AllergenResolutionSagaState_ImplementsISagaState()
    {
        // Arrange & Act
        AllergenResolutionSagaState state = new AllergenResolutionSagaState
        {
            CorrelationId = "test-resolution",
            AllergenProfileId = Guid.NewGuid(),
            MemberId = Guid.NewGuid(),
            FreeFormText = "palm oil",
            Status = SagaStatus.Running,
            StartedAt = DateTimeOffset.UtcNow
        };

        // Assert
        state.Should().BeAssignableTo<ISagaState>();
        state.LinksWritten.Should().Be(0);
        state.CurrentMask.Should().Be(0);
    }

    [Fact]
    public async Task AllergenResolutionWorkflow_OnWorkflowCompleted_WithLinks_PublishesResolvedEvent()
    {
        // Arrange
        SagaWorkflowDefinition<AllergenResolutionSagaState> workflow = AllergenResolutionWorkflow.Build();
        Guid profileId = Guid.NewGuid();
        Guid memberId = Guid.NewGuid();

        AllergenResolutionSagaState state = new AllergenResolutionSagaState
        {
            CorrelationId = "test-resolved",
            AllergenProfileId = profileId,
            MemberId = memberId,
            FreeFormText = "peanuts",
            LinksWritten = 3,
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

        // Assert: AllergenProfileFreeformResolvedEvent published
        busMock.Verify(
            b => b.PublishAsync(
                It.Is<AllergenProfileFreeformResolvedEvent>(e =>
                    e.MemberId == memberId &&
                    e.AllergenProfileId == profileId &&
                    e.FreeFormName == "peanuts" &&
                    e.LinksFound == 3),
                It.IsAny<ExpressRecipe.Messaging.Core.Options.PublishOptions?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert: SagaCompletedNotification also published with Succeeded=true
        busMock.Verify(
            b => b.PublishAsync(
                It.Is<SagaCompletedNotification>(n =>
                    n.WorkflowName == "AllergenResolution" &&
                    n.Succeeded == true),
                It.IsAny<ExpressRecipe.Messaging.Core.Options.PublishOptions?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AllergenResolutionWorkflow_OnWorkflowCompleted_NoLinks_OnlyPublishesSagaCompleted()
    {
        // Arrange: no links written, no ingredient found — still unresolved
        SagaWorkflowDefinition<AllergenResolutionSagaState> workflow = AllergenResolutionWorkflow.Build();

        AllergenResolutionSagaState state = new AllergenResolutionSagaState
        {
            CorrelationId = "test-unresolved",
            AllergenProfileId = Guid.NewGuid(),
            MemberId = Guid.NewGuid(),
            FreeFormText = "mystery substance",
            LinksWritten = 0,
            IngredientId = null,
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

        // Assert: no AllergenProfileFreeformResolvedEvent (nothing was resolved)
        busMock.Verify(
            b => b.PublishAsync(
                It.IsAny<AllergenProfileFreeformResolvedEvent>(),
                It.IsAny<ExpressRecipe.Messaging.Core.Options.PublishOptions?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        // Assert: SagaCompletedNotification published with Succeeded=true (saga itself completed, just no match)
        busMock.Verify(
            b => b.PublishAsync(
                It.Is<SagaCompletedNotification>(n =>
                    n.WorkflowName == "AllergenResolution" &&
                    n.Succeeded == true),
                It.IsAny<ExpressRecipe.Messaging.Core.Options.PublishOptions?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AllergenResolutionWorkflow_OnWorkflowFailed_PublishesFailedNotification()
    {
        // Arrange
        SagaWorkflowDefinition<AllergenResolutionSagaState> workflow = AllergenResolutionWorkflow.Build();

        AllergenResolutionSagaState state = new AllergenResolutionSagaState
        {
            CorrelationId = "test-fail",
            AllergenProfileId = Guid.NewGuid(),
            MemberId = Guid.NewGuid(),
            FreeFormText = "wheat starch",
            Status = SagaStatus.Running,
            StartedAt = DateTimeOffset.UtcNow
        };

        Mock<ExpressRecipe.Messaging.Core.Abstractions.IMessageBus> busMock =
            new Mock<ExpressRecipe.Messaging.Core.Abstractions.IMessageBus>();

        Exception ex = new TimeoutException("IngredientService timed out");

        // Act
        if (workflow.OnWorkflowFailed != null)
        {
            await workflow.OnWorkflowFailed(state, ex, busMock.Object, CancellationToken.None);
        }

        // Assert: SagaCompletedNotification with Succeeded=false
        busMock.Verify(
            b => b.PublishAsync(
                It.Is<SagaCompletedNotification>(n =>
                    n.WorkflowName == "AllergenResolution" &&
                    n.Succeeded == false),
                It.IsAny<ExpressRecipe.Messaging.Core.Options.PublishOptions?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // State should record last error
        state.LastError.Should().Be("IngredientService timed out");
    }
}
