using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Saga.Builder;
using ExpressRecipe.ProductService.Messages;
using ExpressRecipe.Shared.Messages;

namespace ExpressRecipe.ProductService.Saga;

/// <summary>
/// Defines the bit-flag DAG workflow for processing a single product:
///   Step 1 (bit 1): AIVerification – local parsing validation
///   Step 2 (bit 2): Enrichment    – ingredient linking, allergen resolution
///   Step 3 (bit 4): Published     – written to Product table
/// A product is complete when mask == 0b111 == 7.
/// On completion a <see cref="SagaCompletedNotification"/> is also published so that the
/// NotificationService can alert users (subscribe when the NotificationService is ready).
/// </summary>
public static class ProductProcessingWorkflow
{
    public const string WorkflowName = "ProductProcessing";

    public static SagaWorkflowDefinition<ProductProcessingSagaState> Build()
    {
        return new SagaWorkflowBuilder<ProductProcessingSagaState>(WorkflowName)
            .AddStep("AIVerification")
                .Sends(s => new RequestProductAIVerification(
                    s.CorrelationId, s.StagingId,
                    s.ProductName,
                    // IngredientsText, Allergens, Categories are not yet available at this stage;
                    // the AI verifier fetches them directly from the staging record by StagingId.
                    null, null, null))
                .SendsTo("product.ai-verification")
                .OnResult<ProductAIVerified>((state, result, ct) =>
                {
                    state.AIVerificationPassed = result.IsValid;
                    state.AIVerificationNotes = result.ValidationNotes;
                    return Task.FromResult(state);
                })
            .And()
            .AddStep("Enrichment")
                .DependsOn("AIVerification")
                .Sends(s => new RequestProductEnrichment(s.CorrelationId, s.StagingId))
                .SendsTo("product.enrichment")
                .OnResult<ProductEnriched>((state, result, ct) =>
                {
                    state.ProductId = result.ProductId;
                    return Task.FromResult(state);
                })
            .And()
            .AddStep("Published")
                .DependsOn("Enrichment")
                .Sends(s => new RequestProductPublish(s.CorrelationId, s.StagingId))
                .SendsTo("product.publish")
                .OnResult<ProductPublished>()
            .And()
            .OnWorkflowCompleted(async (state, bus, ct) =>
            {
                if (state.ProductId.HasValue)
                {
                    // Primary domain event
                    await bus.PublishAsync(new ProductPublished(
                        state.CorrelationId,
                        state.ProductId.Value,
                        state.ExternalId ?? string.Empty,
                        state.Barcode,
                        DateTimeOffset.UtcNow), cancellationToken: ct);

                    // Notification event: informs NotificationService (subscribe when ready)
                    var summary = $"Product '{state.ProductName ?? state.ProductId.ToString()}' processed successfully and is now available.";
                    await bus.PublishAsync(new SagaCompletedNotification(
                        WorkflowName,
                        state.CorrelationId,
                        Succeeded: true,
                        summary,
                        AffectedEntityId: state.ProductId,
                        DateTimeOffset.UtcNow), cancellationToken: ct);
                }
            })
            .OnWorkflowFailed(async (state, ex, bus, ct) =>
            {
                await bus.PublishAsync(new ProductFailed(
                    state.CorrelationId,
                    state.StagingId,
                    "WorkflowFailed",
                    ex.Message,
                    DateTimeOffset.UtcNow), cancellationToken: ct);

                // Notification event for failure
                await bus.PublishAsync(new SagaCompletedNotification(
                    WorkflowName,
                    state.CorrelationId,
                    Succeeded: false,
                    Summary: $"Product processing failed: {ex.Message}",
                    AffectedEntityId: state.ProductId,
                    DateTimeOffset.UtcNow), cancellationToken: ct);
            })
            .Build();
    }
}

