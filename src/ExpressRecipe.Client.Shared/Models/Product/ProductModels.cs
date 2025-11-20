namespace ExpressRecipe.Client.Shared.Models.Product;

public class ProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? UPC { get; set; }
    public string? ImageUrl { get; set; }
    public List<string> Allergens { get; set; } = new();
    public List<string> Ingredients { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public class ProductSearchRequest
{
    public string? SearchTerm { get; set; }
    public string? Brand { get; set; }
    public List<string>? Allergens { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class ProductSearchResult
{
    public List<ProductDto> Products { get; set; } = new();
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
    public List<string> Allergens { get; set; } = new();
    public List<string> Ingredients { get; set; } = new();
}
