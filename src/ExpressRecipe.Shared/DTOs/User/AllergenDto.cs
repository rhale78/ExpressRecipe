using System.ComponentModel.DataAnnotations;

namespace ExpressRecipe.Shared.DTOs.User;

public class AllergenDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AlternativeNames { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
}

public class AllergenReactionTypeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Severity { get; set; } = string.Empty; // Mild, Moderate, Severe, Life-Threatening
    public bool RequiresMedicalAttention { get; set; }
    public bool IsCommon { get; set; }
}

public class UserAllergenDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid AllergenId { get; set; }
    public string AllergenName { get; set; } = string.Empty;
    public string SeverityLevel { get; set; } = string.Empty; // Mild, Moderate, Severe, Life-Threatening
    public bool RequiresEpiPen { get; set; }
    public int? OnsetTimeMinutes { get; set; }
    public DateTime? LastReactionDate { get; set; }
    public string? DiagnosedBy { get; set; }
    public DateTime? DiagnosisDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public List<AllergenReactionTypeDto>? ReactionTypes { get; set; }
}

public class UserIngredientAllergyDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? IngredientId { get; set; }
    public Guid? BaseIngredientId { get; set; }
    public string? IngredientName { get; set; }
    public string SeverityLevel { get; set; } = string.Empty; // Mild, Moderate, Severe, Life-Threatening
    public bool RequiresEpiPen { get; set; }
    public int? OnsetTimeMinutes { get; set; }
    public DateTime? LastReactionDate { get; set; }
    public string? DiagnosedBy { get; set; }
    public DateTime? DiagnosisDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public List<AllergenReactionTypeDto>? ReactionTypes { get; set; }
}

public class AllergyIncidentDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? UserAllergenId { get; set; }
    public Guid? UserIngredientAllergyId { get; set; }
    public DateTime IncidentDate { get; set; }
    public string? TriggerSource { get; set; }
    public Guid? TriggerProductId { get; set; }
    public Guid? TriggerRecipeId { get; set; }
    public Guid? TriggerMenuItemId { get; set; }
    public string? Symptoms { get; set; }
    public string SeverityLevel { get; set; } = string.Empty;
    public bool EpiPenUsed { get; set; }
    public bool HospitalVisit { get; set; }
    public string? Treatment { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public List<AllergenReactionTypeDto>? ReactionTypes { get; set; }
}

public class AddUserAllergenRequest
{
    [Required]
    public Guid AllergenId { get; set; }

    [Required]
    [StringLength(50)]
    public string SeverityLevel { get; set; } = string.Empty;

    public bool RequiresEpiPen { get; set; }

    public int? OnsetTimeMinutes { get; set; }

    public DateTime? LastReactionDate { get; set; }

    [StringLength(200)]
    public string? DiagnosedBy { get; set; }

    public DateTime? DiagnosisDate { get; set; }

    public string? Notes { get; set; }

    public List<Guid>? ReactionTypeIds { get; set; }
}

public class UpdateUserAllergenRequest
{
    [Required]
    [StringLength(50)]
    public string SeverityLevel { get; set; } = string.Empty;

    public bool RequiresEpiPen { get; set; }

    public int? OnsetTimeMinutes { get; set; }

    public DateTime? LastReactionDate { get; set; }

    [StringLength(200)]
    public string? DiagnosedBy { get; set; }

    public DateTime? DiagnosisDate { get; set; }

    public string? Notes { get; set; }

    public List<Guid>? ReactionTypeIds { get; set; }
}

public class CreateUserIngredientAllergyRequest
{
    public Guid? IngredientId { get; set; }

    public Guid? BaseIngredientId { get; set; }

    [StringLength(200)]
    public string? IngredientName { get; set; }

    [Required]
    [StringLength(50)]
    public string SeverityLevel { get; set; } = string.Empty;

    public bool RequiresEpiPen { get; set; }

    public int? OnsetTimeMinutes { get; set; }

    public DateTime? LastReactionDate { get; set; }

    [StringLength(200)]
    public string? DiagnosedBy { get; set; }

    public DateTime? DiagnosisDate { get; set; }

    public string? Notes { get; set; }

    public List<Guid>? ReactionTypeIds { get; set; }
}

public class UpdateUserIngredientAllergyRequest
{
    [Required]
    [StringLength(50)]
    public string SeverityLevel { get; set; } = string.Empty;

    public bool RequiresEpiPen { get; set; }

    public int? OnsetTimeMinutes { get; set; }

    public DateTime? LastReactionDate { get; set; }

    [StringLength(200)]
    public string? DiagnosedBy { get; set; }

    public DateTime? DiagnosisDate { get; set; }

    public string? Notes { get; set; }

    public List<Guid>? ReactionTypeIds { get; set; }
}

public class CreateAllergyIncidentRequest
{
    public Guid? UserAllergenId { get; set; }

    public Guid? UserIngredientAllergyId { get; set; }

    [Required]
    public DateTime IncidentDate { get; set; }

    [StringLength(500)]
    public string? TriggerSource { get; set; }

    public Guid? TriggerProductId { get; set; }

    public Guid? TriggerRecipeId { get; set; }

    public Guid? TriggerMenuItemId { get; set; }

    public string? Symptoms { get; set; }

    [Required]
    [StringLength(50)]
    public string SeverityLevel { get; set; } = string.Empty;

    public bool EpiPenUsed { get; set; }

    public bool HospitalVisit { get; set; }

    public string? Treatment { get; set; }

    public string? Notes { get; set; }

    public List<Guid>? ReactionTypeIds { get; set; }
}

public class UserAllergenSummaryDto
{
    public int TotalAllergens { get; set; }
    public int SevereAllergens { get; set; }
    public int RequiringEpiPen { get; set; }
    public int IngredientAllergies { get; set; }
    public int TotalIncidents { get; set; }
    public DateTime? LastIncidentDate { get; set; }
    public List<UserAllergenDto>? RecentAllergens { get; set; }
    public List<UserIngredientAllergyDto>? RecentIngredientAllergies { get; set; }
    public List<AllergyIncidentDto>? RecentIncidents { get; set; }
}
