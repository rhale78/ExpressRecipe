using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ExpressRecipe.MealPlanningService.Controllers;
using ExpressRecipe.MealPlanningService.Data;
using ExpressRecipe.MealPlanningService.Tests.Helpers;

namespace ExpressRecipe.MealPlanningService.Tests.Controllers;

public class WorkQueueControllerTests
{
    private readonly Mock<IWorkQueueRepository> _mockRepo;
    private readonly WorkQueueController _controller;
    private readonly Guid _testUserId;
    private readonly Guid _testHouseholdId;

    public WorkQueueControllerTests()
    {
        _mockRepo       = new Mock<IWorkQueueRepository>();
        _controller     = new WorkQueueController(_mockRepo.Object);
        _testUserId     = Guid.NewGuid();
        _testHouseholdId = Guid.NewGuid();
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(
            _testUserId, _testHouseholdId);
    }

    #region GetQueue

    [Fact]
    public async Task GetQueue_ReturnsOkWithPendingItems()
    {
        List<WorkQueueItemDto> items = new()
        {
            new WorkQueueItemDto
            {
                Id          = Guid.NewGuid(),
                HouseholdId = _testHouseholdId,
                ItemType    = "RateRecipe",
                Priority    = WorkQueuePriority.RateRecipe,
                Title       = "Rate your meal",
                Status      = "Pending",
                CreatedAt   = DateTime.UtcNow
            }
        };

        _mockRepo
            .Setup(r => r.GetPendingItemsAsync(_testHouseholdId, _testUserId, 50, default))
            .ReturnsAsync(items);

        IActionResult result = await _controller.GetQueue(50, default);

        result.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)result).Value.Should().BeEquivalentTo(items);
        _mockRepo.Verify(r => r.GetPendingItemsAsync(_testHouseholdId, _testUserId, 50, default), Times.Once);
    }

    [Fact]
    public async Task GetQueue_WithCustomLimit_PassesLimitToRepository()
    {
        _mockRepo
            .Setup(r => r.GetPendingItemsAsync(_testHouseholdId, _testUserId, 10, default))
            .ReturnsAsync(new List<WorkQueueItemDto>());

        IActionResult result = await _controller.GetQueue(10, default);

        result.Should().BeOfType<OkObjectResult>();
        _mockRepo.Verify(r => r.GetPendingItemsAsync(_testHouseholdId, _testUserId, 10, default), Times.Once);
    }

    [Fact]
    public async Task GetQueue_WhenNoHouseholdIdClaim_ReturnsUnauthorized()
    {
        WorkQueueController controller = new(_mockRepo.Object);
        controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);

        IActionResult result = await controller.GetQueue(50, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetQueue_WhenNoUserClaim_ReturnsUnauthorized()
    {
        WorkQueueController controller = new(_mockRepo.Object);
        controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        IActionResult result = await controller.GetQueue(50, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    #endregion

    #region ActionItem

    [Fact]
    public async Task ActionItem_CallsRepositoryAndReturnsNoContent()
    {
        Guid itemId = Guid.NewGuid();
        ActionQueueItemRequest req = new() { ActionTaken = "AddedToShoppingList" };

        _mockRepo
            .Setup(r => r.ActionItemAsync(itemId, _testUserId, "AddedToShoppingList", null, default))
            .Returns(Task.CompletedTask);

        IActionResult result = await _controller.ActionItem(itemId, req, default);

        result.Should().BeOfType<NoContentResult>();
        _mockRepo.Verify(r => r.ActionItemAsync(itemId, _testUserId, "AddedToShoppingList", null, default), Times.Once);
    }

    [Fact]
    public async Task ActionItem_WithActionData_PassesActionDataToRepository()
    {
        Guid itemId = Guid.NewGuid();
        ActionQueueItemRequest req = new()
        {
            ActionTaken = "AddedToShoppingList",
            ActionData  = "{\"shoppingListId\":\"abc123\"}"
        };

        _mockRepo
            .Setup(r => r.ActionItemAsync(itemId, _testUserId, "AddedToShoppingList",
                "{\"shoppingListId\":\"abc123\"}", default))
            .Returns(Task.CompletedTask);

        IActionResult result = await _controller.ActionItem(itemId, req, default);

        result.Should().BeOfType<NoContentResult>();
        _mockRepo.Verify(r => r.ActionItemAsync(
            itemId, _testUserId, "AddedToShoppingList",
            "{\"shoppingListId\":\"abc123\"}", default), Times.Once);
    }

    [Fact]
    public async Task ActionItem_WhenNoUserClaim_ReturnsUnauthorized()
    {
        WorkQueueController controller = new(_mockRepo.Object);
        controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        IActionResult result = await controller.ActionItem(Guid.NewGuid(),
            new ActionQueueItemRequest { ActionTaken = "Rated" }, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    #endregion

    #region Dismiss

    [Fact]
    public async Task Dismiss_CallsRepositoryAndReturnsNoContent()
    {
        Guid itemId = Guid.NewGuid();

        _mockRepo
            .Setup(r => r.DismissItemAsync(itemId, _testUserId, default))
            .Returns(Task.CompletedTask);

        IActionResult result = await _controller.Dismiss(itemId, default);

        result.Should().BeOfType<NoContentResult>();
        _mockRepo.Verify(r => r.DismissItemAsync(itemId, _testUserId, default), Times.Once);
    }

    [Fact]
    public async Task Dismiss_WhenNoUserClaim_ReturnsUnauthorized()
    {
        WorkQueueController controller = new(_mockRepo.Object);
        controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        IActionResult result = await controller.Dismiss(Guid.NewGuid(), default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    #endregion

    #region Snooze

    [Fact]
    public async Task Snooze_WithPositiveHours_SnoozesByCorrectDuration()
    {
        Guid itemId = Guid.NewGuid();
        SnoozeRequest req = new() { Hours = 4 };

        _mockRepo
            .Setup(r => r.SnoozeItemAsync(itemId, _testUserId, It.IsAny<DateTime>(), default))
            .Returns(Task.CompletedTask);

        IActionResult result = await _controller.Snooze(itemId, req, default);

        result.Should().BeOfType<NoContentResult>();
        _mockRepo.Verify(r => r.SnoozeItemAsync(
            itemId, _testUserId, It.Is<DateTime>(d => d > DateTime.UtcNow), default), Times.Once);
    }

    [Fact]
    public async Task Snooze_WithZeroHours_SnoozesUntilTomorrow()
    {
        Guid itemId = Guid.NewGuid();
        SnoozeRequest req = new() { Hours = 0 };

        _mockRepo
            .Setup(r => r.SnoozeItemAsync(itemId, _testUserId, It.IsAny<DateTime>(), default))
            .Returns(Task.CompletedTask);

        DateTime beforeCall = DateTime.UtcNow.Date.AddDays(1);

        IActionResult result = await _controller.Snooze(itemId, req, default);

        result.Should().BeOfType<NoContentResult>();
        _mockRepo.Verify(r => r.SnoozeItemAsync(
            itemId, _testUserId, It.Is<DateTime>(d => d >= beforeCall), default), Times.Once);
    }

    [Fact]
    public async Task Snooze_WhenNoUserClaim_ReturnsUnauthorized()
    {
        WorkQueueController controller = new(_mockRepo.Object);
        controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        IActionResult result = await controller.Snooze(Guid.NewGuid(), new SnoozeRequest(), default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    #endregion
}
