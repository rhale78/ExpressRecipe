using System.ComponentModel.DataAnnotations;

namespace ExpressRecipe.Shared.DTOs.User;

public class DietaryRestrictionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CommonExclusions { get; set; }
}

public class UserDietaryRestrictionDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid DietaryRestrictionId { get; set; }
    public string RestrictionName { get; set; } = string.Empty;
    public string RestrictionType { get; set; } = string.Empty;
    public string Strictness { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class AddUserDietaryRestrictionRequest
{
    [Required]
    public Guid DietaryRestrictionId { get; set; }

    [Required]
    [RegularExpression("Strict|Moderate|Flexible")]
    public string Strictness { get; set; } = string.Empty;

    public string? Notes { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class UpdateUserDietaryRestrictionRequest
{
    [Required]
    [RegularExpression("Strict|Moderate|Flexible")]
    public string Strictness { get; set; } = string.Empty;

    public string? Notes { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
