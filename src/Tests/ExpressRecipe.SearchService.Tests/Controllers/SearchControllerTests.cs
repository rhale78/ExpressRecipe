using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ExpressRecipe.SearchService.Controllers;
using ExpressRecipe.SearchService.Data;
using ExpressRecipe.SearchService.Tests.Helpers;

namespace ExpressRecipe.SearchService.Tests.Controllers;

public class SearchControllerTests
{
    private readonly Mock<ISearchRepository> _mockRepository;
    private readonly Mock<ILogger<SearchController>> _mockLogger;
    private readonly SearchController _controller;
    private readonly Guid _testUserId;

    public SearchControllerTests()
    {
        _mockRepository = new Mock<ISearchRepository>();
        _mockLogger = new Mock<ILogger<SearchController>>();
        _controller = new SearchController(_mockLogger.Object, _mockRepository.Object);
        _testUserId = Guid.NewGuid();
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);
    }

    #region Search Tests

    [Fact]
    public async Task Search_WhenAuthenticated_ReturnsSearchResults()
    {
        // Arrange
        var searchResult = new SearchResultDto
        {
            Query = "pasta",
            TotalResults = 2,
            Items = new List<SearchItemDto>
            {
                new SearchItemDto { EntityType = "Recipe", Title = "Pasta Carbonara" },
                new SearchItemDto { EntityType = "Recipe", Title = "Pasta Bolognese" }
            }
        };

        _mockRepository
            .Setup(r => r.SearchAsync("pasta", null, null, null, 50, 0))
            .ReturnsAsync(searchResult);
        _mockRepository
            .Setup(r => r.RecordSearchAsync(_testUserId, "pasta", null, 2, true))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        var result = await _controller.Search("pasta", null, null, 50, 0);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRepository.Verify(r => r.SearchAsync("pasta", null, null, null, 50, 0), Times.Once);
        _mockRepository.Verify(r => r.RecordSearchAsync(_testUserId, "pasta", null, 2, true), Times.Once);
    }

    [Fact]
    public async Task Search_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        // Act
        var result = await _controller.Search("pasta", null, null, 50, 0);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Search_WithEntityTypeFilter_PassesFilterToRepository()
    {
        // Arrange
        var searchResult = new SearchResultDto { Query = "chicken", TotalResults = 1, Items = new List<SearchItemDto>() };
        _mockRepository
            .Setup(r => r.SearchAsync("chicken", "Recipe", null, null, 50, 0))
            .ReturnsAsync(searchResult);
        _mockRepository
            .Setup(r => r.RecordSearchAsync(_testUserId, "chicken", "Recipe", 1, true))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        var result = await _controller.Search("chicken", "Recipe", null, 50, 0);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRepository.Verify(r => r.SearchAsync("chicken", "Recipe", null, null, 50, 0), Times.Once);
    }

    #endregion

    #region GetSuggestions Tests

    [Fact]
    public async Task GetSuggestions_WithPartialQuery_ReturnsSuggestions()
    {
        // Arrange
        var suggestions = new List<SearchSuggestionDto>
        {
            new SearchSuggestionDto { Suggestion = "pasta carbonara", Frequency = 42 },
            new SearchSuggestionDto { Suggestion = "pasta bolognese", Frequency = 38 }
        };

        _mockRepository
            .Setup(r => r.GetSuggestionsAsync("pas", 10))
            .ReturnsAsync(suggestions);

        // Act
        var result = await _controller.GetSuggestions("pas", 10);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        (okResult!.Value as List<SearchSuggestionDto>)!.Should().HaveCount(2);
    }

    #endregion

    #region GetHistory Tests

    [Fact]
    public async Task GetHistory_WhenAuthenticated_ReturnsSearchHistory()
    {
        // Arrange
        var history = new List<SearchHistoryDto>
        {
            new SearchHistoryDto { UserId = _testUserId, Query = "pasta", SearchedAt = DateTime.UtcNow.AddHours(-1) },
            new SearchHistoryDto { UserId = _testUserId, Query = "pizza", SearchedAt = DateTime.UtcNow.AddHours(-2) }
        };

        _mockRepository
            .Setup(r => r.GetUserSearchHistoryAsync(_testUserId, 20))
            .ReturnsAsync(history);

        // Act
        var result = await _controller.GetHistory(20);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        (okResult!.Value as List<SearchHistoryDto>)!.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetHistory_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        // Act
        var result = await _controller.GetHistory(20);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    #endregion

    #region ClearHistory Tests

    [Fact]
    public async Task ClearHistory_WhenAuthenticated_ReturnsNoContent()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.ClearUserSearchHistoryAsync(_testUserId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.ClearHistory();

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _mockRepository.Verify(r => r.ClearUserSearchHistoryAsync(_testUserId), Times.Once);
    }

    [Fact]
    public async Task ClearHistory_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        // Act
        var result = await _controller.ClearHistory();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    #endregion
}
