namespace ExpressRecipe.MealPlanningService.Data;

public interface IMealPlanningRepository
{
    // Meal Plans
    Task<Guid> CreateMealPlanAsync(Guid userId, DateTime startDate, DateTime endDate, string? name);
    Task<List<MealPlanDto>> GetUserMealPlansAsync(Guid userId, string? status = null);
    Task<MealPlanDto?> GetMealPlanAsync(Guid planId, Guid userId);
    Task UpdateMealPlanAsync(Guid planId, Guid userId, string name, DateTime startDate, DateTime endDate, string status);
    Task DeleteMealPlanAsync(Guid planId, Guid userId);
    Task SetMealPlanStatusAsync(Guid planId, Guid userId, string status);

    // Planned Meals (entries)
    Task<Guid> AddPlannedMealAsync(Guid mealPlanId, Guid userId, Guid? recipeId, DateTime plannedFor, string mealType, int servings, string? customMealName = null, string? notes = null);
    Task<List<PlannedMealDto>> GetPlannedMealsAsync(Guid mealPlanId, DateTime? startDate, DateTime? endDate);
    Task<List<PlannedMealDto>> GetPlannedMealsByDateRangeAsync(Guid userId, DateTime startDate, DateTime endDate);
    Task<PlannedMealDto?> GetPlannedMealAsync(Guid plannedMealId);
    Task UpdatePlannedMealAsync(Guid plannedMealId, DateTime plannedFor, string mealType, int servings, Guid? recipeId = null, string? customMealName = null, string? notes = null);
    Task RemovePlannedMealAsync(Guid plannedMealId);
    Task MarkMealAsPreparedAsync(Guid plannedMealId, bool isPrepared);

    // Summary / Week view
    Task<MealPlanSummaryData> GetMealPlanSummaryAsync(Guid userId);

    // Nutritional Goals
    Task<Guid> SetNutritionalGoalAsync(Guid userId, string goalType, decimal targetValue, string? unit, DateTime? startDate, DateTime? endDate);
    Task<List<NutritionalGoalDto>> GetUserGoalsAsync(Guid userId);
    Task<NutritionSummaryDto> GetNutritionSummaryAsync(Guid userId, DateTime date);

    // Plan Templates
    Task<Guid> SavePlanTemplateAsync(Guid userId, string name, string? description, List<TemplateMealDto> meals);
    Task<List<PlanTemplateDto>> GetUserTemplatesAsync(Guid userId);
    Task<Guid> ApplyTemplateAsync(Guid templateId, Guid userId, DateTime startDate);
}

// Summary data returned by the backend (not the client DTO)
public class MealPlanSummaryData
{
    public int TotalActivePlans { get; set; }
    public int TotalUpcomingMeals { get; set; }
    public int MealsThisWeek { get; set; }
    public int PreparedThisWeek { get; set; }
}

public class MealPlanDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Name { get; set; }
    public string Status { get; set; } = "Active";
    public int TotalMeals { get; set; }
    public int CompletedMeals { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PlannedMealDto
{
    public Guid Id { get; set; }
    public Guid MealPlanId { get; set; }
    public Guid? RecipeId { get; set; }
    public string? RecipeName { get; set; }
    public string? CustomMealName { get; set; }
    public DateTime PlannedFor { get; set; }
    public string MealType { get; set; } = string.Empty;
    public int Servings { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }
}

public class NutritionalGoalDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string GoalType { get; set; } = string.Empty;
    public decimal TargetValue { get; set; }
    public string? Unit { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; }
}

public class NutritionSummaryDto
{
    public DateTime Date { get; set; }
    public decimal TotalCalories { get; set; }
    public decimal TotalProtein { get; set; }
    public decimal TotalCarbs { get; set; }
    public decimal TotalFat { get; set; }
    public Dictionary<string, decimal> GoalProgress { get; set; } = new();
}

public class PlanTemplateDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int MealCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TemplateMealDto
{
    public int DayOffset { get; set; }
    public string MealType { get; set; } = string.Empty;
    public Guid RecipeId { get; set; }
    public int Servings { get; set; }
}
