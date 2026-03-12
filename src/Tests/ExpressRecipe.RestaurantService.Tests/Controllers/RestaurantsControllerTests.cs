using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ExpressRecipe.RestaurantService.Controllers;
using ExpressRecipe.RestaurantService.Data;
using ExpressRecipe.RestaurantService.Tests.Helpers;
using ExpressRecipe.Shared.DTOs.Product;

namespace ExpressRecipe.RestaurantService.Tests.Controllers;

public class RestaurantsControllerTests
{
    private readonly Mock<IRestaurantRepository> _mockRepository;
    private readonly Mock<ILogger<RestaurantsController>> _mockLogger;
    private readonly RestaurantsController _controller;
    private readonly Guid _testUserId;

    public RestaurantsControllerTests()
    {
        _mockRepository = new Mock<IRestaurantRepository>();
        _mockLogger = new Mock<ILogger<RestaurantsController>>();
        _controller = new RestaurantsController(_mockRepository.Object, _mockLogger.Object);
        _testUserId = Guid.NewGuid();
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);
    }

    #region Search Tests

    [Fact]
    public async Task Search_WithSearchTerm_ReturnsMatchingRestaurants()
    {
        // Arrange
        var request = new RestaurantSearchRequest { SearchTerm = "pizza", OnlyApproved = true };
        var restaurants = new List<RestaurantDto>
        {
            new RestaurantDto { Id = Guid.NewGuid(), Name = "Pizza Palace" },
            new RestaurantDto { Id = Guid.NewGuid(), Name = "Mario's Pizza" }
        };

        _mockRepository
            .Setup(r => r.SearchAsync(request))
            .ReturnsAsync(restaurants);

        // Act
        var result = await _controller.Search(request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        (okResult!.Value as List<RestaurantDto>)!.Should().HaveCount(2);
    }

    [Fact]
    public async Task Search_WhenNoResults_ReturnsEmptyList()
    {
        // Arrange
        var request = new RestaurantSearchRequest { SearchTerm = "nonexistent" };
        _mockRepository
            .Setup(r => r.SearchAsync(request))
            .ReturnsAsync(new List<RestaurantDto>());

        // Act
        var result = await _controller.Search(request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region GetById Tests

    [Fact]
    public async Task GetById_WithExistingId_ReturnsRestaurant()
    {
        // Arrange
        var restaurantId = Guid.NewGuid();
        var restaurant = new RestaurantDto { Id = restaurantId, Name = "The Good Fork", City = "New York" };

        _mockRepository
            .Setup(r => r.GetByIdAsync(restaurantId))
            .ReturnsAsync(restaurant);

        // Act
        var result = await _controller.GetById(restaurantId);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        (okResult!.Value as RestaurantDto)!.Id.Should().Be(restaurantId);
    }

    [Fact]
    public async Task GetById_WithNonExistingId_ReturnsNotFound()
    {
        // Arrange
        var restaurantId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.GetByIdAsync(restaurantId))
            .ReturnsAsync((RestaurantDto?)null);

        // Act
        var result = await _controller.GetById(restaurantId);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region Create Tests

    [Fact]
    public async Task Create_WhenAuthenticated_ReturnsCreated()
    {
        // Arrange
        var request = new CreateRestaurantRequest { Name = "New Restaurant", City = "Chicago" };
        var restaurantId = Guid.NewGuid();
        var createdRestaurant = new RestaurantDto { Id = restaurantId, Name = request.Name };

        _mockRepository
            .Setup(r => r.CreateAsync(request, _testUserId))
            .ReturnsAsync(restaurantId);
        _mockRepository
            .Setup(r => r.GetByIdAsync(restaurantId))
            .ReturnsAsync(createdRestaurant);

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
        var request = new CreateRestaurantRequest { Name = "Test Restaurant" };

        // Act
        var result = await _controller.Create(request);

        // Assert
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    #endregion

    #region RateRestaurant Tests

    [Fact]
    public async Task RateRestaurant_WhenAuthenticated_ReturnsNoContent()
    {
        // Arrange
        var restaurantId = Guid.NewGuid();
        var request = new RateRestaurantRequest { Rating = 4, Review = "Great food!" };

        _mockRepository
            .Setup(r => r.RestaurantExistsAsync(restaurantId))
            .ReturnsAsync(true);
        _mockRepository
            .Setup(r => r.AddOrUpdateRatingAsync(restaurantId, _testUserId, request))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        var result = await _controller.RateRestaurant(restaurantId, request);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task RateRestaurant_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();
        var restaurantId = Guid.NewGuid();
        var request = new RateRestaurantRequest { Rating = 5 };

        // Act
        var result = await _controller.RateRestaurant(restaurantId, request);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    #endregion
}
