using ExpressRecipe.IngredientService.Services;
using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Shared.Messages;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ExpressRecipe.IngredientService.Tests.Services;

public class IngredientEventPublisherTests
{
    private readonly Mock<IMessageBus> _bus;
    private readonly IngredientEventPublisher _publisher;

    public IngredientEventPublisherTests()
    {
        _bus = new Mock<IMessageBus>();
        _publisher = new IngredientEventPublisher(
            _bus.Object,
            new Mock<ILogger<IngredientEventPublisher>>().Object);
    }

    [Fact]
    public async Task PublishCreatedAsync_PublishesIngredientCreatedEvent()
    {
        var id = Guid.NewGuid();

        await _publisher.PublishCreatedAsync(id, "Flour");

        _bus.Verify(b => b.PublishAsync(
            It.Is<IngredientCreatedEvent>(e => e.IngredientId == id && e.Name == "Flour"),
            It.IsAny<ExpressRecipe.Messaging.Core.Options.PublishOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishUpdatedAsync_PublishesIngredientUpdatedEvent()
    {
        var id = Guid.NewGuid();

        await _publisher.PublishUpdatedAsync(id, "Sugar", "Sugars");

        _bus.Verify(b => b.PublishAsync(
            It.Is<IngredientUpdatedEvent>(e =>
                e.IngredientId == id &&
                e.Name == "Sugar" &&
                e.OldName == "Sugars"),
            It.IsAny<ExpressRecipe.Messaging.Core.Options.PublishOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishDeletedAsync_PublishesIngredientDeletedEvent()
    {
        var id = Guid.NewGuid();

        await _publisher.PublishDeletedAsync(id, "Salt");

        _bus.Verify(b => b.PublishAsync(
            It.Is<IngredientDeletedEvent>(e => e.IngredientId == id && e.Name == "Salt"),
            It.IsAny<ExpressRecipe.Messaging.Core.Options.PublishOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishCreatedAsync_WhenBusThrows_DoesNotPropagate()
    {
        _bus.Setup(b => b.PublishAsync(
                It.IsAny<IngredientCreatedEvent>(),
                It.IsAny<ExpressRecipe.Messaging.Core.Options.PublishOptions?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("bus offline"));

        var act = () => _publisher.PublishCreatedAsync(Guid.NewGuid(), "Test");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task NullPublisher_AllMethods_CompleteWithoutError()
    {
        var nullPublisher = new NullIngredientEventPublisher();

        await nullPublisher.PublishCreatedAsync(Guid.NewGuid(), "Egg");
        await nullPublisher.PublishUpdatedAsync(Guid.NewGuid(), "Eggs", "Egg");
        await nullPublisher.PublishDeletedAsync(Guid.NewGuid(), "Cheese");
    }
}
