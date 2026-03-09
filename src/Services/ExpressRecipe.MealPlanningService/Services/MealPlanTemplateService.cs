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
        List<TemplateMealEntry> entries = new();

        foreach (PlannedMealDto meal in meals.Where(m =>
            DateOnly.FromDateTime(m.PlannedDate) >= fromDate &&
            DateOnly.FromDateTime(m.PlannedDate) <= toDate))
        {
            int dayOffset = (DateOnly.FromDateTime(meal.PlannedDate).DayNumber - fromDate.DayNumber);
            List<MealCourseDto> courses = await _courses.GetCoursesAsync(meal.Id, ct);
            entries.Add(new TemplateMealEntry
            {
                DayOffset = dayOffset,
                MealType = meal.MealType,
                RecipeId = meal.RecipeId,
                RecipeName = meal.RecipeName,
                Servings = meal.Servings,
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

        string templateJson = System.Text.Json.JsonSerializer.Serialize(new { meals = entries });
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
            int dayOffset = mealEl.GetProperty("dayOffset").GetInt32();
            string mealType = mealEl.GetProperty("mealType").GetString() ?? "Dinner";
            Guid? recipeId = mealEl.TryGetProperty("recipeId", out System.Text.Json.JsonElement rid)
                && rid.ValueKind != System.Text.Json.JsonValueKind.Null ? rid.GetGuid() : null;
            decimal servings = mealEl.GetProperty("servings").GetDecimal();

            Guid newMealId = await _plans.AddPlannedMealAsync(targetPlanId, userId, recipeId,
                startDate.AddDays(dayOffset).ToDateTime(TimeOnly.MinValue), mealType, (int)servings, ct);

            if (mealEl.TryGetProperty("courses", out System.Text.Json.JsonElement coursesEl))
            {
                int sort = 0;
                foreach (System.Text.Json.JsonElement c in coursesEl.EnumerateArray())
                {
                    await _courses.AddCourseAsync(newMealId,
                        c.GetProperty("courseType").GetString() ?? "Main",
                        c.TryGetProperty("recipeId", out System.Text.Json.JsonElement crid)
                            && crid.ValueKind != System.Text.Json.JsonValueKind.Null ? crid.GetGuid() : null,
                        c.TryGetProperty("customName", out System.Text.Json.JsonElement cn) ? cn.GetString() : null,
                        c.GetProperty("servings").GetDecimal(), sort++, ct);
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
