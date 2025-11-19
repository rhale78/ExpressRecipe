namespace ExpressRecipe.Shared.DTOs.Product;

public class BaseIngredientDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ScientificName { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public string? Purpose { get; set; }
    public string? CommonNames { get; set; }
    public bool IsAllergen { get; set; }
    public string? AllergenType { get; set; }
    public bool IsAdditive { get; set; }
    public string? AdditiveCode { get; set; }
    public string? NutritionalHighlights { get; set; }
    public bool IsApproved { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? SubmittedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateBaseIngredientRequest
{
    public string Name { get; set; } = string.Empty;
    public string? ScientificName { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public string? Purpose { get; set; }
    public string? CommonNames { get; set; }
    public bool IsAllergen { get; set; }
    public string? AllergenType { get; set; }
    public bool IsAdditive { get; set; }
    public string? AdditiveCode { get; set; }
    public string? NutritionalHighlights { get; set; }
}

public class UpdateBaseIngredientRequest
{
    public string Name { get; set; } = string.Empty;
    public string? ScientificName { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public string? Purpose { get; set; }
    public string? CommonNames { get; set; }
    public bool IsAllergen { get; set; }
    public string? AllergenType { get; set; }
    public bool IsAdditive { get; set; }
    public string? AdditiveCode { get; set; }
    public string? NutritionalHighlights { get; set; }
}

public class BaseIngredientSearchRequest
{
    public string? SearchTerm { get; set; }
    public string? Category { get; set; }
    public bool? IsAllergen { get; set; }
    public bool? IsAdditive { get; set; }
    public bool OnlyApproved { get; set; } = true;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class IngredientBaseComponentDto
{
    public Guid Id { get; set; }
    public Guid IngredientId { get; set; }
    public Guid BaseIngredientId { get; set; }
    public string BaseIngredientName { get; set; } = string.Empty;
    public string? BaseIngredientCategory { get; set; }
    public int OrderIndex { get; set; }
    public decimal? Percentage { get; set; }
    public bool IsMainComponent { get; set; }
    public string? Notes { get; set; }
}

public class AddIngredientBaseComponentRequest
{
    public Guid BaseIngredientId { get; set; }
    public int OrderIndex { get; set; }
    public decimal? Percentage { get; set; }
    public bool IsMainComponent { get; set; }
    public string? Notes { get; set; }
}

public class UpdateIngredientBaseComponentRequest
{
    public int OrderIndex { get; set; }
    public decimal? Percentage { get; set; }
    public bool IsMainComponent { get; set; }
    public string? Notes { get; set; }
}

// Result of parsing an ingredient string
public class ParsedIngredientResult
{
    public string OriginalString { get; set; } = string.Empty;
    public List<ParsedIngredientComponent> Components { get; set; } = new();
}

public class ParsedIngredientComponent
{
    public string Name { get; set; } = string.Empty;
    public Guid? BaseIngredientId { get; set; } // Matched base ingredient if found
    public string? MatchedName { get; set; } // Name of matched base ingredient
    public int OrderIndex { get; set; }
    public List<ParsedIngredientComponent>? SubComponents { get; set; } // For nested ingredients like "Flour (Wheat, Niacin)"
    public bool IsParenthetical { get; set; } // True if this component was in parentheses
}
