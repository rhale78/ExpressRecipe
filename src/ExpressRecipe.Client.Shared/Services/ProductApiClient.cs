using ExpressRecipe.Client.Shared.Models.Product;

namespace ExpressRecipe.Client.Shared.Services
{
    public interface IProductApiClient
    {
        Task<ProductDto?> GetProductAsync(Guid id);
        Task<ProductDto?> GetProductByUpcAsync(string upc);
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

        public async Task<ProductSearchResult?> SearchProductsAsync(ProductSearchRequest request)
        {
            List<string> queryParams = [];

            if (!string.IsNullOrEmpty(request.SearchTerm))
            {
                queryParams.Add($"searchTerm={Uri.EscapeDataString(request.SearchTerm)}");
            }

            if (!string.IsNullOrEmpty(request.Brand))
            {
                queryParams.Add($"brand={Uri.EscapeDataString(request.Brand)}");
            }

            if (!string.IsNullOrEmpty(request.FirstLetter))
            {
                queryParams.Add($"firstLetter={Uri.EscapeDataString(request.FirstLetter)}");
            }

            if (!string.IsNullOrEmpty(request.SortBy))
            {
                queryParams.Add($"sortBy={Uri.EscapeDataString(request.SortBy)}");
            }

            queryParams.Add($"pageNumber={request.Page}");
            queryParams.Add($"pageSize={request.PageSize}");

            var query = string.Join("&", queryParams);
            return await GetAsync<ProductSearchResult>($"/api/products/search?{query}");
        }

        public async Task<Dictionary<string, int>?> GetLetterCountsAsync(ProductSearchRequest request)
        {
            List<string> queryParams = [];

            if (!string.IsNullOrEmpty(request.SearchTerm))
            {
                queryParams.Add($"searchTerm={Uri.EscapeDataString(request.SearchTerm)}");
            }

            if (!string.IsNullOrEmpty(request.Brand))
            {
                queryParams.Add($"brand={Uri.EscapeDataString(request.Brand)}");
            }

            // Note: Don't include FirstLetter when getting counts - we want all letter counts
            // Don't include page/pageSize either - we want total counts

            var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
            return await GetAsync<Dictionary<string, int>>($"/api/products/letter-counts{query}");
        }

        public async Task<Guid?> CreateProductAsync(CreateProductRequest request)
        {
            CreateProductResponse? response = await PostAsync<CreateProductRequest, CreateProductResponse>("/api/products", request);
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
}
