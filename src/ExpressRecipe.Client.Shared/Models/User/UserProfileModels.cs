namespace ExpressRecipe.Client.Shared.Models.User
{
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
        public List<string> Allergens { get; set; } = []; // Major allergen groups (Milk, Eggs, etc.)
        public List<string> IngredientsToAvoid { get; set; } = []; // Specific ingredients (annatto, gelatin, strawberries, etc.)
        public List<string> DietaryRestrictions { get; set; } = [];
        public List<string> DislikedFoods { get; set; } = []; // Foods they don't like (preference, not medical)

        // Health goals
        public int? DailyCalorieGoal { get; set; }

        // Family members
        public List<FamilyMemberDto> FamilyMembers { get; set; } = [];

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class FamilyMemberDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Relationship { get; set; } = string.Empty; // Spouse, Child, Parent, etc.
        public List<string> Allergens { get; set; } = []; // Major allergen groups
        public List<string> IngredientsToAvoid { get; set; } = []; // Specific ingredients
        public List<string> DietaryRestrictions { get; set; } = [];
        public List<string> DislikedFoods { get; set; } = []; // Foods they don't like (preference, not medical)
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
    
        public List<string> Allergens { get; set; } = [];
        public List<string> IngredientsToAvoid { get; set; } = [];
        public List<string> DietaryRestrictions { get; set; } = [];
        public List<string> DislikedFoods { get; set; } = [];
        public int? DailyCalorieGoal { get; set; }
    }

    public class CreateFamilyMemberRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Relationship { get; set; } = string.Empty;
        public List<string> Allergens { get; set; } = [];
        public List<string> IngredientsToAvoid { get; set; } = [];
        public List<string> DietaryRestrictions { get; set; } = [];
        public List<string> DislikedFoods { get; set; } = [];
        public string? Notes { get; set; }
    }

    public class UpdateFamilyMemberRequest : CreateFamilyMemberRequest
    {
    }

    public class AllergensAndRestrictionsDto
    {
        public List<string> UserAllergens { get; set; } = [];
        public List<string> UserDietaryRestrictions { get; set; } = [];
        public List<string> UserDislikedFoods { get; set; } = [];
        public List<string> FamilyAllergens { get; set; } = [];
        public List<string> FamilyDietaryRestrictions { get; set; } = [];
        public List<string> FamilyDislikedFoods { get; set; } = [];

        // Combined lists (user + all family members)
        public List<string> AllAllergens => UserAllergens.Concat(FamilyAllergens).Distinct().ToList();
        public List<string> AllDietaryRestrictions => UserDietaryRestrictions.Concat(FamilyDietaryRestrictions).Distinct().ToList();
        public List<string> AllDislikedFoods => UserDislikedFoods.Concat(FamilyDislikedFoods).Distinct().ToList();
    }
}
