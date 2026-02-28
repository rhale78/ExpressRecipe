using ExpressRecipe.CookbookService.Controllers;
using ExpressRecipe.CookbookService.Data;
using ExpressRecipe.CookbookService.Models;
using ExpressRecipe.CookbookService.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ExpressRecipe.CookbookService.Tests.Controllers;

public class CookbookSectionsControllerTests
{
    private readonly Mock<ICookbookRepository> _mockRepo;
    private readonly Mock<ILogger<CookbookSectionsController>> _mockLogger;
    private readonly CookbookSectionsController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public CookbookSectionsControllerTests()
    {
        _mockRepo = new Mock<ICookbookRepository>();
        _mockLogger = new Mock<ILogger<CookbookSectionsController>>();
        _controller = new CookbookSectionsController(_mockRepo.Object, _mockLogger.Object);
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_userId);
    }

    // ── CreateSection ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateSection_Success_ReturnsOkWithId()
    {
        var cookbookId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var request = new CreateCookbookSectionRequest { Title = "Desserts" };
        _mockRepo.Setup(r => r.CreateSectionAsync(cookbookId, _userId, request)).ReturnsAsync(sectionId);

        var result = await _controller.CreateSection(cookbookId, request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task CreateSection_Unauthenticated_ReturnsUnauthorized()
    {
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();
        var request = new CreateCookbookSectionRequest { Title = "Desserts" };

        var result = await _controller.CreateSection(Guid.NewGuid(), request);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateSection_UnauthorizedAccessException_ReturnsForbid()
    {
        var cookbookId = Guid.NewGuid();
        var request = new CreateCookbookSectionRequest { Title = "Desserts" };
        _mockRepo.Setup(r => r.CreateSectionAsync(cookbookId, _userId, request))
            .ThrowsAsync(new UnauthorizedAccessException());

        var result = await _controller.CreateSection(cookbookId, request);

        Assert.IsType<ForbidResult>(result.Result);
    }

    // ── UpdateSection ─────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateSection_Success_ReturnsNoContent()
    {
        var cookbookId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var request = new UpdateCookbookSectionRequest { Title = "Updated Title" };
        _mockRepo.Setup(r => r.UpdateSectionAsync(sectionId, _userId, request)).ReturnsAsync(true);

        var result = await _controller.UpdateSection(cookbookId, sectionId, request);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task UpdateSection_NotFound_ReturnsNotFound()
    {
        var cookbookId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var request = new UpdateCookbookSectionRequest { Title = "Updated Title" };
        _mockRepo.Setup(r => r.UpdateSectionAsync(sectionId, _userId, request)).ReturnsAsync(false);

        var result = await _controller.UpdateSection(cookbookId, sectionId, request);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task UpdateSection_Unauthenticated_ReturnsUnauthorized()
    {
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();
        var request = new UpdateCookbookSectionRequest { Title = "Updated Title" };

        var result = await _controller.UpdateSection(Guid.NewGuid(), Guid.NewGuid(), request);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    // ── DeleteSection ─────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteSection_Success_ReturnsNoContent()
    {
        var cookbookId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        _mockRepo.Setup(r => r.DeleteSectionAsync(sectionId, _userId)).ReturnsAsync(true);

        var result = await _controller.DeleteSection(cookbookId, sectionId);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteSection_NotFound_ReturnsNotFound()
    {
        var cookbookId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        _mockRepo.Setup(r => r.DeleteSectionAsync(sectionId, _userId)).ReturnsAsync(false);

        var result = await _controller.DeleteSection(cookbookId, sectionId);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ── ReorderSections ───────────────────────────────────────────────────────

    [Fact]
    public async Task ReorderSections_Success_ReturnsNoContent()
    {
        var cookbookId = Guid.NewGuid();
        var ids = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var request = new ReorderRequest { Ids = ids };
        _mockRepo.Setup(r => r.ReorderSectionsAsync(cookbookId, _userId, ids)).ReturnsAsync(true);

        var result = await _controller.ReorderSections(cookbookId, request);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task ReorderSections_ReturnsFalse_ReturnsForbid()
    {
        var cookbookId = Guid.NewGuid();
        var ids = new List<Guid> { Guid.NewGuid() };
        var request = new ReorderRequest { Ids = ids };
        _mockRepo.Setup(r => r.ReorderSectionsAsync(cookbookId, _userId, ids)).ReturnsAsync(false);

        var result = await _controller.ReorderSections(cookbookId, request);

        Assert.IsType<ForbidResult>(result);
    }

    // ── AddRecipeToSection ────────────────────────────────────────────────────

    [Fact]
    public async Task AddRecipeToSection_Success_ReturnsOkWithId()
    {
        var cookbookId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var recipeEntryId = Guid.NewGuid();
        var request = new AddCookbookRecipeRequest { RecipeId = Guid.NewGuid(), RecipeName = "Pasta" };
        _mockRepo.Setup(r => r.AddRecipeToCookbookAsync(cookbookId, _userId, It.Is<AddCookbookRecipeRequest>(req => req.SectionId == sectionId)))
            .ReturnsAsync(recipeEntryId);

        var result = await _controller.AddRecipeToSection(cookbookId, sectionId, request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task AddRecipeToSection_SetsSectionIdFromRoute()
    {
        var cookbookId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var recipeEntryId = Guid.NewGuid();
        var request = new AddCookbookRecipeRequest { RecipeId = Guid.NewGuid(), RecipeName = "Pasta" };
        AddCookbookRecipeRequest? capturedRequest = null;
        _mockRepo.Setup(r => r.AddRecipeToCookbookAsync(cookbookId, _userId, It.IsAny<AddCookbookRecipeRequest>()))
            .Callback<Guid, Guid, AddCookbookRecipeRequest>((_, _, req) => capturedRequest = req)
            .ReturnsAsync(recipeEntryId);

        await _controller.AddRecipeToSection(cookbookId, sectionId, request);

        Assert.NotNull(capturedRequest);
        Assert.Equal(sectionId, capturedRequest!.SectionId);
    }

    [Fact]
    public async Task AddRecipeToSection_Unauthenticated_ReturnsUnauthorized()
    {
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();
        var request = new AddCookbookRecipeRequest { RecipeId = Guid.NewGuid(), RecipeName = "Pasta" };

        var result = await _controller.AddRecipeToSection(Guid.NewGuid(), Guid.NewGuid(), request);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    // ── AddRecipesBatch ───────────────────────────────────────────────────────

    [Fact]
    public async Task AddRecipesBatch_Success_ReturnsOkWithCountMessage()
    {
        var cookbookId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var recipes = new List<AddCookbookRecipeRequest>
        {
            new() { RecipeId = Guid.NewGuid(), RecipeName = "Recipe1" },
            new() { RecipeId = Guid.NewGuid(), RecipeName = "Recipe2" }
        };
        _mockRepo.Setup(r => r.AddRecipesBatchAsync(cookbookId, _userId, sectionId, recipes)).ReturnsAsync(true);

        var result = await _controller.AddRecipesBatch(cookbookId, sectionId, recipes);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task AddRecipesBatch_ReturnsFalse_ReturnsForbid()
    {
        var cookbookId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var recipes = new List<AddCookbookRecipeRequest>
        {
            new() { RecipeId = Guid.NewGuid(), RecipeName = "Recipe1" }
        };
        _mockRepo.Setup(r => r.AddRecipesBatchAsync(cookbookId, _userId, sectionId, recipes)).ReturnsAsync(false);

        var result = await _controller.AddRecipesBatch(cookbookId, sectionId, recipes);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task AddRecipesBatch_Unauthenticated_ReturnsUnauthorized()
    {
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();
        var recipes = new List<AddCookbookRecipeRequest>();

        var result = await _controller.AddRecipesBatch(Guid.NewGuid(), Guid.NewGuid(), recipes);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    // ── RemoveRecipe ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveRecipe_Success_ReturnsNoContent()
    {
        var cookbookId = Guid.NewGuid();
        var recipeEntryId = Guid.NewGuid();
        _mockRepo.Setup(r => r.RemoveRecipeFromCookbookAsync(recipeEntryId, _userId)).ReturnsAsync(true);

        var result = await _controller.RemoveRecipe(cookbookId, recipeEntryId);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task RemoveRecipe_NotFound_ReturnsNotFound()
    {
        var cookbookId = Guid.NewGuid();
        var recipeEntryId = Guid.NewGuid();
        _mockRepo.Setup(r => r.RemoveRecipeFromCookbookAsync(recipeEntryId, _userId)).ReturnsAsync(false);

        var result = await _controller.RemoveRecipe(cookbookId, recipeEntryId);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ── MoveRecipe ────────────────────────────────────────────────────────────

    [Fact]
    public async Task MoveRecipe_Success_ReturnsNoContent()
    {
        var cookbookId = Guid.NewGuid();
        var recipeEntryId = Guid.NewGuid();
        var newSectionId = Guid.NewGuid();
        var request = new MoveRecipeRequest { NewSectionId = newSectionId };
        _mockRepo.Setup(r => r.MoveRecipeToSectionAsync(recipeEntryId, _userId, newSectionId)).ReturnsAsync(true);

        var result = await _controller.MoveRecipe(cookbookId, recipeEntryId, request);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task MoveRecipe_NotFound_ReturnsNotFound()
    {
        var cookbookId = Guid.NewGuid();
        var recipeEntryId = Guid.NewGuid();
        var request = new MoveRecipeRequest { NewSectionId = null };
        _mockRepo.Setup(r => r.MoveRecipeToSectionAsync(recipeEntryId, _userId, null)).ReturnsAsync(false);

        var result = await _controller.MoveRecipe(cookbookId, recipeEntryId, request);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ── ReorderRecipes ────────────────────────────────────────────────────────

    [Fact]
    public async Task ReorderRecipes_Success_ReturnsNoContent()
    {
        var cookbookId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var ids = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var request = new ReorderRequest { Ids = ids };
        _mockRepo.Setup(r => r.ReorderRecipesAsync(cookbookId, sectionId, ids)).ReturnsAsync(true);

        var result = await _controller.ReorderRecipes(cookbookId, sectionId, request);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task ReorderRecipes_ReturnsFalse_Returns500()
    {
        var cookbookId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var ids = new List<Guid> { Guid.NewGuid() };
        var request = new ReorderRequest { Ids = ids };
        _mockRepo.Setup(r => r.ReorderRecipesAsync(cookbookId, sectionId, ids)).ReturnsAsync(false);

        var result = await _controller.ReorderRecipes(cookbookId, sectionId, request);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, obj.StatusCode);
    }
}
