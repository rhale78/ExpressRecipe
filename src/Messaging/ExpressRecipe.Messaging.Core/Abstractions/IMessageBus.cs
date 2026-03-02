using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.Messaging.Core.Options;

namespace ExpressRecipe.Messaging.Core.Abstractions;

/// <summary>
/// The main entry point for publishing and subscribing to messages.
/// Supports broadcast, direct, competing consumer, service-name routing, and request/response patterns.
/// </summary>
public interface IMessageBus
{
    /// <summary>
    /// Publishes a message to all subscribers (broadcast/fanout).
    /// </summary>
    Task PublishAsync<TMessage>(TMessage message, PublishOptions? options = null, CancellationToken cancellationToken = default)
        where TMessage : IMessage;

    /// <summary>
    /// Sends a message to a single specific destination queue (point-to-point direct routing).
    /// </summary>
    Task SendAsync<TMessage>(TMessage message, string destination, SendOptions? options = null, CancellationToken cancellationToken = default)
        where TMessage : IMessage;

    /// <summary>
    /// Sends a message to a specific service identified by its service name.
    /// </summary>
    Task SendToServiceAsync<TMessage>(TMessage message, string serviceName, SendOptions? options = null, CancellationToken cancellationToken = default)
        where TMessage : IMessage;

    /// <summary>
    /// Sends a request and waits for a response (request/response pattern).
    /// </summary>
    Task<TResponse> RequestAsync<TRequest, TResponse>(TRequest request, RequestOptions? options = null, CancellationToken cancellationToken = default)
        where TRequest : IMessage
        where TResponse : IMessage;

    /// <summary>
    /// Subscribes to receive messages of the specified type using an inline handler delegate.
    /// </summary>
    Task SubscribeAsync<TMessage>(Func<TMessage, MessageContext, CancellationToken, Task> handler, SubscribeOptions? options = null, CancellationToken cancellationToken = default)
        where TMessage : IMessage;

    /// <summary>
    /// Subscribes to receive messages using a handler class resolved from the DI container.
    /// </summary>
    Task SubscribeAsync<TMessage, THandler>(SubscribeOptions? options = null, CancellationToken cancellationToken = default)
        where TMessage : IMessage
        where THandler : IMessageHandler<TMessage>;

    /// <summary>
    /// Subscribes to handle requests and automatically send responses using an inline handler delegate.
    /// </summary>
    Task SubscribeRequestAsync<TRequest, TResponse>(Func<TRequest, MessageContext, CancellationToken, Task<TResponse>> handler, SubscribeOptions? options = null, CancellationToken cancellationToken = default)
        where TRequest : IMessage
        where TResponse : IMessage;

    /// <summary>
    /// Subscribes to handle requests and automatically send responses using a handler class resolved from the DI container.
    /// </summary>
    Task SubscribeRequestAsync<TRequest, TResponse, THandler>(SubscribeOptions? options = null, CancellationToken cancellationToken = default)
        where TRequest : IMessage
        where TResponse : IMessage
        where THandler : IRequestHandler<TRequest, TResponse>;
}
