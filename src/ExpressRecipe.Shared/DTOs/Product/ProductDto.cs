using System.ComponentModel.DataAnnotations;

namespace ExpressRecipe.Shared.DTOs.Product;

public class ProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Barcode { get; set; }
    public string? BarcodeType { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? ServingSize { get; set; }
    public string? ServingUnit { get; set; }
    public string? ImageUrl { get; set; }
    public string ApprovalStatus { get; set; } = "Pending";
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? SubmittedBy { get; set; }
    public List<ProductIngredientDto> Ingredients { get; set; } = new();
    public ProductNutritionDto? Nutrition { get; set; }
    public decimal? AverageRating { get; set; }
    public int RatingCount { get; set; }
}

public class CreateProductRequest
{
    [Required]
    [StringLength(300, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Brand { get; set; }

    [StringLength(50)]
    public string? Barcode { get; set; }

    [StringLength(20)]
    public string? BarcodeType { get; set; }

    public string? Description { get; set; }

    [StringLength(100)]
    public string? Category { get; set; }

    [StringLength(100)]
    public string? ServingSize { get; set; }

    [StringLength(50)]
    public string? ServingUnit { get; set; }

    [Url]
    [StringLength(500)]
    public string? ImageUrl { get; set; }

    public List<Guid> IngredientIds { get; set; } = new();
}

public class UpdateProductRequest
{
    [Required]
    [StringLength(300, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Brand { get; set; }

    [StringLength(50)]
    public string? Barcode { get; set; }

    [StringLength(20)]
    public string? BarcodeType { get; set; }

    public string? Description { get; set; }

    [StringLength(100)]
    public string? Category { get; set; }

    [StringLength(100)]
    public string? ServingSize { get; set; }

    [StringLength(50)]
    public string? ServingUnit { get; set; }

    [Url]
    [StringLength(500)]
    public string? ImageUrl { get; set; }
}

public class ApproveProductRequest
{
    public bool Approve { get; set; }
    public string? RejectionReason { get; set; }
}

public class ProductSearchRequest
{
    public string? SearchTerm { get; set; }
    public string? Barcode { get; set; }
    public string? Category { get; set; }
    public string? Brand { get; set; }
    public bool? OnlyApproved { get; set; } = true;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
