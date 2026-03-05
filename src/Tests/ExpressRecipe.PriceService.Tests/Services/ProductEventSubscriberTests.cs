using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.PriceService.Data;
using ExpressRecipe.PriceService.Services;
using ExpressRecipe.Shared.Messages;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ExpressRecipe.PriceService.Tests.Services;

public class ProductEventSubscriberTests
{
    private readonly Mock<IMessageBus> _bus;
    private readonly Mock<IPriceRepository> _repo;
    private readonly Mock<IBatchProductLookupService> _cache;
    private readonly ProductEventSubscriber _subscriber;
    private readonly IServiceProvider _serviceProvider;

    public ProductEventSubscriberTests()
    {
        _bus   = new Mock<IMessageBus>();
        _repo  = new Mock<IPriceRepository>();
        _cache = new Mock<IBatchProductLookupService>();

        var services = new ServiceCollection();
        services.AddSingleton(_repo.Object);
        _serviceProvider = services.BuildServiceProvider();

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(() =>
        {
            var scope = new Mock<IServiceScope>();
            scope.Setup(s => s.ServiceProvider).Returns(_serviceProvider);
            return scope.Object;
        });

        _subscriber = new ProductEventSubscriber(
            _bus.Object,
            scopeFactory.Object,
            _cache.Object,
            new Mock<ILogger<ProductEventSubscriber>>().Object);
    }

    // -----------------------------------------------------------------------
    // StartAsync – verify all eight subscription calls
    // -----------------------------------------------------------------------

    [Fact]
    public async Task StartAsync_RegistersSubscriptionForEachLifecycleEventType()
    {
        await _subscriber.StartAsync(CancellationToken.None);

        // Each Subscribe call for the eight types
        _bus.Verify(b => b.SubscribeAsync(
            It.IsAny<Func<ProductCreatedEvent, MessageContext, CancellationToken, Task>>(),
            It.IsAny<ExpressRecipe.Messaging.Core.Options.SubscribeOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _bus.Verify(b => b.SubscribeAsync(
            It.IsAny<Func<ProductUpdatedEvent, MessageContext, CancellationToken, Task>>(),
            It.IsAny<ExpressRecipe.Messaging.Core.Options.SubscribeOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _bus.Verify(b => b.SubscribeAsync(
            It.IsAny<Func<ProductDeletedEvent, MessageContext, CancellationToken, Task>>(),
            It.IsAny<ExpressRecipe.Messaging.Core.Options.SubscribeOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _bus.Verify(b => b.SubscribeAsync(
            It.IsAny<Func<ProductRenamedEvent, MessageContext, CancellationToken, Task>>(),
            It.IsAny<ExpressRecipe.Messaging.Core.Options.SubscribeOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _bus.Verify(b => b.SubscribeAsync(
            It.IsAny<Func<ProductBarcodeChangedEvent, MessageContext, CancellationToken, Task>>(),
            It.IsAny<ExpressRecipe.Messaging.Core.Options.SubscribeOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // -----------------------------------------------------------------------
    // Handler routing helpers – invoke captured delegate, verify repo calls
    // -----------------------------------------------------------------------

    private static MessageContext FakeCtx() => new MessageContext
    {
        MessageId   = "test-id",
        MessageType = typeof(object).Name,
        Timestamp   = DateTimeOffset.UtcNow
    };

    private Func<TMsg, MessageContext, CancellationToken, Task> CaptureHandler<TMsg>()
        where TMsg : IMessage
    {
        Func<TMsg, MessageContext, CancellationToken, Task>? captured = null;
        _bus.Setup(b => b.SubscribeAsync(
                It.IsAny<Func<TMsg, MessageContext, CancellationToken, Task>>(),
                It.IsAny<ExpressRecipe.Messaging.Core.Options.SubscribeOptions?>(),
                It.IsAny<CancellationToken>()))
            .Callback<Func<TMsg, MessageContext, CancellationToken, Task>,
                      ExpressRecipe.Messaging.Core.Options.SubscribeOptions?,
                      CancellationToken>((h, _, _) => captured = h)
            .Returns(Task.CompletedTask);

        _subscriber.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        captured.Should().NotBeNull("handler should have been registered");
        return captured!;
    }

    [Fact]
    public async Task HandleDeletedAsync_CallsDeactivatePrices()
    {
        var productId = Guid.NewGuid();
        _repo.Setup(r => r.DeactivatePricesByProductIdAsync(productId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(5);

        var handler = CaptureHandler<ProductDeletedEvent>();
        var evt = new ProductDeletedEvent(productId, "111", null, DateTimeOffset.UtcNow);

        await handler(evt, FakeCtx(), CancellationToken.None);

        _repo.Verify(r => r.DeactivatePricesByProductIdAsync(productId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleRenamedAsync_CallsUpdateProductName()
    {
        var productId = Guid.NewGuid();
        _repo.Setup(r => r.UpdateProductNameOnPricesAsync(productId, "New Name", It.IsAny<CancellationToken>()))
             .ReturnsAsync(3);

        var handler = CaptureHandler<ProductRenamedEvent>();
        var evt = new ProductRenamedEvent(productId, "Old Name", "New Name", null, DateTimeOffset.UtcNow);

        await handler(evt, FakeCtx(), CancellationToken.None);

        _repo.Verify(r => r.UpdateProductNameOnPricesAsync(productId, "New Name", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleBarcodeChangedAsync_CallsUpdateUpcAndRefreshesCache()
    {
        var productId = Guid.NewGuid();
        _repo.Setup(r => r.UpdateProductUpcOnPricesAsync(productId, "NEW", It.IsAny<CancellationToken>()))
             .ReturnsAsync(2);
        _cache.Setup(c => c.GetProductByBarcodeAsync("NEW", It.IsAny<CancellationToken>()))
              .ReturnsAsync((ProductDto?)null);

        var handler = CaptureHandler<ProductBarcodeChangedEvent>();
        var evt = new ProductBarcodeChangedEvent(productId, "OLD", "NEW", null, DateTimeOffset.UtcNow);

        await handler(evt, FakeCtx(), CancellationToken.None);

        _repo.Verify(r => r.UpdateProductUpcOnPricesAsync(productId, "NEW", It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.GetProductByBarcodeAsync("NEW", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleCreatedAsync_WithBarcode_RefreshesCache()
    {
        _cache.Setup(c => c.GetProductByBarcodeAsync("123", It.IsAny<CancellationToken>()))
              .ReturnsAsync((ProductDto?)null);

        var handler = CaptureHandler<ProductCreatedEvent>();
        var evt = new ProductCreatedEvent(Guid.NewGuid(), "New Product", null, "123", null, "Pending", null, DateTimeOffset.UtcNow);

        await handler(evt, FakeCtx(), CancellationToken.None);

        _cache.Verify(c => c.GetProductByBarcodeAsync("123", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleDeletedAsync_WhenRepoThrows_DoesNotPropagate()
    {
        _repo.Setup(r => r.DeactivatePricesByProductIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ThrowsAsync(new InvalidOperationException("DB offline"));

        var handler = CaptureHandler<ProductDeletedEvent>();
        var evt = new ProductDeletedEvent(Guid.NewGuid(), null, null, DateTimeOffset.UtcNow);

        var act = () => handler(evt, FakeCtx(), CancellationToken.None);
        await act.Should().NotThrowAsync();
    }
}
