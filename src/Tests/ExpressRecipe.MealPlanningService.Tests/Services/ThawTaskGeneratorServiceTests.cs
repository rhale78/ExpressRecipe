using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using System.Net;
using ExpressRecipe.MealPlanningService.Data;
using ExpressRecipe.MealPlanningService.Services;

namespace ExpressRecipe.MealPlanningService.Tests.Services;

public class ThawTaskGeneratorServiceTests
{
    private readonly Mock<IHouseholdTaskRepository> _mockTasks;
    private readonly Mock<IHttpClientFactory> _mockHttpFactory;
    private readonly Mock<ILogger<ThawTaskGeneratorService>> _mockLogger;
    private readonly ThawTaskGeneratorService _service;

    private readonly Guid _householdId = Guid.NewGuid();
    private readonly Guid _plannedMealId = Guid.NewGuid();
    private readonly Guid _recipeId = Guid.NewGuid();
    private readonly DateTime _mealDateTime = DateTime.UtcNow.AddDays(2);

    public ThawTaskGeneratorServiceTests()
    {
        _mockTasks       = new Mock<IHouseholdTaskRepository>();
        _mockHttpFactory = new Mock<IHttpClientFactory>();
        _mockLogger      = new Mock<ILogger<ThawTaskGeneratorService>>();
        _service         = new ThawTaskGeneratorService(
            _mockTasks.Object, _mockHttpFactory.Object, _mockLogger.Object);
    }

    private void SetupFrozenIngredientsResponse(List<FrozenIngredientInfo> frozen)
    {
        MockHttpMessageHandler handler = new(HttpStatusCode.OK, frozen);

        HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("http://inventory-service")
        };
        _mockHttpFactory.Setup(f => f.CreateClient("InventoryService")).Returns(httpClient);
    }

    [Fact]
    public async Task GenerateForMealAsync_WithFrozenChicken_CreatesThawTaskWithDueAt24HoursBefore()
    {
        // Arrange
        List<FrozenIngredientInfo> frozen =
        [
            new FrozenIngredientInfo { ItemName = "Chicken Breast", FoodCategory = "Poultry", StorageLocationId = Guid.NewGuid() }
        ];
        SetupFrozenIngredientsResponse(frozen);

        // Act
        await _service.GenerateForMealAsync(_householdId, _plannedMealId, _recipeId, _mealDateTime);

        // Assert: UpsertThawTaskAsync called once
        _mockTasks.Verify(t => t.UpsertThawTaskAsync(
            _householdId,
            _plannedMealId,
            It.Is<string>(title => title.Contains("Chicken Breast")),
            It.IsAny<string>(),
            It.Is<DateTime>(due => Math.Abs((due - _mealDateTime.AddHours(-24)).TotalSeconds) < 5),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateForMealAsync_WithNoFrozenIngredients_DoesNotCreateTask()
    {
        // Arrange
        SetupFrozenIngredientsResponse(new List<FrozenIngredientInfo>());

        // Act
        await _service.GenerateForMealAsync(_householdId, _plannedMealId, _recipeId, _mealDateTime);

        // Assert
        _mockTasks.Verify(t => t.UpsertThawTaskAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateForMealAsync_CalledTwiceForSameMeal_CallsUpsertTwiceNotCreate()
    {
        // Arrange – simulates rescheduled meal
        List<FrozenIngredientInfo> frozen =
        [
            new FrozenIngredientInfo { ItemName = "Beef Steak", FoodCategory = "Meat", StorageLocationId = Guid.NewGuid() }
        ];
        SetupFrozenIngredientsResponse(frozen);

        // Act
        await _service.GenerateForMealAsync(_householdId, _plannedMealId, _recipeId, _mealDateTime);
        await _service.GenerateForMealAsync(_householdId, _plannedMealId, _recipeId, _mealDateTime.AddHours(3));

        // Assert: UpsertThawTaskAsync called twice (once per call); underlying MERGE handles idempotency
        _mockTasks.Verify(t => t.UpsertThawTaskAsync(
            _householdId, _plannedMealId,
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GenerateForMealAsync_MultiFrozenIngredients_UseMaxThawTime()
    {
        // Arrange: chicken (24h) + shrimp (12h) => max = 24h
        List<FrozenIngredientInfo> frozen =
        [
            new FrozenIngredientInfo { ItemName = "Chicken",   FoodCategory = "Poultry",  StorageLocationId = Guid.NewGuid() },
            new FrozenIngredientInfo { ItemName = "Shrimp",    FoodCategory = "Seafood",  StorageLocationId = Guid.NewGuid() }
        ];
        SetupFrozenIngredientsResponse(frozen);

        // Act
        await _service.GenerateForMealAsync(_householdId, _plannedMealId, _recipeId, _mealDateTime);

        // Assert: DueAt = mealDateTime - 24h (max of Poultry 24h and Seafood 12h)
        _mockTasks.Verify(t => t.UpsertThawTaskAsync(
            _householdId, _plannedMealId,
            It.Is<string>(title => title.Contains("Chicken") && title.Contains("Shrimp")),
            It.IsAny<string>(),
            It.Is<DateTime>(due => Math.Abs((due - _mealDateTime.AddHours(-24)).TotalSeconds) < 5),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateForMealAsync_InventoryServiceDown_DoesNotThrow()
    {
        // Arrange: HTTP client that throws
        MockHttpMessageHandler handler = new(new HttpRequestException("Connection refused"));
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://inventory-service") };
        _mockHttpFactory.Setup(f => f.CreateClient("InventoryService")).Returns(httpClient);

        // Act & Assert: should not throw
        Func<Task> act = () => _service.GenerateForMealAsync(_householdId, _plannedMealId, _recipeId, _mealDateTime);
        await act.Should().NotThrowAsync();

        // No task created
        _mockTasks.Verify(t => t.UpsertThawTaskAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveForMealAsync_CallsDeleteTasksByRelatedEntity()
    {
        // Act
        await _service.RemoveForMealAsync(_plannedMealId);

        // Assert
        _mockTasks.Verify(t => t.DeleteTasksByRelatedEntityAsync(_plannedMealId, It.IsAny<CancellationToken>()), Times.Once);
    }
}

// Minimal mock HTTP handler for tests
internal sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpResponseMessage>? _responseFactory;
    private readonly Exception? _exception;

    public MockHttpMessageHandler(HttpStatusCode statusCode, object content)
    {
        string json = System.Text.Json.JsonSerializer.Serialize(content);
        _responseFactory = () => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
    }

    public MockHttpMessageHandler(Exception exception)
        => _exception = exception;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_exception is not null) { throw _exception; }
        return Task.FromResult(_responseFactory!());
    }
}
