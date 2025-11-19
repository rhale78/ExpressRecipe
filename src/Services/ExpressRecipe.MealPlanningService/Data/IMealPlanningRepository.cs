namespace ExpressRecipe.MealPlanningService.Data;

public interface IMealPlanningRepository
{
    // Meal Plans
    Task<Guid> CreateMealPlanAsync(Guid userId, DateTime startDate, DateTime endDate, string? name);
    Task<List<MealPlanDto>> GetUserMealPlansAsync(Guid userId);
    Task<MealPlanDto?> GetMealPlanAsync(Guid planId, Guid userId);
    Task DeleteMealPlanAsync(Guid planId, Guid userId);

    // Planned Meals
    Task<Guid> AddPlannedMealAsync(Guid mealPlanId, Guid userId, Guid recipeId, DateTime plannedFor, string mealType, int servings);
    Task<List<PlannedMealDto>> GetPlannedMealsAsync(Guid mealPlanId, DateTime? startDate, DateTime? endDate);
    Task UpdatePlannedMealAsync(Guid plannedMealId, DateTime plannedFor, string mealType, int servings);
    Task RemovePlannedMealAsync(Guid plannedMealId);
    Task MarkMealAsCompletedAsync(Guid plannedMealId);

    // Nutritional Goals
    Task<Guid> SetNutritionalGoalAsync(Guid userId, string goalType, decimal targetValue, string? unit, DateTime? startDate, DateTime? endDate);
    Task<List<NutritionalGoalDto>> GetUserGoalsAsync(Guid userId);
    Task<NutritionSummaryDto> GetNutritionSummaryAsync(Guid userId, DateTime date);

    // Plan Templates
    Task<Guid> SavePlanTemplateAsync(Guid userId, string name, string? description, List<TemplateMealDto> meals);
    Task<List<PlanTemplateDto>> GetUserTemplatesAsync(Guid userId);
    Task<Guid> ApplyTemplateAsync(Guid templateId, Guid userId, DateTime startDate);
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
    public Guid RecipeId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public DateTime PlannedFor { get; set; }
    public string MealType { get; set; } = string.Empty;
    public int Servings { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
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
