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
    public List<string> Allergens { get; set; } = new();
    public List<string> IngredientsToAvoid { get; set; } = new();
    public List<string> DietaryRestrictions { get; set; } = new();
    public List<string> DislikedFoods { get; set; } = new();
    
    // New fields for user account linking and roles
    public Guid? UserId { get; set; }
    public string UserRole { get; set; } = "Member"; // Admin, Member, Guest
    public bool HasUserAccount { get; set; }
    public bool IsGuest { get; set; }
    public Guid? LinkedUserId { get; set; } // For guests sharing from another family
    public string? Email { get; set; }
    public List<FamilyRelationshipDto> Relationships { get; set; } = new();
}

public class FamilyRelationshipDto
{
    public Guid Id { get; set; }
    public Guid FamilyMemberId { get; set; }
    public Guid RelatedMemberId { get; set; }
    public string RelatedMemberName { get; set; } = string.Empty;
    public string RelationshipType { get; set; } = string.Empty; // Parent, Child, Spouse, Sibling, etc.
    public string? Notes { get; set; }
}

public class CreateFamilyRelationshipRequest
{
    [Required]
    public Guid FamilyMemberId2 { get; set; }
    
    [Required]
    [StringLength(50)]
    public string RelationshipType { get; set; } = string.Empty;
    
    [StringLength(500)]
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
    public int Rating { get; set; } // 1-5 stars
    public string? ReviewText { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateUserProductRatingRequest
{
    public Guid ProductId { get; set; }
    
    [Required]
    [Range(1, 5)]
    public int Rating { get; set; }
    
    [StringLength(2000)]
    public string? ReviewText { get; set; }
}


