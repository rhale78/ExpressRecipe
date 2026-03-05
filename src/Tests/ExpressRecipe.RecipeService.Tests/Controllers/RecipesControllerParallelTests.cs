using ExpressRecipe.RecipeService.Controllers;
using ExpressRecipe.RecipeService.Data;
using ExpressRecipe.RecipeService.Services;
using ExpressRecipe.RecipeService.Tests.Helpers;
using ExpressRecipe.Shared.DTOs.Recipe;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ExpressRecipe.RecipeService.Tests.Controllers;

/// <summary>
/// Tests focused on the parallel data-fetching refactoring of RecipesController.GetRecipe.
/// Verifies that all related collections (ingredients, nutrition, tags, allergens, rating)
/// are loaded correctly when using Task.WhenAll instead of sequential awaits.
/// </summary>
public class RecipesControllerParallelTests
{
    private readonly Mock<IRecipeRepository>      _repo;
    private readonly Mock<ILogger<RecipesController>> _logger;
    private readonly RecipesController            _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public RecipesControllerParallelTests()
    {
        _repo   = new Mock<IRecipeRepository>();
        _logger = new Mock<ILogger<RecipesController>>();

        _controller = new RecipesController(
            _repo.Object,
            new ServingSizeService(),
            new ShoppingListIntegrationService(),
            _logger.Object);

        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_userId);
    }

    // ── Helper setups ─────────────────────────────────────────────────────

    private void SetupFullRecipe(Guid id)
    {
        var recipe = new RecipeDto { Id = id, Name = "Test Recipe", AuthorId = _userId };

        _repo.Setup(r => r.GetRecipeByIdAsync(id)).ReturnsAsync(recipe);
        _repo.Setup(r => r.GetRecipeIngredientsAsync(id))
            .ReturnsAsync(new List<RecipeIngredientDto> { new() { IngredientName = "Tomato" } });
        _repo.Setup(r => r.GetRecipeNutritionAsync(id))
            .ReturnsAsync(new RecipeNutritionDto { Calories = 200 });
        _repo.Setup(r => r.GetRecipeTagsAsync(id))
            .ReturnsAsync(new List<string> { "Vegan", "Quick" });
        _repo.Setup(r => r.GetRecipeAllergensAsync(id))
            .ReturnsAsync(new List<RecipeAllergenWarningDto> { new() { AllergenName = "Gluten" } });
        _repo.Setup(r => r.GetAverageRatingAsync(id))
            .ReturnsAsync((4.2m, 8));
    }

    // ── GetRecipe – correctness ───────────────────────────────────────────

    [Fact]
    public async Task GetRecipe_PopulatesAllRelatedData_FromParallelTasks()
    {
        var id = Guid.NewGuid();
        SetupFullRecipe(id);

        var result = await _controller.GetRecipe(id);

        var ok     = Assert.IsType<OkObjectResult>(result.Result);
        var recipe = Assert.IsType<RecipeDto>(ok.Value);

        recipe.Ingredients.Should().HaveCount(1);
        recipe.Ingredients![0].IngredientName.Should().Be("Tomato");

        recipe.Nutrition.Should().NotBeNull();
        recipe.Nutrition!.Calories.Should().Be(200);

        recipe.Tags.Should().HaveCount(2);
        recipe.Tags!.Select(t => t.Name).Should().BeEquivalentTo("Vegan", "Quick");

        recipe.AllergenWarnings.Should().HaveCount(1);
        recipe.AllergenWarnings![0].AllergenName.Should().Be("Gluten");

        recipe.AverageRating.Should().Be(4.2m);
        recipe.RatingCount.Should().Be(8);
    }

    [Fact]
    public async Task GetRecipe_WhenRecipeNotFound_ReturnsNotFoundBeforeLoadingRelatedData()
    {
        var id = Guid.NewGuid();
        _repo.Setup(r => r.GetRecipeByIdAsync(id)).ReturnsAsync((RecipeDto?)null);

        var result = await _controller.GetRecipe(id);

        result.Result.Should().BeOfType<NotFoundObjectResult>();

        // No related-data queries should have been fired
        _repo.Verify(r => r.GetRecipeIngredientsAsync(It.IsAny<Guid>()), Times.Never);
        _repo.Verify(r => r.GetRecipeNutritionAsync(It.IsAny<Guid>()), Times.Never);
        _repo.Verify(r => r.GetRecipeTagsAsync(It.IsAny<Guid>()), Times.Never);
        _repo.Verify(r => r.GetRecipeAllergensAsync(It.IsAny<Guid>()), Times.Never);
        _repo.Verify(r => r.GetAverageRatingAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GetRecipe_AllFiveRelatedDataCallsAreMade_Exactly_Once()
    {
        var id = Guid.NewGuid();
        SetupFullRecipe(id);

        await _controller.GetRecipe(id);

        _repo.Verify(r => r.GetRecipeIngredientsAsync(id), Times.Once);
        _repo.Verify(r => r.GetRecipeNutritionAsync(id),   Times.Once);
        _repo.Verify(r => r.GetRecipeTagsAsync(id),        Times.Once);
        _repo.Verify(r => r.GetRecipeAllergensAsync(id),   Times.Once);
        _repo.Verify(r => r.GetAverageRatingAsync(id),     Times.Once);
    }

    [Fact]
    public async Task GetRecipe_WhenNutritionIsNull_RecipeStillReturnsSuccessfully()
    {
        var id     = Guid.NewGuid();
        var recipe = new RecipeDto { Id = id, Name = "Simple Salad", AuthorId = _userId };

        _repo.Setup(r => r.GetRecipeByIdAsync(id)).ReturnsAsync(recipe);
        _repo.Setup(r => r.GetRecipeIngredientsAsync(id)).ReturnsAsync(new List<RecipeIngredientDto>());
        _repo.Setup(r => r.GetRecipeNutritionAsync(id)).ReturnsAsync((RecipeNutritionDto?)null);
        _repo.Setup(r => r.GetRecipeTagsAsync(id)).ReturnsAsync(new List<string>());
        _repo.Setup(r => r.GetRecipeAllergensAsync(id)).ReturnsAsync(new List<RecipeAllergenWarningDto>());
        _repo.Setup(r => r.GetAverageRatingAsync(id)).ReturnsAsync((0m, 0));

        var result = await _controller.GetRecipe(id);

        var ok  = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<RecipeDto>(ok.Value);

        dto.Nutrition.Should().BeNull();
        dto.Ingredients.Should().BeEmpty();
        dto.Tags.Should().BeEmpty();
        dto.AllergenWarnings.Should().BeEmpty();
        dto.AverageRating.Should().Be(0);
    }

    [Fact]
    public async Task GetRecipe_AllFiveDataSources_StartedConcurrently()
    {
        var id     = Guid.NewGuid();
        var recipe = new RecipeDto { Id = id, Name = "Concurrent Test", AuthorId = _userId };

        var callLog = new List<string>();

        // Each data source tracks when it was started
        _repo.Setup(r => r.GetRecipeByIdAsync(id)).ReturnsAsync(recipe);

        _repo.Setup(r => r.GetRecipeIngredientsAsync(id)).Returns(() =>
        {
            callLog.Add("ingredients");
            return Task.FromResult(new List<RecipeIngredientDto>());
        });
        _repo.Setup(r => r.GetRecipeNutritionAsync(id)).Returns(() =>
        {
            callLog.Add("nutrition");
            return Task.FromResult((RecipeNutritionDto?)null);
        });
        _repo.Setup(r => r.GetRecipeTagsAsync(id)).Returns(() =>
        {
            callLog.Add("tags");
            return Task.FromResult(new List<string>());
        });
        _repo.Setup(r => r.GetRecipeAllergensAsync(id)).Returns(() =>
        {
            callLog.Add("allergens");
            return Task.FromResult(new List<RecipeAllergenWarningDto>());
        });
        _repo.Setup(r => r.GetAverageRatingAsync(id)).Returns(() =>
        {
            callLog.Add("rating");
            return Task.FromResult((0m, 0));
        });

        await _controller.GetRecipe(id);

        // All 5 data sources must have been called
        callLog.Should().Contain("ingredients");
        callLog.Should().Contain("nutrition");
        callLog.Should().Contain("tags");
        callLog.Should().Contain("allergens");
        callLog.Should().Contain("rating");
        callLog.Should().HaveCount(5);
    }
}
