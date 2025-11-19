using System.ComponentModel.DataAnnotations;

namespace ExpressRecipe.Shared.DTOs.Product;

public class ProductNutritionDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public decimal? Calories { get; set; }
    public decimal? TotalFat { get; set; }
    public decimal? SaturatedFat { get; set; }
    public decimal? TransFat { get; set; }
    public decimal? Cholesterol { get; set; }
    public decimal? Sodium { get; set; }
    public decimal? TotalCarbohydrate { get; set; }
    public decimal? DietaryFiber { get; set; }
    public decimal? Sugars { get; set; }
    public decimal? Protein { get; set; }
    public decimal? VitaminA { get; set; }
    public decimal? VitaminC { get; set; }
    public decimal? Calcium { get; set; }
    public decimal? Iron { get; set; }
    public string? AdditionalNutrients { get; set; }
}

public class UpdateProductNutritionRequest
{
    [Range(0, 10000)]
    public decimal? Calories { get; set; }

    [Range(0, 1000)]
    public decimal? TotalFat { get; set; }

    [Range(0, 1000)]
    public decimal? SaturatedFat { get; set; }

    [Range(0, 1000)]
    public decimal? TransFat { get; set; }

    [Range(0, 1000)]
    public decimal? Cholesterol { get; set; }

    [Range(0, 10000)]
    public decimal? Sodium { get; set; }

    [Range(0, 1000)]
    public decimal? TotalCarbohydrate { get; set; }

    [Range(0, 1000)]
    public decimal? DietaryFiber { get; set; }

    [Range(0, 1000)]
    public decimal? Sugars { get; set; }

    [Range(0, 1000)]
    public decimal? Protein { get; set; }

    [Range(0, 100)]
    public decimal? VitaminA { get; set; }

    [Range(0, 100)]
    public decimal? VitaminC { get; set; }

    [Range(0, 100)]
    public decimal? Calcium { get; set; }

    [Range(0, 100)]
    public decimal? Iron { get; set; }

    public string? AdditionalNutrients { get; set; }
}

public class ProductPriceDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
    public string? Store { get; set; }
    public string? Location { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public Guid ReportedBy { get; set; }
    public DateTime ReportedAt { get; set; }
}

public class ReportProductPriceRequest
{
    [Required]
    [Range(0.01, 999999.99)]
    public decimal Price { get; set; }

    [StringLength(10)]
    public string Currency { get; set; } = "USD";

    [StringLength(200)]
    public string? Store { get; set; }

    [StringLength(500)]
    public string? Location { get; set; }

    [Range(-90, 90)]
    public decimal? Latitude { get; set; }

    [Range(-180, 180)]
    public decimal? Longitude { get; set; }
}

public class ProductRatingDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid UserId { get; set; }
    public int Rating { get; set; }
    public string? Review { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RateProductRequest
{
    [Required]
    [Range(1, 5)]
    public int Rating { get; set; }

    [StringLength(2000)]
    public string? Review { get; set; }
}

public class ProductRecallDto
{
    public Guid Id { get; set; }
    public Guid? ProductId { get; set; }
    public string? Barcode { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string RecallReason { get; set; } = string.Empty;
    public DateTime RecallDate { get; set; }
    public string? RecallSource { get; set; }
    public string? SourceUrl { get; set; }
    public string Severity { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
