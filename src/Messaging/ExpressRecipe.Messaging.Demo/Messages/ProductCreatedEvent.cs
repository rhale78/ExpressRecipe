using ExpressRecipe.Messaging.Core.Abstractions;

namespace ExpressRecipe.Messaging.Demo.Messages;

/// <summary>Raised when a new product is created. Broadcast to all subscribers.</summary>
public sealed record ProductCreatedEvent(
    Guid ProductId,
    string Name,
    string Brand,
    string Barcode,
    decimal Price) : IMessage;
