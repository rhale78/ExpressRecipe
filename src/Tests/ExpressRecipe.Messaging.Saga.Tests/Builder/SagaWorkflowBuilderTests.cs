using ExpressRecipe.Messaging.Saga.Abstractions;
using ExpressRecipe.Messaging.Saga.Builder;
using ExpressRecipe.Messaging.Saga.Tests.Helpers;
using Xunit;

namespace ExpressRecipe.Messaging.Saga.Tests.Builder;

public sealed class SagaWorkflowBuilderTests
{
    [Fact]
    public void Build_SingleStep_HasCorrectBit()
    {
        var builder = new SagaWorkflowBuilder<DocumentProcessingState>("Test");
        builder.AddStep("VirusScan")
            .Sends(s => new StartVirusScanCommand(s.DocumentId))
            .OnResult<VirusScanCompleted>();
        var workflow = builder.Build();

        Assert.Single(workflow.Steps);
        Assert.Equal(1L, workflow.Steps[0].Bit);
        Assert.Equal(1L, workflow.CompletionMask);
    }

    [Fact]
    public void Build_TwoSteps_HaveIncreasingBits()
    {
        var builder = new SagaWorkflowBuilder<DocumentProcessingState>("Test");
        builder.AddStep("Step1")
            .Sends(s => new StartVirusScanCommand(s.DocumentId))
            .OnResult<VirusScanCompleted>();
        builder.AddStep("Step2")
            .Sends(s => new ValidateContentCommand(s.DocumentId))
            .OnResult<ContentValidated>();
        var workflow = builder.Build();

        Assert.Equal(1L, workflow.Steps[0].Bit);
        Assert.Equal(2L, workflow.Steps[1].Bit);
        Assert.Equal(3L, workflow.CompletionMask); // 0b11
    }

    [Fact]
    public void Build_StepWithDependency_HasCorrectDependencyMask()
    {
        var builder = new SagaWorkflowBuilder<DocumentProcessingState>("Test");
        builder.AddStep("VirusScan")
            .Sends(s => new StartVirusScanCommand(s.DocumentId))
            .OnResult<VirusScanCompleted>();
        builder.AddStep("ValidateContent")
            .DependsOn("VirusScan")
            .Sends(s => new ValidateContentCommand(s.DocumentId))
            .OnResult<ContentValidated>();
        var workflow = builder.Build();

        var virusScanStep = workflow.Steps[0];
        var validateStep = workflow.Steps[1];

        Assert.Equal(0L, virusScanStep.DependencyMask);  // no deps
        Assert.Equal(1L, validateStep.DependencyMask);    // depends on bit 1 (VirusScan)
    }

    [Fact]
    public void Build_UnknownDependency_ThrowsInvalidOperationException()
    {
        var builder = new SagaWorkflowBuilder<DocumentProcessingState>("Test");
        builder.AddStep("Step1")
            .DependsOn("NonExistentStep")
            .Sends(s => new StartVirusScanCommand(s.DocumentId))
            .OnResult<VirusScanCompleted>();

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void GetEligibleSteps_NoCompletions_ReturnsStepsWithNoDependencies()
    {
        var builder = new SagaWorkflowBuilder<DocumentProcessingState>("Test");
        builder.AddStep("VirusScan").Sends(s => new StartVirusScanCommand(s.DocumentId)).OnResult<VirusScanCompleted>();
        builder.AddStep("GenerateThumbnail").Sends(s => new GenerateThumbnailCommand(s.DocumentId)).OnResult<ThumbnailGenerated>();
        builder.AddStep("ValidateContent").DependsOn("VirusScan").Sends(s => new ValidateContentCommand(s.DocumentId)).OnResult<ContentValidated>();
        var workflow = builder.Build();

        var eligible = workflow.GetEligibleSteps(0L).ToList();

        Assert.Equal(2, eligible.Count); // VirusScan and GenerateThumbnail (no deps)
        Assert.DoesNotContain(eligible, s => s.Name == "ValidateContent"); // has dep
    }

    [Fact]
    public void GetEligibleSteps_AfterVirusScan_UnlocksValidateContent()
    {
        var builder = new SagaWorkflowBuilder<DocumentProcessingState>("Test");
        builder.AddStep("VirusScan").Sends(s => new StartVirusScanCommand(s.DocumentId)).OnResult<VirusScanCompleted>();
        builder.AddStep("ValidateContent").DependsOn("VirusScan").Sends(s => new ValidateContentCommand(s.DocumentId)).OnResult<ContentValidated>();
        var workflow = builder.Build();

        long maskAfterVirusScan = workflow.Steps[0].Bit; // bit 1 set
        var eligible = workflow.GetEligibleSteps(maskAfterVirusScan).ToList();

        Assert.Single(eligible);
        Assert.Equal("ValidateContent", eligible[0].Name);
    }

    [Fact]
    public void IsComplete_AllBitsSet_ReturnsTrue()
    {
        var builder = new SagaWorkflowBuilder<DocumentProcessingState>("Test");
        builder.AddStep("Step1").Sends(s => new StartVirusScanCommand(s.DocumentId)).OnResult<VirusScanCompleted>();
        builder.AddStep("Step2").Sends(s => new ValidateContentCommand(s.DocumentId)).OnResult<ContentValidated>();
        var workflow = builder.Build();

        Assert.True(workflow.IsComplete(3L));  // 0b11 = all bits set
        Assert.False(workflow.IsComplete(1L)); // only step 1 done
        Assert.False(workflow.IsComplete(0L)); // nothing done
    }

    [Fact]
    public void MultipleParallelPaths_DependentStep_UnlocksOnlyWhenAllParentsDone()
    {
        // VirusScan ──┐
        //              ├──→ Index (depends on both)
        // GenerateThumbnail ──┘
        var builder = new SagaWorkflowBuilder<DocumentProcessingState>("Test");
        builder.AddStep("VirusScan").Sends(s => new StartVirusScanCommand(s.DocumentId)).OnResult<VirusScanCompleted>();
        builder.AddStep("GenerateThumbnail").Sends(s => new GenerateThumbnailCommand(s.DocumentId)).OnResult<ThumbnailGenerated>();
        builder.AddStep("Index").DependsOn("VirusScan", "GenerateThumbnail").Sends(s => new IndexDocumentCommand(s.DocumentId, s.Title)).OnResult<DocumentIndexed>();
        var workflow = builder.Build();

        long virusBit = workflow.Steps[0].Bit;
        long thumbBit = workflow.Steps[1].Bit;

        // Only VirusScan done - Index should NOT be eligible
        var eligibleAfterVirus = workflow.GetEligibleSteps(virusBit).ToList();
        Assert.DoesNotContain(eligibleAfterVirus, s => s.Name == "Index");

        // Both done - Index should now be eligible
        var eligibleAfterBoth = workflow.GetEligibleSteps(virusBit | thumbBit).ToList();
        Assert.Contains(eligibleAfterBoth, s => s.Name == "Index");
    }

    [Fact]
    public void WorkflowName_IsPreserved()
    {
        var workflow = new SagaWorkflowBuilder<DocumentProcessingState>("MyWorkflow").Build();
        Assert.Equal("MyWorkflow", workflow.WorkflowName);
    }

    [Fact]
    public void OnResult_WithHandler_StoresResultHandler()
    {
        var builder = new SagaWorkflowBuilder<DocumentProcessingState>("Test");
        builder.AddStep("VirusScan")
            .Sends(s => new StartVirusScanCommand(s.DocumentId))
            .OnResult<VirusScanCompleted>((state, result, ct) =>
            {
                state.IsClean = result.IsClean;
                return Task.FromResult(state);
            });
        var workflow = builder.Build();

        Assert.NotNull(workflow.Steps[0].ResultHandler);
    }
}
