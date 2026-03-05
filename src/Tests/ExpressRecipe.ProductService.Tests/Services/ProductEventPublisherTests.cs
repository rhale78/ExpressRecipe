using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.ProductService.Services;
using ExpressRecipe.Shared.Messages;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ExpressRecipe.ProductService.Tests.Services;

public class ProductEventPublisherTests
{
    private readonly Mock<IMessageBus> _bus;
    private readonly ProductEventPublisher _publisher;

    public ProductEventPublisherTests()
    {
        _bus = new Mock<IMessageBus>();
        _publisher = new ProductEventPublisher(
            _bus.Object,
            new Mock<ILogger<ProductEventPublisher>>().Object);
    }

    [Fact]
    public async Task PublishCreatedAsync_PublishesCorrectEventType()
    {
        await _publisher.PublishCreatedAsync(
            Guid.NewGuid(), "Test Product", "Acme", "1234567890", "Snacks", "Pending", null);

        _bus.Verify(b => b.PublishAsync(
            It.IsAny<ProductCreatedEvent>(),
            It.IsAny<ExpressRecipe.Messaging.Core.Options.PublishOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishUpdatedAsync_PublishesCorrectEventType()
    {
        await _publisher.PublishUpdatedAsync(
            Guid.NewGuid(), "Test", "Acme", null, "Snacks", "Pending", null,
            new[] { "Brand" });

        _bus.Verify(b => b.PublishAsync(
            It.IsAny<ProductUpdatedEvent>(),
            It.IsAny<ExpressRecipe.Messaging.Core.Options.PublishOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishDeletedAsync_PublishesCorrectEventType()
    {
        await _publisher.PublishDeletedAsync(Guid.NewGuid(), "9876543210", null);

        _bus.Verify(b => b.PublishAsync(
            It.IsAny<ProductDeletedEvent>(),
            It.IsAny<ExpressRecipe.Messaging.Core.Options.PublishOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishApprovedAsync_PublishesCorrectEventType()
    {
        await _publisher.PublishApprovedAsync(Guid.NewGuid(), "Test", "111", Guid.NewGuid());

        _bus.Verify(b => b.PublishAsync(
            It.IsAny<ProductApprovedEvent>(),
            It.IsAny<ExpressRecipe.Messaging.Core.Options.PublishOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishRejectedAsync_PublishesCorrectEventType()
    {
        await _publisher.PublishRejectedAsync(Guid.NewGuid(), "Test", Guid.NewGuid(), "Quality");

        _bus.Verify(b => b.PublishAsync(
            It.IsAny<ProductRejectedEvent>(),
            It.IsAny<ExpressRecipe.Messaging.Core.Options.PublishOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishRenamedAsync_PublishesCorrectEventType()
    {
        await _publisher.PublishRenamedAsync(Guid.NewGuid(), "Old Name", "New Name", null);

        _bus.Verify(b => b.PublishAsync(
            It.IsAny<ProductRenamedEvent>(),
            It.IsAny<ExpressRecipe.Messaging.Core.Options.PublishOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishBarcodeChangedAsync_PublishesCorrectEventType()
    {
        await _publisher.PublishBarcodeChangedAsync(
            Guid.NewGuid(), "111111111111", "222222222222", null);

        _bus.Verify(b => b.PublishAsync(
            It.IsAny<ProductBarcodeChangedEvent>(),
            It.IsAny<ExpressRecipe.Messaging.Core.Options.PublishOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishIngredientsChangedAsync_PublishesCorrectEventType()
    {
        await _publisher.PublishIngredientsChangedAsync(
            Guid.NewGuid(), "Test",
            new[] { Guid.NewGuid() }, Array.Empty<Guid>(), null);

        _bus.Verify(b => b.PublishAsync(
            It.IsAny<ProductIngredientsChangedEvent>(),
            It.IsAny<ExpressRecipe.Messaging.Core.Options.PublishOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishCreatedAsync_WhenBusThrows_DoesNotPropagateException()
    {
        _bus.Setup(b => b.PublishAsync(
                It.IsAny<ProductCreatedEvent>(),
                It.IsAny<ExpressRecipe.Messaging.Core.Options.PublishOptions?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Bus unavailable"));

        // Should swallow the exception – publisher is fire-and-forget
        var act = () => _publisher.PublishCreatedAsync(
            Guid.NewGuid(), "Test", null, null, null, "Pending", null);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task NullProductEventPublisher_AllMethods_DoNotThrowAndCompleteSuccessfully()
    {
        var nullPublisher = new NullProductEventPublisher();
        var productId = Guid.NewGuid();

        // All methods must return completed tasks without throwing
        await nullPublisher.PublishCreatedAsync(productId, "X", null, null, null, "Pending", null);
        await nullPublisher.PublishUpdatedAsync(productId, "X", null, null, null, "Pending", null, Array.Empty<string>());
        await nullPublisher.PublishDeletedAsync(productId, null, null);
        await nullPublisher.PublishApprovedAsync(productId, "X", null, Guid.NewGuid());
        await nullPublisher.PublishRejectedAsync(productId, "X", Guid.NewGuid(), null);
        await nullPublisher.PublishRenamedAsync(productId, "A", "B", null);
        await nullPublisher.PublishBarcodeChangedAsync(productId, null, null, null);
        await nullPublisher.PublishIngredientsChangedAsync(productId, null, Array.Empty<Guid>(), Array.Empty<Guid>(), null);

        // The bus of the real publisher should never have been touched
        _bus.Verify(b => b.PublishAsync(
            It.IsAny<IMessage>(),
            It.IsAny<ExpressRecipe.Messaging.Core.Options.PublishOptions?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
