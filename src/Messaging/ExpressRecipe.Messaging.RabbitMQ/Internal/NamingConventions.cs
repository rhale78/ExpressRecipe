using ExpressRecipe.Messaging.Core.Options;

namespace ExpressRecipe.Messaging.RabbitMQ.Internal;

/// <summary>
/// Centralises the generation of exchange and queue names used by the RabbitMQ messaging implementation.
/// All names follow the pattern <c>{prefix}.{mode}.{messageName}</c> for easy identification in broker tooling.
/// </summary>
internal static class NamingConventions
{
    /// <summary>
    /// Returns the exchange name for the given message type and routing mode.
    /// </summary>
    /// <param name="exchangePrefix">The configured exchange prefix (e.g. <c>"expressrecipe"</c>).</param>
    /// <param name="messageType">The CLR type of the message.</param>
    /// <param name="mode">How the message should be routed.</param>
    /// <param name="serviceName">Service name, used only for <see cref="RoutingMode.ServiceName"/>.</param>
    public static string GetExchangeName(string exchangePrefix, Type messageType, RoutingMode mode, string? serviceName = null)
    {
        return mode switch
        {
            RoutingMode.Broadcast => $"{exchangePrefix}.broadcast.{messageType.Name.ToLowerInvariant()}",
            RoutingMode.CompetingConsumer => $"{exchangePrefix}.work.{messageType.Name.ToLowerInvariant()}",
            RoutingMode.Direct => $"{exchangePrefix}.direct.{messageType.Name.ToLowerInvariant()}",
            RoutingMode.ServiceName => $"{exchangePrefix}.service.{(serviceName ?? messageType.Name).ToLowerInvariant()}",
            _ => $"{exchangePrefix}.direct.{messageType.Name.ToLowerInvariant()}"
        };
    }

    /// <summary>
    /// Returns the exchange type string (as defined by RabbitMQ) for the given routing mode.
    /// </summary>
    public static string GetExchangeType(RoutingMode mode)
    {
        return mode switch
        {
            RoutingMode.Broadcast => "fanout",
            RoutingMode.CompetingConsumer => "direct",
            RoutingMode.Direct => "direct",
            RoutingMode.ServiceName => "topic",
            _ => "direct"
        };
    }

    /// <summary>
    /// Returns the queue name for the given message type and routing mode.
    /// </summary>
    /// <param name="servicePrefix">The configured exchange prefix.</param>
    /// <param name="messageType">The CLR type of the message.</param>
    /// <param name="mode">How messages are routed.</param>
    /// <param name="serviceName">
    ///   For <see cref="RoutingMode.Broadcast"/>, this is the subscriber's service name (each gets its own queue).
    ///   For <see cref="RoutingMode.ServiceName"/>, this is the target service name.
    /// </param>
    public static string GetQueueName(string servicePrefix, Type messageType, RoutingMode mode, string? serviceName = null)
    {
        var msgName = messageType.Name.ToLowerInvariant();
        return mode switch
        {
            RoutingMode.Broadcast => string.IsNullOrEmpty(serviceName)
                ? $"{servicePrefix}.broadcast.{msgName}.queue"
                : $"{servicePrefix}.broadcast.{msgName}.{serviceName.ToLowerInvariant()}.queue",
            RoutingMode.CompetingConsumer => $"{servicePrefix}.work.{msgName}.queue",
            RoutingMode.Direct => $"{servicePrefix}.direct.{msgName}.queue",
            RoutingMode.ServiceName => $"{servicePrefix}.service.{(serviceName ?? msgName).ToLowerInvariant()}.queue",
            _ => $"{servicePrefix}.direct.{msgName}.queue"
        };
    }

    /// <summary>
    /// Returns the dead-letter exchange name derived from the source exchange name.
    /// </summary>
    public static string GetDeadLetterExchangeName(string exchangeName, string suffix = ".dlx")
        => $"{exchangeName}{suffix}";

    /// <summary>
    /// Returns the temporary reply queue name used for request/response patterns.
    /// </summary>
    /// <param name="servicePrefix">The configured exchange prefix.</param>
    /// <param name="instanceId">A unique identifier for this service instance (e.g. a GUID).</param>
    public static string GetReplyQueueName(string servicePrefix, string instanceId)
        => $"{servicePrefix}.reply.{instanceId.ToLowerInvariant()}";
}
