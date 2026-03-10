using System.Text.Json;
using ExpressRecipe.MealPlanningService.Data;
using ExpressRecipe.MealPlanningService.Services;
using FluentAssertions;
using Moq;

namespace ExpressRecipe.MealPlanningService.Tests.Services;

public class MealPlanTemplateServiceTests
{
    private readonly Mock<IMealPlanningRepository> _plansMock   = new();
    private readonly Mock<IMealCourseRepository>   _coursesMock = new();

    private MealPlanTemplateService Create()
        => new(_plansMock.Object, _coursesMock.Object);

    // ── SaveTemplateFromPlanAsync ──────────────────────────────────────────────

    [Fact]
    public async Task SaveTemplateFromPlan_FiltersToDateRange_AndCallsSaveWithJson()
    {
        Guid planId  = Guid.NewGuid();
        Guid userId  = Guid.NewGuid();
        DateOnly from = new(2026, 3, 1);
        DateOnly to   = new(2026, 3, 3);
        Guid newTemplateId = Guid.NewGuid();

        List<PlannedMealDto> meals = new()
        {
            new PlannedMealDto { Id = Guid.NewGuid(), MealType = "Dinner", PlannedDate = new DateTime(2026, 3, 1),  Servings = 2 },
            new PlannedMealDto { Id = Guid.NewGuid(), MealType = "Lunch",  PlannedDate = new DateTime(2026, 3, 2),  Servings = 1 },
            new PlannedMealDto { Id = Guid.NewGuid(), MealType = "Dinner", PlannedDate = new DateTime(2026, 3, 10), Servings = 2 }, // out of range
        };

        _plansMock.Setup(p => p.GetPlannedMealsAsync(planId, null, null, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(meals);
        _coursesMock.Setup(c => c.GetCoursesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<MealCourseDto>());

        string? capturedJson = null;
        _plansMock
            .Setup(p => p.SavePlanTemplateAsync(userId, "My Template", null, It.IsAny<List<TemplateMealDto>>(),
                It.IsAny<string>(), null, false, 3, It.IsAny<CancellationToken>()))
            .Callback<Guid, string, string?, List<TemplateMealDto>, string, string?, bool, int, CancellationToken>(
                (_, _, _, _, json, _, _, _, _) => capturedJson = json)
            .ReturnsAsync(newTemplateId);

        Guid result = await Create().SaveTemplateFromPlanAsync(
            userId, planId, from, to, "My Template", null, null, false);

        result.Should().Be(newTemplateId);

        capturedJson.Should().NotBeNullOrEmpty();
        // Deserialize and check: 2 meals in range (Mar 1, Mar 2) but not Mar 10
        JsonDocument doc = JsonDocument.Parse(capturedJson!);
        doc.RootElement.GetProperty("meals").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task SaveTemplateFromPlan_SpanDays_MatchesDateRangeInclusive()
    {
        Guid planId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        DateOnly from = new(2026, 3, 1);
        DateOnly to   = new(2026, 3, 7); // span = 7 days

        _plansMock.Setup(p => p.GetPlannedMealsAsync(planId, null, null, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new List<PlannedMealDto>());

        int capturedSpan = 0;
        _plansMock
            .Setup(p => p.SavePlanTemplateAsync(userId, It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<List<TemplateMealDto>>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, string, string?, List<TemplateMealDto>, string, string?, bool, int, CancellationToken>(
                (_, _, _, _, _, _, _, span, _) => capturedSpan = span)
            .ReturnsAsync(Guid.NewGuid());

        await Create().SaveTemplateFromPlanAsync(userId, planId, from, to, "T", null, null, false);

        capturedSpan.Should().Be(7);
    }

    [Fact]
    public async Task SaveTemplateFromPlan_RecordsCorrectDayOffsets()
    {
        Guid planId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        DateOnly from = new(2026, 3, 1);
        DateOnly to   = new(2026, 3, 5);

        List<PlannedMealDto> meals = new()
        {
            new PlannedMealDto { Id = Guid.NewGuid(), MealType = "Dinner", PlannedDate = new DateTime(2026, 3, 3), Servings = 2 },
        };

        _plansMock.Setup(p => p.GetPlannedMealsAsync(planId, null, null, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(meals);
        _coursesMock.Setup(c => c.GetCoursesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<MealCourseDto>());

        string? capturedJson = null;
        _plansMock
            .Setup(p => p.SavePlanTemplateAsync(userId, It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<List<TemplateMealDto>>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, string, string?, List<TemplateMealDto>, string, string?, bool, int, CancellationToken>(
                (_, _, _, _, json, _, _, _, _) => capturedJson = json)
            .ReturnsAsync(Guid.NewGuid());

        await Create().SaveTemplateFromPlanAsync(userId, planId, from, to, "T", null, null, false);

        JsonDocument doc = JsonDocument.Parse(capturedJson!);
        int dayOffset = doc.RootElement.GetProperty("meals")[0].GetProperty("dayOffset").GetInt32();
        dayOffset.Should().Be(2); // Mar 3 - Mar 1 = 2
    }

    // ── ApplyTemplateAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyTemplate_WhenTemplateNotFound_ThrowsKeyNotFoundException()
    {
        Guid templateId = Guid.NewGuid();
        _plansMock.Setup(p => p.GetTemplateByIdAsync(templateId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync((PlanTemplateDto?)null);

        Func<Task> act = () => Create().ApplyTemplateAsync(templateId, Guid.NewGuid(),
            Guid.NewGuid(), new DateOnly(2026, 3, 1));

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task ApplyTemplate_WithInvalidJson_ThrowsInvalidOperationException()
    {
        Guid templateId = Guid.NewGuid();
        PlanTemplateDto template = new() { Id = templateId, TemplateJson = "not-valid-json" };

        _plansMock.Setup(p => p.GetTemplateByIdAsync(templateId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(template);

        Func<Task> act = () => Create().ApplyTemplateAsync(templateId, Guid.NewGuid(),
            Guid.NewGuid(), new DateOnly(2026, 3, 1));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ApplyTemplate_WithValidJson_CreatesMealsWithCorrectDates()
    {
        Guid templateId  = Guid.NewGuid();
        Guid targetPlanId = Guid.NewGuid();
        Guid userId      = Guid.NewGuid();
        DateOnly start   = new(2026, 4, 1);

        string json = JsonSerializer.Serialize(new
        {
            meals = new[]
            {
                new { dayOffset = 0, mealType = "Dinner",    recipeId = (Guid?)null, recipeName = "Pasta", servings = 2, courses = Array.Empty<object>() },
                new { dayOffset = 2, mealType = "Breakfast", recipeId = (Guid?)null, recipeName = "Oats",  servings = 1, courses = Array.Empty<object>() }
            }
        });

        PlanTemplateDto template = new() { Id = templateId, TemplateJson = json };

        _plansMock.Setup(p => p.GetTemplateByIdAsync(templateId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(template);
        _plansMock.Setup(p => p.AddPlannedMealAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
                      It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Guid.NewGuid());

        Guid result = await Create().ApplyTemplateAsync(templateId, userId, targetPlanId, start);

        result.Should().Be(targetPlanId);

        // Meal 0: offset=0 → April 1
        _plansMock.Verify(p => p.AddPlannedMealAsync(targetPlanId, userId, null,
            new DateOnly(2026, 4, 1).ToDateTime(TimeOnly.MinValue), "Dinner", 2, It.IsAny<CancellationToken>()), Times.Once);

        // Meal 1: offset=2 → April 3
        _plansMock.Verify(p => p.AddPlannedMealAsync(targetPlanId, userId, null,
            new DateOnly(2026, 4, 3).ToDateTime(TimeOnly.MinValue), "Breakfast", 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApplyTemplate_WithCourses_AddsCourseOnEachMeal()
    {
        Guid templateId   = Guid.NewGuid();
        Guid targetPlanId = Guid.NewGuid();
        Guid userId       = Guid.NewGuid();
        Guid newMealId    = Guid.NewGuid();
        DateOnly start    = new(2026, 4, 1);

        string json = JsonSerializer.Serialize(new
        {
            meals = new[]
            {
                new
                {
                    dayOffset = 0, mealType = "Dinner", recipeId = (Guid?)null, recipeName = "Multi", servings = 1,
                    courses = new[]
                    {
                        new { courseType = "Appetizer", recipeId = (Guid?)null, customName = (string?)null, servings = 1.0m, sortOrder = 0 },
                        new { courseType = "Main",       recipeId = (Guid?)null, customName = (string?)null, servings = 1.0m, sortOrder = 1 }
                    }
                }
            }
        });

        _plansMock.Setup(p => p.GetTemplateByIdAsync(templateId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new PlanTemplateDto { Id = templateId, TemplateJson = json });
        _plansMock.Setup(p => p.AddPlannedMealAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
                      It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(newMealId);
        _coursesMock.Setup(c => c.AddCourseAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(),
                      It.IsAny<string?>(), It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Guid.NewGuid());

        await Create().ApplyTemplateAsync(templateId, userId, targetPlanId, start);

        _coursesMock.Verify(c => c.AddCourseAsync(newMealId, It.IsAny<string>(), It.IsAny<Guid?>(),
            It.IsAny<string?>(), It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // ── GetTemplatesAsync / DeleteTemplateAsync ────────────────────────────────

    [Fact]
    public async Task GetTemplates_DelegatesToRepository()
    {
        Guid userId = Guid.NewGuid();
        List<PlanTemplateDto> expected = new() { new PlanTemplateDto { Id = Guid.NewGuid(), Name = "T1" } };

        _plansMock.Setup(p => p.GetTemplatesAsync(userId, true, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(expected);

        List<PlanTemplateDto> result = await Create().GetTemplatesAsync(userId);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task DeleteTemplate_CompletesWithoutError()
    {
        // Current impl is a stub — verify it at least does not throw
        Func<Task> act = () => Create().DeleteTemplateAsync(Guid.NewGuid(), Guid.NewGuid());
        await act.Should().NotThrowAsync();
    }
}
