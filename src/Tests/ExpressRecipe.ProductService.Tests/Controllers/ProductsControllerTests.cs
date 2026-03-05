using ExpressRecipe.ProductService.Controllers;
using ExpressRecipe.ProductService.Data;
using ExpressRecipe.ProductService.Services;
using ExpressRecipe.ProductService.Tests.Helpers;
using ExpressRecipe.Shared.DTOs.Product;
using ExpressRecipe.Shared.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ExpressRecipe.ProductService.Tests.Controllers;

/// <summary>
/// Tests for ProductsController focusing on parallel data fetching, caching,
/// and cross-service correctness after the ownership refactoring.
/// </summary>
public class ProductsControllerTests
{
    private readonly Mock<IProductRepository>    _repoMock;
    private readonly Mock<IIngredientRepository> _ingredientMock;
    private readonly Mock<IAllergenRepository>   _allergenMock;
    private readonly Mock<IProductEventPublisher> _eventsMock;
    private readonly HybridCacheService          _cache;
    private readonly ProductsController          _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public ProductsControllerTests()
    {
        _repoMock       = new Mock<IProductRepository>();
        _ingredientMock = new Mock<IIngredientRepository>();
        _allergenMock   = new Mock<IAllergenRepository>();
        _eventsMock     = new Mock<IProductEventPublisher>();
        _cache          = ControllerTestHelpers.CreateTestHybridCache();

        _controller = new ProductsController(
            _repoMock.Object,
            _ingredientMock.Object,
            _allergenMock.Object,
            _cache,
            new Mock<ILogger<ProductsController>>().Object,
            _eventsMock.Object);

        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_userId);
    }

    // ── GetById ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_WhenFound_ReturnsOkWithProduct()
    {
        var id      = Guid.NewGuid();
        var product = new ProductDto { Id = id, Name = "Nutella" };

        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(product);
        _ingredientMock.Setup(r => r.GetProductIngredientsAsync(id))
            .ReturnsAsync(new List<ProductIngredientDto>());
        _allergenMock.Setup(r => r.GetProductAllergensAsync(id))
            .ReturnsAsync(new List<string>());

        var result = await _controller.GetById(id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ProductDto>(ok.Value);
        dto.Id.Should().Be(id);
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((ProductDto?)null);

        var result = await _controller.GetById(id);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetById_LoadsIngredientsAndAllergens_BothPopulatedOnResponse()
    {
        var id      = Guid.NewGuid();
        var product = new ProductDto { Id = id, Name = "Test" };

        var ingredients = new List<ProductIngredientDto>
        {
            new() { Id = Guid.NewGuid(), IngredientName = "Sugar" },
            new() { Id = Guid.NewGuid(), IngredientName = "Cocoa" }
        };
        var allergens = new List<string> { "Milk", "Nuts" };

        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(product);
        _ingredientMock.Setup(r => r.GetProductIngredientsAsync(id)).ReturnsAsync(ingredients);
        _allergenMock.Setup(r => r.GetProductAllergensAsync(id)).ReturnsAsync(allergens);

        var result = await _controller.GetById(id);

        var ok  = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ProductDto>(ok.Value);

        dto.Ingredients.Should().HaveCount(2);
        dto.Ingredients!.Select(i => i.IngredientName)
            .Should().BeEquivalentTo("Sugar", "Cocoa");

        dto.Allergens.Should().BeEquivalentTo("Milk", "Nuts");
    }

    [Fact]
    public async Task GetById_BothIngredientsAndAllergensAreFetched_EvenWhenIngredientListIsEmpty()
    {
        var id      = Guid.NewGuid();
        var product = new ProductDto { Id = id, Name = "Plain Water" };

        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(product);
        _ingredientMock.Setup(r => r.GetProductIngredientsAsync(id))
            .ReturnsAsync(new List<ProductIngredientDto>());
        _allergenMock.Setup(r => r.GetProductAllergensAsync(id))
            .ReturnsAsync(new List<string>());

        var result = await _controller.GetById(id);

        // Both repositories must have been called exactly once regardless of result size
        _ingredientMock.Verify(r => r.GetProductIngredientsAsync(id), Times.Once);
        _allergenMock.Verify(r => r.GetProductAllergensAsync(id), Times.Once);
    }

    [Fact]
    public async Task GetById_SecondCall_ServesFromCacheWithoutHittingRepository()
    {
        var id      = Guid.NewGuid();
        var product = new ProductDto { Id = id, Name = "Cached Product" };

        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(product);
        _ingredientMock.Setup(r => r.GetProductIngredientsAsync(id))
            .ReturnsAsync(new List<ProductIngredientDto>());
        _allergenMock.Setup(r => r.GetProductAllergensAsync(id))
            .ReturnsAsync(new List<string>());

        // First call – populates cache
        await _controller.GetById(id);

        // Second call – should read from cache
        await _controller.GetById(id);

        // Product repo and both data repos called exactly once (second call uses cache)
        _repoMock.Verify(r => r.GetByIdAsync(id), Times.Once);
        _ingredientMock.Verify(r => r.GetProductIngredientsAsync(id), Times.Once);
        _allergenMock.Verify(r => r.GetProductAllergensAsync(id), Times.Once);
    }

    [Fact]
    public async Task GetById_IngredientsAndAllergensBothCalled_InParallel_NotSequentially()
    {
        var id      = Guid.NewGuid();
        var product = new ProductDto { Id = id, Name = "Parallel Test" };

        // Track which operations have been started before either completes
        var callOrder = new List<string>();

        // Use TaskCompletionSources so we can verify both tasks start before completing either
        var ingredientStarted = new SemaphoreSlim(0, 1);
        var allergenStarted   = new SemaphoreSlim(0, 1);
        var ingredientReady   = new TaskCompletionSource<List<ProductIngredientDto>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allergenReady     = new TaskCompletionSource<List<string>>(TaskCreationOptions.RunContinuationsAsynchronously);

        _repoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(product);

        _ingredientMock.Setup(r => r.GetProductIngredientsAsync(id))
            .Returns(async () =>
            {
                callOrder.Add("ingredients-started");
                ingredientStarted.Release();
                return await ingredientReady.Task;
            });

        _allergenMock.Setup(r => r.GetProductAllergensAsync(id))
            .Returns(async () =>
            {
                callOrder.Add("allergens-started");
                allergenStarted.Release();
                return await allergenReady.Task;
            });

        // Start the controller call – it will block inside the cache wrapper until results arrive
        var controllerTask = _controller.GetById(id);

        // Wait for both tasks to have started (with a generous timeout for CI systems)
        var bothStarted = Task.WhenAll(
            ingredientStarted.WaitAsync(TimeSpan.FromSeconds(5)),
            allergenStarted.WaitAsync(TimeSpan.FromSeconds(5)));

        // Complete both tasks so the controller can finish
        ingredientReady.SetResult(new List<ProductIngredientDto>());
        allergenReady.SetResult(new List<string>());

        await bothStarted;
        await controllerTask;

        // Both calls must have been started
        callOrder.Should().Contain("ingredients-started");
        callOrder.Should().Contain("allergens-started");
    }
}
