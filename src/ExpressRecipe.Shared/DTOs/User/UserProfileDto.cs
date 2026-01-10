using System.ComponentModel.DataAnnotations;

namespace ExpressRecipe.Shared.DTOs.User;

public class UserProfileDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string FullName => $"{FirstName} {LastName}";
    public string? Phone { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? WeightKg { get; set; }
    public string? ActivityLevel { get; set; }
    public string? CookingSkillLevel { get; set; }
    public string SubscriptionTier { get; set; } = "Free";
    public DateTime? SubscriptionExpiresAt { get; set; }
    
    // User's personal dietary restrictions
    public List<string> Allergens { get; set; } = new(); // Major allergen groups (Milk, Eggs, etc.)
    public List<string> IngredientsToAvoid { get; set; } = new(); // Specific ingredients
    public List<string> DietaryRestrictions { get; set; } = new();
    public List<string> DislikedFoods { get; set; } = new(); // Foods they don't like (preference, not medical)
    
    // Health goals
    public int? DailyCalorieGoal { get; set; }
    
    // Family members
    public List<FamilyMemberDto> FamilyMembers { get; set; } = new();
    
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateUserProfileRequest
{
    [Required]
    public Guid UserId { get; set; }

    [StringLength(100)]
    public string? FirstName { get; set; }

    [StringLength(100)]
    public string? LastName { get; set; }

    [StringLength(256)]
    public string? Email { get; set; }

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

/// <summary>
/// Request for creating user profile during registration (service-to-service)
/// </summary>
public class CreateUserProfileForNewUserRequest
{
    [Required]
    public Guid UserId { get; set; }

    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;
}

public class UpdateUserProfileRequest
{
    [StringLength(100)]
    public string? FirstName { get; set; }

    [StringLength(100)]
    public string? LastName { get; set; }

    public string? Phone { get; set; }

    public DateTime? DateOfBirth { get; set; }

    [StringLength(20)]
    public string? Gender { get; set; }

    [Range(0, 300)]
    public decimal? HeightCm { get; set; }

    [Range(0, 500)]
    public decimal? WeightKg { get; set; }

    public string? ActivityLevel { get; set; }
    public string? CookingSkillLevel { get; set; }
    
    public List<string> Allergens { get; set; } = new();
    public List<string> IngredientsToAvoid { get; set; } = new();
    public List<string> DietaryRestrictions { get; set; } = new();
    public List<string> DislikedFoods { get; set; } = new();
    public int? DailyCalorieGoal { get; set; }
}

public class CreateFamilyMemberRequest
{
    public string Name { get; set; } = string.Empty;
    public string Relationship { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public List<string> Allergens { get; set; } = new();
    public List<string> IngredientsToAvoid { get; set; } = new();
    public List<string> DietaryRestrictions { get; set; } = new();
    public List<string> DislikedFoods { get; set; } = new();
    public string? Notes { get; set; }
}

public class UpdateFamilyMemberRequest : CreateFamilyMemberRequest
{
    public new DateTime? DateOfBirth { get; set; }
}

public class AllergensAndRestrictionsDto
{
    public List<string> UserAllergens { get; set; } = new();
    public List<string> UserDietaryRestrictions { get; set; } = new();
    public List<string> UserDislikedFoods { get; set; } = new();
    public List<string> FamilyAllergens { get; set; } = new();
    public List<string> FamilyDietaryRestrictions { get; set; } = new();
    public List<string> FamilyDislikedFoods { get; set; } = new();

    // Combined lists (user + all family members)
    public List<string> AllAllergens => UserAllergens.Concat(FamilyAllergens).Distinct().ToList();
    public List<string> AllDietaryRestrictions => UserDietaryRestrictions.Concat(FamilyDietaryRestrictions).Distinct().ToList();
    public List<string> AllDislikedFoods => UserDislikedFoods.Concat(FamilyDislikedFoods).Distinct().ToList();
}


