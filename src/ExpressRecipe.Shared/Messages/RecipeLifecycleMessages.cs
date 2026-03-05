using ExpressRecipe.Messaging.Core.Abstractions;

namespace ExpressRecipe.Shared.Messages;

/// <summary>
/// Routing key constants for recipe lifecycle events.
/// </summary>
public static class RecipeEventKeys
{
    public const string Created = "recipe.lifecycle.created";
    public const string Updated = "recipe.lifecycle.updated";
    public const string Deleted = "recipe.lifecycle.deleted";

    /// <summary>Wildcard that matches <em>all</em> recipe lifecycle events.</summary>
    public const string All = "recipe.lifecycle.#";
}

/// <summary>
/// Broadcast when a new recipe is created.
/// </summary>
public record RecipeCreatedEvent(
    Guid   RecipeId,
    string Name,
    string? Category,
    string? Cuisine,
    Guid   CreatedBy,
    DateTimeOffset OccurredAt) : IMessage;

/// <summary>
/// Broadcast when an existing recipe is updated.
/// </summary>
public record RecipeUpdatedEvent(
    Guid   RecipeId,
    string Name,
    string? Category,
    string? Cuisine,
    Guid   UpdatedBy,
    IReadOnlyList<string> ChangedFields,
    DateTimeOffset OccurredAt) : IMessage;

/// <summary>
/// Broadcast when a recipe is deleted.
/// </summary>
public record RecipeDeletedEvent(
    Guid  RecipeId,
    Guid  DeletedBy,
    DateTimeOffset OccurredAt) : IMessage;
