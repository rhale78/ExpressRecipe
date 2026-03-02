using System.Collections.Concurrent;
using System.Diagnostics;
using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.Messaging.Core.Options;
using ExpressRecipe.Messaging.Core.Serialization;
using ExpressRecipe.Messaging.Core.Tracing;
using ExpressRecipe.Messaging.RabbitMQ.Internal;
using ExpressRecipe.Messaging.RabbitMQ.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ExpressRecipe.Messaging.RabbitMQ;

/// <summary>
/// RabbitMQ-backed implementation of <see cref="IMessageBus"/>.
/// Supports broadcast, competing-consumer, direct, and service-name routing patterns,
/// as well as request/response and distributed tracing via W3C TraceContext.
/// </summary>
public sealed class RabbitMqMessageBus : IMessageBus, IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly IMessageSerializer _serializer;
    private readonly RabbitMqMessagingOptions _options;
    private readonly SubscriptionRegistry _subscriptionRegistry;
    private readonly ILogger<RabbitMqMessageBus> _logger;

    // Lazily-initialized channel pool for publishing
    private ChannelPool? _channelPool;
    private readonly SemaphoreSlim _poolInit = new(1, 1);

    // Correlation tracking for request/response
    private readonly ConcurrentDictionary<string, TaskCompletionSource<MessageEnvelope>> _pendingRequests = new();

    // Reply queue channel (one per bus instance)
    private IChannel? _replyChannel;
    private string? _replyQueueName;
    private readonly SemaphoreSlim _replyInit = new(1, 1);

    // Declare-once tracking for idempotent exchange/queue declarations
    private readonly ConcurrentDictionary<string, bool> _declaredExchanges = new();
    private readonly ConcurrentDictionary<string, bool> _declaredQueues = new();

    private readonly string _instanceId = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Initializes a new instance of <see cref="RabbitMqMessageBus"/>.
    /// </summary>
    public RabbitMqMessageBus(
        IConnection connection,
        IMessageSerializer serializer,
        IOptions<RabbitMqMessagingOptions> options,
        SubscriptionRegistry subscriptionRegistry,
        ILogger<RabbitMqMessageBus> logger,
        IServiceProvider serviceProvider)
    {
        _connection = connection;
        _serializer = serializer;
        _options = options.Value;
        _subscriptionRegistry = subscriptionRegistry;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task PublishAsync<TMessage>(TMessage message, PublishOptions? options = null, CancellationToken cancellationToken = default)
        where TMessage : IMessage
    {
        var opts = options ?? new PublishOptions();
        var envelope = BuildEnvelope(message, opts.MessageId, opts.CorrelationId, opts.Headers, opts.Ttl, opts.RoutingMode);

        using var activity = MessagingActivitySource.StartPublishActivity(
            typeof(TMessage).Name,
            NamingConventions.GetExchangeName(_options.ExchangePrefix, typeof(TMessage), opts.RoutingMode));
        MessagingActivitySource.InjectTraceContext(envelope, activity);

        var exchangeName = NamingConventions.GetExchangeName(_options.ExchangePrefix, typeof(TMessage), opts.RoutingMode);
        var routingKey = opts.RoutingMode == RoutingMode.Broadcast ? string.Empty : typeof(TMessage).Name.ToLowerInvariant();

        await PublishEnvelopeAsync(envelope, exchangeName, NamingConventions.GetExchangeType(opts.RoutingMode),
            routingKey, opts.Persistent, opts.Priority, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SendAsync<TMessage>(TMessage message, string destination, SendOptions? options = null, CancellationToken cancellationToken = default)
        where TMessage : IMessage
    {
        var opts = options ?? new SendOptions();
        var envelope = BuildEnvelope(message, opts.MessageId, opts.CorrelationId, opts.Headers, opts.Ttl, RoutingMode.Direct);

        using var activity = MessagingActivitySource.StartPublishActivity(typeof(TMessage).Name, destination);
        MessagingActivitySource.InjectTraceContext(envelope, activity);

        // Direct send: use default exchange with destination as routing key
        await PublishEnvelopeAsync(envelope, string.Empty, "direct",
            destination, opts.Persistent, opts.Priority, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SendToServiceAsync<TMessage>(TMessage message, string serviceName, SendOptions? options = null, CancellationToken cancellationToken = default)
        where TMessage : IMessage
    {
        var opts = options ?? new SendOptions();
        var envelope = BuildEnvelope(message, opts.MessageId, opts.CorrelationId, opts.Headers, opts.Ttl, RoutingMode.ServiceName, serviceName);

        var exchangeName = NamingConventions.GetExchangeName(_options.ExchangePrefix, typeof(TMessage), RoutingMode.ServiceName, serviceName);
        using var activity = MessagingActivitySource.StartPublishActivity(typeof(TMessage).Name, exchangeName);
        MessagingActivitySource.InjectTraceContext(envelope, activity);

        await PublishEnvelopeAsync(envelope, exchangeName, NamingConventions.GetExchangeType(RoutingMode.ServiceName),
            serviceName.ToLowerInvariant(), opts.Persistent, opts.Priority, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<TResponse> RequestAsync<TRequest, TResponse>(TRequest request, RequestOptions? options = null, CancellationToken cancellationToken = default)
        where TRequest : IMessage
        where TResponse : IMessage
    {
        var opts = options ?? new RequestOptions();
        var correlationId = opts.CorrelationId ?? Guid.NewGuid().ToString();

        await EnsureReplyQueueAsync(cancellationToken).ConfigureAwait(false);

        var tcs = new TaskCompletionSource<MessageEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[correlationId] = tcs;

        var envelope = BuildEnvelope(request, null, correlationId, opts.Headers, null, RoutingMode.CompetingConsumer);
        envelope.ReplyTo = _replyQueueName;

        var exchangeName = NamingConventions.GetExchangeName(_options.ExchangePrefix, typeof(TRequest), RoutingMode.CompetingConsumer);
        using var activity = MessagingActivitySource.StartPublishActivity(typeof(TRequest).Name, exchangeName);
        MessagingActivitySource.InjectTraceContext(envelope, activity);

        await PublishEnvelopeAsync(envelope, exchangeName, "direct",
            typeof(TRequest).Name.ToLowerInvariant(), opts.Persistent, null, cancellationToken).ConfigureAwait(false);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(opts.Timeout);

        try
        {
            var responseEnvelope = await tcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            return _serializer.Deserialize<TResponse>(responseEnvelope.Payload);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _pendingRequests.TryRemove(correlationId, out _);
            throw new TimeoutException($"Request of type {typeof(TRequest).Name} timed out after {opts.Timeout}.");
        }
        finally
        {
            _pendingRequests.TryRemove(correlationId, out _);
        }
    }

    /// <inheritdoc />
    public Task SubscribeAsync<TMessage>(Func<TMessage, MessageContext, CancellationToken, Task> handler, SubscribeOptions? options = null, CancellationToken cancellationToken = default)
        where TMessage : IMessage
    {
        var opts = options ?? new SubscribeOptions();
        _subscriptionRegistry.Add(new DelegateSubscriptionRegistration<TMessage>(handler, opts));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SubscribeAsync<TMessage, THandler>(SubscribeOptions? options = null, CancellationToken cancellationToken = default)
        where TMessage : IMessage
        where THandler : IMessageHandler<TMessage>
    {
        var opts = options ?? new SubscribeOptions();
        _subscriptionRegistry.Add(new TypedSubscriptionRegistration<TMessage, THandler>(_serviceProvider, opts));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SubscribeRequestAsync<TRequest, TResponse>(Func<TRequest, MessageContext, CancellationToken, Task<TResponse>> handler, SubscribeOptions? options = null, CancellationToken cancellationToken = default)
        where TRequest : IMessage
        where TResponse : IMessage
    {
        var opts = options ?? new SubscribeOptions();
        _subscriptionRegistry.Add(new DelegateRequestSubscriptionRegistration<TRequest, TResponse>(handler, opts));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SubscribeRequestAsync<TRequest, TResponse, THandler>(SubscribeOptions? options = null, CancellationToken cancellationToken = default)
        where TRequest : IMessage
        where TResponse : IMessage
        where THandler : IRequestHandler<TRequest, TResponse>
    {
        var opts = options ?? new SubscribeOptions();
        _subscriptionRegistry.Add(new TypedRequestSubscriptionRegistration<TRequest, TResponse, THandler>(_serviceProvider, opts));
        return Task.CompletedTask;
    }

    // ── Internal helpers used by RabbitMqConsumerHostedService ───────────────

    /// <summary>
    /// Declares an exchange idempotently (skips if already declared in this session).
    /// </summary>
    internal async Task EnsureExchangeAsync(IChannel channel, string exchangeName, string exchangeType)
    {
        if (_declaredExchanges.ContainsKey(exchangeName)) return;
        await channel.ExchangeDeclareAsync(exchangeName, exchangeType, durable: true, autoDelete: false).ConfigureAwait(false);
        _declaredExchanges[exchangeName] = true;
    }

    /// <summary>
    /// Declares a queue idempotently (skips if already declared in this session).
    /// Returns the queue name.
    /// </summary>
    internal async Task<string> EnsureQueueAsync(IChannel channel, string queueName, bool durable, bool exclusive, bool autoDelete, string? dlxName)
    {
        if (_declaredQueues.ContainsKey(queueName)) return queueName;

        var args = new Dictionary<string, object?>();
        if (dlxName is not null)
            args["x-dead-letter-exchange"] = dlxName;

        await channel.QueueDeclareAsync(queueName, durable: durable, exclusive: exclusive, autoDelete: autoDelete, arguments: args).ConfigureAwait(false);
        _declaredQueues[queueName] = true;
        return queueName;
    }

    /// <summary>
    /// Starts consuming from the given queue and dispatches to the registration's handler.
    /// </summary>
    internal async Task StartConsumerAsync(IChannel channel, SubscriptionRegistration registration, CancellationToken stoppingToken)
    {
        var messageType = registration.MessageType;
        var mode = registration.Options.RoutingMode;
        var exchangeName = NamingConventions.GetExchangeName(_options.ExchangePrefix, messageType, mode, registration.Options.ServiceName);
        var exchangeType = NamingConventions.GetExchangeType(mode);

        string queueName;
        if (mode == RoutingMode.Direct)
        {
            // Direct send: we listen on a queue named after our service
            queueName = registration.Options.QueueName
                ?? NamingConventions.GetQueueName(_options.ExchangePrefix, messageType, mode);
            await EnsureQueueAsync(channel, queueName, registration.Options.Durable,
                registration.Options.Exclusive, registration.Options.AutoDelete, GetDlxName(queueName, registration.Options)).ConfigureAwait(false);
        }
        else
        {
            await EnsureExchangeAsync(channel, exchangeName, exchangeType).ConfigureAwait(false);

            // Dead-letter setup
            if (_options.EnableDeadLetter && registration.Options.DeadLetterEnabled)
            {
                var dlxName = registration.Options.DeadLetterExchange
                    ?? NamingConventions.GetDeadLetterExchangeName(exchangeName, _options.DeadLetterExchangeSuffix);
                await EnsureExchangeAsync(channel, dlxName, "fanout").ConfigureAwait(false);
            }

            queueName = registration.Options.QueueName
                ?? NamingConventions.GetQueueName(_options.ExchangePrefix, messageType, mode,
                    mode == RoutingMode.Broadcast ? _options.ServiceName : registration.Options.ServiceName);

            var dlx = (_options.EnableDeadLetter && registration.Options.DeadLetterEnabled)
                ? (registration.Options.DeadLetterExchange
                    ?? NamingConventions.GetDeadLetterExchangeName(exchangeName, _options.DeadLetterExchangeSuffix))
                : null;

            await EnsureQueueAsync(channel, queueName, registration.Options.Durable,
                registration.Options.Exclusive, registration.Options.AutoDelete, dlx).ConfigureAwait(false);

            var routingKey = mode switch
            {
                RoutingMode.Broadcast => string.Empty,
                RoutingMode.ServiceName => (registration.Options.ServiceName ?? _options.ServiceName).ToLowerInvariant(),
                _ => messageType.Name.ToLowerInvariant()
            };

            await channel.QueueBindAsync(queueName, exchangeName, routingKey).ConfigureAwait(false);
        }

        await channel.BasicQosAsync(0, (ushort)registration.Options.PrefetchCount, false).ConfigureAwait(false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            MessageEnvelope? envelope = null;
            try
            {
                envelope = _serializer.Deserialize<MessageEnvelope>(ea.Body.ToArray());
                var msg = _serializer.Deserialize(envelope.Payload, messageType);

                using var activity = MessagingActivitySource.StartReceiveActivity(
                    envelope.MessageName, queueName, envelope.TraceId, envelope.SpanId);

                var context = new MessageContext
                {
                    MessageId = envelope.MessageId,
                    CorrelationId = envelope.CorrelationId,
                    MessageType = envelope.MessageType,
                    Timestamp = envelope.Timestamp,
                    Headers = envelope.Headers,
                    Activity = activity,
                    ReplyTo = envelope.ReplyTo
                };

                if (registration.IsRequestHandler && envelope.ReplyTo is not null)
                {
                    await HandleRequestAndReplyAsync(registration, msg, context, envelope, stoppingToken).ConfigureAwait(false);
                }
                else
                {
                    await registration.InvokeAsync(msg, context, stoppingToken).ConfigureAwait(false);
                }

                if (!registration.Options.AutoAck)
                    await channel.BasicAckAsync(ea.DeliveryTag, false).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message {MessageType}", envelope?.MessageType ?? "unknown");
                if (!registration.Options.AutoAck)
                    await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false).ConfigureAwait(false);
            }
        };

        await channel.BasicConsumeAsync(queueName, registration.Options.AutoAck, consumer, stoppingToken).ConfigureAwait(false);
        _logger.LogInformation("Started consumer on queue '{QueueName}' for message type '{MessageType}'", queueName, messageType.Name);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private readonly IServiceProvider _serviceProvider;

    private string? GetDlxName(string queueName, SubscribeOptions opts)
    {
        if (!_options.EnableDeadLetter || !opts.DeadLetterEnabled) return null;
        return opts.DeadLetterExchange
            ?? NamingConventions.GetDeadLetterExchangeName(queueName, _options.DeadLetterExchangeSuffix);
    }

    private MessageEnvelope BuildEnvelope<TMessage>(
        TMessage message, string? messageId, string? correlationId,
        Dictionary<string, string>? headers, TimeSpan? ttl,
        RoutingMode mode, string? destinationService = null)
        where TMessage : IMessage
    {
        return new MessageEnvelope
        {
            MessageId = messageId ?? Guid.NewGuid().ToString(),
            CorrelationId = correlationId,
            MessageType = typeof(TMessage).FullName ?? typeof(TMessage).Name,
            MessageName = typeof(TMessage).Name,
            Payload = _serializer.Serialize(message),
            Timestamp = DateTimeOffset.UtcNow,
            Ttl = ttl,
            RoutingMode = mode,
            DestinationServiceName = destinationService,
            Headers = headers ?? new Dictionary<string, string>()
        };
    }

    private async Task PublishEnvelopeAsync(
        MessageEnvelope envelope, string exchangeName, string exchangeType,
        string routingKey, bool persistent, byte? priority, CancellationToken cancellationToken)
    {
        var pool = await EnsureChannelPoolAsync(cancellationToken).ConfigureAwait(false);
        var channel = await pool.AcquireAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Declare exchange if named
            if (!string.IsNullOrEmpty(exchangeName))
                await EnsureExchangeAsync(channel, exchangeName, exchangeType).ConfigureAwait(false);

            var props = new BasicProperties
            {
                MessageId = envelope.MessageId,
                CorrelationId = envelope.CorrelationId,
                ContentType = "application/json",
                DeliveryMode = persistent ? DeliveryModes.Persistent : DeliveryModes.Transient,
                Timestamp = new AmqpTimestamp(envelope.Timestamp.ToUnixTimeSeconds()),
                ReplyTo = envelope.ReplyTo
            };

            if (priority.HasValue)
                props.Priority = priority.Value;

            if (envelope.Ttl.HasValue)
                props.Expiration = ((long)envelope.Ttl.Value.TotalMilliseconds).ToString();

            var body = _serializer.Serialize(envelope);
            await channel.BasicPublishAsync(exchangeName, routingKey, mandatory: false, basicProperties: props, body: body, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await pool.ReturnAsync(channel).ConfigureAwait(false);
        }
    }

    private async Task HandleRequestAndReplyAsync(
        SubscriptionRegistration registration, object message,
        MessageContext context, MessageEnvelope requestEnvelope, CancellationToken cancellationToken)
    {
        if (requestEnvelope.ReplyTo is null)
            return;

        // Use reflection to call the InvokeTypedAsync method which returns Task<TResponse>
        object? response = null;
        var invokeMethod = registration.GetType().GetMethod("InvokeTypedAsync");
        if (invokeMethod is not null)
        {
            var task = invokeMethod.Invoke(registration, [message, context, cancellationToken]);
            if (task is Task awaitableTask)
            {
                await awaitableTask.ConfigureAwait(false);
                var resultProp = awaitableTask.GetType().GetProperty("Result");
                response = resultProp?.GetValue(awaitableTask);
            }
        }
        else
        {
            // Fallback: delegate registrations with non-generic invoke
            await registration.InvokeAsync(message, context, cancellationToken).ConfigureAwait(false);
        }

        if (response is null)
            return;

        var responseEnvelope = new MessageEnvelope
        {
            MessageId = Guid.NewGuid().ToString(),
            CorrelationId = requestEnvelope.CorrelationId ?? requestEnvelope.MessageId,
            MessageType = response.GetType().FullName ?? response.GetType().Name,
            MessageName = response.GetType().Name,
            Payload = _serializer.Serialize(response),
            Timestamp = DateTimeOffset.UtcNow,
            RoutingMode = RoutingMode.Direct
        };

        await PublishEnvelopeAsync(responseEnvelope, string.Empty, "direct",
            requestEnvelope.ReplyTo, persistent: false, priority: null, cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureReplyQueueAsync(CancellationToken cancellationToken)
    {
        if (_replyQueueName is not null) return;

        await _replyInit.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_replyQueueName is not null) return;

            _replyChannel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            _replyQueueName = NamingConventions.GetReplyQueueName(_options.ExchangePrefix, _instanceId);

            await _replyChannel.QueueDeclareAsync(_replyQueueName, durable: false, exclusive: true, autoDelete: true).ConfigureAwait(false);

            var consumer = new AsyncEventingBasicConsumer(_replyChannel);
            consumer.ReceivedAsync += (_, ea) =>
            {
                try
                {
                    var responseEnvelope = _serializer.Deserialize<MessageEnvelope>(ea.Body.ToArray());
                    var correlationId = responseEnvelope.CorrelationId;
                    if (correlationId is not null && _pendingRequests.TryRemove(correlationId, out var tcs))
                        tcs.TrySetResult(responseEnvelope);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing reply message");
                }
                return Task.CompletedTask;
            };

            await _replyChannel.BasicConsumeAsync(_replyQueueName, autoAck: true, consumer: consumer, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _replyInit.Release();
        }
    }

    private async ValueTask<ChannelPool> EnsureChannelPoolAsync(CancellationToken cancellationToken)
    {
        if (_channelPool is not null) return _channelPool;

        await _poolInit.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _channelPool ??= new ChannelPool(_connection, _options.ChannelPoolSize);
        }
        finally
        {
            _poolInit.Release();
        }

        return _channelPool;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_channelPool is not null)
            await _channelPool.DisposeAsync().ConfigureAwait(false);

        if (_replyChannel is not null)
            await _replyChannel.DisposeAsync().ConfigureAwait(false);

        _poolInit.Dispose();
        _replyInit.Dispose();
    }
}
