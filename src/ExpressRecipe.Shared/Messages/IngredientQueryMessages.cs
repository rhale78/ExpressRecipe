using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Shared.Matching;

namespace ExpressRecipe.Shared.Messages;

/// <summary>
/// Request: look up ingredient IDs for a list of names.
/// IngredientService handles this and returns <see cref="IngredientLookupResponse"/>.
/// </summary>
public record RequestIngredientLookup(
    string       CorrelationId,
    List<string> Names,
    string?      SourceService = null) : IMessage;

/// <summary>
/// Response to <see cref="RequestIngredientLookup"/>.
/// <see cref="Results"/> is a dictionary keyed by name (case-insensitive) with the resolved ingredient ID.
/// <see cref="MatchResults"/> contains full <see cref="MatchResult"/> details for each name.
/// </summary>
public record IngredientLookupResponse(
    string                             CorrelationId,
    Dictionary<string, Guid>           Results,
    Dictionary<string, MatchResult>?   MatchResults = null) : IMessage;

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
