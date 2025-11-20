namespace ExpressRecipe.Shared.Models;

/// <summary>
/// Product entity.
/// </summary>
public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public Guid? ManufacturerId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? SubCategory { get; set; }
    public string? Description { get; set; }
    public string? UPC { get; set; }
    public string? EAN { get; set; }
    public decimal? PackageSize { get; set; }
    public string? PackageUnit { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsVerified { get; set; }
    public Guid? VerifiedBy { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public Guid? SubmittedBy { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;

    // Navigation properties (not stored in DB, populated by joins)
    public List<Ingredient> Ingredients { get; set; } = new();
}

/// <summary>
/// Ingredient entity.
/// </summary>
public class Ingredient : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsCommonAllergen { get; set; }
    public string? AllergenType { get; set; }
}

/// <summary>
/// Product-Ingredient relationship.
/// </summary>
public class ProductIngredient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Guid IngredientId { get; set; }
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public int OrderIndex { get; set; }
    public bool IsMajorIngredient { get; set; }
    public bool MayContain { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum ApprovalStatus
{
    Pending,
    Approved,
    Rejected
}
