using System.ComponentModel.DataAnnotations;

namespace ExpressRecipe.Shared.DTOs.User;

public class FamilyMemberDto
{
    public Guid Id { get; set; }
    public Guid PrimaryUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Relationship { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Notes { get; set; }
    public List<UserAllergenDto> Allergens { get; set; } = new();
    public List<UserDietaryRestrictionDto> DietaryRestrictions { get; set; } = new();
}

public class CreateFamilyMemberRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(50)]
    public string? Relationship { get; set; }

    public DateTime? DateOfBirth { get; set; }
    public string? Notes { get; set; }
}

public class UpdateFamilyMemberRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(50)]
    public string? Relationship { get; set; }

    public DateTime? DateOfBirth { get; set; }
    public string? Notes { get; set; }
}
