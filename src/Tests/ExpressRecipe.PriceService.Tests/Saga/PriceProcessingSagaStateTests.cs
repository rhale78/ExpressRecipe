using ExpressRecipe.Messaging.Saga.Abstractions;
using ExpressRecipe.PriceService.Saga;
using FluentAssertions;
using Xunit;

namespace ExpressRecipe.PriceService.Tests.Saga;

public class PriceProcessingSagaStateTests
{
    [Fact]
    public void PriceProcessingSagaState_ImplementsISagaState()
    {
        // Arrange & Act
        var state = new PriceProcessingSagaState
        {
            CorrelationId = "price-test-correlation",
            Status = SagaStatus.Running,
            StartedAt = DateTimeOffset.UtcNow
        };

        // Assert
        state.Should().BeAssignableTo<ISagaState>();
        state.IsProductLinked.Should().BeFalse();
        state.ProductId.Should().BeNull();
    }

    [Fact]
    public void PriceProcessingSagaState_NotCompleteUntilProductLinked()
    {
        // Arrange
        var state = new PriceProcessingSagaState
        {
            CorrelationId = "price-test",
            Status = SagaStatus.Running,
            StartedAt = DateTimeOffset.UtcNow,
            IsProductLinked = false
        };

        // A price should not be considered complete until linked
        // Step 1 (ProductLink) bit must be set before status can be Completed
        (state.CurrentMask & 1L).Should().Be(0, "price is not linked yet");

        // Set link
        state.IsProductLinked = true;
        state.ProductId = Guid.NewGuid();
        state.CurrentMask |= 1L;

        (state.CurrentMask & 1L).Should().Be(1, "price link step completed");
    }
}
