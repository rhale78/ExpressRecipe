using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Options;
using ExpressRecipe.Shared.Messages;

namespace ExpressRecipe.RecipeService.Services;

/// <summary>
/// Hosted service that subscribes to recipe nutrition query messages.
/// Other services (e.g. MealPlanningService) can use request/response messaging
/// to fetch per-serving macros without making REST API calls.
/// </summary>
public sealed class RecipeNutritionQuerySubscriber : IHostedService
{
    private readonly IMessageBus _bus;
    private readonly ILogger<RecipeNutritionQuerySubscriber> _logger;

    public RecipeNutritionQuerySubscriber(IMessageBus bus, ILogger<RecipeNutritionQuerySubscriber> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        SubscribeOptions opts = new()
        {
            RoutingMode = RoutingMode.CompetingConsumer,
            PrefetchCount = 20,
            ConsumerConcurrency = 4
        };

        await _bus.SubscribeRequestAsync<
            RequestRecipeNutrition,
            RecipeNutritionResponse,
            RecipeNutritionQueryHandler>(opts, cancellationToken);

        _logger.LogInformation("[RecipeNutritionQuerySubscriber] Subscribed to nutrition query messages");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
