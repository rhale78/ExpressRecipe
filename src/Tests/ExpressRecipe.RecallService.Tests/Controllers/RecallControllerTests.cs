using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ExpressRecipe.RecallService.Controllers;
using ExpressRecipe.RecallService.Data;
using ExpressRecipe.RecallService.Tests.Helpers;

namespace ExpressRecipe.RecallService.Tests.Controllers;

public class RecallControllerTests
{
    private readonly Mock<IRecallRepository> _mockRepository;
    private readonly Mock<ILogger<RecallController>> _mockLogger;
    private readonly RecallController _controller;
    private readonly Guid _testUserId;

    public RecallControllerTests()
    {
        _mockRepository = new Mock<IRecallRepository>();
        _mockLogger = new Mock<ILogger<RecallController>>();
        _controller = new RecallController(_mockLogger.Object, _mockRepository.Object);
        _testUserId = Guid.NewGuid();
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);
    }

    #region GetRecentRecalls Tests

    [Fact]
    public async Task GetRecentRecalls_ReturnsListOfRecalls()
    {
        // Arrange
        var recalls = new List<RecallDto>
        {
            new RecallDto { Id = Guid.NewGuid(), Title = "Peanut Allergy Alert", Severity = "High" },
            new RecallDto { Id = Guid.NewGuid(), Title = "Listeria Risk", Severity = "Critical" }
        };

        _mockRepository
            .Setup(r => r.GetRecentRecallsAsync(100))
            .ReturnsAsync(recalls);

        // Act
        var result = await _controller.GetRecentRecalls(100);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        (okResult!.Value as List<RecallDto>)!.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetRecentRecalls_UsesDefaultLimit()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetRecentRecallsAsync(100))
            .ReturnsAsync(new List<RecallDto>());

        // Act
        await _controller.GetRecentRecalls();

        // Assert
        _mockRepository.Verify(r => r.GetRecentRecallsAsync(100), Times.Once);
    }

    #endregion

    #region GetRecall Tests

    [Fact]
    public async Task GetRecall_WithExistingId_ReturnsRecall()
    {
        // Arrange
        var recallId = Guid.NewGuid();
        var recall = new RecallDto { Id = recallId, Title = "Salmonella Recall", Severity = "High" };

        _mockRepository
            .Setup(r => r.GetRecallAsync(recallId))
            .ReturnsAsync(recall);

        // Act
        var result = await _controller.GetRecall(recallId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        (okResult!.Value as RecallDto)!.Id.Should().Be(recallId);
    }

    [Fact]
    public async Task GetRecall_WithNonExistingId_ReturnsNotFound()
    {
        // Arrange
        var recallId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.GetRecallAsync(recallId))
            .ReturnsAsync((RecallDto?)null);

        // Act
        var result = await _controller.GetRecall(recallId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region GetUserAlerts Tests

    [Fact]
    public async Task GetUserAlerts_WhenAuthenticated_ReturnsUserAlerts()
    {
        // Arrange
        var alerts = new List<RecallAlertDto>
        {
            new RecallAlertDto { Id = Guid.NewGuid(), UserId = _testUserId, IsAcknowledged = false },
            new RecallAlertDto { Id = Guid.NewGuid(), UserId = _testUserId, IsAcknowledged = false }
        };

        _mockRepository
            .Setup(r => r.GetUserAlertsAsync(_testUserId, true))
            .ReturnsAsync(alerts);

        // Act
        var result = await _controller.GetUserAlerts(true);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        (okResult!.Value as List<RecallAlertDto>)!.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetUserAlerts_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        // Act
        var result = await _controller.GetUserAlerts(true);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    #endregion

    #region SearchRecalls Tests

    [Fact]
    public async Task SearchRecalls_WithSearchTerm_ReturnsMatchingRecalls()
    {
        // Arrange
        var recalls = new List<RecallDto>
        {
            new RecallDto { Id = Guid.NewGuid(), Title = "Peanut Butter Recall", Severity = "High" }
        };

        _mockRepository
            .Setup(r => r.SearchRecallsAsync("peanut", null, null, null))
            .ReturnsAsync(recalls);

        // Act
        var result = await _controller.SearchRecalls("peanut", null, null, null);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRepository.Verify(r => r.SearchRecallsAsync("peanut", null, null, null), Times.Once);
    }

    [Fact]
    public async Task SearchRecalls_WithSeverityFilter_PassesCorrectParameters()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.SearchRecallsAsync("", "Critical", null, null))
            .ReturnsAsync(new List<RecallDto>());

        // Act
        var result = await _controller.SearchRecalls(null, "Critical", null, null);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRepository.Verify(r => r.SearchRecallsAsync("", "Critical", null, null), Times.Once);
    }

    #endregion

    #region AcknowledgeAlert Tests

    [Fact]
    public async Task AcknowledgeAlert_WithValidId_ReturnsNoContent()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.AcknowledgeAlertAsync(alertId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.AcknowledgeAlert(alertId);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _mockRepository.Verify(r => r.AcknowledgeAlertAsync(alertId), Times.Once);
    }

    #endregion
}
