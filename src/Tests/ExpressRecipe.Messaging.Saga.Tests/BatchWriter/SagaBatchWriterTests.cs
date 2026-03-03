using ExpressRecipe.Messaging.Saga.Abstractions;
using ExpressRecipe.Messaging.Saga.BatchWriter;
using ExpressRecipe.Messaging.Saga.Persistence;
using ExpressRecipe.Messaging.Saga.Tests.Helpers;
using Xunit;

namespace ExpressRecipe.Messaging.Saga.Tests.BatchWriter;

public sealed class SagaBatchWriterTests
{
    private static DocumentProcessingState CreateState(string id) => new()
    {
        CorrelationId = id,
        DocumentId = id,
        Status = SagaStatus.Running,
        StartedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task EnqueueMaskUpdate_FlushesToRepository()
    {
        var repo = new InMemorySagaRepository<DocumentProcessingState>();
        await repo.SaveAsync(CreateState("corr-1"));

        var options = new SagaBatchWriterOptions { CoalescingDelay = TimeSpan.Zero, MaxBatchSize = 10 };
        await using var writer = new SagaBatchWriter<DocumentProcessingState>(repo, options);

        await writer.EnqueueMaskUpdateAsync("corr-1", 0b01L);
        await writer.CompleteAsync();

        var state = await repo.LoadAsync("corr-1");
        Assert.NotNull(state);
        Assert.Equal(0b01L, state!.CurrentMask);
    }

    [Fact]
    public async Task MultipleMaskUpdates_AreBatchedAndCoalesced()
    {
        var repo = new InMemorySagaRepository<DocumentProcessingState>();
        await repo.SaveAsync(CreateState("corr-A"));
        await repo.SaveAsync(CreateState("corr-B"));

        var options = new SagaBatchWriterOptions { CoalescingDelay = TimeSpan.FromMilliseconds(10), MaxBatchSize = 100 };
        await using var writer = new SagaBatchWriter<DocumentProcessingState>(repo, options);

        // Enqueue multiple updates for same ID - should be coalesced by OR
        await writer.EnqueueMaskUpdateAsync("corr-A", 0b01L);
        await writer.EnqueueMaskUpdateAsync("corr-A", 0b10L);
        await writer.EnqueueMaskUpdateAsync("corr-B", 0b11L);
        await writer.CompleteAsync();

        var stateA = await repo.LoadAsync("corr-A");
        var stateB = await repo.LoadAsync("corr-B");

        Assert.Equal(0b11L, stateA!.CurrentMask); // 0b01 | 0b10 = 0b11
        Assert.Equal(0b11L, stateB!.CurrentMask);
    }

    [Fact]
    public async Task EnqueueStatusUpdate_UpdatesStatusInRepository()
    {
        var repo = new InMemorySagaRepository<DocumentProcessingState>();
        await repo.SaveAsync(CreateState("corr-1"));

        var options = new SagaBatchWriterOptions { CoalescingDelay = TimeSpan.Zero };
        await using var writer = new SagaBatchWriter<DocumentProcessingState>(repo, options);

        await writer.EnqueueStatusUpdateAsync("corr-1", SagaStatus.Completed, DateTimeOffset.UtcNow);
        await writer.CompleteAsync();

        var state = await repo.LoadAsync("corr-1");
        Assert.Equal(SagaStatus.Completed, state!.Status);
    }

    [Fact]
    public async Task EnqueueAsync_CombinedMaskAndStatus_UpdatesBoth()
    {
        var repo = new InMemorySagaRepository<DocumentProcessingState>();
        await repo.SaveAsync(CreateState("corr-1"));

        var options = new SagaBatchWriterOptions { CoalescingDelay = TimeSpan.Zero };
        await using var writer = new SagaBatchWriter<DocumentProcessingState>(repo, options);

        await writer.EnqueueAsync("corr-1", 0b111L, SagaStatus.Completed, DateTimeOffset.UtcNow);
        await writer.CompleteAsync();

        var state = await repo.LoadAsync("corr-1");
        Assert.Equal(0b111L, state!.CurrentMask);
        Assert.Equal(SagaStatus.Completed, state.Status);
    }
}
