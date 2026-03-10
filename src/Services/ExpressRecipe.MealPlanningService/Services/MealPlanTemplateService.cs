using ExpressRecipe.MealPlanningService.Data;

namespace ExpressRecipe.MealPlanningService.Services;

public interface IMealPlanTemplateService
{
    Task<Guid> SaveTemplateFromPlanAsync(Guid userId, Guid planId, DateOnly fromDate, DateOnly toDate,
        string name, string? description, string? category, bool isPublic, CancellationToken ct = default);
    Task<Guid> ApplyTemplateAsync(Guid templateId, Guid userId, Guid targetPlanId,
        DateOnly startDate, CancellationToken ct = default);
    Task<List<PlanTemplateDto>> GetTemplatesAsync(Guid userId, bool includePublic = true, CancellationToken ct = default);
    Task DeleteTemplateAsync(Guid templateId, Guid userId, CancellationToken ct = default);
}

public sealed class MealPlanTemplateService : IMealPlanTemplateService
{
    private readonly IMealPlanningRepository _plans;
    private readonly IMealCourseRepository _courses;

    private static readonly System.Text.Json.JsonSerializerOptions JsonWriteOptions = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
    };

    private static readonly System.Text.Json.JsonSerializerOptions JsonReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public MealPlanTemplateService(IMealPlanningRepository plans, IMealCourseRepository courses)
    {
        _plans = plans;
        _courses = courses;
    }

    public async Task<Guid> SaveTemplateFromPlanAsync(Guid userId, Guid planId, DateOnly fromDate,
        DateOnly toDate, string name, string? description, string? category, bool isPublic,
        CancellationToken ct = default)
    {
        List<PlannedMealDto> meals = await _plans.GetPlannedMealsAsync(planId, null, null, ct);

        // Filter meals in the requested date range once
        List<PlannedMealDto> filteredMeals = meals
            .Where(m =>
                DateOnly.FromDateTime(m.PlannedDate) >= fromDate &&
                DateOnly.FromDateTime(m.PlannedDate) <= toDate)
            .ToList();

        // Kick off all course fetches in parallel to avoid sequential N+1 latency
        Dictionary<Guid, Task<List<MealCourseDto>>> courseTasks = filteredMeals.ToDictionary(
            meal => meal.Id,
            meal => _courses.GetCoursesAsync(meal.Id, ct));

        await Task.WhenAll(courseTasks.Values);

        List<TemplateMealEntry> entries = new();
        foreach (PlannedMealDto meal in filteredMeals)
        {
            int dayOffset = (DateOnly.FromDateTime(meal.PlannedDate).DayNumber - fromDate.DayNumber);
            List<MealCourseDto> courses = courseTasks[meal.Id].Result;
            entries.Add(new TemplateMealEntry
            {
                DayOffset = dayOffset,
                MealType = meal.MealType,
                RecipeId = meal.RecipeId,
                RecipeName = meal.RecipeName,
                Servings = meal.Servings ?? 1,
                Courses = courses.Select(c => new TemplateCourseEntry
                {
                    CourseType = c.CourseType,
                    RecipeId = c.RecipeId,
                    CustomName = c.CustomName,
                    Servings = c.Servings,
                    SortOrder = c.SortOrder
                }).ToList()
            });
        }

        string templateJson = System.Text.Json.JsonSerializer.Serialize(new { meals = entries }, JsonWriteOptions);
        int spanDays = (toDate.DayNumber - fromDate.DayNumber) + 1;
        return await _plans.SavePlanTemplateAsync(userId, name, description,
            new List<TemplateMealDto>(), templateJson, category, isPublic, spanDays, ct);
    }

    public async Task<Guid> ApplyTemplateAsync(Guid templateId, Guid userId, Guid targetPlanId,
        DateOnly startDate, CancellationToken ct = default)
    {
        PlanTemplateDto? template = await _plans.GetTemplateByIdAsync(templateId, ct);
        if (template is null)
        {
            throw new KeyNotFoundException($"Template {templateId} not found");
        }

        System.Text.Json.JsonDocument doc;
        try
        {
            doc = System.Text.Json.JsonDocument.Parse(template.TemplateJson);
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse template JSON for template {templateId}", ex);
        }

        using (doc)
        foreach (System.Text.Json.JsonElement mealEl in doc.RootElement.GetProperty("meals").EnumerateArray())
        {
            TemplateMealEntry? meal;
            try
            {
                meal = System.Text.Json.JsonSerializer.Deserialize<TemplateMealEntry>(mealEl.GetRawText(), JsonReadOptions);
            }
            catch (System.Text.Json.JsonException ex)
            {
                throw new InvalidOperationException("Failed to deserialize meal entry from template JSON.", ex);
            }

            if (meal is null)
            {
                throw new InvalidOperationException("Deserialized meal entry from template JSON was null.");
            }

            string mealType = string.IsNullOrWhiteSpace(meal.MealType) ? "Dinner" : meal.MealType;
            Guid newMealId = await _plans.AddPlannedMealAsync(targetPlanId, userId, meal.RecipeId,
                startDate.AddDays(meal.DayOffset).ToDateTime(TimeOnly.MinValue), mealType, meal.Servings, ct);

            if (meal.Courses is { Count: > 0 })
            {
                int sort = 0;
                foreach (TemplateCourseEntry c in meal.Courses)
                {
                    string courseType = string.IsNullOrWhiteSpace(c.CourseType) ? "Main" : c.CourseType;
                    await _courses.AddCourseAsync(newMealId, courseType, c.RecipeId,
                        c.CustomName, c.Servings, sort++, ct);
                }
            }
        }

        return targetPlanId;
    }

    public Task<List<PlanTemplateDto>> GetTemplatesAsync(Guid userId, bool includePublic = true, CancellationToken ct = default)
        => _plans.GetTemplatesAsync(userId, includePublic, ct);

    public Task DeleteTemplateAsync(Guid templateId, Guid userId, CancellationToken ct = default)
    {
        // Stub — soft-delete would require a column change; placeholder for now
        return Task.CompletedTask;
    }
}

internal sealed record TemplateMealEntry
{
    public int DayOffset { get; init; }
    public string MealType { get; init; } = string.Empty;
    public Guid? RecipeId { get; init; }
    public string? RecipeName { get; init; }
    public int Servings { get; init; }
    public List<TemplateCourseEntry> Courses { get; init; } = new();
}

internal sealed record TemplateCourseEntry
{
    public string CourseType { get; init; } = string.Empty;
    public Guid? RecipeId { get; init; }
    public string? CustomName { get; init; }
    public decimal Servings { get; init; }
    public int SortOrder { get; init; }
}

