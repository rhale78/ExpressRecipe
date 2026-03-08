using ExpressRecipe.RecipeService.Data;
using ExpressRecipe.Shared.DTOs.Recipe;

namespace ExpressRecipe.RecipeService.Services;

/// <summary>
/// Background service that drains the <see cref="IRecipeBatchChannel"/> and creates
/// each recipe in the database, then fires lifecycle events.
/// Single recipe creates go directly through the REST controller (sync + event).
/// </summary>
public sealed class RecipeBatchChannelWorker : BackgroundService
{
    private readonly IRecipeBatchChannel   _channel;
    private readonly IServiceScopeFactory  _scopeFactory;
    private readonly IRecipeEventPublisher _events;
    private readonly ILogger<RecipeBatchChannelWorker> _logger;

    public RecipeBatchChannelWorker(
        IRecipeBatchChannel   channel,
        IServiceScopeFactory  scopeFactory,
        IRecipeEventPublisher events,
        ILogger<RecipeBatchChannelWorker> logger)
    {
        _channel      = channel;
        _scopeFactory = scopeFactory;
        _events       = events;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[RecipeBatchChannelWorker] Started – waiting for batch recipe submissions");

        await foreach (var item in _channel.ReadAllAsync(stoppingToken))
        {
            await ProcessAsync(item, stoppingToken);
        }

        _logger.LogInformation("[RecipeBatchChannelWorker] Stopped");
    }

    private async Task ProcessAsync(RecipeBatchItem item, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var recipeRepo  = scope.ServiceProvider.GetRequiredService<IRecipeRepository>();

            // Use CreateRecipeAsync(userId, ...) overload instead of mutating the request object
            var recipeId = await recipeRepo.CreateRecipeAsync(item.Request, item.SubmittedBy);

            if (item.Request.Ingredients != null && item.Request.Ingredients.Count > 0)
            {
                var ingredients = item.Request.Ingredients.Select(i => new RecipeIngredientDto
                {
                    IngredientName  = i.Name,
                    Quantity        = i.Quantity,
                    Unit            = i.Unit,
                    OrderIndex      = i.OrderIndex,
                    PreparationNote = i.Notes,
                    IsOptional      = i.IsOptional
                }).ToList();
                await recipeRepo.AddRecipeIngredientsAsync(recipeId, ingredients, item.SubmittedBy);
            }

            if (item.Request.Steps != null && item.Request.Steps.Count > 0)
            {
                foreach (var step in item.Request.Steps)
                    await recipeRepo.AddInstructionAsync(recipeId, step.OrderIndex, step.Instruction, step.DurationMinutes);
            }

            if (item.Request.Tags != null && item.Request.Tags.Count > 0)
                await recipeRepo.AddRecipeTagsAsync(recipeId, item.Request.Tags);

            _logger.LogInformation(
                "[RecipeBatchChannelWorker] Created recipe {RecipeId} '{Name}' (session={Session}) by user {UserId}",
                recipeId, item.Request.Name, item.SessionId, item.SubmittedBy);

            await _events.PublishCreatedAsync(
                recipeId,
                item.Request.Name ?? string.Empty,
                item.Request.Category,
                item.Request.Cuisine,
                item.SubmittedBy,
                ct);
        }
        catch (OperationCanceledException) { /* shutting down */ }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[RecipeBatchChannelWorker] Failed to create recipe '{Name}': {Error}",
                item.Request.Name, ex.Message);
        }
    }
}
