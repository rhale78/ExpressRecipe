using System.ComponentModel.DataAnnotations;

namespace ExpressRecipe.Shared.DTOs.Recipe;

/// <summary>
/// Family member for per-person recipe ratings
/// </summary>
public class FamilyMemberDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Nickname { get; set; }
    public DateTime? BirthDate { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Per-family-member recipe rating (supports half stars)
/// </summary>
public class UserRecipeFamilyRatingDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid RecipeId { get; set; }
    public Guid? FamilyMemberId { get; set; }
    public string? FamilyMemberName { get; set; } // Denormalized for display
    public decimal Rating { get; set; } // 0.0 - 5.0 in 0.5 increments
    public string? Review { get; set; }
    public bool? WouldMakeAgain { get; set; }
    public DateTime? MadeItDate { get; set; }
    public int MadeItCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Aggregated recipe rating information
/// </summary>
public class RecipeRatingDto
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public decimal AverageRating { get; set; }
    public int TotalRatings { get; set; }
    public int FiveStarCount { get; set; }
    public int FourStarCount { get; set; }
    public int ThreeStarCount { get; set; }
    public int TwoStarCount { get; set; }
    public int OneStarCount { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Recipe rating summary with breakdown by family member
/// </summary>
public class RecipeRatingSummaryDto
{
    public Guid RecipeId { get; set; }
    public decimal OverallAverageRating { get; set; }
    public int TotalRatings { get; set; }
    public List<UserRecipeFamilyRatingDto> FamilyRatings { get; set; } = new();
    public RecipeRatingDto? AggregatedRating { get; set; }
}

/// <summary>
/// Create or update family member
/// </summary>
public class CreateFamilyMemberRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Nickname { get; set; }

    public DateTime? BirthDate { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; } = 0;
}

/// <summary>
/// Create or update recipe rating
/// </summary>
public class CreateRecipeRatingRequest
{
    [Required]
    public Guid RecipeId { get; set; }

    public Guid? FamilyMemberId { get; set; } // NULL for user's own rating

    [Required]
    [Range(0, 5)]
    public decimal Rating { get; set; } // Must be in 0.5 increments: 0, 0.5, 1.0, ..., 5.0

    [StringLength(5000)]
    public string? Review { get; set; }

    public bool? WouldMakeAgain { get; set; }

    public DateTime? MadeItDate { get; set; }

    public int MadeItCount { get; set; } = 0;
}

/// <summary>
/// Update recipe rating
/// </summary>
public class UpdateRecipeRatingRequest
{
    [Required]
    [Range(0, 5)]
    public decimal Rating { get; set; }

    [StringLength(5000)]
    public string? Review { get; set; }

    public bool? WouldMakeAgain { get; set; }

    public DateTime? MadeItDate { get; set; }

    public int MadeItCount { get; set; } = 0;
}
