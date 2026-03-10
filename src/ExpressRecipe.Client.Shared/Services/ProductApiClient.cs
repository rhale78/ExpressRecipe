using ExpressRecipe.Client.Shared.Models.Product;

namespace ExpressRecipe.Client.Shared.Services;

public interface IProductApiClient
{
    Task<ProductDto?> GetProductAsync(Guid id);
    Task<ProductDto?> GetProductByUpcAsync(string upc);
    Task<ProductDto?> GetProductByBarcodeAsync(string barcode);
    Task<ProductSearchResult?> SearchProductsAsync(ProductSearchRequest request);
    Task<Dictionary<string, int>?> GetLetterCountsAsync(ProductSearchRequest request);
    Task<Guid?> CreateProductAsync(CreateProductRequest request);
    Task<bool> UpdateProductAsync(Guid id, CreateProductRequest request);
    Task<bool> DeleteProductAsync(Guid id);
}

public class ProductApiClient : ApiClientBase, IProductApiClient
{
    public ProductApiClient(HttpClient httpClient, ITokenProvider tokenProvider)
        : base(httpClient, tokenProvider)
    {
    }

    public async Task<ProductDto?> GetProductAsync(Guid id)
    {
        return await GetAsync<ProductDto>($"/api/products/{id}");
    }

    public async Task<ProductDto?> GetProductByUpcAsync(string upc)
    {
        return await GetAsync<ProductDto>($"/api/products/upc/{upc}");
    }

    public async Task<ProductDto?> GetProductByBarcodeAsync(string barcode)
    {
        return await GetAsync<ProductDto>($"/api/products/barcode/{barcode}");
    }

    public async Task<ProductSearchResult?> SearchProductsAsync(ProductSearchRequest request)
    {
        var queryParams = new List<string>();

        if (!string.IsNullOrEmpty(request.SearchTerm))
            queryParams.Add($"searchTerm={Uri.EscapeDataString(request.SearchTerm)}");

        if (!string.IsNullOrEmpty(request.Brand))
            queryParams.Add($"brand={Uri.EscapeDataString(request.Brand)}");

        if (!string.IsNullOrEmpty(request.FirstLetter))
            queryParams.Add($"firstLetter={Uri.EscapeDataString(request.FirstLetter)}");

        if (!string.IsNullOrEmpty(request.SortBy))
            queryParams.Add($"sortBy={Uri.EscapeDataString(request.SortBy)}");

        queryParams.Add($"pageNumber={request.Page}");
        queryParams.Add($"pageSize={request.PageSize}");

        var query = string.Join("&", queryParams);
        return await GetAsync<ProductSearchResult>($"/api/products/search?{query}");
    }

    public async Task<Dictionary<string, int>?> GetLetterCountsAsync(ProductSearchRequest request)
    {
        var queryParams = new List<string>();

        if (!string.IsNullOrEmpty(request.SearchTerm))
            queryParams.Add($"searchTerm={Uri.EscapeDataString(request.SearchTerm)}");

        if (!string.IsNullOrEmpty(request.Brand))
            queryParams.Add($"brand={Uri.EscapeDataString(request.Brand)}");

        // Note: Don't include FirstLetter when getting counts - we want all letter counts
        // Don't include page/pageSize either - we want total counts

        var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        return await GetAsync<Dictionary<string, int>>($"/api/products/letter-counts{query}");
    }

    public async Task<Guid?> CreateProductAsync(CreateProductRequest request)
    {
        var response = await PostAsync<CreateProductRequest, CreateProductResponse>("/api/products", request);
        return response?.Id;
    }

    public async Task<bool> UpdateProductAsync(Guid id, CreateProductRequest request)
    {
        return await PostAsync($"/api/products/{id}", request);
    }

    public async Task<bool> DeleteProductAsync(Guid id)
    {
        return await DeleteAsync($"/api/products/{id}");
    }

    private class CreateProductResponse
    {
        public Guid Id { get; set; }
    }
}

// ---------------------------------------------------------------------------
// Catalog API client (food groups + substitutes)
// ---------------------------------------------------------------------------

public interface ICatalogApiClient
{
    Task<List<FoodGroupDto>?> GetFoodGroupsAsync(string? search = null, string? functionalRole = null);
    Task<FoodGroupDetailResponse?> GetFoodGroupAsync(Guid id);
    Task<List<SubstituteOptionDto>?> GetSubstitutesAsync(Guid ingredientId, Guid? userId = null, bool filterAllergens = false);
    Task<bool> RecordSubstitutionAsync(RecordSubstitutionRequest request);
}

public class CatalogApiClient : ApiClientBase, ICatalogApiClient
{
    public CatalogApiClient(HttpClient httpClient, ITokenProvider tokenProvider)
        : base(httpClient, tokenProvider)
    {
    }

    public async Task<List<FoodGroupDto>?> GetFoodGroupsAsync(string? search = null, string? functionalRole = null)
    {
        List<string> query = new List<string>();
        if (!string.IsNullOrWhiteSpace(search)) { query.Add($"search={Uri.EscapeDataString(search)}"); }
        if (!string.IsNullOrWhiteSpace(functionalRole)) { query.Add($"functionalRole={Uri.EscapeDataString(functionalRole)}"); }
        string qs = query.Count > 0 ? "?" + string.Join("&", query) : "";
        return await GetAsync<List<FoodGroupDto>>($"/api/catalog/food-groups{qs}");
    }

    public async Task<FoodGroupDetailResponse?> GetFoodGroupAsync(Guid id)
    {
        return await GetAsync<FoodGroupDetailResponse>($"/api/catalog/food-groups/{id}");
    }

    public async Task<List<SubstituteOptionDto>?> GetSubstitutesAsync(
        Guid ingredientId, Guid? userId = null, bool filterAllergens = false)
    {
        List<string> query = new List<string>();
        if (userId.HasValue) { query.Add($"userId={userId}"); }
        if (filterAllergens) { query.Add("filterAllergens=true"); }
        string qs = query.Count > 0 ? "?" + string.Join("&", query) : "";
        return await GetAsync<List<SubstituteOptionDto>>($"/api/catalog/substitutes/{ingredientId}{qs}");
    }

    public async Task<bool> RecordSubstitutionAsync(RecordSubstitutionRequest request)
    {
        return await PostAsync("/api/catalog/substitution-history", request);
    }
}
