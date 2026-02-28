using ExpressRecipe.CookbookService.Controllers;
using ExpressRecipe.CookbookService.Data;
using ExpressRecipe.CookbookService.Models;
using ExpressRecipe.CookbookService.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ExpressRecipe.CookbookService.Tests.Controllers;

public class CookbooksControllerTests
{
    private readonly Mock<ICookbookRepository> _mockRepo;
    private readonly Mock<ILogger<CookbooksController>> _mockLogger;
    private readonly CookbooksController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public CookbooksControllerTests()
    {
        _mockRepo = new Mock<ICookbookRepository>();
        _mockLogger = new Mock<ILogger<CookbooksController>>();
        _controller = new CookbooksController(_mockRepo.Object, _mockLogger.Object);
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_userId);
    }

    // ── GetCookbooks ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCookbooks_AuthenticatedNoFilter_ReturnsOkWithResult()
    {
        var items = new List<CookbookSummaryDto> { new() { Id = Guid.NewGuid(), Title = "CB1" } };
        _mockRepo.Setup(r => r.GetUserCookbooksAsync(_userId, 1, 20)).ReturnsAsync(items);
        _mockRepo.Setup(r => r.GetUserCookbookCountAsync(_userId)).ReturnsAsync(1);

        var result = await _controller.GetCookbooks(null, null);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var search = Assert.IsType<CookbookSearchResult>(ok.Value);
        Assert.Single(search.Items);
        Assert.Equal(1, search.TotalCount);
    }

    [Fact]
    public async Task GetCookbooks_WithVisibilityFilter_CallsSearchAsync()
    {
        var items = new List<CookbookSummaryDto>();
        _mockRepo.Setup(r => r.SearchCookbooksAsync(null, "Public", 1, 20)).ReturnsAsync(items);
        _mockRepo.Setup(r => r.SearchCookbooksCountAsync(null, "Public")).ReturnsAsync(0);

        var result = await _controller.GetCookbooks(null, "Public");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        _mockRepo.Verify(r => r.SearchCookbooksAsync(null, "Public", 1, 20), Times.Once);
        _mockRepo.Verify(r => r.GetUserCookbooksAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetCookbooks_RepositoryThrows_Returns500()
    {
        _mockRepo.Setup(r => r.GetUserCookbooksAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>()))
            .ThrowsAsync(new Exception("DB error"));

        var result = await _controller.GetCookbooks(null, null);

        var obj = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, obj.StatusCode);
    }

    // ── GetCookbook ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCookbook_FoundAndCanView_ReturnsOk()
    {
        var id = Guid.NewGuid();
        var dto = new CookbookDto { Id = id, Title = "My Cookbook", Visibility = "Private" };
        _mockRepo.Setup(r => r.GetCookbookByIdAsync(id, true)).ReturnsAsync(dto);
        _mockRepo.Setup(r => r.CanViewAsync(id, _userId)).ReturnsAsync(true);
        _mockRepo.Setup(r => r.IncrementViewCountAsync(id)).Returns(Task.CompletedTask);

        var result = await _controller.GetCookbook(id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var cookbook = Assert.IsType<CookbookDto>(ok.Value);
        Assert.Equal(id, cookbook.Id);
        _mockRepo.Verify(r => r.IncrementViewCountAsync(id), Times.Once);
    }

    [Fact]
    public async Task GetCookbook_NotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetCookbookByIdAsync(id, true)).ReturnsAsync((CookbookDto?)null);

        var result = await _controller.GetCookbook(id);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetCookbook_CannotView_ReturnsForbid()
    {
        var id = Guid.NewGuid();
        var dto = new CookbookDto { Id = id, Title = "Other", Visibility = "Private" };
        _mockRepo.Setup(r => r.GetCookbookByIdAsync(id, true)).ReturnsAsync(dto);
        _mockRepo.Setup(r => r.CanViewAsync(id, _userId)).ReturnsAsync(false);

        var result = await _controller.GetCookbook(id);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task GetCookbook_RepositoryThrows_Returns500()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetCookbookByIdAsync(id, true)).ThrowsAsync(new Exception("DB error"));

        var result = await _controller.GetCookbook(id);

        var obj = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, obj.StatusCode);
    }

    // ── GetCookbookBySlug ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetCookbookBySlug_PublicCookbook_ReturnsOk()
    {
        var dto = new CookbookDto { Id = Guid.NewGuid(), Title = "Public CB", Visibility = "Public" };
        _mockRepo.Setup(r => r.GetCookbookBySlugAsync("my-cookbook")).ReturnsAsync(dto);
        _mockRepo.Setup(r => r.IncrementViewCountAsync(dto.Id)).Returns(Task.CompletedTask);

        var result = await _controller.GetCookbookBySlug("my-cookbook");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var cookbook = Assert.IsType<CookbookDto>(ok.Value);
        Assert.Equal("Public CB", cookbook.Title);
    }

    [Fact]
    public async Task GetCookbookBySlug_PrivateCookbook_ReturnsForbid()
    {
        var dto = new CookbookDto { Id = Guid.NewGuid(), Title = "Private CB", Visibility = "Private" };
        _mockRepo.Setup(r => r.GetCookbookBySlugAsync("private-cookbook")).ReturnsAsync(dto);

        var result = await _controller.GetCookbookBySlug("private-cookbook");

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task GetCookbookBySlug_NotFound_ReturnsNotFound()
    {
        _mockRepo.Setup(r => r.GetCookbookBySlugAsync("no-such-slug")).ReturnsAsync((CookbookDto?)null);

        var result = await _controller.GetCookbookBySlug("no-such-slug");

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    // ── CreateCookbook ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateCookbook_Authenticated_ReturnsCreatedAtAction()
    {
        var newId = Guid.NewGuid();
        var request = new CreateCookbookRequest { Title = "New Cookbook" };
        _mockRepo.Setup(r => r.CreateCookbookAsync(request, _userId)).ReturnsAsync(newId);

        var result = await _controller.CreateCookbook(request);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(_controller.GetCookbook), created.ActionName);
        Assert.NotNull(created.Value);
    }

    [Fact]
    public async Task CreateCookbook_Unauthenticated_ReturnsUnauthorized()
    {
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();
        var request = new CreateCookbookRequest { Title = "New Cookbook" };

        var result = await _controller.CreateCookbook(request);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateCookbook_RepositoryThrows_Returns500()
    {
        var request = new CreateCookbookRequest { Title = "New" };
        _mockRepo.Setup(r => r.CreateCookbookAsync(request, _userId)).ThrowsAsync(new Exception("DB error"));

        var result = await _controller.CreateCookbook(request);

        var obj = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, obj.StatusCode);
    }

    // ── UpdateCookbook ────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateCookbook_Success_ReturnsNoContent()
    {
        var id = Guid.NewGuid();
        var request = new UpdateCookbookRequest { Title = "Updated" };
        _mockRepo.Setup(r => r.UpdateCookbookAsync(id, _userId, request)).ReturnsAsync(true);

        var result = await _controller.UpdateCookbook(id, request);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task UpdateCookbook_NotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        var request = new UpdateCookbookRequest { Title = "Updated" };
        _mockRepo.Setup(r => r.UpdateCookbookAsync(id, _userId, request)).ReturnsAsync(false);

        var result = await _controller.UpdateCookbook(id, request);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task UpdateCookbook_Unauthenticated_ReturnsUnauthorized()
    {
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();
        var request = new UpdateCookbookRequest { Title = "Updated" };

        var result = await _controller.UpdateCookbook(Guid.NewGuid(), request);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    // ── DeleteCookbook ────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteCookbook_Success_ReturnsNoContent()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.DeleteCookbookAsync(id, _userId)).ReturnsAsync(true);

        var result = await _controller.DeleteCookbook(id);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteCookbook_NotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.DeleteCookbookAsync(id, _userId)).ReturnsAsync(false);

        var result = await _controller.DeleteCookbook(id);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task DeleteCookbook_Unauthenticated_ReturnsUnauthorized()
    {
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        var result = await _controller.DeleteCookbook(Guid.NewGuid());

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    // ── FavoriteCookbook ──────────────────────────────────────────────────────

    [Fact]
    public async Task FavoriteCookbook_Success_ReturnsOk()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.FavoriteCookbookAsync(id, _userId)).ReturnsAsync(true);

        var result = await _controller.FavoriteCookbook(id);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task FavoriteCookbook_Unauthenticated_ReturnsUnauthorized()
    {
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        var result = await _controller.FavoriteCookbook(Guid.NewGuid());

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    // ── UnfavoriteCookbook ────────────────────────────────────────────────────

    [Fact]
    public async Task UnfavoriteCookbook_Success_ReturnsOk()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.UnfavoriteCookbookAsync(id, _userId)).ReturnsAsync(true);

        var result = await _controller.UnfavoriteCookbook(id);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task UnfavoriteCookbook_Unauthenticated_ReturnsUnauthorized()
    {
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        var result = await _controller.UnfavoriteCookbook(Guid.NewGuid());

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    // ── GetMyFavorites ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMyFavorites_Authenticated_ReturnsOkWithList()
    {
        var favorites = new List<CookbookSummaryDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Fav1" },
            new() { Id = Guid.NewGuid(), Title = "Fav2" }
        };
        _mockRepo.Setup(r => r.GetFavoriteCookbooksAsync(_userId, 1, 20)).ReturnsAsync(favorites);

        var result = await _controller.GetMyFavorites();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsType<List<CookbookSummaryDto>>(ok.Value);
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public async Task GetMyFavorites_Unauthenticated_ReturnsUnauthorized()
    {
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        var result = await _controller.GetMyFavorites();

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    // ── RateCookbook ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RateCookbook_ValidRating_ReturnsOkWithAverageAndCount()
    {
        var id = Guid.NewGuid();
        var request = new RateCookbookRequest { Rating = 4 };
        _mockRepo.Setup(r => r.RateCookbookAsync(id, _userId, 4)).ReturnsAsync(true);
        _mockRepo.Setup(r => r.GetRatingsAsync(id)).ReturnsAsync((4.0m, 10));

        var result = await _controller.RateCookbook(id, request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task RateCookbook_RatingZero_ReturnsBadRequest()
    {
        var id = Guid.NewGuid();
        var request = new RateCookbookRequest { Rating = 0 };

        var result = await _controller.RateCookbook(id, request);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task RateCookbook_RatingSix_ReturnsBadRequest()
    {
        var id = Guid.NewGuid();
        var request = new RateCookbookRequest { Rating = 6 };

        var result = await _controller.RateCookbook(id, request);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task RateCookbook_Unauthenticated_ReturnsUnauthorized()
    {
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();
        var request = new RateCookbookRequest { Rating = 3 };

        var result = await _controller.RateCookbook(Guid.NewGuid(), request);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    // ── GetComments ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetComments_ReturnsOkWithComments()
    {
        var id = Guid.NewGuid();
        var comments = new List<CookbookCommentDto>
        {
            new() { Id = Guid.NewGuid(), Content = "Great!", CookbookId = id, UserId = _userId }
        };
        _mockRepo.Setup(r => r.GetCommentsAsync(id, 1, 20)).ReturnsAsync(comments);

        var result = await _controller.GetComments(id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsType<List<CookbookCommentDto>>(ok.Value);
        Assert.Single(list);
    }

    [Fact]
    public async Task GetComments_RepositoryThrows_Returns500()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetCommentsAsync(id, It.IsAny<int>(), It.IsAny<int>()))
            .ThrowsAsync(new Exception("DB error"));

        var result = await _controller.GetComments(id);

        var obj = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, obj.StatusCode);
    }

    // ── AddComment ────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddComment_Authenticated_ReturnsOkWithId()
    {
        var cookbookId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var request = new AddCommentRequest { Content = "Delicious!" };
        _mockRepo.Setup(r => r.AddCommentAsync(cookbookId, _userId, "Delicious!")).ReturnsAsync(commentId);

        var result = await _controller.AddComment(cookbookId, request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task AddComment_Unauthenticated_ReturnsUnauthorized()
    {
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();
        var request = new AddCommentRequest { Content = "Nice!" };

        var result = await _controller.AddComment(Guid.NewGuid(), request);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    // ── DeleteComment ─────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteComment_Success_ReturnsNoContent()
    {
        var cookbookId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        _mockRepo.Setup(r => r.DeleteCommentAsync(commentId, _userId)).ReturnsAsync(true);

        var result = await _controller.DeleteComment(cookbookId, commentId);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteComment_NotFound_ReturnsNotFound()
    {
        var cookbookId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        _mockRepo.Setup(r => r.DeleteCommentAsync(commentId, _userId)).ReturnsAsync(false);

        var result = await _controller.DeleteComment(cookbookId, commentId);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task DeleteComment_Unauthenticated_ReturnsUnauthorized()
    {
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        var result = await _controller.DeleteComment(Guid.NewGuid(), Guid.NewGuid());

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    // ── ShareCookbook ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ShareCookbook_Success_ReturnsOk()
    {
        var id = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var request = new ShareCookbookRequest { TargetUserId = targetId, CanEdit = false };
        _mockRepo.Setup(r => r.ShareCookbookAsync(id, _userId, targetId, false)).ReturnsAsync(true);

        var result = await _controller.ShareCookbook(id, request);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task ShareCookbook_NotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var request = new ShareCookbookRequest { TargetUserId = targetId, CanEdit = false };
        _mockRepo.Setup(r => r.ShareCookbookAsync(id, _userId, targetId, false)).ReturnsAsync(false);

        var result = await _controller.ShareCookbook(id, request);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ShareCookbook_Unauthenticated_ReturnsUnauthorized()
    {
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();
        var request = new ShareCookbookRequest { TargetUserId = Guid.NewGuid() };

        var result = await _controller.ShareCookbook(Guid.NewGuid(), request);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    // ── RevokeShare ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RevokeShare_Success_ReturnsNoContent()
    {
        var id = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        _mockRepo.Setup(r => r.RevokeCookbookShareAsync(id, _userId, targetId)).ReturnsAsync(true);

        var result = await _controller.RevokeShare(id, targetId);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task RevokeShare_NotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        _mockRepo.Setup(r => r.RevokeCookbookShareAsync(id, _userId, targetId)).ReturnsAsync(false);

        var result = await _controller.RevokeShare(id, targetId);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ── MergeCookbooks ────────────────────────────────────────────────────────

    [Fact]
    public async Task MergeCookbooks_Success_ReturnsCreatedAtAction()
    {
        var newId = Guid.NewGuid();
        var request = new MergeCookbooksRequest
        {
            SourceCookbookIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() },
            NewTitle = "Merged"
        };
        _mockRepo.Setup(r => r.MergeCookbooksAsync(_userId, request)).ReturnsAsync(newId);

        var result = await _controller.MergeCookbooks(request);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(_controller.GetCookbook), created.ActionName);
    }

    [Fact]
    public async Task MergeCookbooks_Unauthenticated_ReturnsUnauthorized()
    {
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();
        var request = new MergeCookbooksRequest { NewTitle = "Merged" };

        var result = await _controller.MergeCookbooks(request);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task MergeCookbooks_UnauthorizedAccessException_ReturnsForbid()
    {
        var request = new MergeCookbooksRequest { NewTitle = "Merged" };
        _mockRepo.Setup(r => r.MergeCookbooksAsync(_userId, request))
            .ThrowsAsync(new UnauthorizedAccessException());

        var result = await _controller.MergeCookbooks(request);

        Assert.IsType<ForbidResult>(result.Result);
    }

    // ── SplitCookbook ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SplitCookbook_Success_ReturnsOkWithIds()
    {
        var id = Guid.NewGuid();
        var newIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var request = new SplitCookbookRequest { SectionIds = new List<Guid> { Guid.NewGuid() } };
        _mockRepo.Setup(r => r.SplitCookbookAsync(id, _userId, request)).ReturnsAsync(newIds);

        var result = await _controller.SplitCookbook(id, request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var ids = Assert.IsType<List<Guid>>(ok.Value);
        Assert.Equal(2, ids.Count);
    }

    [Fact]
    public async Task SplitCookbook_Unauthenticated_ReturnsUnauthorized()
    {
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();
        var request = new SplitCookbookRequest();

        var result = await _controller.SplitCookbook(Guid.NewGuid(), request);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task SplitCookbook_UnauthorizedAccessException_ReturnsForbid()
    {
        var id = Guid.NewGuid();
        var request = new SplitCookbookRequest();
        _mockRepo.Setup(r => r.SplitCookbookAsync(id, _userId, request))
            .ThrowsAsync(new UnauthorizedAccessException());

        var result = await _controller.SplitCookbook(id, request);

        Assert.IsType<ForbidResult>(result.Result);
    }

    // ── ExtractRecipes ────────────────────────────────────────────────────────

    [Fact]
    public async Task ExtractRecipes_Success_ReturnsOkWithIds()
    {
        var id = Guid.NewGuid();
        var recipeIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        _mockRepo.Setup(r => r.ExtractRecipesFromCookbookAsync(id, _userId)).ReturnsAsync(recipeIds);

        var result = await _controller.ExtractRecipes(id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var ids = Assert.IsType<List<Guid>>(ok.Value);
        Assert.Equal(3, ids.Count);
    }

    [Fact]
    public async Task ExtractRecipes_Unauthenticated_ReturnsUnauthorized()
    {
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        var result = await _controller.ExtractRecipes(Guid.NewGuid());

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task ExtractRecipes_UnauthorizedAccessException_ReturnsForbid()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.ExtractRecipesFromCookbookAsync(id, _userId))
            .ThrowsAsync(new UnauthorizedAccessException());

        var result = await _controller.ExtractRecipes(id);

        Assert.IsType<ForbidResult>(result.Result);
    }
}
