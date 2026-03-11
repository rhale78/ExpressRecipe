using System.ComponentModel.DataAnnotations;

namespace ExpressRecipe.Shared.DTOs.User;

// ─── Incident Engine DTOs ───────────────────────────────────────────────────

public class AllergyIncidentV2Dto
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public DateTime IncidentDate { get; set; }
    public string ExposureType { get; set; } = "Ingestion"; // Ingestion | Touch | Smell
    public string? ReactionLatency { get; set; }            // Immediate | Minutes | Hours | Delayed
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<AllergyIncidentProductDto> Products { get; set; } = new();
    public List<AllergyIncidentMemberDto> Members { get; set; } = new();
}

public class AllergyIncidentProductDto
{
    public Guid Id { get; set; }
    public Guid? ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public bool HadReaction { get; set; } = true;
}

public class AllergyIncidentMemberDto
{
    public Guid Id { get; set; }
    public Guid? MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string? ReactionTypes { get; set; }
    public string? TreatmentType { get; set; }
    public string? TreatmentDose { get; set; }
    public int? ResolutionTimeMinutes { get; set; }
    public bool RequiredEpipen { get; set; }
    public bool RequiredER { get; set; }
}

public class SuspectedAllergenDto
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public Guid? MemberId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public decimal ConfidenceScore { get; set; }
    public int IncidentCount { get; set; }
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public bool IsPromotedToConfirmed { get; set; }
}

public class ClearedIngredientDto
{
    public Guid Id { get; set; }
    public Guid? MemberId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public DateTime ClearedAt { get; set; }
}

public class ConfirmedAllergenDto
{
    public Guid Id { get; set; }
    public Guid? MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string AllergenName { get; set; } = string.Empty;
    public string SeverityLevel { get; set; } = string.Empty;
    public bool RequiresEpiPen { get; set; }
    public DateTime? DiagnosisDate { get; set; }
}

// ─── Request Types ──────────────────────────────────────────────────────────

public class CreateAllergyIncidentV2Request
{
    [Required]
    public DateTime IncidentDate { get; set; } = DateTime.UtcNow;

    [Required]
    [StringLength(20)]
    public string ExposureType { get; set; } = "Ingestion";

    [StringLength(20)]
    public string? ReactionLatency { get; set; }

    public string? Notes { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "At least one product is required")]
    public List<CreateIncidentProductRequest> Products { get; set; } = new();

    [Required]
    [MinLength(1, ErrorMessage = "At least one affected member is required")]
    public List<CreateIncidentMemberRequest> Members { get; set; } = new();
}

public class CreateIncidentProductRequest
{
    public Guid? ProductId { get; set; }

    [Required]
    [StringLength(200)]
    public string ProductName { get; set; } = string.Empty;

    public bool HadReaction { get; set; } = true;
}

public class CreateIncidentMemberRequest
{
    public Guid? MemberId { get; set; }

    [Required]
    [StringLength(100)]
    public string MemberName { get; set; } = string.Empty;

    [Required]
    [StringLength(30)]
    public string Severity { get; set; } = string.Empty;

    [StringLength(500)]
    public string? ReactionTypes { get; set; }

    [StringLength(100)]
    public string? TreatmentType { get; set; }

    [StringLength(50)]
    public string? TreatmentDose { get; set; }

    public int? ResolutionTimeMinutes { get; set; }

    public bool RequiredEpipen { get; set; }

    public bool RequiredER { get; set; }
}

// ─── Report Model ────────────────────────────────────────────────────────────

public class AllergyReportModel
{
    public string MemberName { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public List<ConfirmedAllergenDto> ConfirmedAllergens { get; set; } = new();
    public List<SuspectedAllergenDto> SuspectedAllergens { get; set; } = new();
    public List<ClearedIngredientDto> ClearedIngredients { get; set; } = new();
    public List<AllergyIncidentV2Dto> Incidents { get; set; } = new();
}
