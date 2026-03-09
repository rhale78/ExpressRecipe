using ExpressRecipe.MealPlanningService.Data;

namespace ExpressRecipe.MealPlanningService.Services;

public interface IMealPlanCopyService
{
    Task<Guid> CloneMealAsync(Guid mealId, DateOnly targetDate, string targetMealType, CancellationToken ct = default);
    Task CopyDayAsync(Guid planId, DateOnly sourceDate, DateOnly targetDate, CancellationToken ct = default);
    Task CopyWeekAsync(Guid planId, DateOnly sourceWeekStart, DateOnly targetWeekStart, CancellationToken ct = default);
    Task CopyMonthAsync(Guid planId, int sourceYear, int sourceMonth, int targetYear, int targetMonth, CancellationToken ct = default);
    Task<Guid> CopyPlanAsync(Guid planId, string newName, DateOnly newStartDate, CancellationToken ct = default);
}

public sealed class MealPlanCopyService : IMealPlanCopyService
{
    private readonly IMealPlanningRepository _plans;
    private readonly IMealCourseRepository _courses;
    private readonly ILogger<MealPlanCopyService> _logger;

    public MealPlanCopyService(IMealPlanningRepository plans, IMealCourseRepository courses,
        ILogger<MealPlanCopyService> logger)
    {
        _plans = plans;
        _courses = courses;
        _logger = logger;
    }

    public async Task<Guid> CloneMealAsync(Guid mealId, DateOnly targetDate,
        string targetMealType, CancellationToken ct = default)
    {
        PlannedMealDto? source = await _plans.GetPlannedMealByIdAsync(mealId, ct);
        if (source is null)
        {
            throw new KeyNotFoundException($"Meal {mealId} not found");
        }

        Guid newMealId = await _plans.AddPlannedMealAsync(source.MealPlanId, source.UserId,
            source.RecipeId, targetDate.ToDateTime(TimeOnly.MinValue), targetMealType, source.Servings, ct);

        List<MealCourseDto> courses = await _courses.GetCoursesAsync(mealId, ct);
        foreach (MealCourseDto course in courses)
        {
            await _courses.AddCourseAsync(newMealId, course.CourseType, course.RecipeId,
                course.CustomName, course.Servings, course.SortOrder, ct);
        }

        _logger.LogInformation("Cloned meal {SourceMealId} to {NewMealId} on {TargetDate}", mealId, newMealId, targetDate);
        return newMealId;
    }

    public async Task CopyDayAsync(Guid planId, DateOnly sourceDate, DateOnly targetDate, CancellationToken ct = default)
    {
        List<PlannedMealDto> meals = await _plans.GetMealsByDateAsync(planId, sourceDate, ct);
        foreach (PlannedMealDto meal in meals)
        {
            await CloneMealAsync(meal.Id, targetDate, meal.MealType, ct);
        }
    }

    public async Task CopyWeekAsync(Guid planId, DateOnly sourceWeekStart,
        DateOnly targetWeekStart, CancellationToken ct = default)
    {
        for (int i = 0; i < 7; i++)
        {
            await CopyDayAsync(planId, sourceWeekStart.AddDays(i), targetWeekStart.AddDays(i), ct);
        }
    }

    public async Task CopyMonthAsync(Guid planId, int sourceYear, int sourceMonth,
        int targetYear, int targetMonth, CancellationToken ct = default)
    {
        int sourceDays = DateTime.DaysInMonth(sourceYear, sourceMonth);
        for (int day = 1; day <= sourceDays; day++)
        {
            int targetDay = Math.Min(day, DateTime.DaysInMonth(targetYear, targetMonth));
            await CopyDayAsync(planId, new DateOnly(sourceYear, sourceMonth, day),
                new DateOnly(targetYear, targetMonth, targetDay), ct);
        }
    }

    public async Task<Guid> CopyPlanAsync(Guid planId, string newName,
        DateOnly newStartDate, CancellationToken ct = default)
    {
        MealPlanDto? source = await _plans.GetMealPlanByIdAsync(planId, ct);
        if (source is null)
        {
            throw new KeyNotFoundException($"Plan {planId} not found");
        }

        // daysBetween: number of days from StartDate to EndDate (exclusive end), used to set the same duration
        int daysBetween = (DateOnly.FromDateTime(source.EndDate).DayNumber - DateOnly.FromDateTime(source.StartDate).DayNumber);
        Guid newPlanId = await _plans.CreateMealPlanAsync(source.UserId,
            newStartDate.ToDateTime(TimeOnly.MinValue),
            newStartDate.AddDays(daysBetween).ToDateTime(TimeOnly.MinValue), newName, ct);

        List<PlannedMealDto> allMeals = await _plans.GetPlannedMealsAsync(planId, null, null, ct);
        foreach (PlannedMealDto meal in allMeals)
        {
            int offset = (DateOnly.FromDateTime(meal.PlannedDate).DayNumber - DateOnly.FromDateTime(source.StartDate).DayNumber);
            await CloneMealAsync(meal.Id, newStartDate.AddDays(offset), meal.MealType, ct);
        }

        _logger.LogInformation("Copied plan {SourcePlanId} to new plan {NewPlanId}", planId, newPlanId);
        return newPlanId;
    }
}
