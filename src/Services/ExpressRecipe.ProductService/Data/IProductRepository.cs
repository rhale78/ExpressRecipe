using ExpressRecipe.Shared.DTOs.Product;

namespace ExpressRecipe.ProductService.Data;

/// <summary>
/// Repository interface for Product operations
/// </summary>
public interface IProductRepository
{
    Task<ProductDto?> GetByIdAsync(Guid id);
    Task<ProductDto?> GetByBarcodeAsync(string barcode);
    Task<ProductDto?> GetProductByBarcodeAsync(string barcode);
    Task<List<ProductDto>> SearchAsync(ProductSearchRequest request);
    Task<int> GetSearchCountAsync(ProductSearchRequest request);
    Task<Dictionary<string, int>> GetLetterCountsAsync(ProductSearchRequest request);
    Task<Guid> CreateAsync(CreateProductRequest request, Guid? createdBy = null);
    Task<Guid> CreateProductAsync(CreateProductRequest request);
    Task<bool> UpdateAsync(Guid id, UpdateProductRequest request, Guid? updatedBy = null);
    Task<bool> DeleteAsync(Guid id, Guid? deletedBy = null);
    Task<bool> ApproveAsync(Guid id, bool approve, Guid approvedBy, string? rejectionReason = null);
    Task<bool> ProductExistsAsync(Guid id);
    Task AddIngredientToProductAsync(Guid productId, string ingredient, int orderIndex = 0);
    Task AddLabelToProductAsync(Guid productId, string label);
    Task AddAllergenToProductAsync(Guid productId, string allergen);
    Task AddExternalLinkAsync(Guid productId, string source, string externalId);
    Task UpdateProductMetadataAsync(Guid productId, string key, string value);
    Task<ProductDto?> GetProductByExternalIdAsync(string source, string externalId);
    Task<int?> GetProductCountAsync();
}
