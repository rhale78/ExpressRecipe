using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.Messaging.Core.Options;

namespace ExpressRecipe.UserService.Services;

/// <summary>
/// No-op IMessageBus used when RabbitMQ is not configured (e.g., local development without messaging).
/// </summary>
internal sealed class NoOpMessageBus : IMessageBus
{
    public Task PublishAsync<TMessage>(TMessage message, PublishOptions? options = null, CancellationToken cancellationToken = default)
        where TMessage : IMessage => Task.CompletedTask;

    public Task SendAsync<TMessage>(TMessage message, string destination, SendOptions? options = null, CancellationToken cancellationToken = default)
        where TMessage : IMessage => Task.CompletedTask;

    public Task SendToServiceAsync<TMessage>(TMessage message, string serviceName, SendOptions? options = null, CancellationToken cancellationToken = default)
        where TMessage : IMessage => Task.CompletedTask;

    public Task<TResponse> RequestAsync<TRequest, TResponse>(TRequest request, RequestOptions? options = null, CancellationToken cancellationToken = default)
        where TRequest : IMessage
        where TResponse : IMessage
        => throw new NotSupportedException("Messaging is not configured.");

    public Task SubscribeAsync<TMessage>(Func<TMessage, MessageContext, CancellationToken, Task> handler, SubscribeOptions? options = null, CancellationToken cancellationToken = default)
        where TMessage : IMessage => Task.CompletedTask;

    public Task SubscribeAsync<TMessage, THandler>(SubscribeOptions? options = null, CancellationToken cancellationToken = default)
        where TMessage : IMessage
        where THandler : IMessageHandler<TMessage>
        => Task.CompletedTask;

    public Task SubscribeRequestAsync<TRequest, TResponse>(Func<TRequest, MessageContext, CancellationToken, Task<TResponse>> handler, SubscribeOptions? options = null, CancellationToken cancellationToken = default)
        where TRequest : IMessage
        where TResponse : IMessage => Task.CompletedTask;

    public Task SubscribeRequestAsync<TRequest, TResponse, THandler>(SubscribeOptions? options = null, CancellationToken cancellationToken = default)
        where TRequest : IMessage
        where TResponse : IMessage
        where THandler : IRequestHandler<TRequest, TResponse>
        => Task.CompletedTask;
}
