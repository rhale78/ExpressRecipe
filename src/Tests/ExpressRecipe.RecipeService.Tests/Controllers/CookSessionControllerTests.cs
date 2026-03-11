using ExpressRecipe.RecipeService.Controllers;
using ExpressRecipe.RecipeService.Data;
using ExpressRecipe.RecipeService.Services;
using ExpressRecipe.RecipeService.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ExpressRecipe.RecipeService.Tests.Controllers;

public class CookSessionControllerTests
{
    private readonly Mock<ICookSessionRepository> _mockRepo;
    private readonly Mock<IRecipeEventPublisher> _mockPublisher;
    private readonly CookSessionController _controller;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _householdId = Guid.NewGuid();
    private readonly Guid _recipeId = Guid.NewGuid();

    public CookSessionControllerTests()
    {
        _mockRepo      = new Mock<ICookSessionRepository>();
        _mockPublisher = new Mock<IRecipeEventPublisher>();

        _controller = new CookSessionController(_mockRepo.Object, _mockPublisher.Object);
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_userId);
    }

    // ── LogSession ────────────────────────────────────────────────────────────

    [Fact]
    public async Task LogSession_ReturnsOkWithId()
    {
        Guid sessionId = Guid.NewGuid();
        LogCookSessionRequest req = new()
        {
            RecipeId    = _recipeId,
            HouseholdId = _householdId,
            Rating      = 4,
            AIHelpUsed  = false
        };
        _mockRepo.Setup(r => r.LogSessionAsync(_userId, req, It.IsAny<DateTimeOffset>(), default))
                 .ReturnsAsync(sessionId);

        IActionResult result = await _controller.LogSession(req, default);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        ok.Value.Should().BeEquivalentTo(new { id = sessionId });
    }

    [Fact]
    public async Task LogSession_WithRating_PublishesCookedSessionEvent()
    {
        Guid sessionId = Guid.NewGuid();
        LogCookSessionRequest req = new()
        {
            RecipeId    = _recipeId,
            HouseholdId = _householdId,
            Rating      = 5,
            AIHelpUsed  = false
        };
        _mockRepo.Setup(r => r.LogSessionAsync(_userId, req, It.IsAny<DateTimeOffset>(), default))
                 .ReturnsAsync(sessionId);

        await _controller.LogSession(req, default);

        _mockPublisher.Verify(p => p.PublishCookedSessionAsync(
            sessionId, _userId, _householdId, _recipeId,
            It.IsAny<DateTimeOffset>(), true, default), Times.Once);
    }

    [Fact]
    public async Task LogSession_WithoutRating_PublishesEventWithHasRatingFalse()
    {
        Guid sessionId = Guid.NewGuid();
        LogCookSessionRequest req = new()
        {
            RecipeId    = _recipeId,
            HouseholdId = _householdId,
            AIHelpUsed  = false
        };
        _mockRepo.Setup(r => r.LogSessionAsync(_userId, req, It.IsAny<DateTimeOffset>(), default))
                 .ReturnsAsync(sessionId);

        await _controller.LogSession(req, default);

        _mockPublisher.Verify(p => p.PublishCookedSessionAsync(
            sessionId, _userId, _householdId, _recipeId,
            It.IsAny<DateTimeOffset>(), false, default), Times.Once);
    }

    [Fact]
    public async Task LogSession_CookedAt_SameValuePassedToRepoAndPublisher()
    {
        // When CookedAt is supplied, both repo and publisher should use the exact same value
        DateTimeOffset suppliedTime = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
        Guid sessionId = Guid.NewGuid();
        LogCookSessionRequest req = new()
        {
            RecipeId    = _recipeId,
            HouseholdId = _householdId,
            CookedAt    = suppliedTime,
            AIHelpUsed  = false
        };
        _mockRepo.Setup(r => r.LogSessionAsync(_userId, req, suppliedTime, default))
                 .ReturnsAsync(sessionId);

        await _controller.LogSession(req, default);

        _mockRepo.Verify(r => r.LogSessionAsync(_userId, req, suppliedTime, default), Times.Once);
        _mockPublisher.Verify(p => p.PublishCookedSessionAsync(
            sessionId, _userId, _householdId, _recipeId, suppliedTime, false, default), Times.Once);
    }

    [Fact]
    public async Task LogSession_UnauthenticatedUser_ReturnsUnauthorized()
    {
        CookSessionController unauthCtrl = new(_mockRepo.Object, _mockPublisher.Object);
        unauthCtrl.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        IActionResult result = await unauthCtrl.LogSession(new LogCookSessionRequest(), default);

        Assert.IsType<UnauthorizedResult>(result);
    }

    // ── GetSessions ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSessions_ReturnsList()
    {
        List<CookSessionDto> sessions = new()
        {
            new CookSessionDto { Id = Guid.NewGuid(), RecipeId = _recipeId, RecipeName = "Pasta" }
        };
        _mockRepo.Setup(r => r.GetSessionsAsync(_userId, null, 20, default))
                 .ReturnsAsync(sessions);

        IActionResult result = await _controller.GetSessions(null, 20, default);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        ((List<CookSessionDto>)ok.Value!).Should().HaveCount(1);
    }

    [Fact]
    public async Task GetSessions_FilteredByRecipe_PassesRecipeIdToRepo()
    {
        _mockRepo.Setup(r => r.GetSessionsAsync(_userId, _recipeId, 10, default))
                 .ReturnsAsync(new List<CookSessionDto>());

        await _controller.GetSessions(_recipeId, 10, default);

        _mockRepo.Verify(r => r.GetSessionsAsync(_userId, _recipeId, 10, default), Times.Once);
    }

    [Fact]
    public async Task GetSessions_LimitZero_ReturnsBadRequest()
    {
        IActionResult result = await _controller.GetSessions(null, 0, default);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetSessions_LimitExceedsMax_ReturnsBadRequest()
    {
        IActionResult result = await _controller.GetSessions(null, 101, default);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetSessions_LimitNegative_ReturnsBadRequest()
    {
        IActionResult result = await _controller.GetSessions(null, -1, default);
        Assert.IsType<BadRequestObjectResult>(result);
    }
}
