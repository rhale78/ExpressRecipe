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
        _mockRepo.Setup(r => r.LogSessionAsync(_userId, req, default))
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
        _mockRepo.Setup(r => r.LogSessionAsync(_userId, req, default))
                 .ReturnsAsync(sessionId);

        await _controller.LogSession(req, default);

        _mockPublisher.Verify(p => p.PublishCookedSessionAsync(
            sessionId, _userId, _householdId, _recipeId,
            It.IsAny<DateTime>(), true, default), Times.Once);
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
        _mockRepo.Setup(r => r.LogSessionAsync(_userId, req, default))
                 .ReturnsAsync(sessionId);

        await _controller.LogSession(req, default);

        _mockPublisher.Verify(p => p.PublishCookedSessionAsync(
            sessionId, _userId, _householdId, _recipeId,
            It.IsAny<DateTime>(), false, default), Times.Once);
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
}

public class RecipeNoteControllerTests
{
    private readonly Mock<ICookSessionRepository> _mockRepo;
    private readonly RecipeNoteController _controller;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _recipeId = Guid.NewGuid();

    public RecipeNoteControllerTests()
    {
        _mockRepo   = new Mock<ICookSessionRepository>();
        _controller = new RecipeNoteController(_mockRepo.Object);
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_userId);
    }

    // ── GetNotes ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetNotes_ReturnsList()
    {
        List<RecipeNoteDto> notes = new()
        {
            new RecipeNoteDto { Id = Guid.NewGuid(), RecipeId = _recipeId, NoteType = "Tip", NoteText = "test" }
        };
        _mockRepo.Setup(r => r.GetNotesAsync(_userId, _recipeId, default)).ReturnsAsync(notes);

        IActionResult result = await _controller.GetNotes(_recipeId, default);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        ((List<RecipeNoteDto>)ok.Value!).Should().HaveCount(1);
    }

    // ── SaveNote ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveNote_ReturnsOkWithId()
    {
        Guid noteId = Guid.NewGuid();
        SaveRecipeNoteRequest req = new() { NoteType = "Tip", NoteText = "dissolve starch first" };
        _mockRepo.Setup(r => r.SaveNoteAsync(_userId, It.IsAny<SaveRecipeNoteRequest>(), default))
                 .ReturnsAsync(noteId);

        IActionResult result = await _controller.SaveNote(_recipeId, req, default);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        ok.Value.Should().BeEquivalentTo(new { id = noteId });
    }

    [Fact]
    public async Task SaveNote_SetsRecipeIdFromRoute()
    {
        SaveRecipeNoteRequest req = new() { NoteText = "test" };
        _mockRepo.Setup(r => r.SaveNoteAsync(_userId, It.IsAny<SaveRecipeNoteRequest>(), default))
                 .ReturnsAsync(Guid.NewGuid());

        await _controller.SaveNote(_recipeId, req, default);

        _mockRepo.Verify(r => r.SaveNoteAsync(_userId,
            It.Is<SaveRecipeNoteRequest>(n => n.RecipeId == _recipeId), default), Times.Once);
    }

    // ── DismissNote ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DismissNote_ReturnsNoContent()
    {
        Guid noteId = Guid.NewGuid();
        IActionResult result = await _controller.DismissNote(_recipeId, noteId, default);
        Assert.IsType<NoContentResult>(result);
        _mockRepo.Verify(r => r.DismissNoteAsync(noteId, _userId, default), Times.Once);
    }

    [Fact]
    public async Task DismissNote_UnauthenticatedUser_ReturnsUnauthorized()
    {
        RecipeNoteController unauthCtrl = new(_mockRepo.Object);
        unauthCtrl.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        IActionResult result = await unauthCtrl.DismissNote(_recipeId, Guid.NewGuid(), default);

        Assert.IsType<UnauthorizedResult>(result);
    }

    // ── DeleteNote ────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteNote_ReturnsNoContent()
    {
        Guid noteId = Guid.NewGuid();
        IActionResult result = await _controller.DeleteNote(_recipeId, noteId, default);
        Assert.IsType<NoContentResult>(result);
        _mockRepo.Verify(r => r.DeleteNoteAsync(noteId, _userId, default), Times.Once);
    }

    [Fact]
    public async Task DeleteNote_WrongUser_DoesNotDeleteOtherUsersNote()
    {
        // Because the WHERE clause in the repo includes UserId, the repo does NOT delete
        // another user's note — the controller just passes userId through. This test verifies
        // the controller routes userId correctly so the repo guard applies.
        Guid noteId = Guid.NewGuid();
        Guid otherUserId = Guid.NewGuid();

        RecipeNoteController otherCtrl = new(_mockRepo.Object);
        otherCtrl.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(otherUserId);

        await otherCtrl.DeleteNote(_recipeId, noteId, default);

        _mockRepo.Verify(r => r.DeleteNoteAsync(noteId, otherUserId, default), Times.Once);
        _mockRepo.Verify(r => r.DeleteNoteAsync(noteId, _userId, default), Times.Never);
    }
}
