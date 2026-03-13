namespace ExpressRecipe.Client.Shared.Models.Restaurant;

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
    public bool IsApproved { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string ApprovalStatus { get; set; } = string.Empty;
    public Guid? SubmittedBy { get; set; }
    public decimal? AverageRating { get; set; }
    public int RatingCount { get; set; }
}

public class CreateRestaurantRequest
{
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
}

public class UpdateRestaurantRequest
{
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
    public bool? OnlyApproved { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class RestaurantSearchResult
{
    public List<RestaurantDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}

public class UserRestaurantRatingDto
{
    public Guid UserId { get; set; }
    public Guid RestaurantId { get; set; }
    public int Rating { get; set; }
    public string? Review { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class RateRestaurantRequest
{
    public int Rating { get; set; }
    public string? Review { get; set; }
}
