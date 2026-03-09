using ExpressRecipe.MealPlanningService.Controllers;
using ExpressRecipe.MealPlanningService.Data;
using ExpressRecipe.MealPlanningService.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ExpressRecipe.MealPlanningService.Tests.Controllers;

/// <summary>
/// Unit tests for <see cref="MealPlanningController"/>.
/// All repository and service dependencies are mocked; HTTP context is set up
/// via <see cref="MealPlanningControllerHelpers.CreateAuthenticatedContext"/>.
/// </summary>
public class MealPlanningControllerTests
{
    private readonly Mock<IMealPlanningRepository> _repoMock = new();
    private readonly Mock<IMealSuggestionService> _suggestionMock = new();
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock = new();
    private readonly IConfiguration _config = new ConfigurationBuilder().Build();
    private readonly Guid _userId = Guid.NewGuid();

    private MealPlanningController CreateController()
    {
        MealPlanningController controller = new(
            NullLogger<MealPlanningController>.Instance,
            _repoMock.Object,
            _suggestionMock.Object,
            _httpClientFactoryMock.Object,
            _config);

        controller.ControllerContext = MealPlanningControllerHelpers.CreateAuthenticatedContext(_userId);
        return controller;
    }

    // ── CreateMealPlan ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateMealPlan_ValidRequest_Returns201CreatedWithPlanDto()
    {
        Guid planId = Guid.NewGuid();
        MealPlanDto expectedPlan = new()
        {
            Id        = planId,
            UserId    = _userId,
            Name      = "Weekly Plan",
            StartDate = DateTime.UtcNow.Date,
            EndDate   = DateTime.UtcNow.Date.AddDays(7)
        };

        _repoMock.Setup(r => r.CreateMealPlanAsync(
                _userId,
                expectedPlan.StartDate,
                expectedPlan.EndDate,
                expectedPlan.Name))
            .ReturnsAsync(planId);

        _repoMock.Setup(r => r.GetMealPlanAsync(planId, _userId))
            .ReturnsAsync(expectedPlan);

        MealPlanningController controller = CreateController();
        CreatePlanRequest request = new()
        {
            StartDate = expectedPlan.StartDate,
            EndDate   = expectedPlan.EndDate,
            Name      = expectedPlan.Name
        };

        IActionResult result = await controller.CreateMealPlan(request);

        CreatedAtActionResult createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(201);
        createdResult.ActionName.Should().Be(nameof(MealPlanningController.GetMealPlan));
        createdResult.Value.Should().Be(expectedPlan);
    }

    [Fact]
    public async Task CreateMealPlan_DelegatesToRepositoryWithAuthenticatedUserId()
    {
        Guid planId = Guid.NewGuid();
        DateTime start = DateTime.UtcNow.Date;
        DateTime end   = start.AddDays(6);

        _repoMock.Setup(r => r.CreateMealPlanAsync(_userId, start, end, "My Plan"))
            .ReturnsAsync(planId);
        _repoMock.Setup(r => r.GetMealPlanAsync(planId, _userId))
            .ReturnsAsync(new MealPlanDto { Id = planId, UserId = _userId });

        MealPlanningController controller = CreateController();

        await controller.CreateMealPlan(new CreatePlanRequest
        {
            StartDate = start,
            EndDate   = end,
            Name      = "My Plan"
        });

        _repoMock.Verify(r => r.CreateMealPlanAsync(_userId, start, end, "My Plan"), Times.Once);
    }

    // ── GetMealPlans ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMealPlans_ReturnsOkWithListOfPlans()
    {
        List<MealPlanDto> plans = new()
        {
            new MealPlanDto { Id = Guid.NewGuid(), UserId = _userId, Name = "Plan A" },
            new MealPlanDto { Id = Guid.NewGuid(), UserId = _userId, Name = "Plan B" }
        };

        _repoMock.Setup(r => r.GetUserMealPlansAsync(_userId))
            .ReturnsAsync(plans);

        MealPlanningController controller = CreateController();

        IActionResult result = await controller.GetMealPlans();

        OkObjectResult okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(plans);
    }

    [Fact]
    public async Task GetMealPlans_NoPlanExists_ReturnsOkWithEmptyList()
    {
        _repoMock.Setup(r => r.GetUserMealPlansAsync(_userId))
            .ReturnsAsync(new List<MealPlanDto>());

        MealPlanningController controller = CreateController();

        IActionResult result = await controller.GetMealPlans();

        OkObjectResult okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.As<List<MealPlanDto>>().Should().BeEmpty();
    }

    // ── GetMealPlan ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMealPlan_ExistingPlan_ReturnsOkWithPlanDto()
    {
        Guid planId = Guid.NewGuid();
        MealPlanDto plan = new() { Id = planId, UserId = _userId, Name = "My Plan" };

        _repoMock.Setup(r => r.GetMealPlanAsync(planId, _userId))
            .ReturnsAsync(plan);

        MealPlanningController controller = CreateController();

        IActionResult result = await controller.GetMealPlan(planId);

        OkObjectResult okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(plan);
    }

    [Fact]
    public async Task GetMealPlan_PlanNotFound_Returns404NotFound()
    {
        Guid planId = Guid.NewGuid();

        _repoMock.Setup(r => r.GetMealPlanAsync(planId, _userId))
            .ReturnsAsync((MealPlanDto?)null);

        MealPlanningController controller = CreateController();

        IActionResult result = await controller.GetMealPlan(planId);

        result.Should().BeOfType<NotFoundResult>();
    }

    // ── DeletePlan ────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeletePlan_ExistingPlan_Returns204NoContent()
    {
        Guid planId = Guid.NewGuid();

        _repoMock.Setup(r => r.DeleteMealPlanAsync(planId, _userId))
            .Returns(Task.CompletedTask);

        MealPlanningController controller = CreateController();

        IActionResult result = await controller.DeletePlan(planId);

        result.Should().BeOfType<NoContentResult>();
        _repoMock.Verify(r => r.DeleteMealPlanAsync(planId, _userId), Times.Once);
    }

    // ── AddPlannedMeal ────────────────────────────────────────────────────────

    [Fact]
    public async Task AddPlannedMeal_ValidRequest_ReturnsOkWithMealId()
    {
        Guid planId  = Guid.NewGuid();
        Guid mealId  = Guid.NewGuid();
        AddMealRequest request = new()
        {
            RecipeId   = Guid.NewGuid(),
            PlannedFor = DateTime.UtcNow.AddDays(1),
            MealType   = "Lunch",
            Servings   = 2
        };

        _repoMock.Setup(r => r.AddPlannedMealAsync(
                planId, _userId,
                request.RecipeId, request.PlannedFor,
                request.MealType, request.Servings))
            .ReturnsAsync(mealId);

        MealPlanningController controller = CreateController();

        IActionResult result = await controller.AddPlannedMeal(planId, request);

        OkObjectResult okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(new { id = mealId });
    }

    [Fact]
    public async Task AddPlannedMeal_DelegatesToRepositoryWithAuthenticatedUserId()
    {
        Guid planId  = Guid.NewGuid();
        Guid recipeId = Guid.NewGuid();
        AddMealRequest request = new() { RecipeId = recipeId, PlannedFor = DateTime.UtcNow.AddDays(2) };

        _repoMock.Setup(r => r.AddPlannedMealAsync(
                planId, _userId,
                recipeId, request.PlannedFor,
                request.MealType, request.Servings))
            .ReturnsAsync(Guid.NewGuid());

        MealPlanningController controller = CreateController();

        await controller.AddPlannedMeal(planId, request);

        _repoMock.Verify(r => r.AddPlannedMealAsync(
            planId, _userId,
            recipeId, request.PlannedFor,
            request.MealType, request.Servings), Times.Once);
    }

    // ── GetPlannedMeals ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetPlannedMeals_ReturnsOkWithMealsList()
    {
        Guid planId = Guid.NewGuid();
        List<PlannedMealDto> meals = new()
        {
            new PlannedMealDto { Id = Guid.NewGuid(), MealPlanId = planId, RecipeName = "Pasta" },
            new PlannedMealDto { Id = Guid.NewGuid(), MealPlanId = planId, RecipeName = "Salad" }
        };

        _repoMock.Setup(r => r.GetPlannedMealsAsync(planId, null, null))
            .ReturnsAsync(meals);

        MealPlanningController controller = CreateController();

        IActionResult result = await controller.GetPlannedMeals(planId, null, null);

        OkObjectResult okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(meals);
    }

    [Fact]
    public async Task GetPlannedMeals_WithDateFilters_PassesFiltersToRepository()
    {
        Guid planId       = Guid.NewGuid();
        DateTime startDate = DateTime.UtcNow.Date;
        DateTime endDate   = startDate.AddDays(7);

        _repoMock.Setup(r => r.GetPlannedMealsAsync(planId, startDate, endDate))
            .ReturnsAsync(new List<PlannedMealDto>());

        MealPlanningController controller = CreateController();

        IActionResult result = await controller.GetPlannedMeals(planId, startDate, endDate);

        result.Should().BeOfType<OkObjectResult>();
        _repoMock.Verify(r => r.GetPlannedMealsAsync(planId, startDate, endDate), Times.Once);
    }

    // ── CompletePlannedMeal ───────────────────────────────────────────────────

    [Fact]
    public async Task CompletePlannedMeal_ValidMeal_ReturnsOkWithHistoryId()
    {
        Guid planId    = Guid.NewGuid();
        Guid mealId    = Guid.NewGuid();
        Guid historyId = Guid.NewGuid();

        PlannedMealDto meal = new()
        {
            Id         = mealId,
            MealPlanId = planId,
            RecipeId   = Guid.NewGuid(),
            RecipeName = "Tacos",
            MealType   = "Dinner",
            Servings   = 2
        };
        MealPlanDto plan = new() { Id = planId, UserId = _userId };

        _repoMock.Setup(r => r.GetPlannedMealAsync(mealId)).ReturnsAsync(meal);
        _repoMock.Setup(r => r.GetMealPlanAsync(planId, _userId)).ReturnsAsync(plan);
        _repoMock.Setup(r => r.MarkMealAsCompletedAsync(mealId)).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.RecordCookingHistoryAsync(It.IsAny<CookingHistoryRecord>(), default))
            .ReturnsAsync(historyId);

        MealPlanningController controller = CreateController();

        IActionResult result = await controller.CompletePlannedMeal(planId, mealId, null);

        OkObjectResult okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(new { historyId });
        _repoMock.Verify(r => r.MarkMealAsCompletedAsync(mealId), Times.Once);
    }

    [Fact]
    public async Task CompletePlannedMeal_MealNotFound_Returns404NotFound()
    {
        Guid planId = Guid.NewGuid();
        Guid mealId = Guid.NewGuid();

        _repoMock.Setup(r => r.GetPlannedMealAsync(mealId))
            .ReturnsAsync((PlannedMealDto?)null);

        MealPlanningController controller = CreateController();

        IActionResult result = await controller.CompletePlannedMeal(planId, mealId, null);

        result.Should().BeOfType<NotFoundResult>();
        _repoMock.Verify(r => r.MarkMealAsCompletedAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task CompletePlannedMeal_MealBelongsToDifferentPlan_Returns404NotFound()
    {
        Guid planId        = Guid.NewGuid();
        Guid mealId        = Guid.NewGuid();
        Guid differentPlan = Guid.NewGuid();

        PlannedMealDto meal = new()
        {
            Id         = mealId,
            MealPlanId = differentPlan,   // does not match the route planId
            RecipeName = "Tacos"
        };

        _repoMock.Setup(r => r.GetPlannedMealAsync(mealId)).ReturnsAsync(meal);

        MealPlanningController controller = CreateController();

        IActionResult result = await controller.CompletePlannedMeal(planId, mealId, null);

        result.Should().BeOfType<NotFoundResult>();
        _repoMock.Verify(r => r.MarkMealAsCompletedAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task CompletePlannedMeal_PlanBelongsToDifferentUser_ReturnsForbid()
    {
        Guid planId = Guid.NewGuid();
        Guid mealId = Guid.NewGuid();

        PlannedMealDto meal = new()
        {
            Id         = mealId,
            MealPlanId = planId,
            RecipeName = "Tacos"
        };

        _repoMock.Setup(r => r.GetPlannedMealAsync(mealId)).ReturnsAsync(meal);
        // Returning null simulates the plan not belonging to the authenticated user
        _repoMock.Setup(r => r.GetMealPlanAsync(planId, _userId))
            .ReturnsAsync((MealPlanDto?)null);

        MealPlanningController controller = CreateController();

        IActionResult result = await controller.CompletePlannedMeal(planId, mealId, null);

        result.Should().BeOfType<ForbidResult>();
        _repoMock.Verify(r => r.MarkMealAsCompletedAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task CompletePlannedMeal_RequestContainsRecipeName_UsesRequestNameInHistoryRecord()
    {
        Guid planId    = Guid.NewGuid();
        Guid mealId    = Guid.NewGuid();
        const string overriddenName = "Special Tacos";

        PlannedMealDto meal = new()
        {
            Id         = mealId,
            MealPlanId = planId,
            RecipeId   = Guid.NewGuid(),
            RecipeName = "Original Name",
            MealType   = "Dinner",
            Servings   = 2
        };
        MealPlanDto plan = new() { Id = planId, UserId = _userId };

        _repoMock.Setup(r => r.GetPlannedMealAsync(mealId)).ReturnsAsync(meal);
        _repoMock.Setup(r => r.GetMealPlanAsync(planId, _userId)).ReturnsAsync(plan);
        _repoMock.Setup(r => r.MarkMealAsCompletedAsync(mealId)).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.RecordCookingHistoryAsync(It.IsAny<CookingHistoryRecord>(), default))
            .ReturnsAsync(Guid.NewGuid());

        MealPlanningController controller = CreateController();
        CompleteMealRequest request = new() { RecipeName = overriddenName };

        await controller.CompletePlannedMeal(planId, mealId, request);

        _repoMock.Verify(r => r.RecordCookingHistoryAsync(
            It.Is<CookingHistoryRecord>(h => h.RecipeName == overriddenName), default), Times.Once);
    }

    [Fact]
    public async Task CompletePlannedMeal_NoRequestRecipeName_FallsBackToMealRecipeName()
    {
        Guid planId    = Guid.NewGuid();
        Guid mealId    = Guid.NewGuid();
        const string mealRecipeName = "Stored Recipe Name";

        PlannedMealDto meal = new()
        {
            Id         = mealId,
            MealPlanId = planId,
            RecipeId   = Guid.NewGuid(),
            RecipeName = mealRecipeName,
            MealType   = "Dinner",
            Servings   = 1
        };
        MealPlanDto plan = new() { Id = planId, UserId = _userId };

        _repoMock.Setup(r => r.GetPlannedMealAsync(mealId)).ReturnsAsync(meal);
        _repoMock.Setup(r => r.GetMealPlanAsync(planId, _userId)).ReturnsAsync(plan);
        _repoMock.Setup(r => r.MarkMealAsCompletedAsync(mealId)).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.RecordCookingHistoryAsync(It.IsAny<CookingHistoryRecord>(), default))
            .ReturnsAsync(Guid.NewGuid());

        MealPlanningController controller = CreateController();

        await controller.CompletePlannedMeal(planId, mealId, null);

        _repoMock.Verify(r => r.RecordCookingHistoryAsync(
            It.Is<CookingHistoryRecord>(h => h.RecipeName == mealRecipeName), default), Times.Once);
    }

    // ── GetSuggestions ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSuggestions_ValidRequest_ReturnsOkWithSuggestionsFromService()
    {
        List<MealSuggestion> suggestions = new()
        {
            new MealSuggestion { RecipeId = Guid.NewGuid(), RecipeName = "Pasta", Score = 95m },
            new MealSuggestion { RecipeId = Guid.NewGuid(), RecipeName = "Salad", Score = 80m }
        };

        _suggestionMock.Setup(s => s.SuggestAsync(It.IsAny<SuggestionRequest>(), default))
            .ReturnsAsync(suggestions);

        MealPlanningController controller = CreateController();
        SuggestionRequest request = new() { Count = 5 };

        IActionResult result = await controller.GetSuggestions(request);

        OkObjectResult okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(suggestions);
    }

    [Fact]
    public async Task GetSuggestions_InjectsAuthenticatedUserIdIntoRequest()
    {
        _suggestionMock.Setup(s => s.SuggestAsync(It.IsAny<SuggestionRequest>(), default))
            .ReturnsAsync(new List<MealSuggestion>());

        MealPlanningController controller = CreateController();
        SuggestionRequest request = new() { Count = 3 };

        await controller.GetSuggestions(request);

        _suggestionMock.Verify(s => s.SuggestAsync(
            It.Is<SuggestionRequest>(r => r.UserId == _userId), default), Times.Once);
    }

    [Fact]
    public async Task GetSuggestions_ForwardsRequestPropertiesWithOverriddenUserId()
    {
        Guid householdId = Guid.NewGuid();
        _suggestionMock.Setup(s => s.SuggestAsync(It.IsAny<SuggestionRequest>(), default))
            .ReturnsAsync(new List<MealSuggestion>());

        MealPlanningController controller = CreateController();
        SuggestionRequest request = new()
        {
            HouseholdId    = householdId,
            MealType       = "Breakfast",
            SuggestionMode = SuggestionModes.SomethingNew,
            Count          = 7
        };

        await controller.GetSuggestions(request);

        _suggestionMock.Verify(s => s.SuggestAsync(
            It.Is<SuggestionRequest>(r =>
                r.UserId      == _userId     &&
                r.HouseholdId == householdId &&
                r.MealType    == "Breakfast" &&
                r.Count       == 7),
            default), Times.Once);
    }

    // ── GetCookingHistory ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetCookingHistory_ReturnsOkWithHistoryList()
    {
        List<CookingHistoryDto> history = new()
        {
            new CookingHistoryDto { Id = Guid.NewGuid(), UserId = _userId, RecipeName = "Pasta" },
            new CookingHistoryDto { Id = Guid.NewGuid(), UserId = _userId, RecipeName = "Pizza" }
        };

        _repoMock.Setup(r => r.GetCookingHistoryAsync(_userId, 90, default))
            .ReturnsAsync(history);

        MealPlanningController controller = CreateController();

        IActionResult result = await controller.GetCookingHistory();

        OkObjectResult okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(history);
    }

    [Fact]
    public async Task GetCookingHistory_CustomDaysBack_PassesDaysBackToRepository()
    {
        _repoMock.Setup(r => r.GetCookingHistoryAsync(_userId, 30, default))
            .ReturnsAsync(new List<CookingHistoryDto>());

        MealPlanningController controller = CreateController();

        IActionResult result = await controller.GetCookingHistory(daysBack: 30);

        result.Should().BeOfType<OkObjectResult>();
        _repoMock.Verify(r => r.GetCookingHistoryAsync(_userId, 30, default), Times.Once);
    }

    [Fact]
    public async Task GetCookingHistory_NoHistory_ReturnsOkWithEmptyList()
    {
        _repoMock.Setup(r => r.GetCookingHistoryAsync(_userId, 90, default))
            .ReturnsAsync(new List<CookingHistoryDto>());

        MealPlanningController controller = CreateController();

        IActionResult result = await controller.GetCookingHistory();

        OkObjectResult okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.As<List<CookingHistoryDto>>().Should().BeEmpty();
    }

    // ── RecordCookingHistory ──────────────────────────────────────────────────

    [Fact]
    public async Task RecordCookingHistory_ValidRequest_ReturnsOkWithHistoryId()
    {
        Guid historyId = Guid.NewGuid();

        _repoMock.Setup(r => r.RecordCookingHistoryAsync(It.IsAny<CookingHistoryRecord>(), default))
            .ReturnsAsync(historyId);

        MealPlanningController controller = CreateController();
        RecordCookingHistoryRequest request = new()
        {
            RecipeId   = Guid.NewGuid(),
            RecipeName = "Homemade Burger",
            Servings   = 4,
            MealType   = "Dinner"
        };

        IActionResult result = await controller.RecordCookingHistory(request);

        OkObjectResult okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(new { id = historyId });
    }

    [Fact]
    public async Task RecordCookingHistory_SetsAuthenticatedUserIdOnRecord()
    {
        _repoMock.Setup(r => r.RecordCookingHistoryAsync(It.IsAny<CookingHistoryRecord>(), default))
            .ReturnsAsync(Guid.NewGuid());

        MealPlanningController controller = CreateController();
        RecordCookingHistoryRequest request = new()
        {
            RecipeId   = Guid.NewGuid(),
            RecipeName = "Soup"
        };

        await controller.RecordCookingHistory(request);

        _repoMock.Verify(r => r.RecordCookingHistoryAsync(
            It.Is<CookingHistoryRecord>(h => h.UserId == _userId), default), Times.Once);
    }

    [Fact]
    public async Task RecordCookingHistory_SetsSpontaneousSource()
    {
        _repoMock.Setup(r => r.RecordCookingHistoryAsync(It.IsAny<CookingHistoryRecord>(), default))
            .ReturnsAsync(Guid.NewGuid());

        MealPlanningController controller = CreateController();
        RecordCookingHistoryRequest request = new()
        {
            RecipeId   = Guid.NewGuid(),
            RecipeName = "Stew"
        };

        await controller.RecordCookingHistory(request);

        _repoMock.Verify(r => r.RecordCookingHistoryAsync(
            It.Is<CookingHistoryRecord>(h => h.Source == "Spontaneous"), default), Times.Once);
    }

    [Fact]
    public async Task RecordCookingHistory_NoCookedAt_DefaultsToCurrent()
    {
        DateTime before = DateTime.UtcNow.AddSeconds(-1);

        _repoMock.Setup(r => r.RecordCookingHistoryAsync(It.IsAny<CookingHistoryRecord>(), default))
            .ReturnsAsync(Guid.NewGuid());

        MealPlanningController controller = CreateController();
        RecordCookingHistoryRequest request = new()
        {
            RecipeId   = Guid.NewGuid(),
            RecipeName = "Quick Meal",
            CookedAt   = null   // should be filled by controller
        };

        await controller.RecordCookingHistory(request);

        _repoMock.Verify(r => r.RecordCookingHistoryAsync(
            It.Is<CookingHistoryRecord>(h => h.CookedAt >= before), default), Times.Once);
    }

    // ── UpdateCookingRating ───────────────────────────────────────────────────

    [Fact]
    public async Task UpdateCookingRating_ValidRequest_Returns204NoContent()
    {
        Guid historyId = Guid.NewGuid();
        UpdateRatingRequest request = new() { Rating = 4, WouldCookAgain = true, Notes = "Really good!" };

        _repoMock.Setup(r => r.UpdateCookingRatingAsync(
                historyId, _userId, 4, true, "Really good!", default))
            .Returns(Task.CompletedTask);

        MealPlanningController controller = CreateController();

        IActionResult result = await controller.UpdateCookingRating(historyId, request);

        result.Should().BeOfType<NoContentResult>();
        _repoMock.Verify(r => r.UpdateCookingRatingAsync(
            historyId, _userId, 4, true, "Really good!", default), Times.Once);
    }

    [Fact]
    public async Task UpdateCookingRating_MinimalRequest_Returns204NoContent()
    {
        Guid historyId = Guid.NewGuid();
        UpdateRatingRequest request = new() { Rating = 3 };

        _repoMock.Setup(r => r.UpdateCookingRatingAsync(historyId, _userId, 3, null, null, default))
            .Returns(Task.CompletedTask);

        MealPlanningController controller = CreateController();

        IActionResult result = await controller.UpdateCookingRating(historyId, request);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task UpdateCookingRating_PassesAuthenticatedUserIdToRepository()
    {
        Guid historyId = Guid.NewGuid();
        UpdateRatingRequest request = new() { Rating = 5 };

        _repoMock.Setup(r => r.UpdateCookingRatingAsync(
                historyId, _userId, It.IsAny<byte>(), It.IsAny<bool?>(), It.IsAny<string?>(), default))
            .Returns(Task.CompletedTask);

        MealPlanningController controller = CreateController();

        await controller.UpdateCookingRating(historyId, request);

        _repoMock.Verify(r => r.UpdateCookingRatingAsync(
            historyId, _userId, 5, null, null, default), Times.Once);
    }
}
