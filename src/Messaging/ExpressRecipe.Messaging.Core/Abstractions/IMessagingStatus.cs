namespace ExpressRecipe.Messaging.Core.Abstractions;

/// <summary>
/// Tracks whether the message bus connection is currently available.
/// Publishers and saga coordinators use this to decide whether to route
/// via messaging or fall back to REST.
/// </summary>
public interface IMessagingStatus
{
    /// <summary>
    /// Returns <c>true</c> when the message bus connection is established and healthy.
    /// </summary>
    bool IsAvailable { get; }
}
