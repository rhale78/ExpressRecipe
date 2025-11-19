using System.ComponentModel.DataAnnotations;

namespace ExpressRecipe.Shared.DTOs.User;

public class UserProfileDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? WeightKg { get; set; }
    public string? ActivityLevel { get; set; }
    public string? CookingSkillLevel { get; set; }
    public string SubscriptionTier { get; set; } = "Free";
    public DateTime? SubscriptionExpiresAt { get; set; }
    // Note: HealthGoals, PreferredCuisines, and Favorite/Disliked Foods
    // are now accessed through separate endpoints for proper normalization
}

public class CreateUserProfileRequest
{
    [Required]
    public Guid UserId { get; set; }

    [StringLength(100)]
    public string? FirstName { get; set; }

    [StringLength(100)]
    public string? LastName { get; set; }

    public DateTime? DateOfBirth { get; set; }

    [StringLength(20)]
    public string? Gender { get; set; }

    [Range(0, 300)]
    public decimal? HeightCm { get; set; }

    [Range(0, 500)]
    public decimal? WeightKg { get; set; }

    public string? ActivityLevel { get; set; }
    public string? CookingSkillLevel { get; set; }
}

public class UpdateUserProfileRequest
{
    [StringLength(100)]
    public string? FirstName { get; set; }

    [StringLength(100)]
    public string? LastName { get; set; }

    public DateTime? DateOfBirth { get; set; }

    [StringLength(20)]
    public string? Gender { get; set; }

    [Range(0, 300)]
    public decimal? HeightCm { get; set; }

    [Range(0, 500)]
    public decimal? WeightKg { get; set; }

    public string? ActivityLevel { get; set; }
    public string? CookingSkillLevel { get; set; }
}
