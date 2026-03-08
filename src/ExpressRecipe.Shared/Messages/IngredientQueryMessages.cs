using ExpressRecipe.Messaging.Core.Abstractions;

namespace ExpressRecipe.Shared.Messages;

/// <summary>
/// Request: look up ingredient IDs for a list of names.
/// IngredientService handles this and returns <see cref="IngredientLookupResponse"/>.
/// </summary>
public record RequestIngredientLookup(
    string       CorrelationId,
    List<string> Names) : IMessage;

/// <summary>
/// Response to <see cref="RequestIngredientLookup"/>.
/// <see cref="Results"/> is a dictionary keyed by name (case-insensitive) with the resolved ingredient ID.
/// </summary>
public record IngredientLookupResponse(
    string                      CorrelationId,
    Dictionary<string, Guid>    Results) : IMessage;

/// <summary>
/// Request: create multiple ingredients by name in a single round-trip.
/// IngredientService handles this and returns <see cref="IngredientBulkCreateResponse"/>.
/// </summary>
public record RequestIngredientBulkCreate(
    string       CorrelationId,
    List<string> Names) : IMessage;

/// <summary>
/// Response to <see cref="RequestIngredientBulkCreate"/>.
/// <see cref="Created"/> is the number of new rows inserted (existing names are skipped).
/// </summary>
public record IngredientBulkCreateResponse(
    string CorrelationId,
    int    Created) : IMessage;
