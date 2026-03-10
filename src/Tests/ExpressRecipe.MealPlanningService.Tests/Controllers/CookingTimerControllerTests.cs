using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ExpressRecipe.MealPlanningService.Controllers;
using ExpressRecipe.MealPlanningService.Data;
using ExpressRecipe.MealPlanningService.Tests.Helpers;

namespace ExpressRecipe.MealPlanningService.Tests.Controllers;

public class CookingTimerControllerTests
{
    private readonly Mock<ICookingTimerRepository> _mockRepository;
    private readonly CookingTimerController _controller;
    private readonly Guid _testUserId;
    private readonly Guid _testHouseholdId;

    public CookingTimerControllerTests()
    {
        _mockRepository = new Mock<ICookingTimerRepository>();
        _controller = new CookingTimerController(_mockRepository.Object);
        _testUserId = Guid.NewGuid();
        _testHouseholdId = Guid.NewGuid();
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId, _testHouseholdId);
    }

    #region GetActive

    [Fact]
    public async Task GetActive_ReturnsOkWithTimers()
    {
        List<CookingTimerDto> timers = new()
        {
            TestDataFactory.CreateCookingTimerDto(userId: _testUserId, status: "Running"),
            TestDataFactory.CreateCookingTimerDto(userId: _testUserId, status: "Preset")
        };

        _mockRepository
            .Setup(r => r.GetActiveTimersAsync(_testUserId, default))
            .ReturnsAsync(timers);

        IActionResult result = await _controller.GetActive(default);

        result.Should().BeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        okResult.Value.Should().BeEquivalentTo(timers);
        _mockRepository.Verify(r => r.GetActiveTimersAsync(_testUserId, default), Times.Once);
    }

    [Fact]
    public async Task GetActive_WhenNoTimers_ReturnsEmptyList()
    {
        _mockRepository
            .Setup(r => r.GetActiveTimersAsync(_testUserId, default))
            .ReturnsAsync(new List<CookingTimerDto>());

        IActionResult result = await _controller.GetActive(default);

        result.Should().BeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        okResult.Value.Should().BeEquivalentTo(new List<CookingTimerDto>());
    }

    #endregion

    #region GetById

    [Fact]
    public async Task GetById_ExistingTimer_OwnedByUser_ReturnsOk()
    {
        Guid timerId = Guid.NewGuid();
        CookingTimerDto timer = TestDataFactory.CreateCookingTimerDto(id: timerId, userId: _testUserId);

        _mockRepository
            .Setup(r => r.GetByIdAsync(timerId, default))
            .ReturnsAsync(timer);

        IActionResult result = await _controller.GetById(timerId, default);

        result.Should().BeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        okResult.Value.Should().BeEquivalentTo(timer);
    }

    [Fact]
    public async Task GetById_TimerOwnedByOtherUser_ReturnsForbid()
    {
        Guid timerId = Guid.NewGuid();
        CookingTimerDto timer = TestDataFactory.CreateCookingTimerDto(id: timerId, userId: Guid.NewGuid());

        _mockRepository
            .Setup(r => r.GetByIdAsync(timerId, default))
            .ReturnsAsync(timer);

        IActionResult result = await _controller.GetById(timerId, default);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetById_NonExistingTimer_ReturnsNotFound()
    {
        _mockRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((CookingTimerDto?)null);

        IActionResult result = await _controller.GetById(Guid.NewGuid(), default);

        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region Create

    [Fact]
    public async Task Create_WithValidRequest_ReturnsOkWithId()
    {
        Guid newId = Guid.NewGuid();
        CreateTimerRequest req = new()
        {
            Label = "Roast chicken",
            DurationSeconds = 3600,
            StartImmediately = false
        };

        _mockRepository
            .Setup(r => r.CreateTimerAsync(_testUserId, _testHouseholdId, req.Label,
                req.DurationSeconds, null, null, false, default))
            .ReturnsAsync(newId);

        IActionResult result = await _controller.Create(req, default);

        result.Should().BeOfType<OkObjectResult>();
        _mockRepository.Verify(r => r.CreateTimerAsync(_testUserId, _testHouseholdId, req.Label,
            req.DurationSeconds, null, null, false, default), Times.Once);
    }

    [Fact]
    public async Task Create_WithStartImmediately_StartsTimer()
    {
        Guid newId = Guid.NewGuid();
        CreateTimerRequest req = new()
        {
            Label = "Boil eggs",
            DurationSeconds = 360,
            StartImmediately = true
        };

        _mockRepository
            .Setup(r => r.CreateTimerAsync(_testUserId, _testHouseholdId, req.Label,
                req.DurationSeconds, null, null, true, default))
            .ReturnsAsync(newId);

        IActionResult result = await _controller.Create(req, default);

        result.Should().BeOfType<OkObjectResult>();
        _mockRepository.Verify(r => r.CreateTimerAsync(_testUserId, _testHouseholdId, req.Label,
            req.DurationSeconds, null, null, true, default), Times.Once);
    }

    [Fact]
    public async Task Create_WithZeroDuration_ReturnsBadRequest()
    {
        CreateTimerRequest req = new() { Label = "Invalid", DurationSeconds = 0 };

        IActionResult result = await _controller.Create(req, default);

        result.Should().BeOfType<BadRequestObjectResult>();
        _mockRepository.Verify(r => r.CreateTimerAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
            It.IsAny<bool>(), default), Times.Never);
    }

    [Fact]
    public async Task Create_WithNegativeDuration_ReturnsBadRequest()
    {
        CreateTimerRequest req = new() { Label = "Invalid", DurationSeconds = -1 };

        IActionResult result = await _controller.Create(req, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region State transitions

    [Fact]
    public async Task Start_OwnedByUser_CallsRepositoryAndReturnsNoContent()
    {
        Guid timerId = Guid.NewGuid();
        CookingTimerDto timer = TestDataFactory.CreateCookingTimerDto(id: timerId, userId: _testUserId, status: "Preset");
        _mockRepository.Setup(r => r.GetByIdAsync(timerId, default)).ReturnsAsync(timer);
        _mockRepository.Setup(r => r.StartTimerAsync(timerId, default)).Returns(Task.CompletedTask);

        IActionResult result = await _controller.Start(timerId, default);

        result.Should().BeOfType<NoContentResult>();
        _mockRepository.Verify(r => r.StartTimerAsync(timerId, default), Times.Once);
    }

    [Fact]
    public async Task Start_OwnedByOtherUser_ReturnsForbid()
    {
        Guid timerId = Guid.NewGuid();
        CookingTimerDto timer = TestDataFactory.CreateCookingTimerDto(id: timerId, userId: Guid.NewGuid());
        _mockRepository.Setup(r => r.GetByIdAsync(timerId, default)).ReturnsAsync(timer);

        IActionResult result = await _controller.Start(timerId, default);

        result.Should().BeOfType<ForbidResult>();
        _mockRepository.Verify(r => r.StartTimerAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task Start_NonExistingTimer_ReturnsNotFound()
    {
        _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((CookingTimerDto?)null);

        IActionResult result = await _controller.Start(Guid.NewGuid(), default);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Pause_OwnedByUser_CallsRepositoryAndReturnsNoContent()
    {
        Guid timerId = Guid.NewGuid();
        CookingTimerDto timer = TestDataFactory.CreateCookingTimerDto(id: timerId, userId: _testUserId, status: "Running");
        _mockRepository.Setup(r => r.GetByIdAsync(timerId, default)).ReturnsAsync(timer);
        _mockRepository.Setup(r => r.PauseTimerAsync(timerId, default)).Returns(Task.CompletedTask);

        IActionResult result = await _controller.Pause(timerId, default);

        result.Should().BeOfType<NoContentResult>();
        _mockRepository.Verify(r => r.PauseTimerAsync(timerId, default), Times.Once);
    }

    [Fact]
    public async Task Pause_OwnedByOtherUser_ReturnsForbid()
    {
        Guid timerId = Guid.NewGuid();
        CookingTimerDto timer = TestDataFactory.CreateCookingTimerDto(id: timerId, userId: Guid.NewGuid());
        _mockRepository.Setup(r => r.GetByIdAsync(timerId, default)).ReturnsAsync(timer);

        IActionResult result = await _controller.Pause(timerId, default);

        result.Should().BeOfType<ForbidResult>();
        _mockRepository.Verify(r => r.PauseTimerAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task Resume_OwnedByUser_CallsRepositoryAndReturnsNoContent()
    {
        Guid timerId = Guid.NewGuid();
        CookingTimerDto timer = TestDataFactory.CreateCookingTimerDto(id: timerId, userId: _testUserId, status: "Paused");
        _mockRepository.Setup(r => r.GetByIdAsync(timerId, default)).ReturnsAsync(timer);
        _mockRepository.Setup(r => r.ResumeTimerAsync(timerId, default)).Returns(Task.CompletedTask);

        IActionResult result = await _controller.Resume(timerId, default);

        result.Should().BeOfType<NoContentResult>();
        _mockRepository.Verify(r => r.ResumeTimerAsync(timerId, default), Times.Once);
    }

    [Fact]
    public async Task Resume_OwnedByOtherUser_ReturnsForbid()
    {
        Guid timerId = Guid.NewGuid();
        CookingTimerDto timer = TestDataFactory.CreateCookingTimerDto(id: timerId, userId: Guid.NewGuid());
        _mockRepository.Setup(r => r.GetByIdAsync(timerId, default)).ReturnsAsync(timer);

        IActionResult result = await _controller.Resume(timerId, default);

        result.Should().BeOfType<ForbidResult>();
        _mockRepository.Verify(r => r.ResumeTimerAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task Cancel_OwnedByUser_CallsRepositoryAndReturnsNoContent()
    {
        Guid timerId = Guid.NewGuid();
        CookingTimerDto timer = TestDataFactory.CreateCookingTimerDto(id: timerId, userId: _testUserId, status: "Running");
        _mockRepository.Setup(r => r.GetByIdAsync(timerId, default)).ReturnsAsync(timer);
        _mockRepository.Setup(r => r.CancelTimerAsync(timerId, default)).Returns(Task.CompletedTask);

        IActionResult result = await _controller.Cancel(timerId, default);

        result.Should().BeOfType<NoContentResult>();
        _mockRepository.Verify(r => r.CancelTimerAsync(timerId, default), Times.Once);
    }

    [Fact]
    public async Task Cancel_OwnedByOtherUser_ReturnsForbid()
    {
        Guid timerId = Guid.NewGuid();
        CookingTimerDto timer = TestDataFactory.CreateCookingTimerDto(id: timerId, userId: Guid.NewGuid());
        _mockRepository.Setup(r => r.GetByIdAsync(timerId, default)).ReturnsAsync(timer);

        IActionResult result = await _controller.Cancel(timerId, default);

        result.Should().BeOfType<ForbidResult>();
        _mockRepository.Verify(r => r.CancelTimerAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task Acknowledge_OwnedByUser_CallsRepositoryAndReturnsNoContent()
    {
        Guid timerId = Guid.NewGuid();
        CookingTimerDto timer = TestDataFactory.CreateCookingTimerDto(id: timerId, userId: _testUserId, status: "Expired");
        _mockRepository.Setup(r => r.GetByIdAsync(timerId, default)).ReturnsAsync(timer);
        _mockRepository.Setup(r => r.AcknowledgeTimerAsync(timerId, default)).Returns(Task.CompletedTask);

        IActionResult result = await _controller.Acknowledge(timerId, default);

        result.Should().BeOfType<NoContentResult>();
        _mockRepository.Verify(r => r.AcknowledgeTimerAsync(timerId, default), Times.Once);
    }

    [Fact]
    public async Task Acknowledge_OwnedByOtherUser_ReturnsForbid()
    {
        Guid timerId = Guid.NewGuid();
        CookingTimerDto timer = TestDataFactory.CreateCookingTimerDto(id: timerId, userId: Guid.NewGuid());
        _mockRepository.Setup(r => r.GetByIdAsync(timerId, default)).ReturnsAsync(timer);

        IActionResult result = await _controller.Acknowledge(timerId, default);

        result.Should().BeOfType<ForbidResult>();
        _mockRepository.Verify(r => r.AcknowledgeTimerAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    #endregion
}
