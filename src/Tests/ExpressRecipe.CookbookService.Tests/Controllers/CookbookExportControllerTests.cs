using ExpressRecipe.CookbookService.Controllers;
using ExpressRecipe.CookbookService.Data;
using ExpressRecipe.CookbookService.Models;
using ExpressRecipe.CookbookService.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ExpressRecipe.CookbookService.Tests.Controllers;

public class CookbookExportControllerTests
{
    private readonly Mock<ICookbookRepository> _mockRepo;
    private readonly Mock<ILogger<CookbookExportController>> _mockLogger;
    private readonly CookbookExportController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public CookbookExportControllerTests()
    {
        _mockRepo = new Mock<ICookbookRepository>();
        _mockLogger = new Mock<ILogger<CookbookExportController>>();
        _controller = new CookbookExportController(_mockRepo.Object, _mockLogger.Object);
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_userId);
    }

    private static CookbookDto BuildCookbook(Guid id, string title, string visibility = "Public") =>
        new()
        {
            Id = id,
            Title = title,
            Visibility = visibility,
            Sections = new List<CookbookSectionDto>(),
            UnsectionedRecipes = new List<CookbookRecipeDto>()
        };

    // ── PrintPreview ──────────────────────────────────────────────────────────

    [Fact]
    public async Task PrintPreview_PublicCookbookNoAuth_ReturnsHtmlContent()
    {
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();
        var id = Guid.NewGuid();
        var cookbook = BuildCookbook(id, "Public Cookbook");
        _mockRepo.Setup(r => r.GetCookbookByIdAsync(id, true)).ReturnsAsync(cookbook);

        var result = await _controller.PrintPreview(id);

        var content = Assert.IsType<ContentResult>(result.Result);
        Assert.Equal("text/html", content.ContentType);
        Assert.NotNull(content.Content);
    }

    [Fact]
    public async Task PrintPreview_PrivateCookbookUserCanView_ReturnsHtmlContent()
    {
        var id = Guid.NewGuid();
        var cookbook = BuildCookbook(id, "Private Cookbook", "Private");
        _mockRepo.Setup(r => r.GetCookbookByIdAsync(id, true)).ReturnsAsync(cookbook);
        _mockRepo.Setup(r => r.CanViewAsync(id, _userId)).ReturnsAsync(true);

        var result = await _controller.PrintPreview(id);

        var content = Assert.IsType<ContentResult>(result.Result);
        Assert.Equal("text/html", content.ContentType);
    }

    [Fact]
    public async Task PrintPreview_PrivateCookbookUserCannotView_ReturnsForbid()
    {
        var id = Guid.NewGuid();
        var cookbook = BuildCookbook(id, "Private Cookbook", "Private");
        _mockRepo.Setup(r => r.GetCookbookByIdAsync(id, true)).ReturnsAsync(cookbook);
        _mockRepo.Setup(r => r.CanViewAsync(id, _userId)).ReturnsAsync(false);

        var result = await _controller.PrintPreview(id);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task PrintPreview_CookbookNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetCookbookByIdAsync(id, true)).ReturnsAsync((CookbookDto?)null);

        var result = await _controller.PrintPreview(id);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task PrintPreview_HtmlContainsCookbookTitle()
    {
        var id = Guid.NewGuid();
        var cookbook = BuildCookbook(id, "Amazing Recipes");
        _mockRepo.Setup(r => r.GetCookbookByIdAsync(id, true)).ReturnsAsync(cookbook);

        var result = await _controller.PrintPreview(id);

        var content = Assert.IsType<ContentResult>(result.Result);
        Assert.Contains("Amazing Recipes", content.Content);
    }

    [Fact]
    public async Task PrintPreview_HtmlContainsSectionTitlesAndRecipeNames()
    {
        var id = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var cookbook = new CookbookDto
        {
            Id = id,
            Title = "My Cookbook",
            Visibility = "Public",
            Sections = new List<CookbookSectionDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Title = "Soups Section",
                    Recipes = new List<CookbookRecipeDto>
                    {
                        new() { Id = recipeId, RecipeName = "Tomato Soup" }
                    }
                }
            },
            UnsectionedRecipes = new List<CookbookRecipeDto>()
        };
        _mockRepo.Setup(r => r.GetCookbookByIdAsync(id, true)).ReturnsAsync(cookbook);

        var result = await _controller.PrintPreview(id);

        var content = Assert.IsType<ContentResult>(result.Result);
        Assert.Contains("Soups Section", content.Content);
        Assert.Contains("Tomato Soup", content.Content);
    }

    // ── HTML generation via PrintPreview ──────────────────────────────────────

    [Fact]
    public async Task PrintPreview_IncludesTitlePageByDefault()
    {
        var id = Guid.NewGuid();
        var cookbook = BuildCookbook(id, "Title Page Test");
        _mockRepo.Setup(r => r.GetCookbookByIdAsync(id, true)).ReturnsAsync(cookbook);

        var result = await _controller.PrintPreview(id);

        var content = Assert.IsType<ContentResult>(result.Result);
        Assert.Contains("<h1>", content.Content);
    }

    [Fact]
    public async Task PrintPreview_RendersIntroductionWhenSet()
    {
        var id = Guid.NewGuid();
        var cookbook = BuildCookbook(id, "Intro Test");
        cookbook.IntroductionContent = "<p>Welcome to my cookbook!</p>";
        _mockRepo.Setup(r => r.GetCookbookByIdAsync(id, true)).ReturnsAsync(cookbook);

        var result = await _controller.PrintPreview(id);

        var content = Assert.IsType<ContentResult>(result.Result);
        Assert.Contains("Introduction", content.Content);
        Assert.Contains("Welcome to my cookbook!", content.Content);
    }

    [Fact]
    public async Task PrintPreview_RendersSectionsCorrectly()
    {
        var id = Guid.NewGuid();
        var cookbook = new CookbookDto
        {
            Id = id,
            Title = "Sections Test",
            Visibility = "Public",
            Sections = new List<CookbookSectionDto>
            {
                new() { Id = Guid.NewGuid(), Title = "Mains", Recipes = new List<CookbookRecipeDto>() },
                new() { Id = Guid.NewGuid(), Title = "Desserts", Recipes = new List<CookbookRecipeDto>() }
            },
            UnsectionedRecipes = new List<CookbookRecipeDto>()
        };
        _mockRepo.Setup(r => r.GetCookbookByIdAsync(id, true)).ReturnsAsync(cookbook);

        var result = await _controller.PrintPreview(id);

        var content = Assert.IsType<ContentResult>(result.Result);
        Assert.Contains("Mains", content.Content);
        Assert.Contains("Desserts", content.Content);
    }

    [Fact]
    public async Task PrintPreview_RendersUnsectionedRecipes()
    {
        var id = Guid.NewGuid();
        var cookbook = new CookbookDto
        {
            Id = id,
            Title = "Unsectioned Test",
            Visibility = "Public",
            Sections = new List<CookbookSectionDto>(),
            UnsectionedRecipes = new List<CookbookRecipeDto>
            {
                new() { Id = Guid.NewGuid(), RecipeName = "Standalone Pie" }
            }
        };
        _mockRepo.Setup(r => r.GetCookbookByIdAsync(id, true)).ReturnsAsync(cookbook);

        var result = await _controller.PrintPreview(id);

        var content = Assert.IsType<ContentResult>(result.Result);
        Assert.Contains("Standalone Pie", content.Content);
    }

    [Fact]
    public async Task PrintPreview_RendersNotesWhenSet()
    {
        var id = Guid.NewGuid();
        var cookbook = BuildCookbook(id, "Notes Test");
        cookbook.NotesContent = "<p>Chef notes here.</p>";
        _mockRepo.Setup(r => r.GetCookbookByIdAsync(id, true)).ReturnsAsync(cookbook);

        var result = await _controller.PrintPreview(id);

        var content = Assert.IsType<ContentResult>(result.Result);
        Assert.Contains("Notes", content.Content);
        Assert.Contains("Chef notes here.", content.Content);
    }

    [Fact]
    public async Task PrintPreview_HtmlEncodesSpecialCharactersInTitle()
    {
        var id = Guid.NewGuid();
        var cookbook = BuildCookbook(id, "<script>alert('xss')</script>");
        _mockRepo.Setup(r => r.GetCookbookByIdAsync(id, true)).ReturnsAsync(cookbook);

        var result = await _controller.PrintPreview(id);

        var content = Assert.IsType<ContentResult>(result.Result);
        Assert.DoesNotContain("<script>alert('xss')</script>", content.Content);
        Assert.Contains("&lt;script&gt;", content.Content);
    }

    // ── ExportPdf ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExportPdf_Success_ReturnsFileWithHtmlContentType()
    {
        var id = Guid.NewGuid();
        var cookbook = BuildCookbook(id, "Export Cookbook", "Private");
        _mockRepo.Setup(r => r.GetCookbookByIdAsync(id, true)).ReturnsAsync(cookbook);
        _mockRepo.Setup(r => r.CanViewAsync(id, _userId)).ReturnsAsync(true);

        var result = await _controller.ExportPdf(id, null);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/html", file.ContentType);
        Assert.NotEmpty(file.FileContents);
    }

    [Fact]
    public async Task ExportPdf_CookbookNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetCookbookByIdAsync(id, true)).ReturnsAsync((CookbookDto?)null);

        var result = await _controller.ExportPdf(id, null);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ExportPdf_CannotView_ReturnsForbid()
    {
        var id = Guid.NewGuid();
        var cookbook = BuildCookbook(id, "Forbidden Cookbook", "Private");
        _mockRepo.Setup(r => r.GetCookbookByIdAsync(id, true)).ReturnsAsync(cookbook);
        _mockRepo.Setup(r => r.CanViewAsync(id, _userId)).ReturnsAsync(false);

        var result = await _controller.ExportPdf(id, null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task ExportPdf_Unauthenticated_ReturnsUnauthorized()
    {
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        var result = await _controller.ExportPdf(Guid.NewGuid(), null);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    // ── ExportWord ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExportWord_Success_ReturnsFileWithHtmlContentType()
    {
        var id = Guid.NewGuid();
        var cookbook = BuildCookbook(id, "Word Export Cookbook", "Private");
        _mockRepo.Setup(r => r.GetCookbookByIdAsync(id, true)).ReturnsAsync(cookbook);
        _mockRepo.Setup(r => r.CanViewAsync(id, _userId)).ReturnsAsync(true);

        var result = await _controller.ExportWord(id, null);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/html", file.ContentType);
        Assert.NotEmpty(file.FileContents);
    }

    [Fact]
    public async Task ExportWord_CookbookNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetCookbookByIdAsync(id, true)).ReturnsAsync((CookbookDto?)null);

        var result = await _controller.ExportWord(id, null);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ExportWord_Unauthenticated_ReturnsUnauthorized()
    {
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        var result = await _controller.ExportWord(Guid.NewGuid(), null);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }
}
