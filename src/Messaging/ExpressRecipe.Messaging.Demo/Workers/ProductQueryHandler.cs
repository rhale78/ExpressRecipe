using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.Messaging.Demo.Messages;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.Messaging.Demo.Workers;

/// <summary>
/// Handles <see cref="GetProductQuery"/> requests and returns a <see cref="ProductQueryResponse"/>.
/// Demonstrates the request/response pattern.
/// </summary>
public sealed class ProductQueryHandler : IRequestHandler<GetProductQuery, ProductQueryResponse>
{
    private readonly ILogger<ProductQueryHandler> _logger;

    // Fake in-memory product catalog for demo purposes
    private static readonly Dictionary<Guid, (string Name, string Brand, decimal Price)> _catalog = new();

    public ProductQueryHandler(ILogger<ProductQueryHandler> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<ProductQueryResponse> HandleAsync(GetProductQuery request, MessageContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling product query for ProductId={ProductId}", request.ProductId);

        if (_catalog.TryGetValue(request.ProductId, out var product))
        {
            return Task.FromResult(new ProductQueryResponse(
                request.ProductId, product.Name, product.Brand, product.Price, Found: true));
        }

        return Task.FromResult(new ProductQueryResponse(
            request.ProductId, string.Empty, string.Empty, 0m, Found: false));
    }
}
