using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Options;
using ExpressRecipe.PriceService.Services;
using ExpressRecipe.Shared.Messages;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ExpressRecipe.PriceService.Tests.Services;

/// <summary>
/// Tests for <see cref="PriceEventPublisher"/> and <see cref="NullPriceEventPublisher"/>.
/// Validates that each CRUD action fires the correct event type and that the publishers
/// never let bus failures propagate to callers.
/// </summary>
public class PriceEventPublisherTests
{
    private readonly Mock<IMessageBus> _bus;
    private readonly PriceEventPublisher _publisher;
    private readonly Guid _productId  = Guid.NewGuid();
    private readonly Guid _storeId    = Guid.NewGuid();
    private readonly Guid _userId     = Guid.NewGuid();

    public PriceEventPublisherTests()
    {
        _bus       = new Mock<IMessageBus>();
        _publisher = new PriceEventPublisher(
            _bus.Object,
            new Mock<ILogger<PriceEventPublisher>>().Object);
    }

    // ── PublishPriceRecordedAsync ──────────────────────────────────────────

    [Fact]
    public async Task PublishPriceRecordedAsync_PublishesPriceRecordedEvent()
    {
        var obsId = Guid.NewGuid();

        await _publisher.PublishPriceRecordedAsync(obsId, _productId, _storeId, 3.99m, _userId);

        _bus.Verify(b => b.PublishAsync(
            It.Is<PriceRecordedEvent>(e =>
                e.PriceObservationId == obsId &&
                e.ProductId == _productId &&
                e.StoreId   == _storeId &&
                e.Price     == 3.99m &&
                e.RecordedBy == _userId),
            It.IsAny<PublishOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishPriceRecordedAsync_LogsPublishedEvent()
    {
        var loggerMock = new Mock<ILogger<PriceEventPublisher>>();
        var publisher  = new PriceEventPublisher(_bus.Object, loggerMock.Object);

        await publisher.PublishPriceRecordedAsync(Guid.NewGuid(), _productId, _storeId, 1.99m, _userId);

        // Verify a LogInformation call was made containing the event type name
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains(nameof(PriceRecordedEvent))),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // ── PublishPriceBatchSubmittedAsync ────────────────────────────────────

    [Fact]
    public async Task PublishPriceBatchSubmittedAsync_PublishesPriceBatchSubmittedEvent()
    {
        await _publisher.PublishPriceBatchSubmittedAsync("session-abc", 50, _userId);

        _bus.Verify(b => b.PublishAsync(
            It.Is<PriceBatchSubmittedEvent>(e =>
                e.SessionId  == "session-abc" &&
                e.ItemCount  == 50 &&
                e.SubmittedBy == _userId),
            It.IsAny<PublishOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── PublishDealCreatedAsync ────────────────────────────────────────────

    [Fact]
    public async Task PublishDealCreatedAsync_PublishesDealCreatedEvent()
    {
        var dealId = Guid.NewGuid();
        var start  = DateTime.UtcNow;
        var end    = DateTime.UtcNow.AddDays(7);

        await _publisher.PublishDealCreatedAsync(
            dealId, _productId, _storeId, "BOGO", 5.00m, 2.50m, start, end);

        _bus.Verify(b => b.PublishAsync(
            It.Is<DealCreatedEvent>(e =>
                e.DealId       == dealId &&
                e.ProductId    == _productId &&
                e.StoreId      == _storeId &&
                e.DealType     == "BOGO" &&
                e.OriginalPrice == 5.00m &&
                e.SalePrice    == 2.50m),
            It.IsAny<PublishOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── PublishStoreAddedAsync ─────────────────────────────────────────────

    [Fact]
    public async Task PublishStoreAddedAsync_PublishesStoreAddedEvent()
    {
        var storeId = Guid.NewGuid();

        await _publisher.PublishStoreAddedAsync(storeId, "Whole Foods", "Raleigh", "NC", "Whole Foods");

        _bus.Verify(b => b.PublishAsync(
            It.Is<StoreAddedEvent>(e =>
                e.StoreId == storeId &&
                e.Name    == "Whole Foods" &&
                e.City    == "Raleigh" &&
                e.State   == "NC"),
            It.IsAny<PublishOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Error resilience ──────────────────────────────────────────────────

    [Fact]
    public async Task PublishPriceRecordedAsync_WhenBusFails_DoesNotPropagate()
    {
        _bus.Setup(b => b.PublishAsync(
                It.IsAny<PriceRecordedEvent>(),
                It.IsAny<PublishOptions?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker down"));

        var act = () => _publisher.PublishPriceRecordedAsync(
            Guid.NewGuid(), _productId, _storeId, 2.49m, _userId);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishPriceRecordedAsync_WhenBusFails_LogsWarning()
    {
        var loggerMock = new Mock<ILogger<PriceEventPublisher>>();
        var publisher  = new PriceEventPublisher(_bus.Object, loggerMock.Object);

        _bus.Setup(b => b.PublishAsync(
                It.IsAny<PriceRecordedEvent>(),
                It.IsAny<PublishOptions?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker down"));

        await publisher.PublishPriceRecordedAsync(
            Guid.NewGuid(), _productId, _storeId, 1.00m, _userId);

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // ── NullPriceEventPublisher ────────────────────────────────────────────

    [Fact]
    public async Task NullPriceEventPublisher_AllMethods_CompleteWithoutError()
    {
        var loggerMock = new Mock<ILogger<NullPriceEventPublisher>>();
        var nullPub    = new NullPriceEventPublisher(loggerMock.Object);

        await nullPub.PublishPriceRecordedAsync(Guid.NewGuid(), _productId, _storeId, 1.49m, _userId);
        await nullPub.PublishPriceBatchSubmittedAsync("s-1", 100, _userId);
        await nullPub.PublishDealCreatedAsync(Guid.NewGuid(), _productId, _storeId, "BOGO", 4m, 2m,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(3));
        await nullPub.PublishStoreAddedAsync(Guid.NewGuid(), "Target", "Durham", "NC", "Target");

        // Bus was never called
        _bus.Verify(b => b.PublishAsync(
            It.IsAny<IMessage>(),
            It.IsAny<PublishOptions?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NullPriceEventPublisher_LogsDebugMessages()
    {
        var loggerMock = new Mock<ILogger<NullPriceEventPublisher>>();
        var nullPub    = new NullPriceEventPublisher(loggerMock.Object);

        await nullPub.PublishPriceRecordedAsync(Guid.NewGuid(), _productId, _storeId, 2.00m, _userId);

        // Debug log written so the event is still observable even without messaging
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("PriceRecorded")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
