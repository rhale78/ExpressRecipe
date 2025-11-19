namespace ExpressRecipe.Client.Shared.Models.User;

public class UserProfileDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string FullName => $"{FirstName} {LastName}";

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
    public string Relationship { get; set; } = string.Empty; // Spouse, Child, Parent, etc.
    public List<string> Allergens { get; set; } = new(); // Major allergen groups
    public List<string> IngredientsToAvoid { get; set; } = new(); // Specific ingredients
    public List<string> DietaryRestrictions { get; set; } = new();
    public List<string> DislikedFoods { get; set; } = new(); // Foods they don't like (preference, not medical)
    public string? Notes { get; set; }
}

public class UpdateUserProfileRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }
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
    public List<string> Allergens { get; set; } = new();
    public List<string> IngredientsToAvoid { get; set; } = new();
    public List<string> DietaryRestrictions { get; set; } = new();
    public List<string> DislikedFoods { get; set; } = new();
    public string? Notes { get; set; }
}

public class UpdateFamilyMemberRequest : CreateFamilyMemberRequest
{
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
