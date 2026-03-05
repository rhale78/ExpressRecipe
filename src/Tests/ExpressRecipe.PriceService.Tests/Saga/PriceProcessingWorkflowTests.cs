using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.Messaging.Saga.Builder;
using ExpressRecipe.PriceService.Messages;
using ExpressRecipe.PriceService.Saga;
using FluentAssertions;
using Xunit;

namespace ExpressRecipe.PriceService.Tests.Saga;

public class PriceProcessingWorkflowTests
{
    private readonly SagaWorkflowDefinition<PriceProcessingSagaState> _workflow;

    public PriceProcessingWorkflowTests()
    {
        _workflow = PriceProcessingWorkflow.Build();
    }

    [Fact]
    public void Build_WorkflowName_IsPriceProcessing()
    {
        _workflow.WorkflowName.Should().Be(PriceProcessingWorkflow.WorkflowName);
        _workflow.WorkflowName.Should().Be("PriceProcessing");
    }

    [Fact]
    public void Build_HasThreeSteps_ProductLinkVerifyPublish()
    {
        _workflow.Steps.Should().HaveCount(3);
        _workflow.Steps[0].Name.Should().Be("ProductLink");
        _workflow.Steps[1].Name.Should().Be("Verify");
        _workflow.Steps[2].Name.Should().Be("Publish");
    }

    [Fact]
    public void Build_StepBits_AreDistinctPowersOfTwo()
    {
        var bits = _workflow.Steps.Select(s => s.Bit).ToList();
        bits.Should().OnlyHaveUniqueItems();
        foreach (var bit in bits)
        {
            // Each bit must be a power of two
            (bit & (bit - 1)).Should().Be(0);
        }
    }

    [Fact]
    public void Build_CompletionMask_EqualsUnionOfAllBits()
    {
        var expected = _workflow.Steps.Aggregate(0L, (acc, s) => acc | s.Bit);
        _workflow.CompletionMask.Should().Be(expected);
        // 3 steps → bits 1,2,4 → mask = 7
        _workflow.CompletionMask.Should().Be(7L);
    }

    [Fact]
    public void Build_VerifyStep_DependsOnProductLink()
    {
        var verifyStep = _workflow.Steps.First(s => s.Name == "Verify");
        verifyStep.DependencyMask.Should().Be(_workflow.Steps.First(s => s.Name == "ProductLink").Bit);
    }

    [Fact]
    public void Build_PublishStep_DependsOnVerify()
    {
        var publishStep = _workflow.Steps.First(s => s.Name == "Publish");
        publishStep.DependencyMask.Should().Be(_workflow.Steps.First(s => s.Name == "Verify").Bit);
    }

    [Fact]
    public void Build_AllStepsHaveCommandFactories()
    {
        foreach (var step in _workflow.Steps)
        {
            step.CommandFactory.Should().NotBeNull($"step '{step.Name}' must have a command factory");
        }
    }

    [Fact]
    public void Build_AllStepsHaveResultTypes()
    {
        foreach (var step in _workflow.Steps)
        {
            step.ResultType.Should().NotBeNull($"step '{step.Name}' must declare a result type");
        }
    }

    [Fact]
    public void Build_ProductLinkStep_SendsRequestPriceProductLink()
    {
        var step = _workflow.Steps.First(s => s.Name == "ProductLink");
        var state = new PriceProcessingSagaState
        {
            CorrelationId  = "corr-1",
            PriceStagingId = Guid.NewGuid(),
            Barcode        = "012345678901",
            ProductName    = "Test Product"
        };

        var (command, cmdType) = step.CommandFactory!(state);

        cmdType.Should().Be(typeof(RequestPriceProductLink));
        var cmd = command.Should().BeOfType<RequestPriceProductLink>().Subject;
        cmd.CorrelationId.Should().Be("corr-1");
        cmd.Barcode.Should().Be("012345678901");
    }

    [Fact]
    public void Build_VerifyStep_SendsRequestPriceVerification()
    {
        var step = _workflow.Steps.First(s => s.Name == "Verify");
        var productId = Guid.NewGuid();
        var stagingId = Guid.NewGuid();
        var state = new PriceProcessingSagaState
        {
            CorrelationId  = "corr-2",
            PriceStagingId = stagingId,
            ProductId      = productId,
            Price          = 9.99m
        };

        var (command, cmdType) = step.CommandFactory!(state);

        cmdType.Should().Be(typeof(RequestPriceVerification));
        var cmd = command.Should().BeOfType<RequestPriceVerification>().Subject;
        cmd.CorrelationId.Should().Be("corr-2");
        cmd.ProductId.Should().Be(productId);
        cmd.Price.Should().Be(9.99m);
    }

    [Fact]
    public void Build_PublishStep_SendsRequestPricePublish()
    {
        var step = _workflow.Steps.First(s => s.Name == "Publish");
        var productId = Guid.NewGuid();
        var storeId   = Guid.NewGuid();
        var stagingId = Guid.NewGuid();
        var state = new PriceProcessingSagaState
        {
            CorrelationId  = "corr-3",
            PriceStagingId = stagingId,
            ProductId      = productId,
            StoreId        = storeId,
            Price          = 4.49m
        };

        var (command, cmdType) = step.CommandFactory!(state);

        cmdType.Should().Be(typeof(RequestPricePublish));
        var cmd = command.Should().BeOfType<RequestPricePublish>().Subject;
        cmd.CorrelationId.Should().Be("corr-3");
        cmd.ProductId.Should().Be(productId);
        cmd.StoreId.Should().Be(storeId);
        cmd.Price.Should().Be(4.49m);
    }

    [Fact]
    public async Task ProductLinkStep_OnResult_UpdatesState()
    {
        var step = _workflow.Steps.First(s => s.Name == "ProductLink");
        step.ResultHandler.Should().NotBeNull();

        var state     = new PriceProcessingSagaState { CorrelationId = "x" };
        var productId = Guid.NewGuid();
        var result    = new PriceLinkedToProduct("x", Guid.NewGuid(), productId, true, DateTimeOffset.UtcNow);

        var updated = await step.ResultHandler!(state, result, CancellationToken.None);

        updated.ProductId.Should().Be(productId);
        updated.IsProductLinked.Should().BeTrue();
        updated.WasExactMatch.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyStep_OnResult_UpdatesIsVerified()
    {
        var step = _workflow.Steps.First(s => s.Name == "Verify");
        step.ResultHandler.Should().NotBeNull();

        var state  = new PriceProcessingSagaState { CorrelationId = "x" };
        var result = new PriceVerified("x", Guid.NewGuid(), true, null, DateTimeOffset.UtcNow);

        var updated = await step.ResultHandler!(state, result, CancellationToken.None);

        updated.IsVerified.Should().BeTrue();
    }

    [Fact]
    public async Task PublishStep_OnResult_UpdatesPriceObservationId()
    {
        var step = _workflow.Steps.First(s => s.Name == "Publish");
        step.ResultHandler.Should().NotBeNull();

        var state      = new PriceProcessingSagaState { CorrelationId = "x" };
        var obsId      = Guid.NewGuid();
        var result     = new PricePublished("x", Guid.NewGuid(), obsId, DateTimeOffset.UtcNow);

        var updated = await step.ResultHandler!(state, result, CancellationToken.None);

        updated.PriceObservationId.Should().Be(obsId);
    }
}
