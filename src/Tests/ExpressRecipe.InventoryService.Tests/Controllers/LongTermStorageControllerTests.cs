using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ExpressRecipe.InventoryService.Controllers;
using ExpressRecipe.InventoryService.Data;
using ExpressRecipe.InventoryService.Tests.Helpers;

namespace ExpressRecipe.InventoryService.Tests.Controllers;

public class LongTermStorageControllerTests
{
    private readonly Mock<IInventoryRepository> _mockRepository;
    private readonly Mock<ILogger<InventoryController>> _mockLogger;
    private readonly InventoryController _controller;
    private readonly Guid _testUserId;
    private readonly Guid _testHouseholdId;

    public LongTermStorageControllerTests()
    {
        _mockRepository  = new Mock<IInventoryRepository>();
        _mockLogger      = new Mock<ILogger<InventoryController>>();
        _controller      = new InventoryController(_mockLogger.Object, _mockRepository.Object);
        _testUserId      = Guid.NewGuid();
        _testHouseholdId = Guid.NewGuid();
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId, _testHouseholdId);
    }

    #region AddLongTermItem Tests

    [Fact]
    public async Task AddLongTermItem_FreezeDried_NoExpirationDate_ReturnsOkWithId()
    {
        // Arrange
        Guid newId = Guid.NewGuid();
        AddLongTermStorageRequest request = new()
        {
            Name          = "Freeze Dried Strawberries",
            Quantity      = 10,
            Unit          = "cans",
            StorageMethod = "FreezeDried",
            ProcessDate   = new DateTime(2025, 1, 1)
        };

        _mockRepository
            .Setup(r => r.AddItemAsync(It.IsAny<AddItemRequest>(), default))
            .ReturnsAsync(newId);

        // Act
        IActionResult result = await _controller.AddLongTermItem(request, default);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRepository.Verify(r => r.AddItemAsync(
            It.Is<AddItemRequest>(req =>
                req.IsLongTermStorage == true &&
                req.StorageMethod == "FreezeDried" &&
                req.ExpirationDate.HasValue &&
                req.ExpirationDate!.Value.Year == 2050), // 2025 + 25 years = 2050
            default), Times.Once);
    }

    [Fact]
    public async Task AddLongTermItem_FrozenMeal_TemperatureIsFrozen()
    {
        // Arrange
        Guid newId = Guid.NewGuid();
        AddLongTermStorageRequest request = new()
        {
            Name          = "Frozen Lasagna",
            Quantity      = 3,
            Unit          = "servings",
            StorageMethod = "FrozenMeal"
        };

        _mockRepository
            .Setup(r => r.AddItemAsync(It.IsAny<AddItemRequest>(), default))
            .ReturnsAsync(newId);

        // Act
        IActionResult result = await _controller.AddLongTermItem(request, default);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRepository.Verify(r => r.AddItemAsync(
            It.Is<AddItemRequest>(req =>
                req.Temperature == "Frozen" &&
                req.IsLongTermStorage == true),
            default), Times.Once);
    }

    [Fact]
    public async Task AddLongTermItem_ExplicitExpirationDate_UsesProvidedDate()
    {
        // Arrange
        Guid newId = Guid.NewGuid();
        DateTime explicitExpiration = new(2030, 6, 1);
        AddLongTermStorageRequest request = new()
        {
            Name           = "Canned Beans",
            Quantity       = 24,
            Unit           = "cans",
            StorageMethod  = "Canned",
            ExpirationDate = explicitExpiration
        };

        _mockRepository
            .Setup(r => r.AddItemAsync(It.IsAny<AddItemRequest>(), default))
            .ReturnsAsync(newId);

        // Act
        IActionResult result = await _controller.AddLongTermItem(request, default);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRepository.Verify(r => r.AddItemAsync(
            It.Is<AddItemRequest>(req => req.ExpirationDate == explicitExpiration),
            default), Times.Once);
    }

    [Fact]
    public async Task AddLongTermItem_Canned_NoExpirationDate_Computes18Months()
    {
        // Arrange
        Guid newId = Guid.NewGuid();
        DateTime processDate = new(2025, 1, 1);
        AddLongTermStorageRequest request = new()
        {
            Name          = "Canned Tomatoes",
            Quantity      = 12,
            Unit          = "cans",
            StorageMethod = "Canned",
            ProcessDate   = processDate
        };

        _mockRepository
            .Setup(r => r.AddItemAsync(It.IsAny<AddItemRequest>(), default))
            .ReturnsAsync(newId);

        // Act
        IActionResult result = await _controller.AddLongTermItem(request, default);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRepository.Verify(r => r.AddItemAsync(
            It.Is<AddItemRequest>(req =>
                req.ExpirationDate == processDate.AddMonths(18)),
            default), Times.Once);
    }

    #endregion

    #region GetLongTermItems Tests

    [Fact]
    public async Task GetLongTermItems_NoFilter_ReturnsAllLongTermItems()
    {
        // Arrange
        List<InventoryItemDto> items = new()
        {
            TestDataFactory.CreateInventoryItemDto(name: "Canned Beans"),
            TestDataFactory.CreateInventoryItemDto(name: "Freeze Dried Corn")
        };

        _mockRepository
            .Setup(r => r.GetItemsAsync(_testHouseholdId, true, null, default))
            .ReturnsAsync(items);

        // Act
        IActionResult result = await _controller.GetLongTermItems(null, default);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        (result as OkObjectResult)!.Value.Should().BeEquivalentTo(items);
        _mockRepository.Verify(r => r.GetItemsAsync(_testHouseholdId, true, null, default), Times.Once);
    }

    [Fact]
    public async Task GetLongTermItems_FilteredByCanned_ReturnsOnlyCannedItems()
    {
        // Arrange
        List<InventoryItemDto> cannedItems = new()
        {
            TestDataFactory.CreateInventoryItemDto(name: "Canned Tomatoes"),
            TestDataFactory.CreateInventoryItemDto(name: "Canned Beans")
        };

        _mockRepository
            .Setup(r => r.GetItemsAsync(_testHouseholdId, true, "Canned", default))
            .ReturnsAsync(cannedItems);

        // Act
        IActionResult result = await _controller.GetLongTermItems("Canned", default);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        (result as OkObjectResult)!.Value.Should().BeEquivalentTo(cannedItems);
        _mockRepository.Verify(r => r.GetItemsAsync(_testHouseholdId, true, "Canned", default), Times.Once);
    }

    #endregion
}
