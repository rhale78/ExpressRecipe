using ExpressRecipe.IngredientService.Data;
using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.Shared.Messages;

namespace ExpressRecipe.IngredientService.Services;

/// <summary>
/// Handles ingredient query messages sent by other services (ProductService, RecipeService, etc.)
/// via the request/response messaging pattern, eliminating REST round-trips for lookups and bulk creates.
/// </summary>
public sealed class IngredientQueryHandler :
    IRequestHandler<RequestIngredientLookup, IngredientLookupResponse>,
    IRequestHandler<RequestIngredientBulkCreate, IngredientBulkCreateResponse>
{
    private readonly IIngredientRepository _repository;
    private readonly ILogger<IngredientQueryHandler> _logger;

    public IngredientQueryHandler(
        IIngredientRepository repository,
        ILogger<IngredientQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IngredientLookupResponse> HandleAsync(
        RequestIngredientLookup request,
        MessageContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[IngredientQueryHandler] Bulk lookup: {Count} names", request.Names.Count);

        var results = await _repository.GetIngredientIdsByNamesAsync(request.Names);

        return new IngredientLookupResponse(request.CorrelationId, results);
    }

    public async Task<IngredientBulkCreateResponse> HandleAsync(
        RequestIngredientBulkCreate request,
        MessageContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[IngredientQueryHandler] Bulk create: {Count} names", request.Names.Count);

        var created = await _repository.BulkCreateIngredientsAsync(request.Names);

        return new IngredientBulkCreateResponse(request.CorrelationId, created);
    }
}
