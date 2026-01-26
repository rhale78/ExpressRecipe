namespace ExpressRecipe.Client.Shared.Models.Product
{
    public class ProductDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? Description { get; set; }
        public string? UPC { get; set; }
        public string? ImageUrl { get; set; }
        public List<string> Allergens { get; set; } = [];
        public List<ProductIngredientDto> Ingredients { get; set; } = [];
        public DateTime CreatedAt { get; set; }
    }

    public class ProductIngredientDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public Guid IngredientId { get; set; }
        public string IngredientName { get; set; } = string.Empty;
        public int OrderIndex { get; set; }
        public string? Quantity { get; set; }
        public string? Notes { get; set; }
        public string? IngredientListString { get; set; }
    }

    public class ProductSearchRequest
    {
        public string? SearchTerm { get; set; }
        public string? Brand { get; set; }
        public List<string>? Allergens { get; set; }
        public List<string>? Restrictions { get; set; } // Dietary restrictions to exclude
        public string? FirstLetter { get; set; }
        public string? SortBy { get; set; } = "name"; // "name", "brand", "created"
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class ProductSearchResult
    {
        public List<ProductDto> Products { get; set; } = [];
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    }

    public class CreateProductRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? UPC { get; set; }
        public List<string> Allergens { get; set; } = [];
        public List<string> Ingredients { get; set; } = [];
    }
}
