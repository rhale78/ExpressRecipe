namespace ExpressRecipe.MealPlanningService.Data;

public interface IMealPlanningRepository
{
    // Meal Plans
    Task<Guid> CreateMealPlanAsync(Guid userId, DateTime startDate, DateTime endDate, string? name, CancellationToken ct = default);
    Task<List<MealPlanDto>> GetUserMealPlansAsync(Guid userId, CancellationToken ct = default);
    Task<MealPlanDto?> GetMealPlanAsync(Guid planId, Guid userId, CancellationToken ct = default);
    Task<MealPlanDto?> GetMealPlanByIdAsync(Guid planId, CancellationToken ct = default);
    Task DeleteMealPlanAsync(Guid planId, Guid userId, CancellationToken ct = default);

    // Planned Meals
    Task<Guid> AddPlannedMealAsync(Guid mealPlanId, Guid userId, Guid? recipeId, DateTime plannedDate, string mealType, int servings, CancellationToken ct = default);
    Task<List<PlannedMealDto>> GetPlannedMealsAsync(Guid mealPlanId, DateTime? startDate, DateTime? endDate, CancellationToken ct = default);
    Task<PlannedMealDto?> GetPlannedMealByIdAsync(Guid mealId, CancellationToken ct = default);
    Task<List<PlannedMealDto>> GetMealsByDateAsync(Guid planId, DateOnly date, CancellationToken ct = default);
    Task UpdatePlannedMealAsync(Guid plannedMealId, DateTime plannedDate, string mealType, int? servings, CancellationToken ct = default);
    Task RemovePlannedMealAsync(Guid plannedMealId, CancellationToken ct = default);
    Task MarkMealAsCompletedAsync(Guid plannedMealId, CancellationToken ct = default);

    // Calendar
    Task<List<MealPlanCalendarDay>> GetCalendarAsync(Guid userId, int year, int month, CancellationToken ct = default);

    // Nutritional Goals
    Task<Guid> SetNutritionalGoalAsync(Guid userId, string goalType, decimal targetValue, string? unit, DateTime? startDate, DateTime? endDate, CancellationToken ct = default);
    Task<List<NutritionalGoalDto>> GetUserGoalsAsync(Guid userId, CancellationToken ct = default);
    Task<NutritionSummaryDto> GetNutritionSummaryAsync(Guid userId, DateTime date, CancellationToken ct = default);

    // Plan Templates
    Task<Guid> SavePlanTemplateAsync(Guid userId, string name, string? description, List<TemplateMealDto> meals, string templateJson, string? category, bool isPublic, int spanDays, CancellationToken ct = default);
    Task<List<PlanTemplateDto>> GetTemplatesAsync(Guid userId, bool includePublic = true, CancellationToken ct = default);
    Task<List<PlanTemplateDto>> GetUserTemplatesAsync(Guid userId, CancellationToken ct = default);
    Task<PlanTemplateDto?> GetTemplateByIdAsync(Guid templateId, CancellationToken ct = default);
    Task<Guid> ApplyTemplateAsync(Guid templateId, Guid userId, DateTime startDate, CancellationToken ct = default);
}

public class MealPlanDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Name { get; set; }
    public int TotalMeals { get; set; }
    public int CompletedMeals { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PlannedMealDto
{
    public Guid Id { get; set; }
    public Guid MealPlanId { get; set; }
    public Guid UserId { get; set; }
    public Guid? RecipeId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public DateTime PlannedDate { get; set; }
    public string MealType { get; set; } = string.Empty;
    public int Servings { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class MealPlanCalendarDay
{
    public DateOnly Date { get; set; }
    public int MealCount { get; set; }
    public bool HasFuturePlan { get; set; }
    public string? HolidayLabel { get; set; }
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
    public string? Category { get; set; }
    public int SpanDays { get; set; }
    public bool IsPublic { get; set; }
    public int MealCount { get; set; }
    public string TemplateJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class TemplateMealDto
{
    public int DayOffset { get; set; }
    public string MealType { get; set; } = string.Empty;
    public Guid? RecipeId { get; set; }
    public int Servings { get; set; }
}
