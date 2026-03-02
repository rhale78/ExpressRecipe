using ExpressRecipe.CookbookService.Data;
using ExpressRecipe.CookbookService.Models;
using Moq;
using Xunit;

namespace ExpressRecipe.CookbookService.Tests.Data;

/// <summary>
/// Contract/behaviour tests for ICookbookRepository.
/// Uses a Mock so every method signature and return-type is exercised
/// without a live database.  These tests verify:
///   1. The interface surface is complete (compiler-enforced via Mock).
///   2. Return types and nullable contracts are correct.
///   3. Common orchestration scenarios work (create → read → update → delete).
/// </summary>
public class CookbookRepositoryContractTests
{
    private readonly Mock<ICookbookRepository> _repo = new();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _cookbookId = Guid.NewGuid();
    private readonly Guid _sectionId = Guid.NewGuid();
    private readonly Guid _recipeEntryId = Guid.NewGuid();
    private readonly Guid _commentId = Guid.NewGuid();

    // ── Cookbook CRUD ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateCookbookAsync_ReturnsNewGuid()
    {
        var request = new CreateCookbookRequest { Title = "Pasta Classics", Visibility = "Private" };
        _repo.Setup(r => r.CreateCookbookAsync(request, _ownerId)).ReturnsAsync(_cookbookId);

        var result = await _repo.Object.CreateCookbookAsync(request, _ownerId);

        Assert.Equal(_cookbookId, result);
        _repo.Verify(r => r.CreateCookbookAsync(request, _ownerId), Times.Once);
    }

    [Fact]
    public async Task GetCookbookByIdAsync_ReturnsDtoWithSections()
    {
        var dto = new CookbookDto
        {
            Id = _cookbookId,
            Title = "Pasta Classics",
            Visibility = "Private",
            OwnerId = _ownerId,
            Sections = new List<CookbookSectionDto>
            {
                new() { Id = _sectionId, Title = "Starters" }
            }
        };
        _repo.Setup(r => r.GetCookbookByIdAsync(_cookbookId, true)).ReturnsAsync(dto);

        var result = await _repo.Object.GetCookbookByIdAsync(_cookbookId, true);

        Assert.NotNull(result);
        Assert.Equal(_cookbookId, result!.Id);
        Assert.Single(result.Sections);
        Assert.Equal("Starters", result.Sections[0].Title);
    }

    [Fact]
    public async Task GetCookbookByIdAsync_ReturnsNullForMissingId()
    {
        _repo.Setup(r => r.GetCookbookByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
            .ReturnsAsync((CookbookDto?)null);

        var result = await _repo.Object.GetCookbookByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetCookbookBySlugAsync_ReturnsNullWhenNotFound()
    {
        _repo.Setup(r => r.GetCookbookBySlugAsync("unknown-slug")).ReturnsAsync((CookbookDto?)null);

        var result = await _repo.Object.GetCookbookBySlugAsync("unknown-slug");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetCookbookBySlugAsync_ReturnsCookbookForPublicSlug()
    {
        var dto = new CookbookDto { Id = _cookbookId, Title = "Public Book", Visibility = "Public", WebSlug = "public-book" };
        _repo.Setup(r => r.GetCookbookBySlugAsync("public-book")).ReturnsAsync(dto);

        var result = await _repo.Object.GetCookbookBySlugAsync("public-book");

        Assert.NotNull(result);
        Assert.Equal("Public", result!.Visibility);
    }

    [Fact]
    public async Task GetUserCookbooksAsync_ReturnsPaginatedList()
    {
        var books = Enumerable.Range(1, 3)
            .Select(i => new CookbookSummaryDto { Id = Guid.NewGuid(), Title = $"Book {i}" })
            .ToList();
        _repo.Setup(r => r.GetUserCookbooksAsync(_ownerId, 1, 20)).ReturnsAsync(books);

        var result = await _repo.Object.GetUserCookbooksAsync(_ownerId, 1, 20);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetUserCookbookCountAsync_ReturnsInteger()
    {
        _repo.Setup(r => r.GetUserCookbookCountAsync(_ownerId)).ReturnsAsync(5);

        var count = await _repo.Object.GetUserCookbookCountAsync(_ownerId);

        Assert.Equal(5, count);
    }

    [Fact]
    public async Task SearchCookbooksAsync_ReturnsMatchingBooks()
    {
        var matches = new List<CookbookSummaryDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Italian Kitchen", Visibility = "Public" }
        };
        _repo.Setup(r => r.SearchCookbooksAsync("italian", "Public", 1, 20)).ReturnsAsync(matches);

        var result = await _repo.Object.SearchCookbooksAsync("italian", "Public", 1, 20);

        Assert.Single(result);
        Assert.Contains("Italian", result[0].Title);
    }

    [Fact]
    public async Task UpdateCookbookAsync_ReturnsTrueOnSuccess()
    {
        var request = new UpdateCookbookRequest { Title = "Updated Title", Visibility = "Shared" };
        _repo.Setup(r => r.UpdateCookbookAsync(_cookbookId, _ownerId, request)).ReturnsAsync(true);

        var success = await _repo.Object.UpdateCookbookAsync(_cookbookId, _ownerId, request);

        Assert.True(success);
    }

    [Fact]
    public async Task UpdateCookbookAsync_ReturnsFalseWhenNotOwner()
    {
        var other = Guid.NewGuid();
        _repo.Setup(r => r.UpdateCookbookAsync(_cookbookId, other, It.IsAny<UpdateCookbookRequest>()))
            .ReturnsAsync(false);

        var success = await _repo.Object.UpdateCookbookAsync(_cookbookId, other, new UpdateCookbookRequest());

        Assert.False(success);
    }

    [Fact]
    public async Task DeleteCookbookAsync_ReturnsTrueForOwner()
    {
        _repo.Setup(r => r.DeleteCookbookAsync(_cookbookId, _ownerId)).ReturnsAsync(true);

        var success = await _repo.Object.DeleteCookbookAsync(_cookbookId, _ownerId);

        Assert.True(success);
    }

    [Fact]
    public async Task DeleteCookbookAsync_ReturnsFalseForNonOwner()
    {
        var other = Guid.NewGuid();
        _repo.Setup(r => r.DeleteCookbookAsync(_cookbookId, other)).ReturnsAsync(false);

        var success = await _repo.Object.DeleteCookbookAsync(_cookbookId, other);

        Assert.False(success);
    }

    // ── Section management ────────────────────────────────────────────────────

    [Fact]
    public async Task CreateSectionAsync_ReturnsNewSectionGuid()
    {
        var request = new CreateCookbookSectionRequest { Title = "Appetisers", SortOrder = 0 };
        _repo.Setup(r => r.CreateSectionAsync(_cookbookId, _ownerId, request)).ReturnsAsync(_sectionId);

        var result = await _repo.Object.CreateSectionAsync(_cookbookId, _ownerId, request);

        Assert.Equal(_sectionId, result);
    }

    [Fact]
    public async Task CreateSectionAsync_ThrowsWhenNotOwner()
    {
        var other = Guid.NewGuid();
        _repo.Setup(r => r.CreateSectionAsync(_cookbookId, other, It.IsAny<CreateCookbookSectionRequest>()))
            .ThrowsAsync(new UnauthorizedAccessException());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _repo.Object.CreateSectionAsync(_cookbookId, other, new CreateCookbookSectionRequest()));
    }

    [Fact]
    public async Task UpdateSectionAsync_ReturnsTrueOnSuccess()
    {
        var req = new UpdateCookbookSectionRequest { Title = "Mains" };
        _repo.Setup(r => r.UpdateSectionAsync(_sectionId, _ownerId, req)).ReturnsAsync(true);

        Assert.True(await _repo.Object.UpdateSectionAsync(_sectionId, _ownerId, req));
    }

    [Fact]
    public async Task DeleteSectionAsync_ReturnsFalseWhenNotFound()
    {
        _repo.Setup(r => r.DeleteSectionAsync(It.IsAny<Guid>(), _ownerId)).ReturnsAsync(false);

        Assert.False(await _repo.Object.DeleteSectionAsync(Guid.NewGuid(), _ownerId));
    }

    [Fact]
    public async Task ReorderSectionsAsync_ReturnsTrueForOwner()
    {
        var ids = new List<Guid> { _sectionId, Guid.NewGuid() };
        _repo.Setup(r => r.ReorderSectionsAsync(_cookbookId, _ownerId, ids)).ReturnsAsync(true);

        Assert.True(await _repo.Object.ReorderSectionsAsync(_cookbookId, _ownerId, ids));
    }

    // ── Recipe management ─────────────────────────────────────────────────────

    [Fact]
    public async Task AddRecipeToCookbookAsync_ReturnsNewEntryGuid()
    {
        var request = new AddCookbookRecipeRequest
        {
            RecipeId = Guid.NewGuid(),
            RecipeName = "Carbonara",
            SectionId = _sectionId
        };
        _repo.Setup(r => r.AddRecipeToCookbookAsync(_cookbookId, _ownerId, request)).ReturnsAsync(_recipeEntryId);

        var id = await _repo.Object.AddRecipeToCookbookAsync(_cookbookId, _ownerId, request);

        Assert.Equal(_recipeEntryId, id);
    }

    [Fact]
    public async Task AddRecipesBatchAsync_ReturnsTrueOnSuccess()
    {
        var recipes = new List<AddCookbookRecipeRequest>
        {
            new() { RecipeId = Guid.NewGuid(), RecipeName = "Soup" },
            new() { RecipeId = Guid.NewGuid(), RecipeName = "Salad" }
        };
        _repo.Setup(r => r.AddRecipesBatchAsync(_cookbookId, _ownerId, _sectionId, recipes)).ReturnsAsync(true);

        Assert.True(await _repo.Object.AddRecipesBatchAsync(_cookbookId, _ownerId, _sectionId, recipes));
    }

    [Fact]
    public async Task RemoveRecipeFromCookbookAsync_ReturnsTrueOnSuccess()
    {
        _repo.Setup(r => r.RemoveRecipeFromCookbookAsync(_recipeEntryId, _ownerId)).ReturnsAsync(true);

        Assert.True(await _repo.Object.RemoveRecipeFromCookbookAsync(_recipeEntryId, _ownerId));
    }

    [Fact]
    public async Task MoveRecipeToSectionAsync_ReturnsTrueOnSuccess()
    {
        var newSection = Guid.NewGuid();
        _repo.Setup(r => r.MoveRecipeToSectionAsync(_recipeEntryId, _ownerId, newSection)).ReturnsAsync(true);

        Assert.True(await _repo.Object.MoveRecipeToSectionAsync(_recipeEntryId, _ownerId, newSection));
    }

    [Fact]
    public async Task MoveRecipeToSectionAsync_NullSection_MovesToUnsectioned()
    {
        _repo.Setup(r => r.MoveRecipeToSectionAsync(_recipeEntryId, _ownerId, null)).ReturnsAsync(true);

        Assert.True(await _repo.Object.MoveRecipeToSectionAsync(_recipeEntryId, _ownerId, null));
    }

    [Fact]
    public async Task ReorderRecipesAsync_ReturnsTrueOnSuccess()
    {
        var ids = new List<Guid> { _recipeEntryId, Guid.NewGuid() };
        _repo.Setup(r => r.ReorderRecipesAsync(_cookbookId, _sectionId, ids)).ReturnsAsync(true);

        Assert.True(await _repo.Object.ReorderRecipesAsync(_cookbookId, _sectionId, ids));
    }

    // ── Ratings & comments ────────────────────────────────────────────────────

    [Fact]
    public async Task RateCookbookAsync_ReturnsTrueOnSuccess()
    {
        _repo.Setup(r => r.RateCookbookAsync(_cookbookId, _ownerId, 4)).ReturnsAsync(true);

        Assert.True(await _repo.Object.RateCookbookAsync(_cookbookId, _ownerId, 4));
    }

    [Fact]
    public async Task GetRatingsAsync_ReturnsAverageAndCount()
    {
        _repo.Setup(r => r.GetRatingsAsync(_cookbookId)).ReturnsAsync((4.5m, 12));

        var (avg, count) = await _repo.Object.GetRatingsAsync(_cookbookId);

        Assert.Equal(4.5m, avg);
        Assert.Equal(12, count);
    }

    [Fact]
    public async Task GetRatingsAsync_ZeroCountWhenNoRatings()
    {
        _repo.Setup(r => r.GetRatingsAsync(It.IsAny<Guid>())).ReturnsAsync((0m, 0));

        var (avg, count) = await _repo.Object.GetRatingsAsync(Guid.NewGuid());

        Assert.Equal(0m, avg);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task AddCommentAsync_ReturnsNewCommentGuid()
    {
        _repo.Setup(r => r.AddCommentAsync(_cookbookId, _ownerId, "Love it!")).ReturnsAsync(_commentId);

        var id = await _repo.Object.AddCommentAsync(_cookbookId, _ownerId, "Love it!");

        Assert.Equal(_commentId, id);
    }

    [Fact]
    public async Task GetCommentsAsync_ReturnsPaginatedComments()
    {
        var comments = new List<CookbookCommentDto>
        {
            new() { Id = _commentId, Content = "Delicious!", UserId = _ownerId, CookbookId = _cookbookId }
        };
        _repo.Setup(r => r.GetCommentsAsync(_cookbookId, 1, 20)).ReturnsAsync(comments);

        var result = await _repo.Object.GetCommentsAsync(_cookbookId, 1, 20);

        Assert.Single(result);
        Assert.Equal("Delicious!", result[0].Content);
    }

    [Fact]
    public async Task DeleteCommentAsync_ReturnsTrueForOwner()
    {
        _repo.Setup(r => r.DeleteCommentAsync(_commentId, _ownerId)).ReturnsAsync(true);

        Assert.True(await _repo.Object.DeleteCommentAsync(_commentId, _ownerId));
    }

    [Fact]
    public async Task DeleteCommentAsync_ReturnsFalseForNonOwner()
    {
        var other = Guid.NewGuid();
        _repo.Setup(r => r.DeleteCommentAsync(_commentId, other)).ReturnsAsync(false);

        Assert.False(await _repo.Object.DeleteCommentAsync(_commentId, other));
    }

    // ── Favorites ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task FavoriteCookbookAsync_ReturnsTrueOnSuccess()
    {
        _repo.Setup(r => r.FavoriteCookbookAsync(_cookbookId, _ownerId)).ReturnsAsync(true);

        Assert.True(await _repo.Object.FavoriteCookbookAsync(_cookbookId, _ownerId));
    }

    [Fact]
    public async Task UnfavoriteCookbookAsync_ReturnsTrueOnSuccess()
    {
        _repo.Setup(r => r.UnfavoriteCookbookAsync(_cookbookId, _ownerId)).ReturnsAsync(true);

        Assert.True(await _repo.Object.UnfavoriteCookbookAsync(_cookbookId, _ownerId));
    }

    [Fact]
    public async Task IsFavoritedAsync_ReturnsTrueWhenFavorited()
    {
        _repo.Setup(r => r.IsFavoritedAsync(_cookbookId, _ownerId)).ReturnsAsync(true);

        Assert.True(await _repo.Object.IsFavoritedAsync(_cookbookId, _ownerId));
    }

    [Fact]
    public async Task IsFavoritedAsync_ReturnsFalseWhenNotFavorited()
    {
        _repo.Setup(r => r.IsFavoritedAsync(_cookbookId, _ownerId)).ReturnsAsync(false);

        Assert.False(await _repo.Object.IsFavoritedAsync(_cookbookId, _ownerId));
    }

    [Fact]
    public async Task GetFavoriteCookbooksAsync_ReturnsList()
    {
        var favs = new List<CookbookSummaryDto> { new() { Id = _cookbookId, Title = "Fav" } };
        _repo.Setup(r => r.GetFavoriteCookbooksAsync(_ownerId, 1, 20)).ReturnsAsync(favs);

        var result = await _repo.Object.GetFavoriteCookbooksAsync(_ownerId, 1, 20);

        Assert.Single(result);
    }

    // ── Merge & split ─────────────────────────────────────────────────────────

    [Fact]
    public async Task MergeCookbooksAsync_ReturnsNewCookbookId()
    {
        var newId = Guid.NewGuid();
        var request = new MergeCookbooksRequest
        {
            SourceCookbookIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() },
            NewTitle = "Merged Collection"
        };
        _repo.Setup(r => r.MergeCookbooksAsync(_ownerId, request)).ReturnsAsync(newId);

        var result = await _repo.Object.MergeCookbooksAsync(_ownerId, request);

        Assert.Equal(newId, result);
    }

    [Fact]
    public async Task MergeCookbooksAsync_ThrowsWhenNotOwnerOfSource()
    {
        var other = Guid.NewGuid();
        _repo.Setup(r => r.MergeCookbooksAsync(other, It.IsAny<MergeCookbooksRequest>()))
            .ThrowsAsync(new UnauthorizedAccessException());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _repo.Object.MergeCookbooksAsync(other, new MergeCookbooksRequest()));
    }

    [Fact]
    public async Task SplitCookbookAsync_ReturnsListOfNewIds()
    {
        var newIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var request = new SplitCookbookRequest { SectionIds = new List<Guid> { _sectionId } };
        _repo.Setup(r => r.SplitCookbookAsync(_cookbookId, _ownerId, request)).ReturnsAsync(newIds);

        var result = await _repo.Object.SplitCookbookAsync(_cookbookId, _ownerId, request);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task ExtractRecipesFromCookbookAsync_ReturnsRecipeIds()
    {
        var recipeIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        _repo.Setup(r => r.ExtractRecipesFromCookbookAsync(_cookbookId, _ownerId)).ReturnsAsync(recipeIds);

        var result = await _repo.Object.ExtractRecipesFromCookbookAsync(_cookbookId, _ownerId);

        Assert.Equal(3, result.Count);
    }

    // ── Sharing ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ShareCookbookAsync_ReturnsTrueForOwner()
    {
        var target = Guid.NewGuid();
        _repo.Setup(r => r.ShareCookbookAsync(_cookbookId, _ownerId, target, false)).ReturnsAsync(true);

        Assert.True(await _repo.Object.ShareCookbookAsync(_cookbookId, _ownerId, target, false));
    }

    [Fact]
    public async Task ShareCookbookAsync_ReturnsFalseForNonOwner()
    {
        var other = Guid.NewGuid();
        _repo.Setup(r => r.ShareCookbookAsync(_cookbookId, other, It.IsAny<Guid>(), It.IsAny<bool>()))
            .ReturnsAsync(false);

        Assert.False(await _repo.Object.ShareCookbookAsync(_cookbookId, other, Guid.NewGuid(), true));
    }

    [Fact]
    public async Task RevokeCookbookShareAsync_ReturnsTrueForOwner()
    {
        var target = Guid.NewGuid();
        _repo.Setup(r => r.RevokeCookbookShareAsync(_cookbookId, _ownerId, target)).ReturnsAsync(true);

        Assert.True(await _repo.Object.RevokeCookbookShareAsync(_cookbookId, _ownerId, target));
    }

    // ── View tracking & ownership ─────────────────────────────────────────────

    [Fact]
    public async Task IncrementViewCountAsync_CompletesSuccessfully()
    {
        _repo.Setup(r => r.IncrementViewCountAsync(_cookbookId)).Returns(Task.CompletedTask);

        await _repo.Object.IncrementViewCountAsync(_cookbookId);
        _repo.Verify(r => r.IncrementViewCountAsync(_cookbookId), Times.Once);
    }

    [Fact]
    public async Task IsOwnerAsync_ReturnsTrueForOwner()
    {
        _repo.Setup(r => r.IsOwnerAsync(_cookbookId, _ownerId)).ReturnsAsync(true);

        Assert.True(await _repo.Object.IsOwnerAsync(_cookbookId, _ownerId));
    }

    [Fact]
    public async Task IsOwnerAsync_ReturnsFalseForNonOwner()
    {
        _repo.Setup(r => r.IsOwnerAsync(_cookbookId, Guid.NewGuid())).ReturnsAsync(false);

        Assert.False(await _repo.Object.IsOwnerAsync(_cookbookId, Guid.NewGuid()));
    }

    [Fact]
    public async Task CanViewAsync_ReturnsTrueForOwner()
    {
        _repo.Setup(r => r.CanViewAsync(_cookbookId, _ownerId)).ReturnsAsync(true);

        Assert.True(await _repo.Object.CanViewAsync(_cookbookId, _ownerId));
    }

    [Fact]
    public async Task CanViewAsync_ReturnsTrueForPublicCookbook()
    {
        var anon = Guid.NewGuid();
        _repo.Setup(r => r.CanViewAsync(_cookbookId, anon)).ReturnsAsync(true);

        Assert.True(await _repo.Object.CanViewAsync(_cookbookId, anon));
    }

    [Fact]
    public async Task CanViewAsync_ReturnsFalseForPrivateCookbookNotShared()
    {
        var stranger = Guid.NewGuid();
        _repo.Setup(r => r.CanViewAsync(_cookbookId, stranger)).ReturnsAsync(false);

        Assert.False(await _repo.Object.CanViewAsync(_cookbookId, stranger));
    }

    [Fact]
    public async Task CanEditAsync_ReturnsTrueForOwner()
    {
        _repo.Setup(r => r.CanEditAsync(_cookbookId, _ownerId)).ReturnsAsync(true);

        Assert.True(await _repo.Object.CanEditAsync(_cookbookId, _ownerId));
    }

    [Fact]
    public async Task CanEditAsync_ReturnsFalseForViewOnlyShare()
    {
        var viewer = Guid.NewGuid();
        _repo.Setup(r => r.CanEditAsync(_cookbookId, viewer)).ReturnsAsync(false);

        Assert.False(await _repo.Object.CanEditAsync(_cookbookId, viewer));
    }
}
