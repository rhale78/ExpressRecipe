namespace ExpressRecipe.Shared.Models;

/// <summary>
/// User entity (from Auth Service).
/// </summary>
public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public bool EmailConfirmed { get; set; }
    public string? PhoneNumber { get; set; }
    public bool PhoneNumberConfirmed { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public DateTime? LockoutEnd { get; set; }
    public bool LockoutEnabled { get; set; } = true;
    public int AccessFailedCount { get; set; }
}

/// <summary>
/// User profile entity (from User Service).
/// </summary>
public class UserProfile : BaseEntity
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? DisplayName { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? Country { get; set; }
    public string? Region { get; set; }
    public string? PostalCode { get; set; }
    public string PreferredLanguage { get; set; } = "en-US";
    public SubscriptionTier SubscriptionTier { get; set; } = SubscriptionTier.Free;
}

/// <summary>
/// Dietary restriction for a user.
/// </summary>
public class DietaryRestriction : BaseEntity
{
    public Guid UserId { get; set; }
    public RestrictionType RestrictionType { get; set; }
    public string Name { get; set; } = string.Empty;
    public RestrictionSeverity Severity { get; set; }
    public string? Notes { get; set; }
    public bool VerifiedByProfessional { get; set; }
    public DateTime? VerificationDate { get; set; }
}

/// <summary>
/// Allergen for a user.
/// </summary>
public class Allergen : BaseEntity
{
    public Guid UserId { get; set; }
    public string AllergenName { get; set; } = string.Empty;
    public string AllergenCategory { get; set; } = string.Empty;
    public string? ReactionType { get; set; }
    public AllergenSeverity Severity { get; set; }
    public string? DiagnosedBy { get; set; }
    public DateTime? DiagnosisDate { get; set; }
    public string? Notes { get; set; }
}

public enum SubscriptionTier
{
    Free,
    Premium,
    Family
}

public enum RestrictionType
{
    Medical,
    Religious,
    Health,
    Preference
}

public enum RestrictionSeverity
{
    Critical,
    High,
    Medium,
    Low
}

public enum AllergenSeverity
{
    LifeThreatening,
    Severe,
    Moderate,
    Mild
}
