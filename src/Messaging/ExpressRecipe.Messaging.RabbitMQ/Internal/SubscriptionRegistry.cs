using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.Messaging.Core.Options;
using Microsoft.Extensions.DependencyInjection;

namespace ExpressRecipe.Messaging.RabbitMQ.Internal;

/// <summary>
/// Holds the information needed to wire up a consumer when the hosted service starts.
/// </summary>
internal abstract class SubscriptionRegistration
{
    public Type MessageType { get; init; } = typeof(object);
    public SubscribeOptions Options { get; init; } = new();
    public abstract Task InvokeAsync(object message, MessageContext context, CancellationToken cancellationToken);
    public bool IsRequestHandler { get; init; }
    public Type? ResponseType { get; init; }
}

/// <summary>
/// Concrete subscription using an inline handler delegate.
/// </summary>
internal sealed class DelegateSubscriptionRegistration<TMessage> : SubscriptionRegistration
    where TMessage : IMessage
{
    private readonly Func<TMessage, MessageContext, CancellationToken, Task> _handler;

    public DelegateSubscriptionRegistration(
        Func<TMessage, MessageContext, CancellationToken, Task> handler,
        SubscribeOptions options)
    {
        _handler = handler;
        MessageType = typeof(TMessage);
        Options = options;
    }

    public override Task InvokeAsync(object message, MessageContext context, CancellationToken cancellationToken)
        => _handler((TMessage)message, context, cancellationToken);
}

/// <summary>
/// Concrete subscription using a handler class resolved from the DI container.
/// </summary>
internal sealed class TypedSubscriptionRegistration<TMessage, THandler> : SubscriptionRegistration
    where TMessage : IMessage
    where THandler : IMessageHandler<TMessage>
{
    private readonly IServiceProvider _serviceProvider;

    public TypedSubscriptionRegistration(IServiceProvider serviceProvider, SubscribeOptions options)
    {
        _serviceProvider = serviceProvider;
        MessageType = typeof(TMessage);
        Options = options;
    }

    public override async Task InvokeAsync(object message, MessageContext context, CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<THandler>();
        await handler.HandleAsync((TMessage)message, context, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Concrete request/response subscription using an inline handler delegate.
/// </summary>
internal sealed class DelegateRequestSubscriptionRegistration<TRequest, TResponse> : SubscriptionRegistration
    where TRequest : IMessage
    where TResponse : IMessage
{
    private readonly Func<TRequest, MessageContext, CancellationToken, Task<TResponse>> _handler;

    public DelegateRequestSubscriptionRegistration(
        Func<TRequest, MessageContext, CancellationToken, Task<TResponse>> handler,
        SubscribeOptions options)
    {
        _handler = handler;
        MessageType = typeof(TRequest);
        ResponseType = typeof(TResponse);
        Options = options;
        IsRequestHandler = true;
    }

    public override async Task InvokeAsync(object message, MessageContext context, CancellationToken cancellationToken)
        => await _handler((TRequest)message, context, cancellationToken).ConfigureAwait(false);

    public async Task<TResponse> InvokeTypedAsync(TRequest message, MessageContext context, CancellationToken cancellationToken)
        => await _handler(message, context, cancellationToken).ConfigureAwait(false);
}

/// <summary>
/// Concrete request/response subscription using a handler class resolved from the DI container.
/// </summary>
internal sealed class TypedRequestSubscriptionRegistration<TRequest, TResponse, THandler> : SubscriptionRegistration
    where TRequest : IMessage
    where TResponse : IMessage
    where THandler : IRequestHandler<TRequest, TResponse>
{
    private readonly IServiceProvider _serviceProvider;

    public TypedRequestSubscriptionRegistration(IServiceProvider serviceProvider, SubscribeOptions options)
    {
        _serviceProvider = serviceProvider;
        MessageType = typeof(TRequest);
        ResponseType = typeof(TResponse);
        Options = options;
        IsRequestHandler = true;
    }

    public override async Task InvokeAsync(object message, MessageContext context, CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<THandler>();
        await handler.HandleAsync((TRequest)message, context, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TResponse> InvokeTypedAsync(TRequest message, MessageContext context, CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<THandler>();
        return await handler.HandleAsync((TRequest)message, context, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Thread-safe registry of all active subscriptions.
/// </summary>
public sealed class SubscriptionRegistry
{
    private readonly List<SubscriptionRegistration> _registrations = new();
    private readonly Lock _lock = new();

    internal void Add(SubscriptionRegistration registration)
    {
        lock (_lock)
            _registrations.Add(registration);
    }

    internal IReadOnlyList<SubscriptionRegistration> GetAll()
    {
        lock (_lock)
            return _registrations.ToList();
    }
}
