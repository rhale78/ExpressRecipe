using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using ExpressRecipe.ShoppingService.Controllers;
using ExpressRecipe.ShoppingService.Data;
using ExpressRecipe.ShoppingService.Services;
using ExpressRecipe.ShoppingService.Tests.Helpers;

namespace ExpressRecipe.ShoppingService.Tests.Controllers;

public class OptimizationControllerTests
{
    private static readonly Guid _userId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid _listId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid _storeId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private readonly Mock<ILogger<OptimizationController>> _mockLogger;
    private readonly Mock<IShoppingRepository> _mockRepo;
    private readonly Mock<IShoppingOptimizationService> _mockOptimizationService;
    private readonly Mock<IShoppingSessionService> _mockSessionService;
    private readonly OptimizationController _controller;

    public OptimizationControllerTests()
    {
        _mockLogger = new Mock<ILogger<OptimizationController>>();
        _mockRepo = new Mock<IShoppingRepository>();
        _mockOptimizationService = new Mock<IShoppingOptimizationService>();
        _mockSessionService = new Mock<IShoppingSessionService>();

        _controller = new OptimizationController(
            _mockLogger.Object,
            _mockRepo.Object,
            _mockOptimizationService.Object,
            _mockSessionService.Object)
        {
            ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_userId)
        };
    }

    private ShoppingListDto MakeList() => new()
    {
        Id = _listId,
        UserId = _userId,
        Name = "Weekly Shop",
        StoreName = "Publix"
    };

    // ── Optimize ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Optimize_ValidList_ReturnsOkWithOptimizedPlan()
    {
        // Arrange
        var plan = new OptimizedShoppingPlan
        {
            Strategy = "SingleStore",
            StoreGroups = new List<StoreShoppingGroup>
            {
                new() { StoreId = _storeId, StoreName = "Publix", Items = new() }
            }
        };
        _mockRepo.Setup(r => r.GetShoppingListAsync(_listId, _userId))
            .ReturnsAsync(MakeList());
        _mockOptimizationService
            .Setup(s => s.OptimizeAsync(_listId, _userId, "SingleStore", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        IActionResult result = await _controller.Optimize(_listId, "SingleStore");

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<OptimizedShoppingPlan>()
            .Which.Strategy.Should().Be("SingleStore");
    }

    [Fact]
    public async Task Optimize_ListNotFound_ReturnsNotFound()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetShoppingListAsync(_listId, _userId))
            .ReturnsAsync((ShoppingListDto?)null);

        // Act
        IActionResult result = await _controller.Optimize(_listId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    // ── GetOptimization ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetOptimization_WithExistingResult_ReturnsOk()
    {
        // Arrange
        var optimizationDto = new ShoppingListOptimizationDto
        {
            Id = Guid.NewGuid(),
            ShoppingListId = _listId,
            Strategy = "CheapestOverall",
            OptimizedAt = DateTime.UtcNow,
            TotalEstimate = 42.50m,
            TotalWithDeals = 38.00m,
            StoreCount = 2,
            ResultJson = "{}"
        };
        _mockRepo.Setup(r => r.GetShoppingListAsync(_listId, _userId))
            .ReturnsAsync(MakeList());
        _mockRepo.Setup(r => r.GetOptimizationResultAsync(_listId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(optimizationDto);

        // Act
        IActionResult result = await _controller.GetOptimization(_listId);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<ShoppingListOptimizationDto>()
            .Which.Strategy.Should().Be("CheapestOverall");
    }

    [Fact]
    public async Task GetOptimization_NoResult_ReturnsNotFound()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetShoppingListAsync(_listId, _userId))
            .ReturnsAsync(MakeList());
        _mockRepo.Setup(r => r.GetOptimizationResultAsync(_listId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShoppingListOptimizationDto?)null);

        // Act
        IActionResult result = await _controller.GetOptimization(_listId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── AddFromRecipe ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AddFromRecipe_ValidRequest_ReturnsOk()
    {
        // Arrange
        Guid recipeId = Guid.NewGuid();
        Guid resultId = Guid.NewGuid();
        var request = new AddFromRecipeRequest(recipeId, 2);

        _mockRepo.Setup(r => r.GetShoppingListAsync(_listId, _userId))
            .ReturnsAsync(MakeList());
        _mockSessionService
            .Setup(s => s.AddItemsFromRecipeAsync(_listId, _userId, recipeId, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultId);

        // Act
        IActionResult result = await _controller.AddFromRecipe(_listId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task AddFromRecipe_NullRequest_ReturnsBadRequest()
    {
        // Act
        IActionResult result = await _controller.AddFromRecipe(_listId, null!);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AddFromRecipe_InvalidServings_ReturnsBadRequest()
    {
        // Arrange
        var request = new AddFromRecipeRequest(Guid.NewGuid(), 0);

        // Act
        IActionResult result = await _controller.AddFromRecipe(_listId, request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AddFromRecipe_ListNotFound_ReturnsNotFound()
    {
        // Arrange
        var request = new AddFromRecipeRequest(Guid.NewGuid(), 2);
        _mockRepo.Setup(r => r.GetShoppingListAsync(_listId, _userId))
            .ReturnsAsync((ShoppingListDto?)null);

        // Act
        IActionResult result = await _controller.AddFromRecipe(_listId, request);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    // ── GetSortedItems ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSortedItems_WithStoreId_ReturnsOkWithItems()
    {
        // Arrange
        var sortedItems = new List<OptimizedShoppingItem>
        {
            new() { ShoppingListItemId = Guid.NewGuid(), Name = "Milk", Quantity = 1, AisleOrder = 1 },
            new() { ShoppingListItemId = Guid.NewGuid(), Name = "Bread", Quantity = 2, AisleOrder = 2 }
        };
        _mockRepo.Setup(r => r.GetShoppingListAsync(_listId, _userId))
            .ReturnsAsync(MakeList());
        _mockRepo
            .Setup(r => r.GetItemsSortedByAisleAsync(_listId, _storeId, "Aisle", It.IsAny<CancellationToken>()))
            .ReturnsAsync(sortedItems);

        // Act
        IActionResult result = await _controller.GetSortedItems(_listId, _storeId, "Aisle");

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<List<OptimizedShoppingItem>>()
            .Which.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetSortedItems_WithoutStoreId_ReturnsBadRequest()
    {
        // Act
        IActionResult result = await _controller.GetSortedItems(_listId, storeId: null);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── GetCategoryPreferences ────────────────────────────────────────────────

    [Fact]
    public async Task GetCategoryPreferences_ReturnsOkWithPreferences()
    {
        // Arrange
        var prefs = new List<UserStoreCategoryPreferenceDto>
        {
            new() { Id = Guid.NewGuid(), UserId = _userId, Category = "Produce", PreferredStoreId = _storeId, RankOrder = 1 },
            new() { Id = Guid.NewGuid(), UserId = _userId, Category = "Dairy", PreferredStoreId = _storeId, RankOrder = 1 }
        };
        _mockRepo.Setup(r => r.GetUserCategoryPreferencesAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);

        // Act
        IActionResult result = await _controller.GetCategoryPreferences();

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<List<UserStoreCategoryPreferenceDto>>()
            .Which.Should().HaveCount(2);
    }

    // ── UpsertCategoryPreferences ─────────────────────────────────────────────

    [Fact]
    public async Task UpsertCategoryPreferences_SavesAllPreferences_ReturnsNoContent()
    {
        // Arrange
        var preferences = new List<UserStoreCategoryPreferenceRecord>
        {
            new() { Category = "Produce", PreferredStoreId = _storeId, RankOrder = 1 },
            new() { Category = "Dairy", PreferredStoreId = _storeId, RankOrder = 1 }
        };
        _mockRepo
            .Setup(r => r.UpsertStoreCategoryPreferenceAsync(It.IsAny<UserStoreCategoryPreferenceRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        IActionResult result = await _controller.UpsertCategoryPreferences(preferences);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _mockRepo.Verify(
            r => r.UpsertStoreCategoryPreferenceAsync(
                It.Is<UserStoreCategoryPreferenceRecord>(p => p.UserId == _userId),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    // ── DeleteCategoryPreference ──────────────────────────────────────────────

    [Fact]
    public async Task DeleteCategoryPreference_ReturnsNoContent()
    {
        // Arrange
        Guid preferenceId = Guid.NewGuid();
        _mockRepo
            .Setup(r => r.DeleteStoreCategoryPreferenceAsync(preferenceId, _userId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        IActionResult result = await _controller.DeleteCategoryPreference(preferenceId);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _mockRepo.Verify(
            r => r.DeleteStoreCategoryPreferenceAsync(preferenceId, _userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── GetPriceProfile ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetPriceProfile_ExistingProfile_ReturnsOk()
    {
        // Arrange
        var profile = new UserPriceSearchProfileDto
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            StrategyPriority = "[\"Cheapest\"]",
            MaxStoreDistanceMiles = 10,
            OnlineAllowed = true,
            UpdatedAt = DateTime.UtcNow
        };
        _mockRepo.Setup(r => r.GetPriceSearchProfileAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        // Act
        IActionResult result = await _controller.GetPriceProfile();

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<UserPriceSearchProfileDto>()
            .Which.UserId.Should().Be(_userId);
    }

    [Fact]
    public async Task GetPriceProfile_NoProfile_ReturnsNotFound()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetPriceSearchProfileAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserPriceSearchProfileDto?)null);

        // Act
        IActionResult result = await _controller.GetPriceProfile();

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── UpsertPriceProfile ────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertPriceProfile_SetsUserId_ReturnsNoContent()
    {
        // Arrange
        var profileRecord = new UserPriceSearchProfileRecord
        {
            StrategyPriority = "[\"Cheapest\"]",
            MaxStoreDistanceMiles = 15,
            OnlineAllowed = true,
            DeliveryAllowed = false
        };
        _mockRepo
            .Setup(r => r.UpsertPriceSearchProfileAsync(It.IsAny<UserPriceSearchProfileRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        IActionResult result = await _controller.UpsertPriceProfile(profileRecord);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        // Verify the UserId was injected from the authenticated user
        _mockRepo.Verify(
            r => r.UpsertPriceSearchProfileAsync(
                It.Is<UserPriceSearchProfileRecord>(p => p.UserId == _userId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
