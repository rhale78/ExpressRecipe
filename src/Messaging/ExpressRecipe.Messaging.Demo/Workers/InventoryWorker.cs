using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.Messaging.Demo.Messages;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.Messaging.Demo.Workers;

/// <summary>
/// A message handler that simulates inventory processing for <see cref="ProcessInventoryCommand"/>.
/// Multiple instances of this handler compete to process commands from the work queue.
/// </summary>
public sealed class InventoryWorker : IMessageHandler<ProcessInventoryCommand>
{
    private readonly ILogger<InventoryWorker> _logger;

    public InventoryWorker(ILogger<InventoryWorker> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(ProcessInventoryCommand message, MessageContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing inventory: ProductId={ProductId} Quantity={Delta} Warehouse={Warehouse} Reason={Reason}",
            message.ProductId, message.QuantityDelta, message.Warehouse, message.Reason);

        // Simulate processing delay
        await Task.Delay(50, cancellationToken);

        _logger.LogInformation("Inventory update applied for ProductId={ProductId}", message.ProductId);
    }
}
