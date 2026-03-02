namespace ExpressRecipe.Shared.DTOs.Product;

/// <summary>
/// DTO for importing a full product with all its related data in one operation
/// </summary>
public class FullProductImportDto
{
    public CreateProductRequest Product { get; set; } = new();
    public List<Guid> IngredientIds { get; set; } = new();
    public List<string> IngredientNames { get; set; } = new(); // For mapping if ID not found yet
    public List<ProductImageDto> Images { get; set; } = new();
    public List<string> Allergens { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
    public string? ExternalId { get; set; }
    public string? ExternalSource { get; set; }
}
