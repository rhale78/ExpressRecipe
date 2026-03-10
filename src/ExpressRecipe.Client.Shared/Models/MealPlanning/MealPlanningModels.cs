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
    public Guid? HouseholdId { get; set; }
    public bool IsFuturePlan { get; set; }
    public string? OccasionLabel { get; set; }
    public decimal? BudgetTarget { get; set; }
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

/// <summary>
/// Client-side model for a meal suggestion returned by POST /api/mealplanning/suggest.
/// </summary>
public class MealSuggestionResult
{
    public Guid RecipeId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public int CookMinutes { get; set; }
    public decimal UserRating { get; set; }
    public decimal GlobalRating { get; set; }
    public int UserCookCount { get; set; }
    public decimal InventoryMatchPct { get; set; }
    public bool IsAllergenSafe { get; set; }
    public decimal Score { get; set; }
    public List<string> MissingIngredients { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}

/// <summary>Single day entry returned by the month-calendar endpoint.</summary>
public class MealPlanCalendarDay
{
    public DateOnly Date { get; set; }
    public int MealCount { get; set; }
    public bool HasFuturePlan { get; set; }
    public string? HolidayLabel { get; set; }
}

// ── Multi-course ─────────────────────────────────────────────────────────────

public class MealCourseDto
{
    public Guid Id { get; set; }
    public Guid PlannedMealId { get; set; }
    public string CourseType { get; set; } = string.Empty;
    public Guid? RecipeId { get; set; }
    public string? RecipeName { get; set; }
    public string? CustomName { get; set; }
    public decimal Servings { get; set; }
    public int SortOrder { get; set; }
    public bool IsCompleted { get; set; }
}

public class AddCourseRequest
{
    public string CourseType { get; set; } = "Main";
    public Guid? RecipeId { get; set; }
    public string? CustomName { get; set; }
    public decimal Servings { get; set; } = 1;
}

public class UpdateCourseRequest
{
    public Guid? RecipeId { get; set; }
    public string? CustomName { get; set; }
    public decimal Servings { get; set; } = 1;
    public int SortOrder { get; set; }
}

// ── Attendees ────────────────────────────────────────────────────────────────

public class MealAttendeeDto
{
    public Guid? UserId { get; set; }
    public Guid? FamilyMemberId { get; set; }
    public string? GuestName { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}

// ── Copy / Clone ─────────────────────────────────────────────────────────────

public class CloneMealRequest
{
    public DateOnly TargetDate { get; set; }
    public string TargetMealType { get; set; } = "Dinner";
}

public class CopyDayRequest
{
    public DateOnly SourceDate { get; set; }
    public DateOnly TargetDate { get; set; }
}

public class CopyWeekRequest
{
    public DateOnly SourceWeekStart { get; set; }
    public DateOnly TargetWeekStart { get; set; }
}

// ── Templates ────────────────────────────────────────────────────────────────

public class PlanTemplateDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public int SpanDays { get; set; }
    public bool IsPublic { get; set; }
    public int MealCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SaveTemplateRequest
{
    public Guid PlanId { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool IsPublic { get; set; }
}

public class ApplyTemplateRequest
{
    public Guid TargetPlanId { get; set; }
    public DateOnly StartDate { get; set; }
}
