using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Options;
using ExpressRecipe.Messaging.Saga.Abstractions;
using ExpressRecipe.PriceService.Messages;
using ExpressRecipe.PriceService.Saga;
using ExpressRecipe.ProductService.Messages;
using ExpressRecipe.ProductService.Saga;
using ExpressRecipe.Shared.Messages;
using FluentAssertions;
using Moq;
using Xunit;

namespace ExpressRecipe.PriceService.Tests.Saga;

/// <summary>
/// Verifies that both the PriceProcessingWorkflow and ProductProcessingWorkflow publish
/// a <see cref="SagaCompletedNotification"/> when they complete or fail.
/// </summary>
public class SagaCompletedNotificationTests
{
    private readonly Mock<IMessageBus> _busMock = new();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static PriceProcessingSagaState CompletedPriceState(string correlationId) =>
        new()
        {
            CorrelationId      = correlationId,
            PriceObservationId = Guid.NewGuid(),
            ProductId          = Guid.NewGuid(),
            StoreId            = Guid.NewGuid(),
            Price              = 2.99m,
            Status             = SagaStatus.Completed
        };

    private static ProductProcessingSagaState CompletedProductState(string correlationId) =>
        new()
        {
            CorrelationId = correlationId,
            ProductId     = Guid.NewGuid(),
            StagingId     = Guid.NewGuid(),
            ProductName   = "Test Product",
            Status        = SagaStatus.Completed
        };

    // ── Price saga completion ─────────────────────────────────────────────────

    [Fact]
    public async Task PriceWorkflow_OnCompleted_PublishesSagaCompletedNotification()
    {
        var correlationId = Guid.NewGuid().ToString();
        var state = CompletedPriceState(correlationId);
        var workflow = PriceProcessingWorkflow.Build();

        // Invoke the completion callback directly
        await workflow.OnWorkflowCompleted!(state, _busMock.Object, CancellationToken.None);

        _busMock.Verify(b => b.PublishAsync(
            It.Is<SagaCompletedNotification>(n =>
                n.WorkflowName    == PriceProcessingWorkflow.WorkflowName &&
                n.CorrelationId   == correlationId &&
                n.Succeeded       == true &&
                n.AffectedEntityId == state.PriceObservationId),
            It.IsAny<PublishOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PriceWorkflow_OnFailed_PublishesFailedNotification()
    {
        var correlationId = Guid.NewGuid().ToString();
        var state = new PriceProcessingSagaState
        {
            CorrelationId = correlationId,
            PriceStagingId = Guid.NewGuid(),
            Status = SagaStatus.Failed
        };
        var workflow  = PriceProcessingWorkflow.Build();
        var exception = new InvalidOperationException("product not found");

        await workflow.OnWorkflowFailed!(state, exception, _busMock.Object, CancellationToken.None);

        _busMock.Verify(b => b.PublishAsync(
            It.Is<SagaCompletedNotification>(n =>
                n.WorkflowName == PriceProcessingWorkflow.WorkflowName &&
                n.CorrelationId == correlationId &&
                n.Succeeded == false &&
                n.Summary!.Contains("product not found")),
            It.IsAny<PublishOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PriceWorkflow_OnCompleted_AlsoPublishesPriceObservationCompleted()
    {
        var state    = CompletedPriceState(Guid.NewGuid().ToString());
        var workflow = PriceProcessingWorkflow.Build();

        await workflow.OnWorkflowCompleted!(state, _busMock.Object, CancellationToken.None);

        // Both events published
        _busMock.Verify(b => b.PublishAsync(
            It.IsAny<PriceObservationCompleted>(),
            It.IsAny<PublishOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _busMock.Verify(b => b.PublishAsync(
            It.IsAny<SagaCompletedNotification>(),
            It.IsAny<PublishOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Product saga completion ────────────────────────────────────────────────

    [Fact]
    public async Task ProductWorkflow_OnCompleted_PublishesSagaCompletedNotification()
    {
        var correlationId = Guid.NewGuid().ToString();
        var state    = CompletedProductState(correlationId);
        var workflow = ProductProcessingWorkflow.Build();

        await workflow.OnWorkflowCompleted!(state, _busMock.Object, CancellationToken.None);

        _busMock.Verify(b => b.PublishAsync(
            It.Is<SagaCompletedNotification>(n =>
                n.WorkflowName    == ProductProcessingWorkflow.WorkflowName &&
                n.CorrelationId   == correlationId &&
                n.Succeeded       == true &&
                n.AffectedEntityId == state.ProductId),
            It.IsAny<PublishOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProductWorkflow_OnFailed_PublishesFailedNotification()
    {
        var correlationId = Guid.NewGuid().ToString();
        var state = new ProductProcessingSagaState
        {
            CorrelationId = correlationId,
            StagingId     = Guid.NewGuid(),
            Status        = SagaStatus.Failed
        };
        var workflow  = ProductProcessingWorkflow.Build();
        var exception = new InvalidOperationException("AI verification failed");

        await workflow.OnWorkflowFailed!(state, exception, _busMock.Object, CancellationToken.None);

        _busMock.Verify(b => b.PublishAsync(
            It.Is<SagaCompletedNotification>(n =>
                n.WorkflowName == ProductProcessingWorkflow.WorkflowName &&
                n.Succeeded == false &&
                n.Summary!.Contains("AI verification failed")),
            It.IsAny<PublishOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProductWorkflow_OnCompleted_AlsoPublishesProductPublishedEvent()
    {
        var state    = CompletedProductState(Guid.NewGuid().ToString());
        state.ExternalId = "ext-123";
        var workflow = ProductProcessingWorkflow.Build();

        await workflow.OnWorkflowCompleted!(state, _busMock.Object, CancellationToken.None);

        // Both events published
        _busMock.Verify(b => b.PublishAsync(
            It.IsAny<ProductPublished>(),
            It.IsAny<PublishOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _busMock.Verify(b => b.PublishAsync(
            It.IsAny<SagaCompletedNotification>(),
            It.IsAny<PublishOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── SagaCompletedNotification contract ────────────────────────────────────

    [Fact]
    public void SagaCompletedNotification_SucceededTrue_HasExpectedShape()
    {
        var notification = new SagaCompletedNotification(
            "PriceProcessing",
            "corr-1",
            Succeeded: true,
            Summary: "All done",
            AffectedEntityId: Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        notification.WorkflowName.Should().Be("PriceProcessing");
        notification.Succeeded.Should().BeTrue();
        notification.AffectedEntityId.Should().NotBeNull();
    }

    [Fact]
    public void SagaCompletedNotification_SucceededFalse_HasExpectedShape()
    {
        var notification = new SagaCompletedNotification(
            "ProductProcessing",
            "corr-2",
            Succeeded: false,
            Summary: "Workflow failed: timeout",
            AffectedEntityId: null,
            DateTimeOffset.UtcNow);

        notification.Succeeded.Should().BeFalse();
        notification.Summary.Should().Contain("timeout");
        notification.AffectedEntityId.Should().BeNull();
    }
}
