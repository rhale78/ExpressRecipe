using ExpressRecipe.Messaging.Saga.Abstractions;
using ExpressRecipe.ProductService.Saga;
using FluentAssertions;
using Xunit;

namespace ExpressRecipe.ProductService.Tests.Saga;

public class ProductProcessingSagaStateTests
{
    [Fact]
    public void ProductProcessingSagaState_ImplementsISagaState()
    {
        // Arrange & Act
        var state = new ProductProcessingSagaState
        {
            CorrelationId = "test-correlation",
            StagingId = Guid.NewGuid(),
            ExternalId = "12345",
            Status = SagaStatus.Running,
            StartedAt = DateTimeOffset.UtcNow
        };

        // Assert
        state.Should().BeAssignableTo<ISagaState>();
        state.CorrelationId.Should().Be("test-correlation");
        state.CurrentMask.Should().Be(0);
        state.Status.Should().Be(SagaStatus.Running);
    }

    [Fact]
    public void ProductProcessingSagaState_BitMaskTracksSteps()
    {
        // Arrange
        var state = new ProductProcessingSagaState
        {
            CorrelationId = "test",
            Status = SagaStatus.Running,
            StartedAt = DateTimeOffset.UtcNow
        };

        // Step 1 complete (bit 1)
        state.CurrentMask |= 1L;
        state.CurrentMask.Should().Be(1);

        // Step 2 complete (bit 2)
        state.CurrentMask |= 2L;
        state.CurrentMask.Should().Be(3);

        // Step 3 complete (bit 4) - all done
        state.CurrentMask |= 4L;
        state.CurrentMask.Should().Be(7);
    }

    [Fact]
    public void ProductProcessingSagaState_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var state = new ProductProcessingSagaState();

        // Assert
        state.CorrelationId.Should().BeEmpty();
        state.CurrentMask.Should().Be(0);
        state.Status.Should().Be(SagaStatus.Pending);
        state.AIVerificationPassed.Should().BeFalse();
        state.RetryCount.Should().Be(0);
    }
}
