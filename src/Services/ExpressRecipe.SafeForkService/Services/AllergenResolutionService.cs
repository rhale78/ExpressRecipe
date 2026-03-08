using ExpressRecipe.SafeForkService.Contracts.Responses;
using ExpressRecipe.SafeForkService.Data;
using System.Net.Http.Json;

namespace ExpressRecipe.SafeForkService.Services;

public class AllergenResolutionService
{
    private readonly IAllergenProfileRepository _profileRepo;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AllergenResolutionService> _logger;

    public AllergenResolutionService(
        IAllergenProfileRepository profileRepo,
        IHttpClientFactory httpClientFactory,
        ILogger<AllergenResolutionService> logger)
    {
        _profileRepo = profileRepo;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Attempts to resolve a freeform allergen entry through the resolution pipeline.
    /// Returns true if at least one link was written, false if still unresolved.
    /// </summary>
    public async Task<bool> TryResolveAsync(Guid allergenProfileId, Guid memberId, string freeFormText, string? brand, CancellationToken ct)
    {
        Guid? ingredientId = null;
        string? matchMethod = null;
        int linksWritten = 0;

        // Step 1: Direct name match against IngredientService
        (ingredientId, matchMethod) = await TryIngredientDirectNameAsync(freeFormText, ct);

        // Step 2: Alias match against IngredientService
        if (!ingredientId.HasValue)
        {
            (ingredientId, matchMethod) = await TryIngredientAliasAsync(freeFormText, ct);
        }

        // Step 3: Barcode/UPC match against ProductService
        Guid? productId = null;
        if (!ingredientId.HasValue)
        {
            (productId, matchMethod) = await TryProductBarcodeAsync(freeFormText, ct);
        }

        // Step 4: Product name contains match
        if (!ingredientId.HasValue && !productId.HasValue)
        {
            (productId, matchMethod) = await TryProductNameAsync(freeFormText, ct);
        }

        // Step 5: Walk ProductIngredient graph if we have a product
        if (productId.HasValue)
        {
            List<Guid> ingredientIds = await WalkProductIngredientGraphAsync(productId.Value, ct);
            foreach (Guid linkedIngredient in ingredientIds)
            {
                await _profileRepo.AddLinkAsync(
                    allergenProfileId,
                    "Ingredient",
                    linkedIngredient,
                    "IngredientGraph",
                    ct: ct);
                linksWritten++;
            }

            if (linksWritten == 0)
            {
                await _profileRepo.AddLinkAsync(
                    allergenProfileId,
                    "Product",
                    productId.Value,
                    matchMethod ?? "ProductName",
                    ct: ct);
                linksWritten++;
            }
        }

        if (ingredientId.HasValue)
        {
            await _profileRepo.AddLinkAsync(
                allergenProfileId,
                "Ingredient",
                ingredientId.Value,
                matchMethod ?? "DirectName",
                ct: ct);
            linksWritten++;
        }

        if (linksWritten == 0)
        {
            // No match found — mark as unresolved
            await _profileRepo.SetUnresolvedAsync(allergenProfileId, true, ct);
            _logger.LogInformation("Allergen entry {ProfileId} remains unresolved for text '{Text}'", allergenProfileId, freeFormText);
            return false;
        }

        await _profileRepo.SetUnresolvedAsync(allergenProfileId, false, ct);
        _logger.LogInformation("Allergen entry {ProfileId} resolved with {Count} links for text '{Text}'", allergenProfileId, linksWritten, freeFormText);
        return true;
    }

    /// <summary>
    /// Detects common ingredient patterns when a member has 5+ ingredient links.
    /// Returns candidates grouped by frequency.
    /// </summary>
    public async Task<List<(Guid IngredientId, int Count)>> DetectCommonIngredientsAsync(Guid memberId, CancellationToken ct)
    {
        int totalLinks = await _profileRepo.CountLinksByMemberIngredientAsync(memberId, ct);
        if (totalLinks < 5)
        {
            return new List<(Guid, int)>();
        }

        return await _profileRepo.GetTopIngredientLinksAsync(memberId, minCount: 5, ct);
    }

    private async Task<(Guid? Id, string? Method)> TryIngredientDirectNameAsync(string name, CancellationToken ct)
    {
        try
        {
            HttpClient client = _httpClientFactory.CreateClient("IngredientService");
            HttpResponseMessage response = await client.GetAsync($"/api/ingredients/search?name={Uri.EscapeDataString(name)}&exact=true", ct);

            if (response.IsSuccessStatusCode)
            {
                IngredientSearchResult? result = await response.Content.ReadFromJsonAsync<IngredientSearchResult>(cancellationToken: ct);
                if (result?.Id != null)
                {
                    return (result.Id, "DirectName");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "IngredientService direct name lookup failed for '{Name}'", name);
        }

        return (null, null);
    }

    private async Task<(Guid? Id, string? Method)> TryIngredientAliasAsync(string name, CancellationToken ct)
    {
        try
        {
            HttpClient client = _httpClientFactory.CreateClient("IngredientService");
            HttpResponseMessage response = await client.GetAsync($"/api/ingredients/search?name={Uri.EscapeDataString(name)}&alias=true", ct);

            if (response.IsSuccessStatusCode)
            {
                IngredientSearchResult? result = await response.Content.ReadFromJsonAsync<IngredientSearchResult>(cancellationToken: ct);
                if (result?.Id != null)
                {
                    return (result.Id, "Alias");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "IngredientService alias lookup failed for '{Name}'", name);
        }

        return (null, null);
    }

    private async Task<(Guid? Id, string? Method)> TryProductBarcodeAsync(string barcode, CancellationToken ct)
    {
        try
        {
            HttpClient client = _httpClientFactory.CreateClient("ProductService");
            HttpResponseMessage response = await client.GetAsync($"/api/products/barcode/{Uri.EscapeDataString(barcode)}", ct);

            if (response.IsSuccessStatusCode)
            {
                ProductSearchResult? result = await response.Content.ReadFromJsonAsync<ProductSearchResult>(cancellationToken: ct);
                if (result?.Id != null)
                {
                    return (result.Id, "UpcLookup");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ProductService barcode lookup failed for '{Barcode}'", barcode);
        }

        return (null, null);
    }

    private async Task<(Guid? Id, string? Method)> TryProductNameAsync(string name, CancellationToken ct)
    {
        try
        {
            HttpClient client = _httpClientFactory.CreateClient("ProductService");
            HttpResponseMessage response = await client.GetAsync($"/api/products/search?name={Uri.EscapeDataString(name)}", ct);

            if (response.IsSuccessStatusCode)
            {
                ProductSearchResult? result = await response.Content.ReadFromJsonAsync<ProductSearchResult>(cancellationToken: ct);
                if (result?.Id != null)
                {
                    return (result.Id, "ProductName");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ProductService name lookup failed for '{Name}'", name);
        }

        return (null, null);
    }

    private async Task<List<Guid>> WalkProductIngredientGraphAsync(Guid productId, CancellationToken ct)
    {
        try
        {
            HttpClient client = _httpClientFactory.CreateClient("ProductService");
            HttpResponseMessage response = await client.GetAsync($"/api/products/{productId}/ingredients", ct);

            if (response.IsSuccessStatusCode)
            {
                List<Guid>? ingredientIds = await response.Content.ReadFromJsonAsync<List<Guid>>(cancellationToken: ct);
                return ingredientIds ?? new List<Guid>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ProductService ingredient graph walk failed for product {ProductId}", productId);
        }

        return new List<Guid>();
    }

    // Simple DTOs for HTTP deserialization
    private sealed class IngredientSearchResult
    {
        public Guid? Id { get; set; }
        public string? Name { get; set; }
    }

    private sealed class ProductSearchResult
    {
        public Guid? Id { get; set; }
        public string? Name { get; set; }
    }
}
