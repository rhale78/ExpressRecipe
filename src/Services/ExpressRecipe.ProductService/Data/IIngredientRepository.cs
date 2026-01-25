using ExpressRecipe.Shared.DTOs.Product;

namespace ExpressRecipe.ProductService.Data
{
    /// <summary>
    /// Repository interface for Ingredient operations
    /// </summary>
    public interface IIngredientRepository
    {
        Task<List<IngredientDto>> GetAllAsync();
        Task<IngredientDto?> GetByIdAsync(Guid id);
        Task<List<IngredientDto>> SearchByNameAsync(string searchTerm);
        Task<List<IngredientDto>> GetByCategoryAsync(string category);
        Task<Guid> CreateAsync(CreateIngredientRequest request, Guid? createdBy = null);
        Task<bool> UpdateAsync(Guid id, UpdateIngredientRequest request, Guid? updatedBy = null);
        Task<bool> DeleteAsync(Guid id, Guid? deletedBy = null);
        Task<List<ProductIngredientDto>> GetProductIngredientsAsync(Guid productId);
        Task<Guid> AddProductIngredientAsync(Guid productId, AddProductIngredientRequest request, Guid? createdBy = null);
        Task<bool> RemoveProductIngredientAsync(Guid productIngredientId, Guid? deletedBy = null);
        Task<Dictionary<string, Guid>> GetIngredientIdsByNamesAsync(IEnumerable<string> names);
        Task<int> BulkCreateIngredientsAsync(IEnumerable<string> names, Guid? createdBy = null);
        Task<int> BulkAddProductIngredientsAsync(IEnumerable<(Guid ProductId, Guid IngredientId, int OrderIndex)> links);
    }
}
