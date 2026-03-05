using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.RecipeService.Services;
using ExpressRecipe.Shared.Messages;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ExpressRecipe.RecipeService.Tests.Services;

public class RecipeEventPublisherTests
{
    private readonly Mock<IMessageBus> _bus;
    private readonly RecipeEventPublisher _publisher;

    public RecipeEventPublisherTests()
    {
        _bus = new Mock<IMessageBus>();
        _publisher = new RecipeEventPublisher(
            _bus.Object,
            new Mock<ILogger<RecipeEventPublisher>>().Object);
    }

    [Fact]
    public async Task PublishCreatedAsync_PublishesRecipeCreatedEvent()
    {
        var recipeId = Guid.NewGuid();
        var userId   = Guid.NewGuid();

        await _publisher.PublishCreatedAsync(recipeId, "Pasta", "Dinner", "Italian", userId);

        _bus.Verify(b => b.PublishAsync(
            It.Is<RecipeCreatedEvent>(e =>
                e.RecipeId == recipeId &&
                e.Name == "Pasta" &&
                e.Category == "Dinner" &&
                e.Cuisine == "Italian" &&
                e.CreatedBy == userId),
            It.IsAny<ExpressRecipe.Messaging.Core.Options.PublishOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishUpdatedAsync_PublishesRecipeUpdatedEvent()
    {
        var recipeId = Guid.NewGuid();
        var userId   = Guid.NewGuid();
        var fields   = new List<string> { "Name", "Category" };

        await _publisher.PublishUpdatedAsync(recipeId, "Updated Pasta", "Lunch", null, userId, fields);

        _bus.Verify(b => b.PublishAsync(
            It.Is<RecipeUpdatedEvent>(e =>
                e.RecipeId == recipeId &&
                e.Name == "Updated Pasta" &&
                e.UpdatedBy == userId &&
                e.ChangedFields.Contains("Name")),
            It.IsAny<ExpressRecipe.Messaging.Core.Options.PublishOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishDeletedAsync_PublishesRecipeDeletedEvent()
    {
        var recipeId = Guid.NewGuid();
        var userId   = Guid.NewGuid();

        await _publisher.PublishDeletedAsync(recipeId, userId);

        _bus.Verify(b => b.PublishAsync(
            It.Is<RecipeDeletedEvent>(e =>
                e.RecipeId == recipeId &&
                e.DeletedBy == userId),
            It.IsAny<ExpressRecipe.Messaging.Core.Options.PublishOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishCreatedAsync_WhenBusThrows_DoesNotPropagate()
    {
        _bus.Setup(b => b.PublishAsync(
                It.IsAny<RecipeCreatedEvent>(),
                It.IsAny<ExpressRecipe.Messaging.Core.Options.PublishOptions?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("bus offline"));

        var act = () => _publisher.PublishCreatedAsync(Guid.NewGuid(), "Test", null, null, Guid.NewGuid());
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task NullPublisher_AllMethods_CompleteWithoutError()
    {
        var nullPublisher = new NullRecipeEventPublisher();

        await nullPublisher.PublishCreatedAsync(Guid.NewGuid(), "name", null, null, Guid.NewGuid());
        await nullPublisher.PublishUpdatedAsync(Guid.NewGuid(), "name", null, null, Guid.NewGuid(), new List<string>());
        await nullPublisher.PublishDeletedAsync(Guid.NewGuid(), Guid.NewGuid());
    }
}
