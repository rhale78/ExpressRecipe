using System.ComponentModel.DataAnnotations;

namespace ExpressRecipe.Shared.DTOs.User;

public class CuisineDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Region { get; set; }
}

public class UserPreferredCuisineDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid CuisineId { get; set; }
    public string CuisineName { get; set; } = string.Empty;
    public string? Region { get; set; }
    public int? PreferenceLevel { get; set; }
}

public class AddUserPreferredCuisineRequest
{
    [Required]
    public Guid CuisineId { get; set; }

    [Range(1, 5)]
    public int? PreferenceLevel { get; set; }
}

public class HealthGoalDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
}

public class UserHealthGoalDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid HealthGoalId { get; set; }
    public string HealthGoalName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int? Priority { get; set; }
    public string? Notes { get; set; }
}

public class AddUserHealthGoalRequest
{
    [Required]
    public Guid HealthGoalId { get; set; }

    [Range(1, 5)]
    public int? Priority { get; set; }

    public string? Notes { get; set; }
}

public class UpdateUserHealthGoalRequest
{
    [Range(1, 5)]
    public int? Priority { get; set; }

    public string? Notes { get; set; }
}

public class UserFavoriteIngredientDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public int? Rating { get; set; }
    public string? Notes { get; set; }
}

public class AddUserFavoriteIngredientRequest
{
    [Required]
    public Guid IngredientId { get; set; }

    [Range(1, 5)]
    public int? Rating { get; set; }

    public string? Notes { get; set; }
}

public class UserDislikedIngredientDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? Notes { get; set; }
}

public class AddUserDislikedIngredientRequest
{
    [Required]
    public Guid IngredientId { get; set; }

    [StringLength(200)]
    public string? Reason { get; set; }

    public string? Notes { get; set; }
}
