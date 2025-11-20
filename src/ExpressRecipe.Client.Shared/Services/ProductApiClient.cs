using ExpressRecipe.Client.Shared.Models.Product;

namespace ExpressRecipe.Client.Shared.Services;

public interface IProductApiClient
{
    Task<ProductDto?> GetProductAsync(Guid id);
    Task<ProductDto?> GetProductByUpcAsync(string upc);
    Task<ProductSearchResult?> SearchProductsAsync(ProductSearchRequest request);
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

    public async Task<ProductSearchResult?> SearchProductsAsync(ProductSearchRequest request)
    {
        var queryParams = new List<string>();

        if (!string.IsNullOrEmpty(request.SearchTerm))
            queryParams.Add($"searchTerm={Uri.EscapeDataString(request.SearchTerm)}");

        if (!string.IsNullOrEmpty(request.Brand))
            queryParams.Add($"brand={Uri.EscapeDataString(request.Brand)}");

        queryParams.Add($"page={request.Page}");
        queryParams.Add($"pageSize={request.PageSize}");

        var query = string.Join("&", queryParams);
        return await GetAsync<ProductSearchResult>($"/api/products/search?{query}");
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
