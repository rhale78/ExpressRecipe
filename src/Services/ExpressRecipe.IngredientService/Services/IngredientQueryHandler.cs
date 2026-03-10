using ExpressRecipe.IngredientService.Data;
using ExpressRecipe.IngredientService.Services.Matching;
using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.Shared.Matching;
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
    private readonly IIngredientMatchingService _matchingService;
    private readonly ILogger<IngredientQueryHandler> _logger;

    public IngredientQueryHandler(
        IIngredientRepository repository,
        IIngredientMatchingService matchingService,
        ILogger<IngredientQueryHandler> logger)
    {
        _repository = repository;
        _matchingService = matchingService;
        _logger = logger;
    }

    public async Task<IngredientLookupResponse> HandleAsync(
        RequestIngredientLookup request,
        MessageContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[IngredientQueryHandler] Bulk lookup: {Count} names", request.Names.Count);

        List<MatchResult> matches = await _matchingService.MatchBulkAsync(
            request.Names, request.SourceService ?? "Unknown", ct: cancellationToken);

        Dictionary<string, Guid> ids = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, MatchResult> matchResults = new(StringComparer.OrdinalIgnoreCase);
        foreach (MatchResult match in matches)
        {
            matchResults[match.RawInput] = match;
            if (match.IsResolved) { ids[match.RawInput] = match.IngredientId!.Value; }
        }
        return new IngredientLookupResponse(request.CorrelationId, ids, matchResults);
    }

    public async Task<IngredientBulkCreateResponse> HandleAsync(
        RequestIngredientBulkCreate request,
        MessageContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[IngredientQueryHandler] Bulk create: {Count} names", request.Names.Count);

        int created = await _repository.BulkCreateIngredientsAsync(request.Names);

        return new IngredientBulkCreateResponse(request.CorrelationId, created);
    }
}
