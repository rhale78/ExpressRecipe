using System.ComponentModel.DataAnnotations;

namespace ExpressRecipe.Shared.DTOs.Product;

public class RestaurantDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Description { get; set; }
    public string? CuisineType { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Country { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Website { get; set; }
    public string? ImageUrl { get; set; }
    public string? PriceRange { get; set; }
    public bool IsChain { get; set; }
    public string ApprovalStatus { get; set; } = "Pending";
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? SubmittedBy { get; set; }
    public decimal? AverageRating { get; set; }
    public int RatingCount { get; set; }
}

public class CreateRestaurantRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Brand { get; set; }

    public string? Description { get; set; }

    [StringLength(100)]
    public string? CuisineType { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(50)]
    public string? State { get; set; }

    [StringLength(20)]
    public string? ZipCode { get; set; }

    [StringLength(100)]
    public string? Country { get; set; }

    [Range(-90, 90)]
    public decimal? Latitude { get; set; }

    [Range(-180, 180)]
    public decimal? Longitude { get; set; }

    [Phone]
    [StringLength(20)]
    public string? PhoneNumber { get; set; }

    [Url]
    [StringLength(500)]
    public string? Website { get; set; }

    [Url]
    [StringLength(500)]
    public string? ImageUrl { get; set; }

    [RegularExpression(@"^\$+$")]
    [StringLength(10)]
    public string? PriceRange { get; set; }

    public bool IsChain { get; set; }
}

public class UpdateRestaurantRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Brand { get; set; }

    public string? Description { get; set; }

    [StringLength(100)]
    public string? CuisineType { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(50)]
    public string? State { get; set; }

    [StringLength(20)]
    public string? ZipCode { get; set; }

    [StringLength(100)]
    public string? Country { get; set; }

    [Range(-90, 90)]
    public decimal? Latitude { get; set; }

    [Range(-180, 180)]
    public decimal? Longitude { get; set; }

    [Phone]
    [StringLength(20)]
    public string? PhoneNumber { get; set; }

    [Url]
    [StringLength(500)]
    public string? Website { get; set; }

    [Url]
    [StringLength(500)]
    public string? ImageUrl { get; set; }

    [RegularExpression(@"^\$+$")]
    [StringLength(10)]
    public string? PriceRange { get; set; }

    public bool IsChain { get; set; }
}

public class RestaurantSearchRequest
{
    public string? SearchTerm { get; set; }
    public string? CuisineType { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public double? RadiusMiles { get; set; }
    public bool? OnlyApproved { get; set; } = true;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class UserRestaurantRatingDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid RestaurantId { get; set; }
    public string RestaurantName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? Review { get; set; }
    public DateTime? VisitDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RateRestaurantRequest
{
    [Required]
    [Range(1, 5)]
    public int Rating { get; set; }

    [StringLength(2000)]
    public string? Review { get; set; }

    public DateTime? VisitDate { get; set; }
}
