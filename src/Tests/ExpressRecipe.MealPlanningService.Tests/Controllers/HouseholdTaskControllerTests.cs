using FluentAssertions;
using Moq;
using Microsoft.AspNetCore.Mvc;
using ExpressRecipe.MealPlanningService.Controllers;
using ExpressRecipe.MealPlanningService.Data;
using ExpressRecipe.MealPlanningService.Tests.Helpers;

namespace ExpressRecipe.MealPlanningService.Tests.Controllers;

public class HouseholdTaskControllerTests
{
    private readonly Mock<IHouseholdTaskRepository> _mockTasks;
    private readonly HouseholdTaskController _controller;
    private readonly Guid _userId      = Guid.NewGuid();
    private readonly Guid _householdId = Guid.NewGuid();

    public HouseholdTaskControllerTests()
    {
        _mockTasks  = new Mock<IHouseholdTaskRepository>();
        _controller = new HouseholdTaskController(_mockTasks.Object);
        _controller.ControllerContext =
            ControllerTestHelpers.CreateAuthenticatedContext(_userId, _householdId);
    }

    private static HouseholdTaskDto MakeTask(string status = "Pending") => new()
    {
        Id                = Guid.NewGuid(),
        HouseholdId       = Guid.NewGuid(),
        TaskType          = "ThawReminder",
        Title             = "Move to fridge: Chicken",
        DueAt             = DateTime.UtcNow.AddHours(12),
        Status            = status,
        EscalateAfterMins = 120,
        CreatedAt         = DateTime.UtcNow
    };

    [Fact]
    public async Task GetActive_ReturnsOkWithActiveTasks()
    {
        // Arrange
        List<HouseholdTaskDto> tasks = [MakeTask(), MakeTask("Escalated")];
        _mockTasks.Setup(r => r.GetActiveTasksAsync(_householdId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(tasks);

        // Act
        IActionResult result = await _controller.GetActive(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        OkObjectResult ok = (OkObjectResult)result;
        ok.Value.Should().BeEquivalentTo(tasks);
        _mockTasks.Verify(r => r.GetActiveTasksAsync(_householdId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetHistory_DefaultRange_CallsRepositoryWithLast30Days()
    {
        // Arrange
        _mockTasks.Setup(r => r.GetTaskHistoryAsync(
                    _householdId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                    It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new List<HouseholdTaskDto>());

        // Act
        IActionResult result = await _controller.GetHistory(null, null, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockTasks.Verify(r => r.GetTaskHistoryAsync(
            _householdId,
            It.Is<DateOnly>(d => d <= DateOnly.FromDateTime(DateTime.Today.AddDays(-29))),
            It.Is<DateOnly>(d => d == DateOnly.FromDateTime(DateTime.Today)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TakeAction_WithValidAction_ReturnsNoContent()
    {
        // Arrange
        Guid taskId = Guid.NewGuid();
        TaskActionRequest req = new() { ActionTaken = "Moved" };
        _mockTasks.Setup(r => r.ActionTaskAsync(taskId, _householdId, _userId, "Moved", It.IsAny<CancellationToken>()))
                  .ReturnsAsync(true);

        // Act
        IActionResult result = await _controller.TakeAction(taskId, req, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _mockTasks.Verify(r => r.ActionTaskAsync(
            taskId, _householdId, _userId, "Moved", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TakeAction_WhenTaskNotFound_ReturnsNotFound()
    {
        // Arrange
        TaskActionRequest req = new() { ActionTaken = "Moved" };
        _mockTasks.Setup(r => r.ActionTaskAsync(
                    It.IsAny<Guid>(), _householdId, _userId, "Moved", It.IsAny<CancellationToken>()))
                  .ReturnsAsync(false);

        // Act
        IActionResult result = await _controller.TakeAction(Guid.NewGuid(), req, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task TakeAction_WithInvalidAction_ReturnsBadRequest()
    {
        // Arrange
        TaskActionRequest req = new() { ActionTaken = "InvalidAction" };

        // Act
        IActionResult result = await _controller.TakeAction(Guid.NewGuid(), req, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        _mockTasks.Verify(r => r.ActionTaskAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("Moved")]
    [InlineData("AlreadyMoved")]
    [InlineData("Ignored")]
    public async Task TakeAction_WithEachValidAction_ReturnsNoContent(string action)
    {
        // Arrange
        Guid taskId = Guid.NewGuid();
        TaskActionRequest req = new() { ActionTaken = action };
        _mockTasks.Setup(r => r.ActionTaskAsync(
                    taskId, _householdId, _userId, action, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(true);

        // Act
        IActionResult result = await _controller.TakeAction(taskId, req, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Dismiss_WhenTaskExists_ReturnsNoContent()
    {
        // Arrange
        Guid taskId = Guid.NewGuid();
        _mockTasks.Setup(r => r.DismissTaskAsync(taskId, _householdId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(true);

        // Act
        IActionResult result = await _controller.Dismiss(taskId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _mockTasks.Verify(r => r.DismissTaskAsync(taskId, _householdId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Dismiss_WhenTaskNotFound_ReturnsNotFound()
    {
        // Arrange
        _mockTasks.Setup(r => r.DismissTaskAsync(It.IsAny<Guid>(), _householdId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(false);

        // Act
        IActionResult result = await _controller.Dismiss(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }
}
