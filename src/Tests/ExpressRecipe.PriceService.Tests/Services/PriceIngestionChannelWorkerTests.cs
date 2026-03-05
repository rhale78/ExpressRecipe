using ExpressRecipe.PriceService.Data;
using ExpressRecipe.PriceService.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Threading.Channels;
using Xunit;

namespace ExpressRecipe.PriceService.Tests.Services;

/// <summary>
/// Tests for <see cref="PriceIngestionChannelWorker"/> – the background service that reads
/// from the ingestion channel and persists + publishes events.
/// </summary>
public class PriceIngestionChannelWorkerTests
{
    private readonly Mock<IPriceRepository>   _repoMock;
    private readonly Mock<IPriceEventPublisher> _eventsMock;
    private readonly Mock<ILogger<PriceIngestionChannelWorker>> _loggerMock;

    public PriceIngestionChannelWorkerTests()
    {
        _repoMock   = new Mock<IPriceRepository>();
        _eventsMock = new Mock<IPriceEventPublisher>();
        _loggerMock = new Mock<ILogger<PriceIngestionChannelWorker>>();
    }

    private (PriceIngestionChannelWorker worker, Channel<PriceIngestionRequest> innerChannel)
        CreateWorkerWithChannel()
    {
        // Use an unbounded channel so writes never block in tests
        var inner   = Channel.CreateUnbounded<PriceIngestionRequest>();
        var channel = new TestPriceIngestionChannel(inner);

        var services = new ServiceCollection();
        services.AddScoped<IPriceRepository>(_ => _repoMock.Object);
        var sp = services.BuildServiceProvider();

        var worker = new PriceIngestionChannelWorker(
            channel, sp.GetRequiredService<IServiceScopeFactory>(),
            _eventsMock.Object, _loggerMock.Object);

        return (worker, inner);
    }

    [Fact]
    public async Task Worker_WhenItemQueued_CallsRepositoryAndFiresEvent()
    {
        // Arrange
        var priceId   = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var storeId   = Guid.NewGuid();
        var userId    = Guid.NewGuid();
        _repoMock
            .Setup(r => r.RecordPriceAsync(productId, storeId, 1.99m, userId, null))
            .ReturnsAsync(priceId);

        var (worker, inner) = CreateWorkerWithChannel();
        using var cts = new CancellationTokenSource();

        // Write one item before starting so it will be processed
        await inner.Writer.WriteAsync(new PriceIngestionRequest
        {
            ProductId = productId, StoreId = storeId, Price = 1.99m, SubmittedBy = userId
        });

        // Start the worker and give it time to process
        _ = worker.StartAsync(cts.Token);
        await Task.Delay(200);
        cts.Cancel();

        // Assert
        _repoMock.Verify(r => r.RecordPriceAsync(productId, storeId, 1.99m, userId, null), Times.Once);
        _eventsMock.Verify(e => e.PublishPriceRecordedAsync(
            priceId, productId, storeId, 1.99m, userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Worker_WhenRepositoryThrows_LogsErrorAndContinues()
    {
        // Arrange – first call throws, second succeeds
        var productId1 = Guid.NewGuid();
        var productId2 = Guid.NewGuid();
        var storeId    = Guid.NewGuid();
        var userId     = Guid.NewGuid();
        var priceId2   = Guid.NewGuid();

        _repoMock
            .SetupSequence(r => r.RecordPriceAsync(It.IsAny<Guid>(), storeId, 1.00m, userId, null))
            .ThrowsAsync(new InvalidOperationException("DB unavailable"))
            .ReturnsAsync(priceId2);

        var (worker, inner) = CreateWorkerWithChannel();
        using var cts = new CancellationTokenSource();

        await inner.Writer.WriteAsync(new PriceIngestionRequest
        {
            ProductId = productId1, StoreId = storeId, Price = 1.00m, SubmittedBy = userId
        });
        await inner.Writer.WriteAsync(new PriceIngestionRequest
        {
            ProductId = productId2, StoreId = storeId, Price = 1.00m, SubmittedBy = userId
        });

        _ = worker.StartAsync(cts.Token);
        await Task.Delay(300);
        cts.Cancel();

        // Second item still processed despite first failing
        _eventsMock.Verify(e => e.PublishPriceRecordedAsync(
            priceId2, productId2, storeId, 1.00m, userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Worker_WhenEventPublishFails_DoesNotAffectNextItem()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var storeId   = Guid.NewGuid();
        var userId    = Guid.NewGuid();
        var priceId   = Guid.NewGuid();
        _repoMock
            .Setup(r => r.RecordPriceAsync(productId, storeId, 2.00m, userId, null))
            .ReturnsAsync(priceId);
        _eventsMock
            .Setup(e => e.PublishPriceRecordedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("bus down"));

        var (worker, inner) = CreateWorkerWithChannel();
        using var cts = new CancellationTokenSource();

        await inner.Writer.WriteAsync(new PriceIngestionRequest
        {
            ProductId = productId, StoreId = storeId, Price = 2.00m, SubmittedBy = userId
        });

        // Act – should not crash
        _ = worker.StartAsync(cts.Token);
        await Task.Delay(200);
        cts.Cancel();

        // Repo was still called
        _repoMock.Verify(r => r.RecordPriceAsync(productId, storeId, 2.00m, userId, null), Times.Once);
    }

    // ── Test double ──────────────────────────────────────────────────────────

    private sealed class TestPriceIngestionChannel : IPriceIngestionChannel
    {
        private readonly Channel<PriceIngestionRequest> _inner;

        public TestPriceIngestionChannel(Channel<PriceIngestionRequest> inner) => _inner = inner;

        public bool TryWrite(PriceIngestionRequest r) => _inner.Writer.TryWrite(r);
        public ValueTask WriteAsync(PriceIngestionRequest r, CancellationToken ct = default)
            => _inner.Writer.WriteAsync(r, ct);
        public IAsyncEnumerable<PriceIngestionRequest> ReadAllAsync(CancellationToken ct = default)
            => _inner.Reader.ReadAllAsync(ct);
        public int Count => _inner.Reader.Count;
    }
}
