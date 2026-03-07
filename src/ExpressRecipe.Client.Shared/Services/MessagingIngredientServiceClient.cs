using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Options;
using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.Shared.Messages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ExpressRecipe.Client.Shared.Services;

/// <summary>
/// Implements <see cref="IIngredientServiceClient"/> using the message bus request/response
/// pattern for lookup and bulk-create operations. All other operations (parsing, validation)
/// delegate to the REST <see cref="IngredientServiceClient"/> since they are CPU-bound work
/// local to the IngredientService that does not benefit from async messaging.
/// </summary>
public sealed class MessagingIngredientServiceClient : IIngredientServiceClient
{
    private readonly IMessageBus _bus;
    private readonly IngredientServiceClient _restFallback;
    private readonly ILogger<MessagingIngredientServiceClient> _logger;
    private readonly TimeSpan _requestTimeout;

    public MessagingIngredientServiceClient(
        IMessageBus bus,
        IngredientServiceClient restFallback,
        ILogger<MessagingIngredientServiceClient> logger,
        IConfiguration configuration)
    {
        _bus            = bus;
        _restFallback   = restFallback;
        _logger         = logger;
        _requestTimeout = TimeSpan.FromSeconds(
            configuration.GetValue("IngredientService:Messaging:TimeoutSeconds", 30));
    }

    public async Task<Dictionary<string, Guid>> LookupIngredientIdsAsync(List<string> names)
    {
        if (names == null || names.Count == 0)
            return new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var request = new RequestIngredientLookup(
                CorrelationId: Guid.NewGuid().ToString(),
                Names: names.Distinct(StringComparer.OrdinalIgnoreCase).ToList());

            var opts = new RequestOptions { Timeout = _requestTimeout };
            var response = await _bus.RequestAsync<RequestIngredientLookup, IngredientLookupResponse>(
                request, opts).ConfigureAwait(false);

            return response.Results;
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("[MessagingIngredientClient] Timeout on LookupIngredientIds ({Count} names). Falling back to REST.", names.Count);
            return await _restFallback.LookupIngredientIdsAsync(names).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[MessagingIngredientClient] Error on LookupIngredientIds. Falling back to REST.");
            return await _restFallback.LookupIngredientIdsAsync(names).ConfigureAwait(false);
        }
    }

    public async Task<int> BulkCreateIngredientsAsync(List<string> names)
    {
        if (names == null || names.Count == 0)
            return 0;

        try
        {
            var request = new RequestIngredientBulkCreate(
                CorrelationId: Guid.NewGuid().ToString(),
                Names: names);

            var opts = new RequestOptions { Timeout = _requestTimeout };
            var response = await _bus.RequestAsync<RequestIngredientBulkCreate, IngredientBulkCreateResponse>(
                request, opts).ConfigureAwait(false);

            return response.Created;
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("[MessagingIngredientClient] Timeout on BulkCreateIngredients ({Count} names). Falling back to REST.", names.Count);
            return await _restFallback.BulkCreateIngredientsAsync(names).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[MessagingIngredientClient] Error on BulkCreateIngredients. Falling back to REST.");
            return await _restFallback.BulkCreateIngredientsAsync(names).ConfigureAwait(false);
        }
    }

    // ── Delegated to REST (CPU-bound parsing on IngredientService) ────────────

    public Task<Guid?> GetIngredientIdByNameAsync(string name)
        => _restFallback.GetIngredientIdByNameAsync(name);

    public Task<IngredientDto?> GetIngredientAsync(Guid id)
        => _restFallback.GetIngredientAsync(id);

    public Task<List<IngredientDto>> GetAllIngredientsAsync()
        => _restFallback.GetAllIngredientsAsync();

    public Task<Guid?> CreateIngredientAsync(CreateIngredientRequest request)
        => _restFallback.CreateIngredientAsync(request);

    public Task<List<string>> ParseIngredientListAsync(string ingredientsText)
        => _restFallback.ParseIngredientListAsync(ingredientsText);

    public Task<Dictionary<string, List<string>>> BulkParseIngredientListsAsync(List<string> texts)
        => _restFallback.BulkParseIngredientListsAsync(texts);

    public Task<ParsedIngredientResult?> ParseIngredientStringAsync(string text)
        => _restFallback.ParseIngredientStringAsync(text);

    public Task<Dictionary<string, ParsedIngredientResult>> BulkParseIngredientStringsAsync(List<string> texts)
        => _restFallback.BulkParseIngredientStringsAsync(texts);

    public Task<IngredientValidationResult?> ValidateIngredientAsync(string ingredient)
        => _restFallback.ValidateIngredientAsync(ingredient);
}
