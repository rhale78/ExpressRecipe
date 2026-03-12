using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ExpressRecipe.MenuItemService.Controllers;
using ExpressRecipe.MenuItemService.Data;
using ExpressRecipe.MenuItemService.Tests.Helpers;
using ExpressRecipe.Shared.DTOs.Product;

namespace ExpressRecipe.MenuItemService.Tests.Controllers;

public class MenuItemsControllerTests
{
    private readonly Mock<IMenuItemRepository> _mockRepository;
    private readonly Mock<ILogger<MenuItemsController>> _mockLogger;
    private readonly MenuItemsController _controller;
    private readonly Guid _testUserId;

    public MenuItemsControllerTests()
    {
        _mockRepository = new Mock<IMenuItemRepository>();
        _mockLogger = new Mock<ILogger<MenuItemsController>>();
        _controller = new MenuItemsController(_mockRepository.Object, _mockLogger.Object);
        _testUserId = Guid.NewGuid();
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);
    }

    #region Search Tests

    [Fact]
    public async Task Search_WithValidRequest_ReturnsOkWithResults()
    {
        // Arrange
        var request = new MenuItemSearchRequest { SearchTerm = "pasta", PageNumber = 1, PageSize = 20 };
        var menuItems = new List<MenuItemDto>
        {
            new MenuItemDto { Id = Guid.NewGuid(), Name = "Spaghetti Carbonara", RestaurantId = Guid.NewGuid() },
            new MenuItemDto { Id = Guid.NewGuid(), Name = "Penne Arrabbiata", RestaurantId = Guid.NewGuid() }
        };

        _mockRepository
            .Setup(r => r.SearchAsync(request))
            .ReturnsAsync(menuItems);

        // Act
        var result = await _controller.Search(request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(menuItems);
    }

    [Fact]
    public async Task Search_WhenNoResults_ReturnsEmptyList()
    {
        // Arrange
        var request = new MenuItemSearchRequest { SearchTerm = "nonexistent" };
        _mockRepository
            .Setup(r => r.SearchAsync(request))
            .ReturnsAsync(new List<MenuItemDto>());

        // Act
        var result = await _controller.Search(request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        (okResult!.Value as List<MenuItemDto>).Should().BeEmpty();
    }

    #endregion

    #region GetById Tests

    [Fact]
    public async Task GetById_WithExistingId_ReturnsMenuItem()
    {
        // Arrange
        var menuItemId = Guid.NewGuid();
        var menuItem = new MenuItemDto { Id = menuItemId, Name = "Pizza Margherita", RestaurantId = Guid.NewGuid() };

        _mockRepository
            .Setup(r => r.GetByIdAsync(menuItemId))
            .ReturnsAsync(menuItem);
        _mockRepository
            .Setup(r => r.GetMenuItemIngredientsAsync(menuItemId))
            .ReturnsAsync(new List<MenuItemIngredientDto>());
        _mockRepository
            .Setup(r => r.GetMenuItemNutritionAsync(menuItemId))
            .ReturnsAsync((MenuItemNutritionDto?)null);

        // Act
        var result = await _controller.GetById(menuItemId);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        (okResult!.Value as MenuItemDto)!.Id.Should().Be(menuItemId);
    }

    [Fact]
    public async Task GetById_WithNonExistingId_ReturnsNotFound()
    {
        // Arrange
        var menuItemId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.GetByIdAsync(menuItemId))
            .ReturnsAsync((MenuItemDto?)null);

        // Act
        var result = await _controller.GetById(menuItemId);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region Create Tests

    [Fact]
    public async Task Create_WhenAuthenticated_ReturnsCreatedWithMenuItem()
    {
        // Arrange
        var restaurantId = Guid.NewGuid();
        var request = new CreateMenuItemRequest { RestaurantId = restaurantId, Name = "New Dish", Price = 12.99m };
        var newId = Guid.NewGuid();
        var createdItem = new MenuItemDto { Id = newId, RestaurantId = restaurantId, Name = request.Name };

        _mockRepository
            .Setup(r => r.CreateAsync(request, _testUserId))
            .ReturnsAsync(newId);
        _mockRepository
            .Setup(r => r.GetByIdAsync(newId))
            .ReturnsAsync(createdItem);
        _mockRepository
            .Setup(r => r.GetMenuItemIngredientsAsync(newId))
            .ReturnsAsync(new List<MenuItemIngredientDto>());
        _mockRepository
            .Setup(r => r.GetMenuItemNutritionAsync(newId))
            .ReturnsAsync((MenuItemNutritionDto?)null);

        // Act
        var result = await _controller.Create(request);

        // Assert
        result.Result.Should().BeOfType<CreatedAtActionResult>();
        _mockRepository.Verify(r => r.CreateAsync(request, _testUserId), Times.Once);
    }

    [Fact]
    public async Task Create_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();
        var request = new CreateMenuItemRequest { RestaurantId = Guid.NewGuid(), Name = "Test" };

        // Act
        var result = await _controller.Create(request);

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    #endregion

    #region RateMenuItem Tests

    [Fact]
    public async Task RateMenuItem_WhenAuthenticated_ReturnsNoContent()
    {
        // Arrange
        var menuItemId = Guid.NewGuid();
        var request = new RateMenuItemRequest { Rating = 4, Review = "Great dish!" };

        _mockRepository
            .Setup(r => r.MenuItemExistsAsync(menuItemId))
            .ReturnsAsync(true);
        _mockRepository
            .Setup(r => r.AddOrUpdateRatingAsync(menuItemId, _testUserId, request))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        var result = await _controller.RateMenuItem(menuItemId, request);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task RateMenuItem_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();
        var menuItemId = Guid.NewGuid();
        var request = new RateMenuItemRequest { Rating = 5 };

        // Act
        var result = await _controller.RateMenuItem(menuItemId, request);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    #endregion
}
