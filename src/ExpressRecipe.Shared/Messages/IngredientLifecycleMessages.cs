using ExpressRecipe.Messaging.Core.Abstractions;

namespace ExpressRecipe.Shared.Messages;

/// <summary>
/// Routing key constants for ingredient lifecycle events.
/// </summary>
public static class IngredientEventKeys
{
    public const string Created = "ingredient.lifecycle.created";
    public const string Updated = "ingredient.lifecycle.updated";
    public const string Deleted = "ingredient.lifecycle.deleted";

    /// <summary>Wildcard that matches <em>all</em> ingredient lifecycle events.</summary>
    public const string All = "ingredient.lifecycle.#";
}

/// <summary>
/// Broadcast when a new ingredient is created.
/// </summary>
public record IngredientCreatedEvent(
    Guid   IngredientId,
    string Name,
    DateTimeOffset OccurredAt) : IMessage;

/// <summary>
/// Broadcast when an existing ingredient is updated.
/// </summary>
public record IngredientUpdatedEvent(
    Guid   IngredientId,
    string Name,
    string? OldName,
    DateTimeOffset OccurredAt) : IMessage;

/// <summary>
/// Broadcast when an ingredient is deleted.
/// </summary>
public record IngredientDeletedEvent(
    Guid   IngredientId,
    string? Name,
    DateTimeOffset OccurredAt) : IMessage;
