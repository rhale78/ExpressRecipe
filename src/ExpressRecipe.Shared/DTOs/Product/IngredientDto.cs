using System.ComponentModel.DataAnnotations;

namespace ExpressRecipe.Shared.DTOs.Product;

public class IngredientDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AlternativeNames { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool IsCommonAllergen { get; set; }
    public List<Guid> AllergenIds { get; set; } = new();
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
}

public class CreateIngredientRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    public string? AlternativeNames { get; set; }
    public string? Description { get; set; }

    [StringLength(100)]
    public string? Category { get; set; }

    public bool IsCommonAllergen { get; set; }
    public List<Guid> AllergenIds { get; set; } = new();
}

public class UpdateIngredientRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    public string? AlternativeNames { get; set; }
    public string? Description { get; set; }

    [StringLength(100)]
    public string? Category { get; set; }

    public bool IsCommonAllergen { get; set; }
}

public class AddProductIngredientRequest
{
    [Required]
    public Guid IngredientId { get; set; }

    public int OrderIndex { get; set; }
    public string? Quantity { get; set; }
    public string? Notes { get; set; }
}
