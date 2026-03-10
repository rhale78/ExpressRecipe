using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using ExpressRecipe.InventoryService.Controllers;
using ExpressRecipe.InventoryService.Data;
using ExpressRecipe.InventoryService.Tests.Helpers;

namespace ExpressRecipe.InventoryService.Tests.Controllers;

public class InventoryControllerTests
{
    private readonly Mock<IInventoryRepository> _mockRepository;
    private readonly Mock<ILogger<InventoryController>> _mockLogger;
    private readonly InventoryController _controller;
    private readonly Guid _testUserId;

    public InventoryControllerTests()
    {
        _mockRepository = new Mock<IInventoryRepository>();
        _mockLogger = new Mock<ILogger<InventoryController>>();
        IConfiguration config = new ConfigurationBuilder().Build();
        _controller = new InventoryController(_mockLogger.Object, _mockRepository.Object, null, config);
        _testUserId = Guid.NewGuid();
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);
    }

    #region GetInventory Tests

    [Fact]
    public async Task GetInventory_ReturnsOkWithUserItems()
    {
        // Arrange
        var items = new List<InventoryItemDto>
        {
            TestDataFactory.CreateInventoryItemDto(name: "Milk"),
            TestDataFactory.CreateInventoryItemDto(name: "Bread")
        };

        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);

        _mockRepository
            .Setup(r => r.GetUserInventoryAsync(_testUserId))
            .ReturnsAsync(items);

        // Act
        var result = await _controller.GetInventory();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(items);

        _mockRepository.Verify(r => r.GetUserInventoryAsync(_testUserId), Times.Once);
    }

    [Fact]
    public async Task GetInventory_WhenNoItems_ReturnsEmptyList()
    {
        // Arrange
        var emptyList = new List<InventoryItemDto>();

        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);

        _mockRepository
            .Setup(r => r.GetUserInventoryAsync(_testUserId))
            .ReturnsAsync(emptyList);

        // Act
        var result = await _controller.GetInventory();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        (okResult!.Value as List<InventoryItemDto>).Should().BeEmpty();
    }

    #endregion

    #region AddItem Tests

    [Fact]
    public async Task AddItem_WithValidRequest_ReturnsCreatedAtAction()
    {
        // Arrange
        var request = new AddInventoryItemRequest
        {
            HouseholdId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            CustomName = "Organic Milk",
            StorageLocationId = Guid.NewGuid(),
            Quantity = 2.0m,
            Unit = "liters",
            ExpirationDate = DateTime.UtcNow.AddDays(7),
            Barcode = "123456789",
            Price = 4.99m,
            PreferredStore = "Whole Foods",
            StoreLocation = "Dairy Section"
        };

        var itemId = Guid.NewGuid();
        var itemDto = TestDataFactory.CreateInventoryItemDto(itemId, request.CustomName);

        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);

        _mockRepository
            .Setup(r => r.AddInventoryItemAsync(
                _testUserId,
                request.HouseholdId,
                request.ProductId,
                request.CustomName,
                request.StorageLocationId,
                request.Quantity,
                request.Unit,
                request.ExpirationDate,
                request.Barcode,
                request.Price,
                request.PreferredStore,
                request.StoreLocation))
            .ReturnsAsync(itemId);

        _mockRepository
            .Setup(r => r.GetInventoryItemAsync(itemId, _testUserId))
            .ReturnsAsync(itemDto);

        // Act
        var result = await _controller.AddItem(request);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
        var createdResult = result as CreatedAtActionResult;
        createdResult!.Value.Should().BeEquivalentTo(itemDto);
        createdResult.ActionName.Should().Be(nameof(InventoryController.GetItem));

        _mockRepository.Verify(r => r.AddInventoryItemAsync(
            _testUserId,
            request.HouseholdId,
            request.ProductId,
            request.CustomName,
            request.StorageLocationId,
            request.Quantity,
            request.Unit,
            request.ExpirationDate,
            request.Barcode,
            request.Price,
            request.PreferredStore,
            request.StoreLocation), Times.Once);
    }

    #endregion

    #region GetItem Tests

    [Fact]
    public async Task GetItem_WithValidId_ReturnsOkWithItem()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var item = TestDataFactory.CreateInventoryItemDto(itemId, "Milk");

        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);

        _mockRepository
            .Setup(r => r.GetInventoryItemAsync(itemId, _testUserId))
            .ReturnsAsync(item);

        // Act
        var result = await _controller.GetItem(itemId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(item);
    }

    [Fact]
    public async Task GetItem_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var itemId = Guid.NewGuid();

        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);

        _mockRepository
            .Setup(r => r.GetInventoryItemAsync(itemId, _testUserId))
            .ReturnsAsync((InventoryItemDto?)null);

        // Act
        var result = await _controller.GetItem(itemId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region UpdateQuantity Tests

    [Fact]
    public async Task UpdateQuantity_WithValidRequest_ReturnsOkWithUpdatedItem()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var request = new UpdateQuantityRequest
        {
            Quantity = 5.0m,
            ActionType = "Add",
            Reason = "Restocked"
        };

        var updatedItem = TestDataFactory.CreateInventoryItemDto(itemId, "Milk", 5.0m);

        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);

        _mockRepository
            .Setup(r => r.UpdateInventoryQuantityAsync(itemId, request.Quantity, request.ActionType, _testUserId, request.Reason, request.DisposalReason, request.AllergenDetected))
            .Returns(Task.CompletedTask);

        _mockRepository
            .Setup(r => r.GetInventoryItemAsync(itemId, _testUserId))
            .ReturnsAsync(updatedItem);

        // Act
        var result = await _controller.UpdateQuantity(itemId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(updatedItem);

        _mockRepository.Verify(r => r.UpdateInventoryQuantityAsync(
            itemId, request.Quantity, request.ActionType, _testUserId, request.Reason, request.DisposalReason, request.AllergenDetected), Times.Once);
    }

    #endregion

    #region DeleteItem Tests

    [Fact]
    public async Task DeleteItem_WithValidId_ReturnsNoContent()
    {
        // Arrange
        var itemId = Guid.NewGuid();

        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);

        _mockRepository
            .Setup(r => r.DeleteInventoryItemAsync(itemId, _testUserId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteItem(itemId);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _mockRepository.Verify(r => r.DeleteInventoryItemAsync(itemId, _testUserId), Times.Once);
    }

    #endregion

    #region GetExpiringItems Tests

    [Fact]
    public async Task GetExpiringItems_WithDefaultDays_ReturnsExpiringItems()
    {
        // Arrange
        var items = new List<InventoryItemDto>
        {
            TestDataFactory.CreateInventoryItemDto(name: "Milk", expirationDate: DateTime.UtcNow.AddDays(3)),
            TestDataFactory.CreateInventoryItemDto(name: "Yogurt", expirationDate: DateTime.UtcNow.AddDays(5))
        };

        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);

        _mockRepository
            .Setup(r => r.GetExpiringItemsAsync(_testUserId, 7))
            .ReturnsAsync(items);

        // Act
        var result = await _controller.GetExpiringItems();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(items);
    }

    [Fact]
    public async Task GetExpiringItems_WithCustomDays_UsesSpecifiedDays()
    {
        // Arrange
        var items = new List<InventoryItemDto>();

        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);

        _mockRepository
            .Setup(r => r.GetExpiringItemsAsync(_testUserId, 14))
            .ReturnsAsync(items);

        // Act
        var result = await _controller.GetExpiringItems(14);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRepository.Verify(r => r.GetExpiringItemsAsync(_testUserId, 14), Times.Once);
    }

    #endregion

    #region GetLowStockItems Tests

    [Fact]
    public async Task GetLowStockItems_WithDefaultThreshold_ReturnsLowStockItems()
    {
        // Arrange
        var items = new List<InventoryItemDto>
        {
            TestDataFactory.CreateInventoryItemDto(name: "Milk", quantity: 1.0m),
            TestDataFactory.CreateInventoryItemDto(name: "Eggs", quantity: 0.5m)
        };

        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);

        _mockRepository
            .Setup(r => r.GetLowStockItemsAsync(_testUserId, 2.0m))
            .ReturnsAsync(items);

        // Act
        var result = await _controller.GetLowStockItems();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(items);
    }

    [Fact]
    public async Task GetLowStockItems_WithCustomThreshold_UsesSpecifiedThreshold()
    {
        // Arrange
        var items = new List<InventoryItemDto>();

        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);

        _mockRepository
            .Setup(r => r.GetLowStockItemsAsync(_testUserId, 5.0m))
            .ReturnsAsync(items);

        // Act
        var result = await _controller.GetLowStockItems(5.0m);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRepository.Verify(r => r.GetLowStockItemsAsync(_testUserId, 5.0m), Times.Once);
    }

    #endregion

    #region GetItemsRunningOut Tests

    [Fact]
    public async Task GetItemsRunningOut_WithDefaultDays_ReturnsRunningOutItems()
    {
        // Arrange
        var items = new List<InventoryItemDto>
        {
            TestDataFactory.CreateInventoryItemDto(name: "Coffee"),
            TestDataFactory.CreateInventoryItemDto(name: "Sugar")
        };

        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);

        _mockRepository
            .Setup(r => r.GetItemsRunningOutAsync(_testUserId, 7))
            .ReturnsAsync(items);

        // Act
        var result = await _controller.GetItemsRunningOut();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(items);
    }

    #endregion

    #region GetItemsAboutToExpire Tests

    [Fact]
    public async Task GetItemsAboutToExpire_ReturnsItemsAboutToExpire()
    {
        // Arrange
        var items = new List<InventoryItemDto>
        {
            TestDataFactory.CreateInventoryItemDto(name: "Milk", expirationDate: DateTime.UtcNow.AddDays(2))
        };

        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);

        _mockRepository
            .Setup(r => r.GetItemsAboutToExpireAsync(_testUserId, 3))
            .ReturnsAsync(items);

        // Act
        var result = await _controller.GetItemsAboutToExpire();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(items);
    }

    #endregion

    #region GetHouseholdInventory Tests

    [Fact]
    public async Task GetHouseholdInventory_ReturnsHouseholdItems()
    {
        // Arrange
        var householdId = Guid.NewGuid();
        var items = new List<InventoryItemDto>
        {
            TestDataFactory.CreateInventoryItemDto(name: "Milk"),
            TestDataFactory.CreateInventoryItemDto(name: "Bread")
        };

        _mockRepository
            .Setup(r => r.IsUserMemberOfHouseholdAsync(householdId, _testUserId))
            .ReturnsAsync(true);
        _mockRepository
            .Setup(r => r.GetHouseholdInventoryAsync(householdId))
            .ReturnsAsync(items);

        // Act
        var result = await _controller.GetHouseholdInventory(householdId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(items);
    }

    #endregion

    #region GetInventoryByAddress Tests

    [Fact]
    public async Task GetInventoryByAddress_ReturnsAddressItems()
    {
        // Arrange
        var addressId = Guid.NewGuid();
        var householdId = Guid.NewGuid();
        var items = new List<InventoryItemDto>
        {
            TestDataFactory.CreateInventoryItemDto(name: "Milk")
        };
        var addressDto = TestDataFactory.CreateAddressDto(addressId, householdId);

        _mockRepository
            .Setup(r => r.GetAddressByIdAsync(addressId))
            .ReturnsAsync(addressDto);
        _mockRepository
            .Setup(r => r.IsUserMemberOfHouseholdAsync(householdId, _testUserId))
            .ReturnsAsync(true);
        _mockRepository
            .Setup(r => r.GetInventoryByAddressAsync(addressId))
            .ReturnsAsync(items);

        // Act
        var result = await _controller.GetInventoryByAddress(addressId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(items);

        _mockRepository.Verify(r => r.GetAddressByIdAsync(addressId), Times.Once);
        _mockRepository.Verify(r => r.IsUserMemberOfHouseholdAsync(householdId, _testUserId), Times.Once);
    }

    #endregion

    #region GetInventoryByLocation Tests

    [Fact]
    public async Task GetInventoryByLocation_ReturnsLocationItems()
    {
        // Arrange
        var locationId = Guid.NewGuid();
        var items = new List<InventoryItemDto>
        {
            TestDataFactory.CreateInventoryItemDto(name: "Milk"),
            TestDataFactory.CreateInventoryItemDto(name: "Cheese")
        };

        _mockRepository
            .Setup(r => r.GetInventoryByStorageLocationAsync(locationId))
            .ReturnsAsync(items);

        // Act
        var result = await _controller.GetInventoryByLocation(locationId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(items);
    }

    #endregion

    #region GetInventoryReport Tests

    [Fact]
    public async Task GetInventoryReport_WithoutHouseholdId_ReturnsUserReport()
    {
        // Arrange
        var report = new InventoryReportDto
        {
            TotalItems = 50,
            ExpiringSoonItems = 5,
            LowStockItems = 3,
            RunningOutItems = 7
        };

        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);

        _mockRepository
            .Setup(r => r.GetInventoryReportAsync(_testUserId, null))
            .ReturnsAsync(report);

        // Act
        var result = await _controller.GetInventoryReport();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(report);
    }

    [Fact]
    public async Task GetInventoryReport_WithHouseholdId_ReturnsHouseholdReport()
    {
        // Arrange
        var householdId = Guid.NewGuid();
        var report = new InventoryReportDto
        {
            TotalItems = 100,
            ExpiringSoonItems = 10,
            LowStockItems = 8,
            RunningOutItems = 15
        };

        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);

        _mockRepository
            .Setup(r => r.GetInventoryReportAsync(_testUserId, householdId))
            .ReturnsAsync(report);

        // Act
        var result = await _controller.GetInventoryReport(householdId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(report);
    }

    #endregion

    #region GetUsageHistory Tests

    [Fact]
    public async Task GetUsageHistory_ReturnsItemHistory()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var history = new List<InventoryHistoryDto>
        {
            new InventoryHistoryDto { ActionType = "Added", QuantityChange = 2.0m, CreatedAt = DateTime.UtcNow.AddDays(-5) },
            new InventoryHistoryDto { ActionType = "Used", QuantityChange = -1.0m, CreatedAt = DateTime.UtcNow.AddDays(-2) }
        };

        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);

        _mockRepository
            .Setup(r => r.GetUsageHistoryAsync(itemId, 50))
            .ReturnsAsync(history);

        // Act
        var result = await _controller.GetUsageHistory(itemId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(history);
    }

    [Fact]
    public async Task GetUsageHistory_WithCustomLimit_UsesSpecifiedLimit()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var history = new List<InventoryHistoryDto>();

        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);

        _mockRepository
            .Setup(r => r.GetUsageHistoryAsync(itemId, 100))
            .ReturnsAsync(history);

        // Act
        var result = await _controller.GetUsageHistory(itemId, 100);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRepository.Verify(r => r.GetUsageHistoryAsync(itemId, 100), Times.Once);
    }

    #endregion
}
