using ExpressRecipe.Messaging.Core.Abstractions;

namespace ExpressRecipe.Messaging.Demo.Messages;

/// <summary>Request to look up a product by its ID. Uses request/response pattern.</summary>
public sealed record GetProductQuery(Guid ProductId) : IMessage;

/// <summary>Response to a <see cref="GetProductQuery"/>.</summary>
public sealed record ProductQueryResponse(
    Guid ProductId,
    string Name,
    string Brand,
    decimal Price,
    bool Found) : IMessage;
