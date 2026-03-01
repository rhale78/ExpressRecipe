using ExpressRecipe.Messaging.Core.Abstractions;

namespace ExpressRecipe.Messaging.Demo.Messages;

/// <summary>Command to process an inventory update. Uses competing-consumer (work queue) pattern.</summary>
public sealed record ProcessInventoryCommand(
    Guid ProductId,
    int QuantityDelta,
    string Warehouse,
    string Reason) : IMessage;
