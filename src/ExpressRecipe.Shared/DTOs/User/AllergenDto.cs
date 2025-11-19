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

public class UserAllergenDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid AllergenId { get; set; }
    public string AllergenName { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime? DiagnosedDate { get; set; }
    public bool VerifiedByDoctor { get; set; }
}

public class AddUserAllergenRequest
{
    [Required]
    public Guid AllergenId { get; set; }

    [Required]
    [RegularExpression("Mild|Moderate|Severe|Anaphylaxis")]
    public string Severity { get; set; } = string.Empty;

    public string? Notes { get; set; }
    public DateTime? DiagnosedDate { get; set; }
    public bool VerifiedByDoctor { get; set; }
}

public class UpdateUserAllergenRequest
{
    [Required]
    [RegularExpression("Mild|Moderate|Severe|Anaphylaxis")]
    public string Severity { get; set; } = string.Empty;

    public string? Notes { get; set; }
    public DateTime? DiagnosedDate { get; set; }
    public bool VerifiedByDoctor { get; set; }
}
