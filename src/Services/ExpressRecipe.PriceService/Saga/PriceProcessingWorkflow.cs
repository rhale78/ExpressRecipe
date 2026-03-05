using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Saga.Builder;
using ExpressRecipe.PriceService.Messages;

namespace ExpressRecipe.PriceService.Saga;

/// <summary>
/// Defines the bit-flag DAG workflow for processing a single price observation:
///   Step 1 (bit 1): ProductLink – link the raw price staging record to a known Product
///   Step 2 (bit 2): Verify     – validate pricing data (range checks, duplicate detection)
///   Step 3 (bit 4): Publish    – write the verified price observation to the ProductPrice table
/// A price observation is complete when mask == 0b111 == 7.
/// </summary>
public static class PriceProcessingWorkflow
{
    public const string WorkflowName = "PriceProcessing";

    public static SagaWorkflowDefinition<PriceProcessingSagaState> Build()
    {
        return new SagaWorkflowBuilder<PriceProcessingSagaState>(WorkflowName)
            .AddStep("ProductLink")
                .Sends(s => new RequestPriceProductLink(
                    s.CorrelationId,
                    s.PriceStagingId ?? Guid.Empty,
                    s.Barcode,
                    s.ExternalProductId,
                    s.ProductName))
                .SendsTo("price.product-link")
                .OnResult<PriceLinkedToProduct>((state, result, ct) =>
                {
                    state.ProductId       = result.ProductId;
                    state.IsProductLinked = true;
                    state.WasExactMatch   = result.WasExactMatch;
                    return Task.FromResult(state);
                })
            .And()
            .AddStep("Verify")
                .DependsOn("ProductLink")
                .Sends(s => new RequestPriceVerification(
                    s.CorrelationId,
                    s.PriceStagingId ?? Guid.Empty,
                    s.ProductId,
                    s.Price))
                .SendsTo("price.verify")
                .OnResult<PriceVerified>((state, result, ct) =>
                {
                    state.IsVerified = result.IsValid;
                    return Task.FromResult(state);
                })
            .And()
            .AddStep("Publish")
                .DependsOn("Verify")
                .Sends(s => new RequestPricePublish(
                    s.CorrelationId,
                    s.PriceStagingId ?? Guid.Empty,
                    s.ProductId,
                    s.StoreId,
                    s.Price))
                .SendsTo("price.publish")
                .OnResult<PricePublished>((state, result, ct) =>
                {
                    state.PriceObservationId = result.PriceObservationId;
                    return Task.FromResult(state);
                })
            .And()
            .OnWorkflowCompleted(async (state, bus, ct) =>
            {
                if (state.PriceObservationId.HasValue && state.ProductId.HasValue && state.StoreId.HasValue)
                {
                    await bus.PublishAsync(new PriceObservationCompleted(
                        state.CorrelationId,
                        state.PriceObservationId.Value,
                        state.ProductId.Value,
                        state.StoreId.Value,
                        state.Price ?? 0m,
                        DateTimeOffset.UtcNow), cancellationToken: ct);
                }
            })
            .OnWorkflowFailed(async (state, ex, bus, ct) =>
            {
                await bus.PublishAsync(new PriceLinkFailed(
                    state.CorrelationId,
                    state.PriceStagingId ?? Guid.Empty,
                    ex.Message,
                    DateTimeOffset.UtcNow), cancellationToken: ct);
            })
            .Build();
    }
}
