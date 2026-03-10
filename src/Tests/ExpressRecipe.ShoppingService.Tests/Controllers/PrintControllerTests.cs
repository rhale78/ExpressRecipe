using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using ExpressRecipe.ShoppingService.Controllers;
using ExpressRecipe.ShoppingService.Data;
using ExpressRecipe.ShoppingService.Tests.Helpers;

namespace ExpressRecipe.ShoppingService.Tests.Controllers;

public class PrintControllerTests
{
    private static readonly Guid _userId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid _listId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid _storeA = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid _storeB = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Mock<ILogger<PrintController>> _mockLogger;
    private readonly Mock<IShoppingRepository> _mockRepo;
    private readonly PrintController _controller;

    public PrintControllerTests()
    {
        _mockLogger = new Mock<ILogger<PrintController>>();
        _mockRepo = new Mock<IShoppingRepository>();

        _controller = new PrintController(_mockLogger.Object, _mockRepo.Object)
        {
            ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_userId)
        };
    }

    private static ShoppingListDto MakeList(string name = "Weekly Shop", string? storeName = "Publix") => new()
    {
        Id = _listId,
        UserId = _userId,
        Name = name,
        StoreName = storeName
    };

    private static ShoppingListItemDto MakeItem(string name, int orderIndex = 0) => new()
    {
        Id = Guid.NewGuid(),
        ShoppingListId = _listId,
        CustomName = name,
        Quantity = 1,
        Unit = "each",
        OrderIndex = orderIndex
    };

    private static string SerializePlan(OptimizedShoppingPlan plan) =>
        JsonSerializer.Serialize(plan, _jsonOptions);

    // ── List not found ────────────────────────────────────────────────────────

    [Fact]
    public async Task PrintList_ListNotFound_ReturnsNotFound()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetShoppingListAsync(_listId, _userId))
            .ReturnsAsync((ShoppingListDto?)null);

        // Act
        IActionResult result = await _controller.PrintList(_listId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    // ── HTML format ───────────────────────────────────────────────────────────

    [Fact]
    public async Task PrintList_HtmlFormat_ReturnsHtmlContent()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetShoppingListAsync(_listId, _userId))
            .ReturnsAsync(MakeList());
        _mockRepo.Setup(r => r.GetOptimizationResultAsync(_listId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShoppingListOptimizationDto?)null);
        _mockRepo.Setup(r => r.GetListItemsAsync(_listId, _userId))
            .ReturnsAsync(new List<ShoppingListItemDto> { MakeItem("Milk"), MakeItem("Eggs") });

        // Act
        IActionResult result = await _controller.PrintList(_listId, format: "html");

        // Assert
        var content = result.Should().BeOfType<ContentResult>().Subject;
        content.ContentType.Should().Be("text/html");
        content.Content.Should().Contain("<!DOCTYPE html>");
        content.Content.Should().Contain("Weekly Shop");
    }

    // ── PDF format ────────────────────────────────────────────────────────────

    [Fact]
    public async Task PrintList_PdfFormat_ReturnsPdfBytes()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetShoppingListAsync(_listId, _userId))
            .ReturnsAsync(MakeList());
        _mockRepo.Setup(r => r.GetOptimizationResultAsync(_listId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShoppingListOptimizationDto?)null);
        _mockRepo.Setup(r => r.GetListItemsAsync(_listId, _userId))
            .ReturnsAsync(new List<ShoppingListItemDto> { MakeItem("Bread") });

        // Act
        IActionResult result = await _controller.PrintList(_listId, format: "pdf");

        // Assert
        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be("application/pdf");
        fileResult.FileDownloadName.Should().Be("ShoppingList.pdf");
        fileResult.FileContents.Should().NotBeEmpty();
    }

    // ── Optimization result used ──────────────────────────────────────────────

    [Fact]
    public async Task PrintList_WithOptimizationResult_UsesOptimizedPlan()
    {
        // Arrange
        var plan = new OptimizedShoppingPlan
        {
            Strategy = "CheapestOverall",
            StoreGroups = new List<StoreShoppingGroup>
            {
                new()
                {
                    StoreId = _storeA,
                    StoreName = "Store Alpha",
                    SubTotal = 15.00m,
                    Items = new List<OptimizedShoppingItem>
                    {
                        new() { ShoppingListItemId = Guid.NewGuid(), Name = "Milk", Quantity = 1, AisleOrder = 1 }
                    }
                },
                new()
                {
                    StoreId = _storeB,
                    StoreName = "Store Beta",
                    SubTotal = 10.00m,
                    Items = new List<OptimizedShoppingItem>
                    {
                        new() { ShoppingListItemId = Guid.NewGuid(), Name = "Bread", Quantity = 2, AisleOrder = 2 }
                    }
                }
            }
        };

        var optimizationDto = new ShoppingListOptimizationDto
        {
            Id = Guid.NewGuid(),
            ShoppingListId = _listId,
            Strategy = "CheapestOverall",
            OptimizedAt = DateTime.UtcNow,
            ResultJson = SerializePlan(plan)
        };

        _mockRepo.Setup(r => r.GetShoppingListAsync(_listId, _userId))
            .ReturnsAsync(MakeList("My List"));
        _mockRepo.Setup(r => r.GetOptimizationResultAsync(_listId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(optimizationDto);

        // Act
        IActionResult result = await _controller.PrintList(_listId, format: "html");

        // Assert
        var content = result.Should().BeOfType<ContentResult>().Subject;
        content.Content.Should().Contain("Store Alpha");
        content.Content.Should().Contain("Store Beta");
        content.Content.Should().Contain("Milk");
        content.Content.Should().Contain("Bread");
    }

    // ── No optimization → raw items ───────────────────────────────────────────

    [Fact]
    public async Task PrintList_WithoutOptimizationResult_UsesRawItems()
    {
        // Arrange
        var rawItems = new List<ShoppingListItemDto>
        {
            MakeItem("Apples", 0),
            MakeItem("Bananas", 1)
        };

        _mockRepo.Setup(r => r.GetShoppingListAsync(_listId, _userId))
            .ReturnsAsync(MakeList("Fruit Run", storeName: "Whole Foods"));
        _mockRepo.Setup(r => r.GetOptimizationResultAsync(_listId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShoppingListOptimizationDto?)null);
        _mockRepo.Setup(r => r.GetListItemsAsync(_listId, _userId))
            .ReturnsAsync(rawItems);

        // Act
        IActionResult result = await _controller.PrintList(_listId, format: "html");

        // Assert
        var content = result.Should().BeOfType<ContentResult>().Subject;
        // Falls back to list StoreName as the group heading
        content.Content.Should().Contain("Whole Foods");
        content.Content.Should().Contain("Apples");
        content.Content.Should().Contain("Bananas");
    }

    // ── Store ID filter ───────────────────────────────────────────────────────

    [Fact]
    public async Task PrintList_WithStoreIdFilter_FiltersStoreGroups()
    {
        // Arrange: plan has two store groups; filter to storeA only
        var plan = new OptimizedShoppingPlan
        {
            Strategy = "SingleStore",
            StoreGroups = new List<StoreShoppingGroup>
            {
                new()
                {
                    StoreId = _storeA,
                    StoreName = "Alpha Market",
                    SubTotal = 20.00m,
                    Items = new List<OptimizedShoppingItem>
                    {
                        new() { ShoppingListItemId = Guid.NewGuid(), Name = "Cheese", Quantity = 1 }
                    }
                },
                new()
                {
                    StoreId = _storeB,
                    StoreName = "Beta Market",
                    SubTotal = 5.00m,
                    Items = new List<OptimizedShoppingItem>
                    {
                        new() { ShoppingListItemId = Guid.NewGuid(), Name = "Crackers", Quantity = 1 }
                    }
                }
            }
        };

        var optimizationDto = new ShoppingListOptimizationDto
        {
            Id = Guid.NewGuid(),
            ShoppingListId = _listId,
            Strategy = "SingleStore",
            OptimizedAt = DateTime.UtcNow,
            ResultJson = SerializePlan(plan)
        };

        _mockRepo.Setup(r => r.GetShoppingListAsync(_listId, _userId))
            .ReturnsAsync(MakeList("Cheese Run"));
        _mockRepo.Setup(r => r.GetOptimizationResultAsync(_listId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(optimizationDto);

        // Act: filter to storeA only
        IActionResult result = await _controller.PrintList(_listId, format: "html", storeId: _storeA);

        // Assert: only Alpha Market's items appear; Beta Market is excluded
        var content = result.Should().BeOfType<ContentResult>().Subject;
        content.Content.Should().Contain("Alpha Market");
        content.Content.Should().Contain("Cheese");
        content.Content.Should().NotContain("Beta Market");
        content.Content.Should().NotContain("Crackers");
    }
}
