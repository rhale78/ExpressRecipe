using ExpressRecipe.Messaging.Saga.Builder;
using ExpressRecipe.Shared.Messages;

namespace ExpressRecipe.SafeForkService.Saga;

public static class AllergenResolutionWorkflow
{
    public const string WorkflowName = "AllergenResolution";

    public static SagaWorkflowDefinition<AllergenResolutionSagaState> Build()
    {
        return new SagaWorkflowBuilder<AllergenResolutionSagaState>(WorkflowName)
            .AddStep("IngredientLookup")
                .Sends(s => new RequestAllergenIngredientLookup(
                    s.CorrelationId,
                    s.AllergenProfileId,
                    s.FreeFormText,
                    s.Brand))
                .SendsTo(AllergenResolutionKeys.IngredientLookup)
                .OnResult<AllergenIngredientLookupResult>((state, result, ct) =>
                {
                    state.IngredientId = result.IngredientId;
                    state.MatchMethod = result.MatchMethod;
                    return Task.FromResult(state);
                })
            .And()
            .AddStep("ProductLookup")
                .DependsOn("IngredientLookup")
                .Sends(s => new RequestProductLookup(
                    s.CorrelationId,
                    s.AllergenProfileId,
                    s.FreeFormText,
                    s.Brand,
                    s.IngredientId))
                .SendsTo(AllergenResolutionKeys.ProductLookup)
                .OnResult<ProductLookupResult>((state, result, ct) =>
                {
                    state.ProductId = result.ProductId;
                    if (state.MatchMethod == null)
                    {
                        state.MatchMethod = result.MatchMethod;
                    }
                    return Task.FromResult(state);
                })
            .And()
            .AddStep("GraphWalk")
                .DependsOn("ProductLookup")
                .Sends(s => new RequestIngredientGraphWalk(
                    s.CorrelationId,
                    s.AllergenProfileId,
                    s.ProductId ?? Guid.Empty))
                .SendsTo(AllergenResolutionKeys.IngredientGraphWalk)
                .OnResult<IngredientGraphWalkResult>((state, result, ct) =>
                {
                    state.LinksWritten = result.LinksWritten;
                    return Task.FromResult(state);
                })
            .And()
            .AddStep("PersistResult")
                .DependsOn("ProductLookup")
                .DependsOn("GraphWalk")
                .Sends(s => new RequestPersistResolution(
                    s.CorrelationId,
                    s.AllergenProfileId,
                    s.LinksWritten > 0 || s.IngredientId.HasValue))
                .SendsTo(AllergenResolutionKeys.PersistResolution)
                .OnResult<ResolutionPersisted>()
            .And()
            .OnWorkflowCompleted(async (state, bus, ct) =>
            {
                if (state.LinksWritten > 0 || state.IngredientId.HasValue)
                {
                    await bus.PublishAsync(new AllergenProfileFreeformResolvedEvent(
                        state.MemberId,
                        state.AllergenProfileId,
                        state.FreeFormText,
                        state.LinksWritten,
                        DateTimeOffset.UtcNow), cancellationToken: ct);
                }

                await bus.PublishAsync(new SagaCompletedNotification(
                    WorkflowName,
                    state.CorrelationId,
                    Succeeded: true,
                    Summary: $"Allergen resolution completed with {state.LinksWritten} links for '{state.FreeFormText}'",
                    AffectedEntityId: state.AllergenProfileId,
                    DateTimeOffset.UtcNow), cancellationToken: ct);
            })
            .OnWorkflowFailed(async (state, ex, bus, ct) =>
            {
                state.LastError = ex.Message;

                await bus.PublishAsync(new SagaCompletedNotification(
                    WorkflowName,
                    state.CorrelationId,
                    Succeeded: false,
                    Summary: $"Allergen resolution failed for '{state.FreeFormText}': {ex.Message}",
                    AffectedEntityId: state.AllergenProfileId,
                    DateTimeOffset.UtcNow), cancellationToken: ct);
            })
            .Build();
    }
}
