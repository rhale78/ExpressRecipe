namespace ExpressRecipe.Client.Shared.Models.User
{
    /// <summary>
    /// Represents a dietary restriction (allergen, preference, religious requirement, etc.)
    /// </summary>
    public class DietaryRestrictionDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty; // allergens, dietary, religious, health
        public string? Description { get; set; }
        public RestrictionSeverity Severity { get; set; } = RestrictionSeverity.Moderate;
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Severity level for dietary restrictions
    /// </summary>
    public enum RestrictionSeverity
    {
        /// <summary>
        /// Informational only - preference
        /// </summary>
        Preference = 0,

        /// <summary>
        /// Should avoid but not life-threatening
        /// </summary>
        Moderate = 1,

        /// <summary>
        /// Must avoid - health consequences
        /// </summary>
        Serious = 2,

        /// <summary>
        /// Life-threatening allergy
        /// </summary>
        Severe = 3
    }

    /// <summary>
    /// Request to add dietary restriction to user profile
    /// </summary>
    public class AddDietaryRestrictionRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = "allergens";
        public string? Description { get; set; }
        public RestrictionSeverity Severity { get; set; } = RestrictionSeverity.Moderate;
    }

    /// <summary>
    /// Request to add family member
    /// </summary>
    public class AddFamilyMemberRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Relationship { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public List<string> Restrictions { get; set; } = [];
    }

    /// <summary>
    /// User's complete dietary profile
    /// </summary>
    public class UserDietaryProfileDto
    {
        public Guid UserId { get; set; }
        public List<DietaryRestrictionDto> PersonalRestrictions { get; set; } = [];
        public List<FamilyMemberDto> FamilyMembers { get; set; } = [];
        public List<string> CustomRestrictions { get; set; } = [];
    }
}
