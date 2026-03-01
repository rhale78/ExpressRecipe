using ExpressRecipe.Messaging.Core.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace ExpressRecipe.Messaging.Core.Extensions;

/// <summary>
/// Extension methods for registering core messaging abstractions in a dependency injection container.
/// </summary>
public static class MessagingCoreExtensions
{
    /// <summary>
    /// Registers the core messaging services (serializer, etc.) required by all messaging implementations.
    /// </summary>
    public static IServiceCollection AddMessagingCore(this IServiceCollection services)
    {
        services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
        return services;
    }
}
