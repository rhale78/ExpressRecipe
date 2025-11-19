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

    // Raw ingredient string from product label (e.g., "Enriched Wheat Flour (Wheat Flour, Niacin, Iron)")
    public string? IngredientListString { get; set; }

    // Base components this ingredient is composed of (populated on demand)
    public List<IngredientBaseComponentDto>? BaseComponents { get; set; }
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

    // Raw ingredient string from product label for this specific ingredient
    public string? IngredientListString { get; set; }
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

    // Optional raw ingredient string for parsing into base components
    public string? IngredientListString { get; set; }
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

    // Optional raw ingredient string for parsing into base components
    public string? IngredientListString { get; set; }
}

public class AddProductIngredientRequest
{
    [Required]
    public Guid IngredientId { get; set; }

    public int OrderIndex { get; set; }
    public string? Quantity { get; set; }
    public string? Notes { get; set; }

    // Optional raw ingredient string from product label
    public string? IngredientListString { get; set; }
}
