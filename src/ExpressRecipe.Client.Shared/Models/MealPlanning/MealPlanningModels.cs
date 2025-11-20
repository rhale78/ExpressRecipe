namespace ExpressRecipe.Client.Shared.Models.MealPlanning;

public class MealPlanDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<MealPlanEntryDto> Entries { get; set; } = new();
    public string Status { get; set; } = "Active"; // Active, Completed, Archived

    public int TotalMeals => Entries.Count;
    public int PreparedMeals => Entries.Count(e => e.IsPrepared);

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class MealPlanEntryDto
{
    public Guid Id { get; set; }
    public Guid MealPlanId { get; set; }
    public DateTime Date { get; set; }
    public string MealType { get; set; } = "Dinner"; // Breakfast, Lunch, Dinner, Snack

    // Recipe linkage
    public Guid? RecipeId { get; set; }
    public string? RecipeName { get; set; }
    public string? RecipeImageUrl { get; set; }
    public int? RecipePrepTime { get; set; }
    public int? RecipeCookTime { get; set; }
    public List<string> RecipeAllergens { get; set; } = new();
    public List<string> RecipeDietaryInfo { get; set; } = new();

    // Or custom meal (no recipe)
    public string? CustomMealName { get; set; }
    public string? CustomMealDescription { get; set; }

    // Servings
    public int Servings { get; set; } = 4;

    // Status
    public bool IsPrepared { get; set; }
    public DateTime? PreparedAt { get; set; }

    // Notes
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class CreateMealPlanRequest
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class UpdateMealPlanRequest : CreateMealPlanRequest
{
    public string Status { get; set; } = "Active";
}

public class AddMealPlanEntryRequest
{
    public Guid MealPlanId { get; set; }
    public DateTime Date { get; set; }
    public string MealType { get; set; } = "Dinner";

    // Recipe or custom
    public Guid? RecipeId { get; set; }
    public string? CustomMealName { get; set; }
    public string? CustomMealDescription { get; set; }

    public int Servings { get; set; } = 4;
    public string? Notes { get; set; }
}

public class UpdateMealPlanEntryRequest : AddMealPlanEntryRequest
{
}

public class MarkMealPreparedRequest
{
    public Guid EntryId { get; set; }
    public bool IsPrepared { get; set; }
}

public class GenerateShoppingListFromMealPlanRequest
{
    public Guid MealPlanId { get; set; }
    public DateTime? StartDate { get; set; } // Optional: only include meals from this date forward
    public DateTime? EndDate { get; set; } // Optional: only include meals up to this date
    public bool SubtractInventory { get; set; } = true; // Don't add items already in inventory
    public string? ShoppingListName { get; set; } // Optional custom name, defaults to "Meal Plan: {plan name}"
}

public class MealPlanSearchRequest
{
    public string? Status { get; set; } // Active, Completed, Archived
    public DateTime? StartDateFrom { get; set; }
    public DateTime? StartDateTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SortBy { get; set; } = "StartDate";
    public bool SortDescending { get; set; } = true;
}

public class MealPlanSearchResult
{
    public List<MealPlanDto> Plans { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public class MealPlanCalendarView
{
    public Guid MealPlanId { get; set; }
    public string MealPlanName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<MealPlanDayDto> Days { get; set; } = new();
}

public class MealPlanDayDto
{
    public DateTime Date { get; set; }
    public List<MealPlanEntryDto> Meals { get; set; } = new();
    public int TotalMeals => Meals.Count;
    public int PreparedMeals => Meals.Count(m => m.IsPrepared);
    public bool IsComplete => TotalMeals > 0 && TotalMeals == PreparedMeals;
}

public class MealPlanWeekView
{
    public DateTime WeekStartDate { get; set; } // Always a Sunday or Monday
    public DateTime WeekEndDate { get; set; }
    public List<MealPlanDayDto> Days { get; set; } = new(); // 7 days
}

public class MealPlanSummaryDto
{
    public int TotalActivePlans { get; set; }
    public int TotalUpcomingMeals { get; set; }
    public int MealsThisWeek { get; set; }
    public int PreparedThisWeek { get; set; }
}

public class MealPlanNutritionSummaryDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalCalories { get; set; }
    public decimal TotalProtein { get; set; }
    public decimal TotalCarbohydrates { get; set; }
    public decimal TotalFat { get; set; }
    public int DaysInPlan { get; set; }

    public decimal AvgDailyCalories => DaysInPlan > 0 ? TotalCalories / (decimal)DaysInPlan : 0;
    public decimal AvgDailyProtein => DaysInPlan > 0 ? TotalProtein / DaysInPlan : 0;
    public decimal AvgDailyCarbs => DaysInPlan > 0 ? TotalCarbohydrates / DaysInPlan : 0;
    public decimal AvgDailyFat => DaysInPlan > 0 ? TotalFat / DaysInPlan : 0;
}

public class QuickMealPlanRequest
{
    public DateTime StartDate { get; set; }
    public int DurationDays { get; set; } = 7;
    public List<string> MealTypes { get; set; } = new(); // Breakfast, Lunch, Dinner
    public int DefaultServings { get; set; } = 4;
    public bool UseUserPreferences { get; set; } = true; // Auto-filter recipes by user/family allergens
}
