using ExpressRecipe.Messaging.Core.Extensions;
using ExpressRecipe.Messaging.RabbitMQ.Internal;
using ExpressRecipe.Messaging.RabbitMQ.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using global::RabbitMQ.Client;

namespace ExpressRecipe.Messaging.RabbitMQ.Extensions;

/// <summary>
/// Extension methods for registering the RabbitMQ messaging implementation in a dependency injection container.
/// </summary>
public static class RabbitMqMessagingExtensions
{
    /// <summary>
    /// Registers the RabbitMQ messaging implementation for use with .NET Aspire.
    /// The RabbitMQ connection is registered automatically via Aspire's <c>AddRabbitMQClient</c>.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="connectionName">The Aspire resource connection name (default <c>"messaging"</c>).</param>
    /// <param name="configure">Optional delegate to configure <see cref="RabbitMqMessagingOptions"/>.</param>
    public static IHostApplicationBuilder AddRabbitMqMessaging(
        this IHostApplicationBuilder builder,
        string connectionName = "messaging",
        Action<RabbitMqMessagingOptions>? configure = null)
    {
        builder.AddRabbitMQClient(connectionName);
        AddRabbitMqMessagingCore(builder.Services, configure);
        return builder;
    }

    /// <summary>
    /// Registers the RabbitMQ messaging implementation for standalone use (without Aspire).
    /// A RabbitMQ connection is created from the provided connection string.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">An AMQP URI (e.g. <c>amqp://guest:guest@localhost/</c>).</param>
    /// <param name="configure">Optional delegate to configure <see cref="RabbitMqMessagingOptions"/>.</param>
    public static IServiceCollection AddRabbitMqMessaging(
        this IServiceCollection services,
        string connectionString,
        Action<RabbitMqMessagingOptions>? configure = null)
    {
        services.AddSingleton<IConnection>(sp =>
        {
            var factory = new ConnectionFactory { Uri = new Uri(connectionString) };
            return factory.CreateConnectionAsync().GetAwaiter().GetResult();
        });

        AddRabbitMqMessagingCore(services, configure);
        return services;
    }

    private static void AddRabbitMqMessagingCore(IServiceCollection services, Action<RabbitMqMessagingOptions>? configure)
    {
        services.AddMessagingCore();

        if (configure is not null)
            services.Configure(configure);
        else
            services.AddOptions<RabbitMqMessagingOptions>();

        services.AddSingleton<SubscriptionRegistry>();
        services.AddSingleton<RabbitMqMessageBus>();
        services.AddSingleton<ExpressRecipe.Messaging.Core.Abstractions.IMessageBus>(sp => sp.GetRequiredService<RabbitMqMessageBus>());
        services.AddHostedService<RabbitMqConsumerHostedService>();
    }
}
