namespace ExpressRecipe.Messaging.Core.Options;

/// <summary>
/// Defines how a message should be routed to its destination(s).
/// </summary>
public enum RoutingMode
{
    /// <summary>Routes the message directly to a single named queue (point-to-point).</summary>
    Direct,

    /// <summary>Broadcasts the message to all subscribers (fanout exchange).</summary>
    Broadcast,

    /// <summary>Places the message in a shared work queue where the first available consumer processes it.</summary>
    CompetingConsumer,

    /// <summary>Routes the message to a specific service identified by its service name.</summary>
    ServiceName
}
