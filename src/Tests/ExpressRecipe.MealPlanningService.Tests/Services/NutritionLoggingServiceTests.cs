using ExpressRecipe.MealPlanningService.Data;
using ExpressRecipe.MealPlanningService.Services;
using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Shared.Messages;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ExpressRecipe.MealPlanningService.Tests.Services;

public class NutritionLoggingServiceTests
{
    private readonly Mock<INutritionLogRepository> _logRepoMock = new();
    private readonly Mock<IMessageBus>             _busMock      = new();

    private NutritionLoggingService Create(bool includeBus = true)
        => new(
            _logRepoMock.Object,
            includeBus ? _busMock.Object : null,
            NullLogger<NutritionLoggingService>.Instance);

    // ── LogCookingEventAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task LogCookingEvent_WhenBusIsNull_InsertsRowWithNullMacros()
    {
        NutritionLoggingService svc = Create(includeBus: false);
        DailyNutritionLogRow? captured = null;

        _logRepoMock
            .Setup(r => r.InsertLogAsync(It.IsAny<DailyNutritionLogRow>(), It.IsAny<CancellationToken>()))
            .Callback<DailyNutritionLogRow, CancellationToken>((row, _) => captured = row)
            .Returns(Task.CompletedTask);

        Guid userId   = Guid.NewGuid();
        Guid recipeId = Guid.NewGuid();

        await svc.LogCookingEventAsync(userId, recipeId, "Pasta", "Dinner", 2m, null, null);

        _logRepoMock.Verify(r => r.InsertLogAsync(It.IsAny<DailyNutritionLogRow>(), It.IsAny<CancellationToken>()), Times.Once);
        captured.Should().NotBeNull();
        captured!.Calories.Should().BeNull();
        captured.Protein.Should().BeNull();
        captured.UserId.Should().Be(userId);
        captured.RecipeId.Should().Be(recipeId);
        captured.RecipeName.Should().Be("Pasta");
        captured.ServingsEaten.Should().Be(2m);
        captured.IsManualEntry.Should().BeFalse();
    }

    [Fact]
    public async Task LogCookingEvent_WhenBusReturnsNutrition_InsertsRowWithScaledMacros()
    {
        NutritionLoggingService svc = Create(includeBus: true);

        RecipeNutritionResponse nutrition = new()
        {
            HasData            = true,
            CaloriesPerServing = 400m,
            ProteinPerServing  = 30m,
            CarbsPerServing    = 50m,
            FatPerServing      = 10m,
            FiberPerServing    = 5m,
            SodiumPerServing   = 800m
        };

        _busMock
            .Setup(b => b.RequestAsync<RequestRecipeNutrition, RecipeNutritionResponse>(
                It.IsAny<RequestRecipeNutrition>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(nutrition);

        DailyNutritionLogRow? captured = null;
        _logRepoMock
            .Setup(r => r.InsertLogAsync(It.IsAny<DailyNutritionLogRow>(), It.IsAny<CancellationToken>()))
            .Callback<DailyNutritionLogRow, CancellationToken>((row, _) => captured = row)
            .Returns(Task.CompletedTask);

        await svc.LogCookingEventAsync(Guid.NewGuid(), Guid.NewGuid(), "Steak", "Dinner", 2m, null, null);

        captured!.Calories.Should().Be(800m);   // 400 × 2
        captured.Protein.Should().Be(60m);       // 30 × 2
        captured.Carbohydrates.Should().Be(100m);
        captured.TotalFat.Should().Be(20m);
        captured.DietaryFiber.Should().Be(10m);
        captured.Sodium.Should().Be(1600m);
    }

    [Fact]
    public async Task LogCookingEvent_WithCookingHistoryId_CallsMarkNutritionLogged()
    {
        NutritionLoggingService svc = Create(includeBus: false);
        Guid historyId = Guid.NewGuid();

        _logRepoMock
            .Setup(r => r.InsertLogAsync(It.IsAny<DailyNutritionLogRow>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _logRepoMock
            .Setup(r => r.MarkNutritionLoggedAsync(historyId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await svc.LogCookingEventAsync(Guid.NewGuid(), Guid.NewGuid(), "Chicken", null, 1m, historyId, null);

        _logRepoMock.Verify(r => r.MarkNutritionLoggedAsync(historyId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LogCookingEvent_WithoutCookingHistoryId_DoesNotCallMarkNutritionLogged()
    {
        NutritionLoggingService svc = Create(includeBus: false);

        _logRepoMock
            .Setup(r => r.InsertLogAsync(It.IsAny<DailyNutritionLogRow>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await svc.LogCookingEventAsync(Guid.NewGuid(), Guid.NewGuid(), "Salad", null, 1m, null, null);

        _logRepoMock.Verify(r => r.MarkNutritionLoggedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LogCookingEvent_WhenBusThrows_StillInsertsRowWithNullMacros()
    {
        NutritionLoggingService svc = Create(includeBus: true);

        _busMock
            .Setup(b => b.RequestAsync<RequestRecipeNutrition, RecipeNutritionResponse>(
                It.IsAny<RequestRecipeNutrition>(), null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("bus timeout"));

        DailyNutritionLogRow? captured = null;
        _logRepoMock
            .Setup(r => r.InsertLogAsync(It.IsAny<DailyNutritionLogRow>(), It.IsAny<CancellationToken>()))
            .Callback<DailyNutritionLogRow, CancellationToken>((row, _) => captured = row)
            .Returns(Task.CompletedTask);

        await svc.LogCookingEventAsync(Guid.NewGuid(), Guid.NewGuid(), "Fish", null, 1m, null, null);

        _logRepoMock.Verify(r => r.InsertLogAsync(It.IsAny<DailyNutritionLogRow>(), It.IsAny<CancellationToken>()), Times.Once);
        captured!.Calories.Should().BeNull("bus failure must not block log insertion");
    }

    // ── LogManualEntryAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task LogManualEntry_InsertsRowWithProvidedValues()
    {
        NutritionLoggingService svc = Create(includeBus: false);
        DateOnly logDate = new(2026, 3, 1);
        DailyNutritionLogRow? captured = null;

        _logRepoMock
            .Setup(r => r.InsertLogAsync(It.IsAny<DailyNutritionLogRow>(), It.IsAny<CancellationToken>()))
            .Callback<DailyNutritionLogRow, CancellationToken>((row, _) => captured = row)
            .Returns(Task.CompletedTask);

        Guid userId = Guid.NewGuid();
        await svc.LogManualEntryAsync(userId, "Salad", "Lunch", 1.5m, logDate,
            calories: 200m, protein: 10m, carbs: 25m, fat: 5m, fiber: 3m, sodium: 400m);

        captured.Should().NotBeNull();
        captured!.UserId.Should().Be(userId);
        captured.LogDate.Should().Be(logDate);
        captured.MealType.Should().Be("Lunch");
        captured.ServingsEaten.Should().Be(1.5m);
        captured.Calories.Should().Be(200m);
        captured.Protein.Should().Be(10m);
        captured.Carbohydrates.Should().Be(25m);
        captured.TotalFat.Should().Be(5m);
        captured.DietaryFiber.Should().Be(3m);
        captured.Sodium.Should().Be(400m);
        captured.IsManualEntry.Should().BeTrue();
    }

    [Fact]
    public async Task LogManualEntry_WithNullDate_UsesCurrentUtcDate()
    {
        NutritionLoggingService svc = Create(includeBus: false);
        DailyNutritionLogRow? captured = null;
        DateOnly expectedDate = DateOnly.FromDateTime(DateTime.UtcNow);  // capture before call

        _logRepoMock
            .Setup(r => r.InsertLogAsync(It.IsAny<DailyNutritionLogRow>(), It.IsAny<CancellationToken>()))
            .Callback<DailyNutritionLogRow, CancellationToken>((row, _) => captured = row)
            .Returns(Task.CompletedTask);

        await svc.LogManualEntryAsync(Guid.NewGuid(), "Snack", null, 1m, null, 100m, null, null, null, null, null);

        captured!.LogDate.Should().Be(expectedDate);
    }

    [Fact]
    public async Task LogManualEntry_DoesNotInvolveMessageBus()
    {
        NutritionLoggingService svc = Create(includeBus: true);

        _logRepoMock
            .Setup(r => r.InsertLogAsync(It.IsAny<DailyNutritionLogRow>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await svc.LogManualEntryAsync(Guid.NewGuid(), "Snack", null, 1m, null, null, null, null, null, null, null);

        _busMock.Verify(b => b.RequestAsync<RequestRecipeNutrition, RecipeNutritionResponse>(
            It.IsAny<RequestRecipeNutrition>(), null, It.IsAny<CancellationToken>()), Times.Never);
    }
}
