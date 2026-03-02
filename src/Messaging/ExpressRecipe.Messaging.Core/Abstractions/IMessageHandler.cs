using ExpressRecipe.Messaging.Core.Messages;

namespace ExpressRecipe.Messaging.Core.Abstractions;

/// <summary>
/// Handler for messages of type <typeparamref name="TMessage"/>.
/// </summary>
/// <typeparam name="TMessage">The message type to handle.</typeparam>
public interface IMessageHandler<TMessage> where TMessage : IMessage
{
    /// <summary>
    /// Handles the received message.
    /// </summary>
    Task HandleAsync(TMessage message, MessageContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Handler for request/response messages.
/// </summary>
/// <typeparam name="TRequest">The request message type.</typeparam>
/// <typeparam name="TResponse">The response message type.</typeparam>
public interface IRequestHandler<TRequest, TResponse>
    where TRequest : IMessage
    where TResponse : IMessage
{
    /// <summary>
    /// Handles the request and returns a response.
    /// </summary>
    Task<TResponse> HandleAsync(TRequest request, MessageContext context, CancellationToken cancellationToken = default);
}
