using ExpressRecipe.Messaging.Core.Abstractions;

namespace ExpressRecipe.Shared.Messages;

/// <summary>Routing key constants for the AllergenResolution saga.</summary>
public static class AllergenResolutionKeys
{
    public const string IngredientLookup  = "ingredient.lookup";
    public const string ProductLookup     = "product.lookup";
    public const string IngredientGraphWalk = "product.graphwalk";
    public const string PersistResolution = "safefork.persistresolution";
}

// ── Requests ─────────────────────────────────────────────────────────────────

public record RequestAllergenIngredientLookup(
    string CorrelationId,
    Guid AllergenProfileId,
    string FreeFormText,
    string? Brand) : IMessage;

public record RequestProductLookup(
    string CorrelationId,
    Guid AllergenProfileId,
    string FreeFormText,
    string? Brand,
    Guid? IngredientId) : IMessage;

public record RequestIngredientGraphWalk(
    string CorrelationId,
    Guid AllergenProfileId,
    Guid ProductId) : IMessage;

public record RequestPersistResolution(
    string CorrelationId,
    Guid AllergenProfileId,
    bool HasLinks) : IMessage;

// ── Results ──────────────────────────────────────────────────────────────────

public record AllergenIngredientLookupResult(
    string CorrelationId,
    Guid? IngredientId,
    string? MatchMethod) : IMessage;

public record ProductLookupResult(
    string CorrelationId,
    Guid? ProductId,
    string? MatchMethod) : IMessage;

public record IngredientGraphWalkResult(
    string CorrelationId,
    int LinksWritten) : IMessage;

public record ResolutionPersisted(
    string CorrelationId,
    bool IsUnresolved) : IMessage;
