using ExpressRecipe.Shared.Events;
using ExpressRecipe.Shared.Services;
using ExpressRecipe.UserService.Data;
using RabbitMQ.Client;

namespace ExpressRecipe.UserService.Services;

/// <summary>
/// Subscribes to the points.earned queue and credits user points.
/// </summary>
public class PointsEarnedSubscriber : EventSubscriber
{
    private readonly IServiceProvider _serviceProvider;

    public PointsEarnedSubscriber(
        IConnectionFactory connectionFactory,
        ILogger<PointsEarnedSubscriber> logger,
        IServiceProvider serviceProvider)
        : base(connectionFactory, logger)
    {
        _serviceProvider = serviceProvider;
    }

    protected override string QueueName => "points.earned";

    protected override List<string> RoutingKeys => new()
    {
        "points.earned"
    };

    protected override async Task ProcessEventAsync(string routingKey, string message, CancellationToken cancellationToken)
    {
        var @event = DeserializeEvent<PointsEarnedEvent>(message);
        if (@event == null)
        {
            Logger.LogWarning("Received null PointsEarnedEvent from queue");
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var pointsRepository = scope.ServiceProvider.GetRequiredService<IPointsRepository>();

        try
        {
            await pointsRepository.CreditAsync(@event.UserId, @event.Points, @event.Reason, @event.RelatedEntityId);
            Logger.LogInformation(
                "Credited {Points} points to user {UserId} for reason {Reason}",
                @event.Points, @event.UserId, @event.Reason);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to credit {Points} points to user {UserId}", @event.Points, @event.UserId);
            throw;
        }
    }
}
