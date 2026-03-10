using ExpressRecipe.MealPlanningService.Data;
using ExpressRecipe.MealPlanningService.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ExpressRecipe.MealPlanningService.Tests.Services;

public class MealPlanCopyServiceTests
{
    private readonly Mock<IMealPlanningRepository> _plansMock   = new();
    private readonly Mock<IMealCourseRepository>   _coursesMock = new();

    private MealPlanCopyService Create()
        => new(_plansMock.Object, _coursesMock.Object, NullLogger<MealPlanCopyService>.Instance);

    private static PlannedMealDto MakeMeal(Guid id, Guid planId, Guid userId, DateOnly date, string mealType = "Dinner")
        => new()
        {
            Id         = id,
            MealPlanId = planId,
            UserId     = userId,
            PlannedDate = date.ToDateTime(TimeOnly.MinValue),
            MealType   = mealType,
            Servings   = 2
        };

    // ── CloneMeal ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CloneMeal_WhenSourceMealExists_CreatesNewMealAndReturnItsId()
    {
        Guid sourceMealId = Guid.NewGuid();
        Guid planId       = Guid.NewGuid();
        Guid userId       = Guid.NewGuid();
        Guid newMealId    = Guid.NewGuid();
        DateOnly targetDate = new(2026, 3, 15);

        PlannedMealDto source = MakeMeal(sourceMealId, planId, userId, new DateOnly(2026, 3, 10));

        _plansMock.Setup(p => p.GetPlannedMealByIdAsync(sourceMealId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(source);
        _plansMock.Setup(p => p.AddPlannedMealAsync(planId, userId, source.RecipeId,
                      targetDate.ToDateTime(TimeOnly.MinValue), "Dinner", 2, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(newMealId);
        _coursesMock.Setup(c => c.GetCoursesAsync(sourceMealId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<MealCourseDto>());

        Guid result = await Create().CloneMealAsync(sourceMealId, targetDate, "Dinner");

        result.Should().Be(newMealId);
        _plansMock.Verify(p => p.AddPlannedMealAsync(planId, userId, source.RecipeId,
            targetDate.ToDateTime(TimeOnly.MinValue), "Dinner", 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CloneMeal_WithCourses_ClonesCoursesOntoNewMeal()
    {
        Guid sourceMealId = Guid.NewGuid();
        Guid newMealId    = Guid.NewGuid();
        Guid planId       = Guid.NewGuid();
        Guid userId       = Guid.NewGuid();
        DateOnly targetDate = new(2026, 3, 15);

        PlannedMealDto source = MakeMeal(sourceMealId, planId, userId, new DateOnly(2026, 3, 10));

        _plansMock.Setup(p => p.GetPlannedMealByIdAsync(sourceMealId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(source);
        _plansMock.Setup(p => p.AddPlannedMealAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
                      It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(newMealId);

        List<MealCourseDto> courses = new()
        {
            new MealCourseDto { Id = Guid.NewGuid(), CourseType = "Appetizer", Servings = 1, SortOrder = 0 },
            new MealCourseDto { Id = Guid.NewGuid(), CourseType = "Main",      Servings = 2, SortOrder = 1 }
        };
        _coursesMock.Setup(c => c.GetCoursesAsync(sourceMealId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(courses);
        _coursesMock.Setup(c => c.AddCourseAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(),
                      It.IsAny<string?>(), It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Guid.NewGuid());

        await Create().CloneMealAsync(sourceMealId, targetDate, "Dinner");

        _coursesMock.Verify(c => c.AddCourseAsync(newMealId, It.IsAny<string>(), It.IsAny<Guid?>(),
            It.IsAny<string?>(), It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task CloneMeal_WhenSourceNotFound_ThrowsKeyNotFoundException()
    {
        Guid mealId = Guid.NewGuid();
        _plansMock.Setup(p => p.GetPlannedMealByIdAsync(mealId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync((PlannedMealDto?)null);

        Func<Task> act = () => Create().CloneMealAsync(mealId, new DateOnly(2026, 3, 15), "Dinner");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── CopyDay ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CopyDay_ClonesAllMealsFromSourceDate()
    {
        Guid planId  = Guid.NewGuid();
        Guid userId  = Guid.NewGuid();
        DateOnly src = new(2026, 3, 10);
        DateOnly tgt = new(2026, 3, 17);

        PlannedMealDto meal1 = MakeMeal(Guid.NewGuid(), planId, userId, src, "Breakfast");
        PlannedMealDto meal2 = MakeMeal(Guid.NewGuid(), planId, userId, src, "Dinner");

        _plansMock.Setup(p => p.GetMealsByDateAsync(planId, src, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new List<PlannedMealDto> { meal1, meal2 });
        _plansMock.Setup(p => p.GetPlannedMealByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync<Guid, CancellationToken, IMealPlanningRepository, PlannedMealDto?>(
                      (id, _) => id == meal1.Id ? meal1 : meal2);
        _plansMock.Setup(p => p.AddPlannedMealAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
                      It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Guid.NewGuid());
        _coursesMock.Setup(c => c.GetCoursesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<MealCourseDto>());

        await Create().CopyDayAsync(planId, src, tgt);

        _plansMock.Verify(p => p.AddPlannedMealAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
            tgt.ToDateTime(TimeOnly.MinValue), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task CopyDay_WhenSourceDateEmpty_NothingIsAdded()
    {
        Guid planId  = Guid.NewGuid();
        DateOnly src = new(2026, 3, 10);
        DateOnly tgt = new(2026, 3, 17);

        _plansMock.Setup(p => p.GetMealsByDateAsync(planId, src, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new List<PlannedMealDto>());

        await Create().CopyDayAsync(planId, src, tgt);

        _plansMock.Verify(p => p.AddPlannedMealAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
            It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── CopyPlan ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CopyPlan_WhenSourcePlanExists_CreatesNewPlanAndCopiesMeals()
    {
        Guid sourcePlanId = Guid.NewGuid();
        Guid newPlanId    = Guid.NewGuid();
        Guid userId       = Guid.NewGuid();
        DateTime start    = new DateTime(2026, 3, 1);
        DateTime end      = new DateTime(2026, 3, 7);
        DateOnly newStart = new(2026, 4, 1);

        MealPlanDto source = new() { Id = sourcePlanId, UserId = userId, StartDate = start, EndDate = end, Name = "Week 1" };

        PlannedMealDto meal = MakeMeal(Guid.NewGuid(), sourcePlanId, userId, new DateOnly(2026, 3, 3), "Dinner");

        _plansMock.Setup(p => p.GetMealPlanByIdAsync(sourcePlanId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(source);
        _plansMock.Setup(p => p.CreateMealPlanAsync(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                      "Week 1 (Copy)", It.IsAny<CancellationToken>()))
                  .ReturnsAsync(newPlanId);
        _plansMock.Setup(p => p.GetPlannedMealsAsync(sourcePlanId, null, null, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new List<PlannedMealDto> { meal });
        _plansMock.Setup(p => p.GetPlannedMealByIdAsync(meal.Id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(meal);
        _plansMock.Setup(p => p.AddPlannedMealAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
                      It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Guid.NewGuid());
        _coursesMock.Setup(c => c.GetCoursesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<MealCourseDto>());

        Guid result = await Create().CopyPlanAsync(sourcePlanId, "Week 1 (Copy)", newStart);

        result.Should().Be(newPlanId);
        _plansMock.Verify(p => p.CreateMealPlanAsync(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>(),
            "Week 1 (Copy)", It.IsAny<CancellationToken>()), Times.Once);
        // Offset for Mar 3 relative to Mar 1 = 2 days → copied to Apr 3
        _plansMock.Verify(p => p.AddPlannedMealAsync(newPlanId, It.IsAny<Guid>(), It.IsAny<Guid?>(),
            new DateOnly(2026, 4, 3).ToDateTime(TimeOnly.MinValue), "Dinner", 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CopyPlan_WhenSourceNotFound_ThrowsKeyNotFoundException()
    {
        Guid planId = Guid.NewGuid();
        _plansMock.Setup(p => p.GetMealPlanByIdAsync(planId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync((MealPlanDto?)null);

        Func<Task> act = () => Create().CopyPlanAsync(planId, "Copy", new DateOnly(2026, 4, 1));

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── CopyWeek ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CopyWeek_InvokesGetMealsByDateForEachOfThe7Days()
    {
        Guid planId     = Guid.NewGuid();
        DateOnly srcStart = new(2026, 3, 9);   // Monday
        DateOnly tgtStart = new(2026, 3, 16);

        _plansMock.Setup(p => p.GetMealsByDateAsync(planId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new List<PlannedMealDto>());

        await Create().CopyWeekAsync(planId, srcStart, tgtStart);

        _plansMock.Verify(p => p.GetMealsByDateAsync(planId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Exactly(7));
    }
}
