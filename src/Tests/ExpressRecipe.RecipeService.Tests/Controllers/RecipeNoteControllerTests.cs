using ExpressRecipe.RecipeService.Controllers;
using ExpressRecipe.RecipeService.Data;
using ExpressRecipe.RecipeService.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ExpressRecipe.RecipeService.Tests.Controllers;

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
        _mockRepo.Verify(r => r.DismissNoteAsync(noteId, _recipeId, _userId, default), Times.Once);
    }

    [Fact]
    public async Task DismissNote_PassesRecipeIdToRepo()
    {
        Guid noteId = Guid.NewGuid();
        await _controller.DismissNote(_recipeId, noteId, default);
        _mockRepo.Verify(r => r.DismissNoteAsync(noteId, _recipeId, _userId, default), Times.Once);
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
        _mockRepo.Verify(r => r.DeleteNoteAsync(noteId, _recipeId, _userId, default), Times.Once);
    }

    [Fact]
    public async Task DeleteNote_PassesRecipeIdToRepo()
    {
        Guid noteId = Guid.NewGuid();
        await _controller.DeleteNote(_recipeId, noteId, default);
        _mockRepo.Verify(r => r.DeleteNoteAsync(noteId, _recipeId, _userId, default), Times.Once);
    }

    [Fact]
    public async Task DeleteNote_WrongUser_DoesNotDeleteOtherUsersNote()
    {
        // The controller routes the caller's userId to the repo; the repo's WHERE clause
        // includes RecipeId+UserId so a different user's token cannot delete this note.
        Guid noteId = Guid.NewGuid();
        Guid otherUserId = Guid.NewGuid();

        RecipeNoteController otherCtrl = new(_mockRepo.Object);
        otherCtrl.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(otherUserId);

        await otherCtrl.DeleteNote(_recipeId, noteId, default);

        _mockRepo.Verify(r => r.DeleteNoteAsync(noteId, _recipeId, otherUserId, default), Times.Once);
        _mockRepo.Verify(r => r.DeleteNoteAsync(noteId, _recipeId, _userId, default), Times.Never);
    }
}
