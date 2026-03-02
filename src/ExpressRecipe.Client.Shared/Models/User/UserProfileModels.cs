namespace ExpressRecipe.Client.Shared.Models.User;

public class UserProfileDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string FullName => $"{FirstName} {LastName}";

    // Health & Physical Info
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? WeightKg { get; set; }
    public string? ActivityLevel { get; set; }
    public string? CookingSkillLevel { get; set; }

    // User's personal dietary restrictions
    public List<string> Allergens { get; set; } = new(); // Major allergen groups (Milk, Eggs, etc.)
    public List<string> IngredientsToAvoid { get; set; } = new(); // Specific ingredients (annatto, gelatin, strawberries, etc.)
    public List<string> DietaryRestrictions { get; set; } = new();
    public List<string> DislikedFoods { get; set; } = new(); // Foods they don't like (preference, not medical)

    // Health goals
    public int? DailyCalorieGoal { get; set; }

    // Family members
    public List<FamilyMemberDto> FamilyMembers { get; set; } = new();

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class FamilyMemberDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Relationship { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public List<string> Allergens { get; set; } = new(); // Major allergen groups
    public List<string> IngredientsToAvoid { get; set; } = new(); // Specific ingredients
    public List<string> DietaryRestrictions { get; set; } = new();
    public List<string> DislikedFoods { get; set; } = new(); // Foods they don't like (preference, not medical)
    public string? Notes { get; set; }
    
    // New fields for account linking and roles
    public Guid? UserId { get; set; }
    public string UserRole { get; set; } = "Member"; // Admin, Member, Guest
    public bool HasUserAccount { get; set; }
    public bool IsGuest { get; set; }
    public string? Email { get; set; }
    public List<FamilyRelationshipDto> Relationships { get; set; } = new();
}

public class FamilyRelationshipDto
{
    public Guid Id { get; set; }
    public Guid FamilyMemberId { get; set; }
    public Guid RelatedMemberId { get; set; }
    public string RelatedMemberName { get; set; } = string.Empty;
    public string RelationshipType { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class UpdateUserProfileRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    
    // Health & Physical Info
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public decimal? HeightCm { get; set; }
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
    public string? Relationship { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public List<string> Allergens { get; set; } = new();
    public List<string> IngredientsToAvoid { get; set; } = new();
    public List<string> DietaryRestrictions { get; set; } = new();
    public List<string> DislikedFoods { get; set; } = new();
    public string? Notes { get; set; }
    public string UserRole { get; set; } = "Member";
    public bool IsGuest { get; set; }
}

public class CreateFamilyMemberWithAccountRequest : CreateFamilyMemberRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool SendWelcomeEmail { get; set; } = true;
}

public class UpdateFamilyMemberRequest : CreateFamilyMemberRequest
{
}

public class CreateFamilyRelationshipRequest
{
    public Guid FamilyMemberId2 { get; set; }
    public string RelationshipType { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class UserFavoriteRecipeDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid RecipeId { get; set; }
    public string? RecipeName { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UserFavoriteProductDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UserProductRatingDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ProductId { get; set; }
    public string? ProductName { get; set; }
    public int Rating { get; set; }
    public string? ReviewText { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class ProductRatingStatsDto
{
    public double AverageRating { get; set; }
    public int TotalRatings { get; set; }
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
