using ExpressRecipe.Client.Shared.Models.Cookbook;
using ExpressRecipe.Client.Shared.Services;
using ExpressRecipe.Shared.Services;
using Moq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace ExpressRecipe.CookbookService.Tests.Client;

/// <summary>
/// Unit tests for <see cref="CookbookApiClient"/> using a <see cref="MockHttpMessageHandler"/>
/// to intercept HTTP calls without a live server.
/// </summary>
public class CookbookApiClientTests
{
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (CookbookApiClient Client, MockHttpMessageHandler Handler) CreateClient(
        string responseBody, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new MockHttpMessageHandler(responseBody, status);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var tokenProvider = new Mock<ITokenProvider>();
        tokenProvider.Setup(t => t.GetAccessTokenAsync()).ReturnsAsync("test-token");
        return (new CookbookApiClient(http, tokenProvider.Object), handler);
    }

    private static string Json<T>(T value) => JsonSerializer.Serialize(value, _json);

    // ── GetCookbookAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetCookbookAsync_Success_ReturnsCookbookDto()
    {
        var id = Guid.NewGuid();
        var dto = new CookbookDto { Id = id, Title = "Test Cookbook" };
        var (client, _) = CreateClient(Json(dto));

        var result = await client.GetCookbookAsync(id);

        Assert.NotNull(result);
        Assert.Equal(id, result!.Id);
        Assert.Equal("Test Cookbook", result.Title);
    }

    [Fact]
    public async Task GetCookbookAsync_NotFound_ReturnsNull()
    {
        var (client, _) = CreateClient("", HttpStatusCode.NotFound);

        var result = await client.GetCookbookAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    // ── GetCookbookBySlugAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetCookbookBySlugAsync_Success_ReturnsCookbookDto()
    {
        var dto = new CookbookDto { Id = Guid.NewGuid(), Title = "Slug Book", WebSlug = "slug-book" };
        var (client, handler) = CreateClient(Json(dto));

        var result = await client.GetCookbookBySlugAsync("slug-book");

        Assert.NotNull(result);
        Assert.Equal("slug-book", result!.WebSlug);
        Assert.Contains("/api/cookbooks/slug/slug-book", handler.LastRequestUri);
    }

    // ── SearchCookbooksAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task SearchCookbooksAsync_BuildsCorrectQueryString()
    {
        var searchResult = new CookbookSearchResult
        {
            Items = new List<CookbookSummaryDto> { new() { Title = "Match" } },
            TotalCount = 1, Page = 1, PageSize = 10
        };
        var (client, handler) = CreateClient(Json(searchResult));

        var result = await client.SearchCookbooksAsync(new CookbookSearchRequest
        {
            SearchTerm = "pasta",
            Visibility = "Public",
            Page = 1,
            PageSize = 10
        });

        Assert.NotNull(result);
        Assert.Single(result!.Items);
        Assert.Contains("searchTerm=pasta", handler.LastRequestUri);
        Assert.Contains("visibility=Public", handler.LastRequestUri);
    }

    // ── CreateCookbookAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task CreateCookbookAsync_Success_ReturnsNewId()
    {
        var newId = Guid.NewGuid();
        var (client, handler) = CreateClient(Json(new { id = newId }), HttpStatusCode.Created);

        var result = await client.CreateCookbookAsync(new CreateCookbookRequest { Title = "New Book" });

        Assert.Equal(newId, result);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Contains("/api/cookbooks", handler.LastRequestUri);
    }

    [Fact]
    public async Task CreateCookbookAsync_ServerError_ReturnsNull()
    {
        var (client, _) = CreateClient("{\"message\":\"error\"}", HttpStatusCode.InternalServerError);

        await Assert.ThrowsAsync<ApiException>(() =>
            client.CreateCookbookAsync(new CreateCookbookRequest { Title = "Book" }));
    }

    // ── UpdateCookbookAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task UpdateCookbookAsync_Success_ReturnsTrue()
    {
        var id = Guid.NewGuid();
        var (client, handler) = CreateClient("", HttpStatusCode.NoContent);

        var result = await client.UpdateCookbookAsync(id, new UpdateCookbookRequest { Title = "Updated" });

        Assert.True(result);
        Assert.Equal(HttpMethod.Put, handler.LastMethod);
        Assert.Contains($"/api/cookbooks/{id}", handler.LastRequestUri);
    }

    [Fact]
    public async Task UpdateCookbookAsync_NotFound_ReturnsFalse()
    {
        var (client, _) = CreateClient("", HttpStatusCode.NotFound);

        var result = await client.UpdateCookbookAsync(Guid.NewGuid(), new UpdateCookbookRequest());

        Assert.False(result);
    }

    // ── DeleteCookbookAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task DeleteCookbookAsync_Success_ReturnsTrue()
    {
        var id = Guid.NewGuid();
        var (client, handler) = CreateClient("", HttpStatusCode.NoContent);

        var result = await client.DeleteCookbookAsync(id);

        Assert.True(result);
        Assert.Equal(HttpMethod.Delete, handler.LastMethod);
        Assert.Contains($"/api/cookbooks/{id}", handler.LastRequestUri);
    }

    // ── FavoriteCookbookAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task FavoriteCookbookAsync_Success_ReturnsTrue()
    {
        var id = Guid.NewGuid();
        var (client, handler) = CreateClient(Json(new { message = "ok" }));

        var result = await client.FavoriteCookbookAsync(id);

        Assert.True(result);
        Assert.Contains($"/api/cookbooks/{id}/favorite", handler.LastRequestUri);
    }

    // ── UnfavoriteCookbookAsync ───────────────────────────────────────────────

    [Fact]
    public async Task UnfavoriteCookbookAsync_Success_ReturnsTrue()
    {
        var id = Guid.NewGuid();
        var (client, handler) = CreateClient("", HttpStatusCode.OK);

        var result = await client.UnfavoriteCookbookAsync(id);

        Assert.True(result);
        Assert.Equal(HttpMethod.Delete, handler.LastMethod);
    }

    // ── RateCookbookAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task RateCookbookAsync_Success_ReturnsTrue()
    {
        var id = Guid.NewGuid();
        var (client, handler) = CreateClient(Json(new { averageRating = 4.2, ratingCount = 5 }));

        var result = await client.RateCookbookAsync(id, 4);

        Assert.True(result);
        Assert.Contains($"/api/cookbooks/{id}/rate", handler.LastRequestUri);
    }

    // ── GetCommentsAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetCommentsAsync_Success_ReturnsCommentList()
    {
        var id = Guid.NewGuid();
        var comments = new List<CookbookCommentDto>
        {
            new() { Id = Guid.NewGuid(), Content = "Great cookbook!", UserId = Guid.NewGuid(), CookbookId = id }
        };
        var (client, _) = CreateClient(Json(comments));

        var result = await client.GetCommentsAsync(id);

        Assert.NotNull(result);
        Assert.Single(result!);
        Assert.Equal("Great cookbook!", result![0].Content);
    }

    // ── AddCommentAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task AddCommentAsync_Success_ReturnsCommentId()
    {
        var id = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var (client, _) = CreateClient(Json(new { id = commentId }));

        var result = await client.AddCommentAsync(id, "Delicious!");

        Assert.Equal(commentId, result);
    }

    // ── DeleteCommentAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteCommentAsync_Success_ReturnsTrue()
    {
        var cookbookId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var (client, handler) = CreateClient("", HttpStatusCode.NoContent);

        var result = await client.DeleteCommentAsync(cookbookId, commentId);

        Assert.True(result);
        Assert.Equal(HttpMethod.Delete, handler.LastMethod);
        Assert.Contains($"/api/cookbooks/{cookbookId}/comments/{commentId}", handler.LastRequestUri);
    }

    // ── Section methods ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateSectionAsync_Success_ReturnsSectionId()
    {
        var cookbookId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var (client, handler) = CreateClient(Json(new { id = sectionId }));

        var result = await client.CreateSectionAsync(cookbookId, new CreateCookbookSectionRequest { Title = "Starters" });

        Assert.Equal(sectionId, result);
        Assert.Contains($"/api/cookbooks/{cookbookId}/sections", handler.LastRequestUri);
    }

    [Fact]
    public async Task UpdateSectionAsync_Success_ReturnsTrue()
    {
        var cookbookId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var (client, handler) = CreateClient("", HttpStatusCode.NoContent);

        var result = await client.UpdateSectionAsync(cookbookId, sectionId,
            new UpdateCookbookSectionRequest { Title = "Mains" });

        Assert.True(result);
        Assert.Contains($"/api/cookbooks/{cookbookId}/sections/{sectionId}", handler.LastRequestUri);
    }

    [Fact]
    public async Task DeleteSectionAsync_Success_ReturnsTrue()
    {
        var cookbookId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var (client, handler) = CreateClient("", HttpStatusCode.NoContent);

        var result = await client.DeleteSectionAsync(cookbookId, sectionId);

        Assert.True(result);
        Assert.Equal(HttpMethod.Delete, handler.LastMethod);
    }

    [Fact]
    public async Task ReorderSectionsAsync_Success_ReturnsTrue()
    {
        var cookbookId = Guid.NewGuid();
        var ids = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var (client, handler) = CreateClient("", HttpStatusCode.NoContent);

        var result = await client.ReorderSectionsAsync(cookbookId, ids);

        Assert.True(result);
        Assert.Contains($"/api/cookbooks/{cookbookId}/sections/reorder", handler.LastRequestUri);
    }

    // ── Recipe methods ────────────────────────────────────────────────────────

    [Fact]
    public async Task AddRecipeToCookbookAsync_Success_ReturnsEntryId()
    {
        var cookbookId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var (client, _) = CreateClient(Json(new { id = entryId }));
        var request = new AddCookbookRecipeRequest { RecipeId = Guid.NewGuid(), RecipeName = "Carbonara" };

        var result = await client.AddRecipeToCookbookAsync(cookbookId, sectionId, request);

        Assert.Equal(entryId, result);
    }

    [Fact]
    public async Task AddRecipesBatchAsync_Success_ReturnsTrue()
    {
        var cookbookId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var recipes = new List<AddCookbookRecipeRequest>
        {
            new() { RecipeId = Guid.NewGuid(), RecipeName = "Soup" },
            new() { RecipeId = Guid.NewGuid(), RecipeName = "Bread" }
        };
        var (client, handler) = CreateClient(Json(new { message = "2 recipes added" }));

        var result = await client.AddRecipesBatchAsync(cookbookId, sectionId, recipes);

        Assert.True(result);
        Assert.Contains("recipes/batch", handler.LastRequestUri);
    }

    [Fact]
    public async Task RemoveRecipeFromCookbookAsync_Success_ReturnsTrue()
    {
        var cookbookId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var (client, handler) = CreateClient("", HttpStatusCode.NoContent);

        var result = await client.RemoveRecipeFromCookbookAsync(cookbookId, entryId);

        Assert.True(result);
        Assert.Equal(HttpMethod.Delete, handler.LastMethod);
    }

    [Fact]
    public async Task MoveRecipeAsync_Success_ReturnsTrue()
    {
        var cookbookId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var newSection = Guid.NewGuid();
        var (client, handler) = CreateClient("", HttpStatusCode.NoContent);

        var result = await client.MoveRecipeAsync(cookbookId, entryId, newSection);

        Assert.True(result);
        Assert.Contains($"/move", handler.LastRequestUri);
    }

    // ── Advanced operations ───────────────────────────────────────────────────

    [Fact]
    public async Task MergeCookbooksAsync_Success_ReturnsNewId()
    {
        var newId = Guid.NewGuid();
        var (client, handler) = CreateClient(Json(new { id = newId }), HttpStatusCode.Created);
        var request = new MergeCookbooksRequest
        {
            SourceCookbookIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() },
            NewTitle = "Merged"
        };

        var result = await client.MergeCookbooksAsync(request);

        Assert.Equal(newId, result);
        Assert.Contains("/api/cookbooks/merge", handler.LastRequestUri);
    }

    [Fact]
    public async Task SplitCookbookAsync_Success_ReturnsNewIds()
    {
        var id = Guid.NewGuid();
        var newIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var (client, _) = CreateClient(Json(newIds));
        var request = new SplitCookbookRequest { SectionIds = new List<Guid> { Guid.NewGuid() } };

        var result = await client.SplitCookbookAsync(id, request);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
    }

    [Fact]
    public async Task ExtractRecipesAsync_Success_ReturnsRecipeIds()
    {
        var id = Guid.NewGuid();
        var recipeIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var (client, _) = CreateClient(Json(recipeIds));

        var result = await client.ExtractRecipesAsync(id);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
    }

    // ── Sharing ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ShareCookbookAsync_Success_ReturnsTrue()
    {
        var id = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var (client, handler) = CreateClient(Json(new { message = "shared" }));

        var result = await client.ShareCookbookAsync(id, targetId, false);

        Assert.True(result);
        Assert.Contains($"/api/cookbooks/{id}/share", handler.LastRequestUri);
    }

    [Fact]
    public async Task RevokeShareAsync_Success_ReturnsTrue()
    {
        var id = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var (client, handler) = CreateClient("", HttpStatusCode.NoContent);

        var result = await client.RevokeShareAsync(id, targetId);

        Assert.True(result);
        Assert.Equal(HttpMethod.Delete, handler.LastMethod);
        Assert.Contains($"/api/cookbooks/{id}/share/{targetId}", handler.LastRequestUri);
    }

    // ── Export / Web view ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetPrintPreviewAsync_Success_ReturnsHtmlString()
    {
        var id = Guid.NewGuid();
        var html = "<html><body><h1>My Cookbook</h1></body></html>";
        var (client, handler) = CreateClient($"\"{html}\""); // string is JSON-quoted

        var result = await client.GetPrintPreviewAsync(id);

        Assert.NotNull(result);
        Assert.Contains("/api/cookbooks/", handler.LastRequestUri);
        Assert.Contains("/print-preview", handler.LastRequestUri);
    }

    [Fact]
    public async Task ExportPdfAsync_ReturnsNull_PendingImplementation()
    {
        var (client, _) = CreateClient("{}");

        var result = await client.ExportPdfAsync(Guid.NewGuid());

        // Currently returns null (PDF library not yet integrated)
        Assert.Null(result);
    }

    [Fact]
    public async Task ExportWordAsync_ReturnsNull_PendingImplementation()
    {
        var (client, _) = CreateClient("{}");

        var result = await client.ExportWordAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    // ── GetMyCookbooksAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetMyCookbooksAsync_Success_ReturnsItems()
    {
        var searchResult = new CookbookSearchResult
        {
            Items = new List<CookbookSummaryDto>
            {
                new() { Id = Guid.NewGuid(), Title = "My Book" }
            },
            TotalCount = 1, Page = 1, PageSize = 20
        };
        var (client, handler) = CreateClient(Json(searchResult));

        var result = await client.GetMyCookbooksAsync();

        Assert.NotNull(result);
        Assert.Single(result!);
        Assert.Contains("page=1", handler.LastRequestUri);
    }

    // ── GetFavoriteCookbooksAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetFavoriteCookbooksAsync_Success_ReturnsFavorites()
    {
        var favs = new List<CookbookSummaryDto>
        {
            new() { Id = Guid.NewGuid(), Title = "Fav Cookbook", IsUserFavorite = true }
        };
        var (client, handler) = CreateClient(Json(favs));

        var result = await client.GetFavoriteCookbooksAsync();

        Assert.NotNull(result);
        Assert.Single(result!);
        Assert.Contains("/api/cookbooks/favorites", handler.LastRequestUri);
    }
}

/// <summary>
/// Minimal <see cref="HttpMessageHandler"/> that returns a preconfigured response
/// and records the most-recent request for assertion.
/// </summary>
internal sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly string _responseBody;
    private readonly HttpStatusCode _statusCode;

    public string LastRequestUri { get; private set; } = string.Empty;
    public HttpMethod LastMethod { get; private set; } = HttpMethod.Get;

    public MockHttpMessageHandler(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _responseBody = responseBody;
        _statusCode = statusCode;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequestUri = request.RequestUri?.ToString() ?? string.Empty;
        LastMethod = request.Method;

        var response = new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}
