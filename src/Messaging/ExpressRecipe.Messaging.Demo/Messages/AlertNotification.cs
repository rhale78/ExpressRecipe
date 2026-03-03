using ExpressRecipe.Messaging.Core.Abstractions;

namespace ExpressRecipe.Messaging.Demo.Messages;

/// <summary>Alert notification sent directly to a named service.</summary>
public sealed record AlertNotification(
    string Severity,
    string Title,
    string Message,
    DateTimeOffset OccurredAt) : IMessage;

/// <summary>Raised when a recipe is published. Broadcast with TTL.</summary>
public sealed record RecipePublishedEvent(
    Guid RecipeId,
    string Title,
    string AuthorName,
    string[] Tags) : IMessage;
