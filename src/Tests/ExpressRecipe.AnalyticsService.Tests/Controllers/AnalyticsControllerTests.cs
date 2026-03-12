using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ExpressRecipe.AnalyticsService.Controllers;
using ExpressRecipe.AnalyticsService.Data;
using ExpressRecipe.AnalyticsService.Tests.Helpers;

namespace ExpressRecipe.AnalyticsService.Tests.Controllers;

public class AnalyticsControllerTests
{
    private readonly Mock<IAnalyticsRepository> _mockRepository;
    private readonly Mock<ILogger<AnalyticsController>> _mockLogger;
    private readonly AnalyticsController _controller;
    private readonly Guid _testUserId;

    public AnalyticsControllerTests()
    {
        _mockRepository = new Mock<IAnalyticsRepository>();
        _mockLogger = new Mock<ILogger<AnalyticsController>>();
        _controller = new AnalyticsController(_mockLogger.Object, _mockRepository.Object);
        _testUserId = Guid.NewGuid();
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);
    }

    #region TrackEvent Tests

    [Fact]
    public async Task TrackEvent_WhenAuthenticated_ReturnsOkWithEventId()
    {
        // Arrange
        var request = new TrackEventRequest
        {
            EventType = "UserAction",
            EventName = "RecipeViewed",
            Properties = new Dictionary<string, string> { ["recipeId"] = Guid.NewGuid().ToString() }
        };
        var eventId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.TrackEventAsync(_testUserId, request.EventType, request.EventName, request.Properties, request.SessionId, request.DeviceId))
            .ReturnsAsync(eventId);

        // Act
        var result = await _controller.TrackEvent(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRepository.Verify(r => r.TrackEventAsync(_testUserId, request.EventType, request.EventName, request.Properties, request.SessionId, request.DeviceId), Times.Once);
    }

    [Fact]
    public async Task TrackEvent_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();
        var request = new TrackEventRequest { EventType = "UserAction", EventName = "Test" };

        // Act
        var result = await _controller.TrackEvent(request);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
        _mockRepository.Verify(r => r.TrackEventAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    #endregion

    #region GetEvents Tests

    [Fact]
    public async Task GetEvents_WhenAuthenticated_ReturnsUserEvents()
    {
        // Arrange
        var events = new List<UserEventDto>
        {
            new UserEventDto { Id = Guid.NewGuid(), UserId = _testUserId, EventType = "UserAction", EventName = "RecipeViewed" },
            new UserEventDto { Id = Guid.NewGuid(), UserId = _testUserId, EventType = "UserAction", EventName = "ProductScanned" }
        };

        _mockRepository
            .Setup(r => r.GetUserEventsAsync(_testUserId, null, null, 100))
            .ReturnsAsync(events);

        // Act
        var result = await _controller.GetEvents(null, null, 100);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(events);
    }

    [Fact]
    public async Task GetEvents_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        // Act
        var result = await _controller.GetEvents(null, null, 100);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetEvents_WithDateRange_PassesParametersToRepository()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;

        _mockRepository
            .Setup(r => r.GetUserEventsAsync(_testUserId, startDate, endDate, 50))
            .ReturnsAsync(new List<UserEventDto>());

        // Act
        var result = await _controller.GetEvents(startDate, endDate, 50);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRepository.Verify(r => r.GetUserEventsAsync(_testUserId, startDate, endDate, 50), Times.Once);
    }

    #endregion

    #region GetUsageStats Tests

    [Fact]
    public async Task GetUsageStats_WhenAuthenticated_ReturnsStats()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;
        var stats = new List<UsageStatisticsDto>
        {
            new UsageStatisticsDto { UserId = _testUserId, SessionCount = 5, ActionCount = 42 }
        };

        _mockRepository
            .Setup(r => r.GetUserUsageStatsAsync(_testUserId, startDate, endDate))
            .ReturnsAsync(stats);

        // Act
        var result = await _controller.GetUsageStats(startDate, endDate);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(stats);
    }

    [Fact]
    public async Task GetUsageStats_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        // Act
        var result = await _controller.GetUsageStats(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    #endregion

    #region GetInsights Tests

    [Fact]
    public async Task GetInsights_WhenAuthenticated_ReturnsInsights()
    {
        // Arrange
        var insights = new List<InsightDto>
        {
            new InsightDto { Id = Guid.NewGuid(), UserId = _testUserId, Title = "You scan more on weekends", IsRead = false }
        };

        _mockRepository
            .Setup(r => r.GetUserInsightsAsync(_testUserId, false))
            .ReturnsAsync(insights);

        // Act
        var result = await _controller.GetInsights(false);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(insights);
    }

    [Fact]
    public async Task GetInsights_UnreadOnlyFilter_PassesCorrectParameter()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetUserInsightsAsync(_testUserId, true))
            .ReturnsAsync(new List<InsightDto>());

        // Act
        var result = await _controller.GetInsights(true);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRepository.Verify(r => r.GetUserInsightsAsync(_testUserId, true), Times.Once);
    }

    #endregion
}
