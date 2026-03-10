using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ExpressRecipe.MealPlanningService.Data;
using ExpressRecipe.MealPlanningService.Workers;

namespace ExpressRecipe.MealPlanningService.Tests.Workers;

public class WorkQueueCleanupWorkerTests
{
    private readonly Mock<IWorkQueueRepository> _mockQueue;

    public WorkQueueCleanupWorkerTests()
    {
        _mockQueue = new Mock<IWorkQueueRepository>();
    }

    private WorkQueueCleanupWorker CreateWorker()
        => new(_mockQueue.Object, NullLogger<WorkQueueCleanupWorker>.Instance);

    [Fact]
    public async Task ExecuteAsync_CallsExpireStaleItemsAndStops()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(1));

        _mockQueue
            .Setup(q => q.ExpireStaleItemsAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        WorkQueueCleanupWorker worker = CreateWorker();

        // Start the worker, let it run for a moment, then cancel
        await worker.StartAsync(cts.Token);
        await Task.Delay(200, CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        _mockQueue.Verify(q => q.ExpireStaleItemsAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExpireStaleItems_WhenRepositoryThrows_DoesNotPropagateException()
    {
        _mockQueue
            .Setup(q => q.ExpireStaleItemsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        WorkQueueCleanupWorker worker = CreateWorker();

        // Use reflection to call the protected ExecuteAsync indirectly via StartAsync
        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(300));
        Func<Task> act = async () =>
        {
            await worker.StartAsync(cts.Token);
            await Task.Delay(100, CancellationToken.None);
        };

        // The worker should catch exceptions internally and not crash
        await act.Should().NotThrowAsync();
    }
}
