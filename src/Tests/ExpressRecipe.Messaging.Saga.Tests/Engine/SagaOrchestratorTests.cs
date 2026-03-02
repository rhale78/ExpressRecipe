using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.Messaging.Core.Options;
using ExpressRecipe.Messaging.Saga.Abstractions;
using ExpressRecipe.Messaging.Saga.BatchWriter;
using ExpressRecipe.Messaging.Saga.Builder;
using ExpressRecipe.Messaging.Saga.Engine;
using ExpressRecipe.Messaging.Saga.Persistence;
using ExpressRecipe.Messaging.Saga.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ExpressRecipe.Messaging.Saga.Tests.Engine;

public sealed class SagaOrchestratorTests : IAsyncDisposable
{
    private readonly InMemorySagaRepository<DocumentProcessingState> _repository;
    private readonly Mock<IMessageBus> _busMock;
    private readonly SagaBatchWriter<DocumentProcessingState> _writer;
    private SagaOrchestrator<DocumentProcessingState>? _orchestrator;

    public SagaOrchestratorTests()
    {
        _repository = new InMemorySagaRepository<DocumentProcessingState>();
        _busMock = new Mock<IMessageBus>();
        var opts = new SagaBatchWriterOptions { CoalescingDelay = TimeSpan.Zero, MaxBatchSize = 100 };
        _writer = new SagaBatchWriter<DocumentProcessingState>(_repository, opts);
    }

    public async ValueTask DisposeAsync()
    {
        if (_orchestrator is not null)
            await _orchestrator.DisposeAsync();
        await _writer.DisposeAsync();
    }

    private SagaWorkflowDefinition<DocumentProcessingState> BuildSingleStepWorkflow()
    {
        var builder = new SagaWorkflowBuilder<DocumentProcessingState>("SingleStep");
        builder.AddStep("VirusScan")
            .Sends(s => new StartVirusScanCommand(s.DocumentId))
            .OnResult<VirusScanCompleted>();
        return builder.Build();
    }

    private SagaWorkflowDefinition<DocumentProcessingState> BuildTwoStepWorkflow()
    {
        var builder = new SagaWorkflowBuilder<DocumentProcessingState>("TwoStep");
        builder.AddStep("VirusScan")
            .Sends(s => new StartVirusScanCommand(s.DocumentId))
            .OnResult<VirusScanCompleted>();
        builder.AddStep("ValidateContent")
            .DependsOn("VirusScan")
            .Sends(s => new ValidateContentCommand(s.DocumentId))
            .OnResult<ContentValidated>();
        return builder.Build();
    }

    [Fact]
    public async Task StartAsync_SavesStateAsRunning()
    {
        _busMock.Setup(b => b.SubscribeAsync(
            It.IsAny<Func<VirusScanCompleted, MessageContext, CancellationToken, Task>>(),
            It.IsAny<SubscribeOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _busMock.Setup(b => b.SendAsync(
            It.IsAny<StartVirusScanCommand>(),
            It.IsAny<string>(),
            It.IsAny<SendOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var workflow = BuildSingleStepWorkflow();
        _orchestrator = new SagaOrchestrator<DocumentProcessingState>(
            workflow, _repository, _busMock.Object, _writer,
            NullLogger<SagaOrchestrator<DocumentProcessingState>>.Instance);

        await _orchestrator.InitializeAsync();

        var state = new DocumentProcessingState
        {
            CorrelationId = "saga-001",
            DocumentId = "doc-001",
            Title = "Test Document"
        };
        await _orchestrator.StartAsync(state);

        var loaded = await _repository.LoadAsync("saga-001");
        Assert.NotNull(loaded);
        Assert.Equal(SagaStatus.Running, loaded!.Status);
        Assert.Equal(0L, loaded.CurrentMask); // no steps done yet
    }

    [Fact]
    public async Task StartAsync_SingleStepNoCommand_Dispatches()
    {
        _busMock.Setup(b => b.SubscribeAsync(
            It.IsAny<Func<VirusScanCompleted, MessageContext, CancellationToken, Task>>(),
            It.IsAny<SubscribeOptions?>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        StartVirusScanCommand? capturedCommand = null;
        _busMock.Setup(b => b.SendAsync(
            It.IsAny<StartVirusScanCommand>(),
            It.IsAny<string>(),
            It.IsAny<SendOptions?>(),
            It.IsAny<CancellationToken>()))
            .Callback<StartVirusScanCommand, string, SendOptions?, CancellationToken>((cmd, _, _, _) => capturedCommand = cmd)
            .Returns(Task.CompletedTask);

        var workflow = BuildSingleStepWorkflow();
        _orchestrator = new SagaOrchestrator<DocumentProcessingState>(
            workflow, _repository, _busMock.Object, _writer,
            NullLogger<SagaOrchestrator<DocumentProcessingState>>.Instance);

        await _orchestrator.InitializeAsync();

        var state = new DocumentProcessingState
        {
            CorrelationId = "saga-002",
            DocumentId = "doc-002",
            Title = "Test"
        };
        await _orchestrator.StartAsync(state);

        Assert.NotNull(capturedCommand);
        Assert.Equal("doc-002", capturedCommand!.DocumentId);
    }

    [Fact]
    public void WorkflowDefinition_BitMasks_AreCorrect()
    {
        var workflow = BuildTwoStepWorkflow();

        var virusScanStep = workflow.Steps.Single(s => s.Name == "VirusScan");
        var validateStep = workflow.Steps.Single(s => s.Name == "ValidateContent");

        Assert.Equal(1L, virusScanStep.Bit);
        Assert.Equal(2L, validateStep.Bit);
        Assert.Equal(0L, virusScanStep.DependencyMask);
        Assert.Equal(1L, validateStep.DependencyMask); // depends on VirusScan
        Assert.Equal(3L, workflow.CompletionMask); // all steps
    }
}
