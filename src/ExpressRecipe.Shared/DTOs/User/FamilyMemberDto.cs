using System.ComponentModel.DataAnnotations;

namespace ExpressRecipe.Shared.DTOs.User
{
    public class FamilyMemberDto
    {
        public Guid Id { get; set; }
        public Guid PrimaryUserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Relationship { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Notes { get; set; }
        public List<string> Allergens { get; set; } = [];
        public List<string> IngredientsToAvoid { get; set; } = [];
        public List<string> DietaryRestrictions { get; set; } = [];
        public List<string> DislikedFoods { get; set; } = [];
    }


}
