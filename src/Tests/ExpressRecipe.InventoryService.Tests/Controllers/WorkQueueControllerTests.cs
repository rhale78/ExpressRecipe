using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ExpressRecipe.InventoryService.Controllers;
using ExpressRecipe.InventoryService.Data;
using ExpressRecipe.InventoryService.Tests.Helpers;

namespace ExpressRecipe.InventoryService.Tests.Controllers;

public class WorkQueueControllerTests
{
    private readonly Mock<IWorkQueueRepository> _mockRepo;
    private readonly Mock<ILogger<WorkQueueController>> _mockLogger;
    private readonly WorkQueueController _controller;
    private readonly Guid _userId;
    private readonly Guid _householdId;

    public WorkQueueControllerTests()
    {
        _mockRepo   = new Mock<IWorkQueueRepository>();
        _mockLogger = new Mock<ILogger<WorkQueueController>>();
        _controller = new WorkQueueController(_mockRepo.Object, _mockLogger.Object);
        _userId      = Guid.NewGuid();
        _householdId = Guid.NewGuid();
        _controller.ControllerContext =
            ControllerTestHelpers.CreateAuthenticatedContext(_userId, _householdId);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static WorkQueueItemDto MakeItem(int priority = 5) => new WorkQueueItemDto
    {
        Id          = Guid.NewGuid(),
        UserId      = Guid.NewGuid(),
        HouseholdId = Guid.NewGuid(),
        ItemType    = "ExpiringSoon",
        Title       = "Use by tomorrow",
        Priority    = priority,
        Status      = "Pending",
        CreatedAt   = DateTime.UtcNow
    };

    // ── GetItems ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetItems_MissingUserClaim_ReturnsUnauthorized()
    {
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        IActionResult result = await _controller.GetItems(default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetItems_MissingHouseholdClaim_ReturnsUnauthorized()
    {
        // Context with user only, no household_id
        _controller.ControllerContext =
            ControllerTestHelpers.CreateAuthenticatedContext(_userId);

        IActionResult result = await _controller.GetItems(default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetItems_ReturnsOkWithItems()
    {
        var items = new List<WorkQueueItemDto> { MakeItem(), MakeItem() };

        _mockRepo
            .Setup(r => r.WakeSnoozedItemsAsync(_userId, default))
            .Returns(Task.CompletedTask);
        _mockRepo
            .Setup(r => r.GetPendingItemsAsync(_userId, _householdId, default))
            .ReturnsAsync(items);

        IActionResult result = await _controller.GetItems(default);

        result.Should().BeOfType<OkObjectResult>();
        var ok = result as OkObjectResult;
        ok!.Value.Should().BeEquivalentTo(items);
    }

    [Fact]
    public async Task GetItems_CallsWakeSnoozedFirst()
    {
        var callOrder = new List<string>();

        _mockRepo
            .Setup(r => r.WakeSnoozedItemsAsync(_userId, default))
            .Callback(() => callOrder.Add("wake"))
            .Returns(Task.CompletedTask);
        _mockRepo
            .Setup(r => r.GetPendingItemsAsync(_userId, _householdId, default))
            .Callback(() => callOrder.Add("get"))
            .ReturnsAsync(new List<WorkQueueItemDto>());

        await _controller.GetItems(default);

        callOrder.Should().Equal(["wake", "get"]);
    }

    // ── GetCount ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCount_MissingUserClaim_ReturnsUnauthorized()
    {
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        IActionResult result = await _controller.GetCount(default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetCount_MissingHouseholdClaim_ReturnsUnauthorized()
    {
        _controller.ControllerContext =
            ControllerTestHelpers.CreateAuthenticatedContext(_userId);

        IActionResult result = await _controller.GetCount(default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetCount_ReturnsCountAndHasCritical_WhenCriticalItems()
    {
        var items = new List<WorkQueueItemDto>
        {
            MakeItem(priority: 2),  // critical
            MakeItem(priority: 5)   // medium
        };

        _mockRepo
            .Setup(r => r.GetPendingItemsAsync(_userId, _householdId, default))
            .ReturnsAsync(items);

        IActionResult result = await _controller.GetCount(default);

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        // Anonymous type — access via reflection/dynamic
        dynamic value = ok.Value!;
        ((int)value.count).Should().Be(2);
        ((bool)value.hasCritical).Should().BeTrue();
    }

    [Fact]
    public async Task GetCount_HasCriticalFalse_WhenNoCriticalItems()
    {
        var items = new List<WorkQueueItemDto>
        {
            MakeItem(priority: 5),
            MakeItem(priority: 7)
        };

        _mockRepo
            .Setup(r => r.GetPendingItemsAsync(_userId, _householdId, default))
            .ReturnsAsync(items);

        IActionResult result = await _controller.GetCount(default);

        result.Should().BeOfType<OkObjectResult>();
        dynamic value = ((OkObjectResult)result).Value!;
        ((bool)value.hasCritical).Should().BeFalse();
    }

    // ── ActionItem ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ActionItem_MissingUserClaim_ReturnsUnauthorized()
    {
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        IActionResult result = await _controller.ActionItem(
            Guid.NewGuid(), new WorkQueueActionRequest { ActionTaken = "Done" }, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task ActionItem_NotFound_ReturnsNotFound()
    {
        Guid itemId = Guid.NewGuid();
        _mockRepo
            .Setup(r => r.ActionItemAsync(itemId, _userId, "Done", null, default))
            .ReturnsAsync(false);

        IActionResult result = await _controller.ActionItem(
            itemId, new WorkQueueActionRequest { ActionTaken = "Done" }, default);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ActionItem_Success_ReturnsNoContent()
    {
        Guid itemId = Guid.NewGuid();
        _mockRepo
            .Setup(r => r.ActionItemAsync(itemId, _userId, "Done", null, default))
            .ReturnsAsync(true);

        IActionResult result = await _controller.ActionItem(
            itemId, new WorkQueueActionRequest { ActionTaken = "Done" }, default);

        result.Should().BeOfType<NoContentResult>();
    }

    // ── DismissItem ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DismissItem_MissingUserClaim_ReturnsUnauthorized()
    {
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        IActionResult result = await _controller.DismissItem(Guid.NewGuid(), default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task DismissItem_NotFound_ReturnsNotFound()
    {
        Guid itemId = Guid.NewGuid();
        _mockRepo.Setup(r => r.DismissItemAsync(itemId, _userId, default)).ReturnsAsync(false);

        IActionResult result = await _controller.DismissItem(itemId, default);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DismissItem_Success_ReturnsNoContent()
    {
        Guid itemId = Guid.NewGuid();
        _mockRepo.Setup(r => r.DismissItemAsync(itemId, _userId, default)).ReturnsAsync(true);

        IActionResult result = await _controller.DismissItem(itemId, default);

        result.Should().BeOfType<NoContentResult>();
    }

    // ── SnoozeItem ────────────────────────────────────────────────────────────

    [Fact]
    public async Task SnoozeItem_MissingUserClaim_ReturnsUnauthorized()
    {
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        IActionResult result = await _controller.SnoozeItem(
            Guid.NewGuid(), new WorkQueueSnoozeRequest { Hours = 24 }, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task SnoozeItem_HoursBelowMinimum_ReturnsBadRequest()
    {
        IActionResult result = await _controller.SnoozeItem(
            Guid.NewGuid(), new WorkQueueSnoozeRequest { Hours = 0 }, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SnoozeItem_HoursAboveMaximum_ReturnsBadRequest()
    {
        IActionResult result = await _controller.SnoozeItem(
            Guid.NewGuid(), new WorkQueueSnoozeRequest { Hours = 200 }, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SnoozeItem_NotFound_ReturnsNotFound()
    {
        Guid itemId = Guid.NewGuid();
        _mockRepo
            .Setup(r => r.SnoozeItemAsync(itemId, _userId, 24, default))
            .ReturnsAsync(false);

        IActionResult result = await _controller.SnoozeItem(
            itemId, new WorkQueueSnoozeRequest { Hours = 24 }, default);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task SnoozeItem_NullRequest_UsesDefault24Hours()
    {
        Guid itemId = Guid.NewGuid();
        _mockRepo
            .Setup(r => r.SnoozeItemAsync(itemId, _userId, 24, default))
            .ReturnsAsync(true);

        IActionResult result = await _controller.SnoozeItem(itemId, null, default);

        result.Should().BeOfType<NoContentResult>();
        _mockRepo.Verify(r => r.SnoozeItemAsync(itemId, _userId, 24, default), Times.Once);
    }

    [Fact]
    public async Task SnoozeItem_Success_ReturnsNoContent()
    {
        Guid itemId = Guid.NewGuid();
        _mockRepo
            .Setup(r => r.SnoozeItemAsync(itemId, _userId, 48, default))
            .ReturnsAsync(true);

        IActionResult result = await _controller.SnoozeItem(
            itemId, new WorkQueueSnoozeRequest { Hours = 48 }, default);

        result.Should().BeOfType<NoContentResult>();
    }
}
