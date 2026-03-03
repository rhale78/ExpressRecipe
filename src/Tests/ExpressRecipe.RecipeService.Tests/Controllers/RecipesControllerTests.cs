using ExpressRecipe.RecipeService.Controllers;
using ExpressRecipe.RecipeService.Data;
using ExpressRecipe.RecipeService.Services;
using ExpressRecipe.RecipeService.Tests.Helpers;
using ExpressRecipe.Shared.DTOs.Recipe;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using ControllerSearchResult = ExpressRecipe.RecipeService.Controllers.RecipeSearchResult;

namespace ExpressRecipe.RecipeService.Tests.Controllers;

public class RecipesControllerTests
{
    private readonly Mock<IRecipeRepository> _mockRepo;
    private readonly Mock<ILogger<RecipesController>> _mockLogger;
    private readonly ServingSizeService _servingSizeService;
    private readonly ShoppingListIntegrationService _shoppingListService;
    private readonly RecipesController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public RecipesControllerTests()
    {
        _mockRepo = new Mock<IRecipeRepository>();
        _mockLogger = new Mock<ILogger<RecipesController>>();
        _servingSizeService = new ServingSizeService();
        _shoppingListService = new ShoppingListIntegrationService();

        _controller = new RecipesController(
            _mockRepo.Object,
            _servingSizeService,
            _shoppingListService,
            _mockLogger.Object);

        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_userId);
    }

    // ── GetRecipe ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRecipe_WhenFound_ReturnsOkWithRecipe()
    {
        var id = Guid.NewGuid();
        var recipe = new RecipeDto { Id = id, Name = "Pasta", AuthorId = _userId };
        _mockRepo.Setup(r => r.GetRecipeByIdAsync(id)).ReturnsAsync(recipe);
        _mockRepo.Setup(r => r.GetRecipeIngredientsAsync(id)).ReturnsAsync(new List<RecipeIngredientDto>());
        _mockRepo.Setup(r => r.GetRecipeNutritionAsync(id)).ReturnsAsync((RecipeNutritionDto?)null);
        _mockRepo.Setup(r => r.GetRecipeTagsAsync(id)).ReturnsAsync(new List<string>());
        _mockRepo.Setup(r => r.GetRecipeAllergensAsync(id)).ReturnsAsync(new List<RecipeAllergenWarningDto>());
        _mockRepo.Setup(r => r.GetAverageRatingAsync(id)).ReturnsAsync((4.5m, 10));

        var result = await _controller.GetRecipe(id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<RecipeDto>(ok.Value);
        dto.Id.Should().Be(id);
        dto.AverageRating.Should().Be(4.5m);
    }

    [Fact]
    public async Task GetRecipe_WhenNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetRecipeByIdAsync(id)).ReturnsAsync((RecipeDto?)null);

        var result = await _controller.GetRecipe(id);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetRecipe_LoadsIngredients_TagsAndAllergens()
    {
        var id = Guid.NewGuid();
        var recipe = new RecipeDto { Id = id, Name = "Soup", AuthorId = _userId };
        var ingredients = new List<RecipeIngredientDto> { new() { IngredientName = "Tomato" } };
        var tags = new List<string> { "Vegan", "Healthy" };
        var allergens = new List<RecipeAllergenWarningDto> { new() { AllergenName = "Nuts" } };

        _mockRepo.Setup(r => r.GetRecipeByIdAsync(id)).ReturnsAsync(recipe);
        _mockRepo.Setup(r => r.GetRecipeIngredientsAsync(id)).ReturnsAsync(ingredients);
        _mockRepo.Setup(r => r.GetRecipeNutritionAsync(id)).ReturnsAsync((RecipeNutritionDto?)null);
        _mockRepo.Setup(r => r.GetRecipeTagsAsync(id)).ReturnsAsync(tags);
        _mockRepo.Setup(r => r.GetRecipeAllergensAsync(id)).ReturnsAsync(allergens);
        _mockRepo.Setup(r => r.GetAverageRatingAsync(id)).ReturnsAsync((0m, 0));

        var result = await _controller.GetRecipe(id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<RecipeDto>(ok.Value);
        dto.Ingredients.Should().HaveCount(1);
        dto.Tags.Should().HaveCount(2);
        dto.AllergenWarnings.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetRecipe_RepositoryThrows_Returns500()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetRecipeByIdAsync(id)).ThrowsAsync(new Exception("DB error"));

        var result = await _controller.GetRecipe(id);

        var obj = Assert.IsType<ObjectResult>(result.Result);
        obj.StatusCode.Should().Be(500);
    }

    // ── SearchRecipes ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchRecipes_NoSearchTerm_ReturnsAllRecipes()
    {
        var recipes = new List<RecipeDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Pasta" },
            new() { Id = Guid.NewGuid(), Name = "Salad" }
        };
        _mockRepo.Setup(r => r.GetAllRecipesAsync(20, 0)).ReturnsAsync(recipes);

        var result = await _controller.SearchRecipes();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var searchResult = Assert.IsType<ControllerSearchResult>(ok.Value);
        searchResult.Recipes.Should().HaveCount(2);
        searchResult.Page.Should().Be(1);
        searchResult.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task SearchRecipes_WithSearchTerm_CallsSearchMethod()
    {
        var recipes = new List<RecipeDto> { new() { Id = Guid.NewGuid(), Name = "Tomato Soup" } };
        _mockRepo.Setup(r => r.SearchRecipesAsync("tomato", 20, 0)).ReturnsAsync(recipes);

        var result = await _controller.SearchRecipes(searchTerm: "tomato");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var searchResult = Assert.IsType<ControllerSearchResult>(ok.Value);
        searchResult.Recipes.Should().HaveCount(1);
        _mockRepo.Verify(r => r.SearchRecipesAsync("tomato", 20, 0), Times.Once);
    }

    [Fact]
    public async Task SearchRecipes_WithCategoryFilter_FiltersResults()
    {
        var recipes = new List<RecipeDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Pasta", Category = "Italian" },
            new() { Id = Guid.NewGuid(), Name = "Tacos", Category = "Mexican" }
        };
        _mockRepo.Setup(r => r.GetAllRecipesAsync(20, 0)).ReturnsAsync(recipes);

        var result = await _controller.SearchRecipes(category: "Italian");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var searchResult = Assert.IsType<ControllerSearchResult>(ok.Value);
        searchResult.Recipes.Should().HaveCount(1);
        searchResult.Recipes[0].Category.Should().Be("Italian");
    }

    [Fact]
    public async Task SearchRecipes_PageBelowOne_NormalizesToPage1()
    {
        _mockRepo.Setup(r => r.GetAllRecipesAsync(20, 0)).ReturnsAsync(new List<RecipeDto>());

        var result = await _controller.SearchRecipes(page: -5);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var searchResult = Assert.IsType<ControllerSearchResult>(ok.Value);
        searchResult.Page.Should().Be(1);
    }

    [Fact]
    public async Task SearchRecipes_PageSizeExceedsMax_ClampsTo100()
    {
        _mockRepo.Setup(r => r.GetAllRecipesAsync(100, 0)).ReturnsAsync(new List<RecipeDto>());

        var result = await _controller.SearchRecipes(pageSize: 500);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var searchResult = Assert.IsType<ControllerSearchResult>(ok.Value);
        searchResult.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task SearchRecipes_RepositoryThrows_Returns500()
    {
        _mockRepo.Setup(r => r.GetAllRecipesAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ThrowsAsync(new Exception("DB error"));

        var result = await _controller.SearchRecipes();

        var obj = Assert.IsType<ObjectResult>(result.Result);
        obj.StatusCode.Should().Be(500);
    }

    // ── CreateRecipe ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateRecipe_Authenticated_ReturnsCreatedResult()
    {
        var newId = Guid.NewGuid();
        var request = new CreateRecipeRequest { Name = "New Pasta" };
        _mockRepo.Setup(r => r.CreateRecipeAsync(It.IsAny<CreateRecipeRequest>(), _userId))
            .ReturnsAsync(newId);

        var result = await _controller.CreateRecipe(request);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        created.StatusCode.Should().Be(201);
    }

    [Fact]
    public async Task CreateRecipe_Unauthenticated_ReturnsUnauthorized()
    {
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();
        var request = new CreateRecipeRequest { Name = "New Pasta" };

        var result = await _controller.CreateRecipe(request);

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task CreateRecipe_WithIngredients_AddsIngredients()
    {
        var newId = Guid.NewGuid();
        var request = new CreateRecipeRequest
        {
            Name = "Pasta",
            Ingredients = new List<CreateRecipeIngredientRequest>
            {
                new() { Name = "Flour", Quantity = 2, Unit = "cups" }
            }
        };
        _mockRepo.Setup(r => r.CreateRecipeAsync(It.IsAny<CreateRecipeRequest>(), _userId))
            .ReturnsAsync(newId);
        _mockRepo.Setup(r => r.AddRecipeIngredientsAsync(newId, It.IsAny<List<RecipeIngredientDto>>(), _userId))
            .Returns(Task.CompletedTask);

        var result = await _controller.CreateRecipe(request);

        result.Result.Should().BeOfType<CreatedAtActionResult>();
        _mockRepo.Verify(r => r.AddRecipeIngredientsAsync(newId, It.IsAny<List<RecipeIngredientDto>>(), _userId), Times.Once);
    }

    [Fact]
    public async Task CreateRecipe_WithTags_AddsTags()
    {
        var newId = Guid.NewGuid();
        var request = new CreateRecipeRequest
        {
            Name = "Pasta",
            Tags = new List<string> { "Italian", "Easy" }
        };
        _mockRepo.Setup(r => r.CreateRecipeAsync(It.IsAny<CreateRecipeRequest>(), _userId))
            .ReturnsAsync(newId);
        _mockRepo.Setup(r => r.AddRecipeTagsAsync(newId, request.Tags))
            .Returns(Task.CompletedTask);

        var result = await _controller.CreateRecipe(request);

        result.Result.Should().BeOfType<CreatedAtActionResult>();
        _mockRepo.Verify(r => r.AddRecipeTagsAsync(newId, request.Tags), Times.Once);
    }

    [Fact]
    public async Task CreateRecipe_RepositoryThrows_Returns500()
    {
        var request = new CreateRecipeRequest { Name = "Pasta" };
        _mockRepo.Setup(r => r.CreateRecipeAsync(It.IsAny<CreateRecipeRequest>(), _userId))
            .ThrowsAsync(new Exception("DB error"));

        var result = await _controller.CreateRecipe(request);

        var obj = Assert.IsType<ObjectResult>(result.Result);
        obj.StatusCode.Should().Be(500);
    }

    // ── UpdateRecipe ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateRecipe_OwnRecipe_ReturnsNoContent()
    {
        var id = Guid.NewGuid();
        var existing = new RecipeDto { Id = id, AuthorId = _userId };
        var request = new UpdateRecipeRequest { Name = "Updated Pasta" };

        _mockRepo.Setup(r => r.GetRecipeByIdAsync(id)).ReturnsAsync(existing);
        _mockRepo.Setup(r => r.UpdateRecipeAsync(id, request, _userId)).Returns(Task.CompletedTask);

        var result = await _controller.UpdateRecipe(id, request);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task UpdateRecipe_RecipeNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetRecipeByIdAsync(id)).ReturnsAsync((RecipeDto?)null);

        var result = await _controller.UpdateRecipe(id, new UpdateRecipeRequest { Name = "Test" });

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateRecipe_DifferentUser_ReturnsForbid()
    {
        var id = Guid.NewGuid();
        var existing = new RecipeDto { Id = id, AuthorId = Guid.NewGuid() }; // Different author
        _mockRepo.Setup(r => r.GetRecipeByIdAsync(id)).ReturnsAsync(existing);

        var result = await _controller.UpdateRecipe(id, new UpdateRecipeRequest { Name = "Test" });

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task UpdateRecipe_Unauthenticated_ReturnsUnauthorized()
    {
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        var result = await _controller.UpdateRecipe(Guid.NewGuid(), new UpdateRecipeRequest { Name = "Test" });

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task UpdateRecipe_UpdatesIngredientsWhenProvided()
    {
        var id = Guid.NewGuid();
        var existing = new RecipeDto { Id = id, AuthorId = _userId };
        var request = new UpdateRecipeRequest
        {
            Name = "Pasta",
            Ingredients = new List<CreateRecipeIngredientRequest> { new() { Name = "Egg", Quantity = 2 } }
        };

        _mockRepo.Setup(r => r.GetRecipeByIdAsync(id)).ReturnsAsync(existing);
        _mockRepo.Setup(r => r.UpdateRecipeAsync(id, request, _userId)).Returns(Task.CompletedTask);
        _mockRepo.Setup(r => r.ClearRecipeIngredientsAsync(id)).Returns(Task.CompletedTask);
        _mockRepo.Setup(r => r.AddRecipeIngredientsAsync(id, It.IsAny<List<RecipeIngredientDto>>(), _userId)).Returns(Task.CompletedTask);

        var result = await _controller.UpdateRecipe(id, request);

        result.Should().BeOfType<NoContentResult>();
        _mockRepo.Verify(r => r.ClearRecipeIngredientsAsync(id), Times.Once);
        _mockRepo.Verify(r => r.AddRecipeIngredientsAsync(id, It.IsAny<List<RecipeIngredientDto>>(), _userId), Times.Once);
    }

    // ── DeleteRecipe ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteRecipe_OwnRecipe_ReturnsNoContent()
    {
        var id = Guid.NewGuid();
        var existing = new RecipeDto { Id = id, AuthorId = _userId };

        _mockRepo.Setup(r => r.GetRecipeByIdAsync(id)).ReturnsAsync(existing);
        _mockRepo.Setup(r => r.DeleteRecipeAsync(id)).Returns(Task.CompletedTask);

        var result = await _controller.DeleteRecipe(id);

        result.Should().BeOfType<NoContentResult>();
        _mockRepo.Verify(r => r.DeleteRecipeAsync(id), Times.Once);
    }

    [Fact]
    public async Task DeleteRecipe_RecipeNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetRecipeByIdAsync(id)).ReturnsAsync((RecipeDto?)null);

        var result = await _controller.DeleteRecipe(id);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DeleteRecipe_DifferentUser_ReturnsForbid()
    {
        var id = Guid.NewGuid();
        var existing = new RecipeDto { Id = id, AuthorId = Guid.NewGuid() };
        _mockRepo.Setup(r => r.GetRecipeByIdAsync(id)).ReturnsAsync(existing);

        var result = await _controller.DeleteRecipe(id);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task DeleteRecipe_Unauthenticated_ReturnsUnauthorized()
    {
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        var result = await _controller.DeleteRecipe(Guid.NewGuid());

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task DeleteRecipe_RepositoryThrows_Returns500()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetRecipeByIdAsync(id)).ThrowsAsync(new Exception("DB error"));

        var result = await _controller.DeleteRecipe(id);

        var obj = Assert.IsType<ObjectResult>(result);
        obj.StatusCode.Should().Be(500);
    }

    // ── GetMyRecipes ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMyRecipes_Authenticated_ReturnsUserRecipes()
    {
        var recipes = new List<RecipeDto>
        {
            new() { Id = Guid.NewGuid(), Name = "My Pasta", AuthorId = _userId }
        };
        _mockRepo.Setup(r => r.GetUserRecipesAsync(_userId, 50)).ReturnsAsync(recipes);

        var result = await _controller.GetMyRecipes();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsType<List<RecipeDto>>(ok.Value);
        list.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetMyRecipes_Unauthenticated_ReturnsUnauthorized()
    {
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        var result = await _controller.GetMyRecipes();

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }
}
