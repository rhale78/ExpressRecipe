using ExpressRecipe.Shared.DTOs.User;
using ExpressRecipe.UserService.Controllers;
using ExpressRecipe.UserService.Data;
using ExpressRecipe.UserService.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ExpressRecipe.UserService.Tests.Controllers;

public class UserFavoritesControllerTests
{
    private readonly Mock<IUserFavoritesRepository> _mockRepository;
    private readonly Mock<IUserProductRatingRepository> _mockRatingRepository;
    private readonly Mock<ILogger<UserFavoritesController>> _mockLogger;
    private readonly UserFavoritesController _controller;
    private readonly Guid _testUserId;

    public UserFavoritesControllerTests()
    {
        _mockRepository = new Mock<IUserFavoritesRepository>();
        _mockRatingRepository = new Mock<IUserProductRatingRepository>();
        _mockLogger = new Mock<ILogger<UserFavoritesController>>();
        
        _controller = new UserFavoritesController(
            _mockRepository.Object,
            _mockRatingRepository.Object,
            _mockLogger.Object
        );

        _testUserId = Guid.NewGuid();
        ControllerTestHelpers.SetupControllerContext(_controller, _testUserId);
    }

    #region Recipe Favorites Tests

    [Fact]
    public async Task GetFavoriteRecipes_WithAuthenticatedUser_ReturnsOkWithRecipes()
    {
        // Arrange
        var expectedFavorites = new List<UserFavoriteRecipeDto>
        {
            new UserFavoriteRecipeDto { Id = Guid.NewGuid(), UserId = _testUserId, RecipeId = Guid.NewGuid(), RecipeName = "Pasta" },
            new UserFavoriteRecipeDto { Id = Guid.NewGuid(), UserId = _testUserId, RecipeId = Guid.NewGuid(), RecipeName = "Pizza" }
        };

        _mockRepository
            .Setup(r => r.GetFavoriteRecipesByUserIdAsync(_testUserId))
            .ReturnsAsync(expectedFavorites);

        // Act
        var result = await _controller.GetFavoriteRecipes();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var actualFavorites = okResult.Value.Should().BeAssignableTo<List<UserFavoriteRecipeDto>>().Subject;
        actualFavorites.Should().HaveCount(2);
        actualFavorites.Should().BeEquivalentTo(expectedFavorites);
    }

    [Fact]
    public async Task GetFavoriteRecipes_WithUnauthenticatedUser_ReturnsUnauthorized()
    {
        // Arrange
        ControllerTestHelpers.SetupUnauthenticatedControllerContext(_controller);

        // Act
        var result = await _controller.GetFavoriteRecipes();

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task AddFavoriteRecipe_WithValidRecipeId_ReturnsCreatedAtAction()
    {
        // Arrange
        var recipeId = Guid.NewGuid();
        var favoriteId = Guid.NewGuid();
        var favorite = new UserFavoriteRecipeDto
        {
            Id = favoriteId,
            UserId = _testUserId,
            RecipeId = recipeId,
            Notes = "Test notes",
            CreatedAt = DateTime.UtcNow
        };

        // First call returns null (doesn't exist)
        _mockRepository
            .SetupSequence(r => r.GetFavoriteRecipeAsync(_testUserId, recipeId))
            .ReturnsAsync((UserFavoriteRecipeDto?)null)
            .ReturnsAsync(favorite);

        _mockRepository
            .Setup(r => r.AddFavoriteRecipeAsync(_testUserId, recipeId, It.IsAny<string>(), _testUserId))
            .ReturnsAsync(favoriteId);

        // Act
        var result = await _controller.AddFavoriteRecipe(recipeId, null);

        // Assert
        result.Should().NotBeNull();
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var actualFavorite = createdResult.Value.Should().BeAssignableTo<UserFavoriteRecipeDto>().Subject;
        actualFavorite.RecipeId.Should().Be(recipeId);
    }

    [Fact]
    public async Task AddFavoriteRecipe_WhenAlreadyExists_ReturnsConflict()
    {
        // Arrange
        var recipeId = Guid.NewGuid();
        var existingFavorite = new UserFavoriteRecipeDto
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            RecipeId = recipeId
        };

        _mockRepository
            .Setup(r => r.GetFavoriteRecipeAsync(_testUserId, recipeId))
            .ReturnsAsync(existingFavorite);

        // Act
        var result = await _controller.AddFavoriteRecipe(recipeId, null);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task RemoveFavoriteRecipe_WithExistingFavorite_ReturnsNoContent()
    {
        // Arrange
        var recipeId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.RemoveFavoriteRecipeAsync(_testUserId, recipeId))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.RemoveFavoriteRecipe(recipeId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task RemoveFavoriteRecipe_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        var recipeId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.RemoveFavoriteRecipeAsync(_testUserId, recipeId))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.RemoveFavoriteRecipe(recipeId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region Product Favorites Tests

    [Fact]
    public async Task GetFavoriteProducts_WithAuthenticatedUser_ReturnsOkWithProducts()
    {
        // Arrange
        var expectedFavorites = new List<UserFavoriteProductDto>
        {
            new UserFavoriteProductDto { Id = Guid.NewGuid(), UserId = _testUserId, ProductId = Guid.NewGuid(), ProductName = "Product 1" },
            new UserFavoriteProductDto { Id = Guid.NewGuid(), UserId = _testUserId, ProductId = Guid.NewGuid(), ProductName = "Product 2" }
        };

        _mockRepository
            .Setup(r => r.GetFavoriteProductsByUserIdAsync(_testUserId))
            .ReturnsAsync(expectedFavorites);

        // Act
        var result = await _controller.GetFavoriteProducts();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var actualFavorites = okResult.Value.Should().BeAssignableTo<List<UserFavoriteProductDto>>().Subject;
        actualFavorites.Should().HaveCount(2);
    }

    [Fact]
    public async Task AddFavoriteProduct_WithValidProductId_ReturnsCreatedAtAction()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var favoriteId = Guid.NewGuid();
        var favorite = new UserFavoriteProductDto
        {
            Id = favoriteId,
            UserId = _testUserId,
            ProductId = productId,
            CreatedAt = DateTime.UtcNow
        };

        // First call returns null (doesn't exist), second call returns the created favorite
        _mockRepository
            .SetupSequence(r => r.GetFavoriteProductAsync(_testUserId, productId))
            .ReturnsAsync((UserFavoriteProductDto?)null)
            .ReturnsAsync(favorite);

        _mockRepository
            .Setup(r => r.AddFavoriteProductAsync(_testUserId, productId, It.IsAny<string>(), _testUserId))
            .ReturnsAsync(favoriteId);

        // Act
        var result = await _controller.AddFavoriteProduct(productId, null);

        // Assert
        result.Should().NotBeNull();
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var actualFavorite = createdResult.Value.Should().BeAssignableTo<UserFavoriteProductDto>().Subject;
        actualFavorite.ProductId.Should().Be(productId);
    }

    [Fact]
    public async Task RemoveFavoriteProduct_WithExistingFavorite_ReturnsNoContent()
    {
        // Arrange
        var productId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.RemoveFavoriteProductAsync(_testUserId, productId))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.RemoveFavoriteProduct(productId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<NoContentResult>();
    }

    #endregion

    #region Product Ratings Tests

    [Fact]
    public async Task GetMyRatings_WithAuthenticatedUser_ReturnsOkWithRatings()
    {
        // Arrange
        var expectedRatings = new List<UserProductRatingDto>
        {
            new UserProductRatingDto { Id = Guid.NewGuid(), UserId = _testUserId, ProductId = Guid.NewGuid(), Rating = 4, ReviewText = "Good" },
            new UserProductRatingDto { Id = Guid.NewGuid(), UserId = _testUserId, ProductId = Guid.NewGuid(), Rating = 5, ReviewText = "Excellent" }
        };

        _mockRatingRepository
            .Setup(r => r.GetRatingsByUserIdAsync(_testUserId))
            .ReturnsAsync(expectedRatings);

        // Act
        var result = await _controller.GetMyRatings();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var actualRatings = okResult.Value.Should().BeAssignableTo<List<UserProductRatingDto>>().Subject;
        actualRatings.Should().HaveCount(2);
        actualRatings.Should().BeEquivalentTo(expectedRatings);
    }

    [Fact]
    public async Task GetProductRating_WithExistingRating_ReturnsOkWithRating()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var expectedRating = new UserProductRatingDto
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            ProductId = productId,
            Rating = 4,
            ReviewText = "Good product"
        };

        _mockRatingRepository
            .Setup(r => r.GetRatingAsync(_testUserId, productId))
            .ReturnsAsync(expectedRating);

        // Act
        var result = await _controller.GetProductRating(productId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var actualRating = okResult.Value.Should().BeAssignableTo<UserProductRatingDto>().Subject;
        actualRating.Should().BeEquivalentTo(expectedRating);
    }

    [Fact]
    public async Task GetProductRating_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        var productId = Guid.NewGuid();

        _mockRatingRepository
            .Setup(r => r.GetRatingAsync(_testUserId, productId))
            .ReturnsAsync((UserProductRatingDto?)null);

        // Act
        var result = await _controller.GetProductRating(productId);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task RateProduct_WithValidRequest_ReturnsOkWithRating()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var request = new CreateUserProductRatingRequest
        {
            ProductId = productId,
            Rating = 5,
            ReviewText = "Excellent product!"
        };

        var ratingId = Guid.NewGuid();
        var createdRating = new UserProductRatingDto
        {
            Id = ratingId,
            UserId = _testUserId,
            ProductId = productId,
            Rating = request.Rating,
            ReviewText = request.ReviewText,
            CreatedAt = DateTime.UtcNow
        };

        _mockRatingRepository
            .Setup(r => r.CreateOrUpdateRatingAsync(_testUserId, It.IsAny<CreateUserProductRatingRequest>(), _testUserId))
            .ReturnsAsync(ratingId);

        _mockRatingRepository
            .Setup(r => r.GetRatingAsync(_testUserId, productId))
            .ReturnsAsync(createdRating);

        // Act
        var result = await _controller.RateProduct(productId, request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var actualRating = okResult.Value.Should().BeAssignableTo<UserProductRatingDto>().Subject;
        actualRating.Rating.Should().Be(request.Rating);
        actualRating.ReviewText.Should().Be(request.ReviewText);
    }

    [Fact]
    public async Task RateProduct_UpdatesExistingRating_ReturnsOkWithUpdatedRating()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var existingRatingId = Guid.NewGuid();
        var request = new CreateUserProductRatingRequest
        {
            ProductId = productId,
            Rating = 5,
            ReviewText = "Updated review!"
        };

        var updatedRating = new UserProductRatingDto
        {
            Id = existingRatingId,
            UserId = _testUserId,
            ProductId = productId,
            Rating = request.Rating,
            ReviewText = request.ReviewText,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow
        };

        _mockRatingRepository
            .Setup(r => r.CreateOrUpdateRatingAsync(_testUserId, It.IsAny<CreateUserProductRatingRequest>(), _testUserId))
            .ReturnsAsync(existingRatingId);

        _mockRatingRepository
            .Setup(r => r.GetRatingAsync(_testUserId, productId))
            .ReturnsAsync(updatedRating);

        // Act
        var result = await _controller.RateProduct(productId, request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var actualRating = okResult.Value.Should().BeAssignableTo<UserProductRatingDto>().Subject;
        actualRating.Rating.Should().Be(request.Rating);
        actualRating.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteProductRating_WithExistingRating_ReturnsNoContent()
    {
        // Arrange
        var productId = Guid.NewGuid();

        _mockRatingRepository
            .Setup(r => r.DeleteRatingAsync(_testUserId, productId))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteProductRating(productId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteProductRating_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        var productId = Guid.NewGuid();

        _mockRatingRepository
            .Setup(r => r.DeleteRatingAsync(_testUserId, productId))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.DeleteProductRating(productId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetProductRatingStats_ReturnsOkWithStats()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var expectedStats = (averageRating: 4.5, totalRatings: 10);

        _mockRatingRepository
            .Setup(r => r.GetProductRatingStatsAsync(productId))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await _controller.GetProductRatingStats(productId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var actualStats = okResult.Value;
        
        // Use reflection to get the anonymous type properties
        var statsType = actualStats!.GetType();
        var averageRating = (double)statsType.GetProperty("averageRating")!.GetValue(actualStats)!;
        var totalRatings = (int)statsType.GetProperty("totalRatings")!.GetValue(actualStats)!;
        
        averageRating.Should().Be(4.5);
        totalRatings.Should().Be(10);
    }

    [Fact]
    public async Task GetProductRatingStats_WithNoRatings_ReturnsZeroStats()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var expectedStats = (averageRating: 0.0, totalRatings: 0);

        _mockRatingRepository
            .Setup(r => r.GetProductRatingStatsAsync(productId))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await _controller.GetProductRatingStats(productId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var actualStats = okResult.Value;
        
        var statsType = actualStats!.GetType();
        var averageRating = (double)statsType.GetProperty("averageRating")!.GetValue(actualStats)!;
        var totalRatings = (int)statsType.GetProperty("totalRatings")!.GetValue(actualStats)!;
        
        averageRating.Should().Be(0.0);
        totalRatings.Should().Be(0);
    }

    #endregion
}
