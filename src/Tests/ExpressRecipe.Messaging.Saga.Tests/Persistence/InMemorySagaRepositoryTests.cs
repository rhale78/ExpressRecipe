using ExpressRecipe.Messaging.Saga.Abstractions;
using ExpressRecipe.Messaging.Saga.Persistence;
using ExpressRecipe.Messaging.Saga.Tests.Helpers;
using Xunit;

namespace ExpressRecipe.Messaging.Saga.Tests.Persistence;

public sealed class InMemorySagaRepositoryTests
{
    private static DocumentProcessingState CreateState(string id, long mask = 0) => new()
    {
        CorrelationId = id,
        DocumentId = id,
        CurrentMask = mask,
        Status = SagaStatus.Running,
        StartedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task SaveAndLoad_RoundTrips()
    {
        var repo = new InMemorySagaRepository<DocumentProcessingState>();
        var state = CreateState("test-1");
        await repo.SaveAsync(state);

        var loaded = await repo.LoadAsync("test-1");
        Assert.NotNull(loaded);
        Assert.Equal("test-1", loaded!.CorrelationId);
    }

    [Fact]
    public async Task Load_NonExistent_ReturnsNull()
    {
        var repo = new InMemorySagaRepository<DocumentProcessingState>();
        var loaded = await repo.LoadAsync("non-existent");
        Assert.Null(loaded);
    }

    [Fact]
    public async Task BatchLoad_ReturnsAllFound()
    {
        var repo = new InMemorySagaRepository<DocumentProcessingState>();
        await repo.SaveAsync(CreateState("a"));
        await repo.SaveAsync(CreateState("b"));
        await repo.SaveAsync(CreateState("c"));

        var loaded = await repo.BatchLoadAsync(["a", "c", "unknown"]);
        Assert.Equal(2, loaded.Count);
        Assert.Contains(loaded, s => s.CorrelationId == "a");
        Assert.Contains(loaded, s => s.CorrelationId == "c");
    }

    [Fact]
    public async Task BatchUpdateMask_OrsIntoCurrent()
    {
        var repo = new InMemorySagaRepository<DocumentProcessingState>();
        await repo.SaveAsync(CreateState("a", mask: 0b01));
        await repo.SaveAsync(CreateState("b", mask: 0b10));

        var results = await repo.BatchUpdateMaskAsync([("a", 0b10L), ("b", 0b01L)]);

        var stateA = await repo.LoadAsync("a");
        var stateB = await repo.LoadAsync("b");
        Assert.Equal(0b11L, stateA!.CurrentMask);
        Assert.Equal(0b11L, stateB!.CurrentMask);
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task BatchUpdateStatus_UpdatesStatusFields()
    {
        var repo = new InMemorySagaRepository<DocumentProcessingState>();
        await repo.SaveAsync(CreateState("a"));
        await repo.SaveAsync(CreateState("b"));

        var now = DateTimeOffset.UtcNow;
        await repo.BatchUpdateStatusAsync([
            ("a", SagaStatus.Completed, now),
            ("b", SagaStatus.Failed, null)
        ]);

        var stateA = await repo.LoadAsync("a");
        var stateB = await repo.LoadAsync("b");

        Assert.Equal(SagaStatus.Completed, stateA!.Status);
        Assert.NotNull(stateA.CompletedAt);
        Assert.Equal(SagaStatus.Failed, stateB!.Status);
        Assert.Null(stateB.CompletedAt);
    }
}

public sealed class SqlSagaRepositoryConstructorTests
{
    [Theory]
    [InlineData("MyTable")]
    [InlineData("saga_state")]
    [InlineData("DocProcessingState")]
    [InlineData("_internal")]
    public void Constructor_ValidTableName_DoesNotThrow(string tableName)
    {
        var ex = Record.Exception(() => new SqlSagaRepository<DocumentProcessingState>("server=.;", tableName));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("my table")]
    [InlineData("table; DROP TABLE Users--")]
    [InlineData("table'name")]
    [InlineData("1leading_digit")]
    [InlineData("")]
    public void Constructor_InvalidTableName_ThrowsArgumentException(string tableName)
    {
        Assert.Throws<ArgumentException>(() => new SqlSagaRepository<DocumentProcessingState>("server=.;", tableName));
    }
}
