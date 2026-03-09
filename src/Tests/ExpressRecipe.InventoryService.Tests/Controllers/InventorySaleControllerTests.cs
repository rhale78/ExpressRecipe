using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ExpressRecipe.InventoryService.Controllers;
using ExpressRecipe.InventoryService.Data;
using ExpressRecipe.InventoryService.Tests.Helpers;

namespace ExpressRecipe.InventoryService.Tests.Controllers;

public class InventorySaleControllerTests
{
    private readonly Mock<IInventoryRepository> _mockRepository;
    private readonly Mock<IInventorySaleRepository> _mockSaleRepository;
    private readonly Mock<ILogger<InventoryController>> _mockLogger;
    private readonly InventoryController _controller;
    private readonly Guid _testUserId;
    private readonly Guid _testHouseholdId;

    public InventorySaleControllerTests()
    {
        _mockRepository = new Mock<IInventoryRepository>();
        _mockSaleRepository = new Mock<IInventorySaleRepository>();
        _mockLogger = new Mock<ILogger<InventoryController>>();
        _controller = new InventoryController(
            _mockLogger.Object, _mockRepository.Object, _mockSaleRepository.Object);
        _testUserId = Guid.NewGuid();
        _testHouseholdId = Guid.NewGuid();
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SellItem
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SellItem_ReturnsOk_WithSaleId_WhenQuantitySufficient()
    {
        // Arrange
        Guid itemId = Guid.NewGuid();
        Guid saleId = Guid.NewGuid();

        InventoryItemDto item = TestDataFactory.CreateInventoryItemDto(
            id: itemId, quantity: 10.0m);
        item.HouseholdId = _testHouseholdId;

        SellItemRequest request = new SellItemRequest
        {
            Quantity = 3.0m,
            Unit     = "dozen",
            Buyer    = "Farmers Market",
        };

        _mockRepository
            .Setup(r => r.GetInventoryItemAsync(itemId, _testUserId))
            .ReturnsAsync(item);

        _mockRepository
            .Setup(r => r.IsUserMemberOfHouseholdAsync(_testHouseholdId, _testUserId))
            .ReturnsAsync(true);

        _mockSaleRepository
            .Setup(r => r.RecordSaleAsync(
                _testHouseholdId, itemId,
                It.IsAny<string>(),
                request.Quantity, request.Unit,
                It.IsAny<DateOnly>(),
                request.Buyer, request.Notes,
                request.AutoRemoveOnZero))
            .ReturnsAsync(saleId);

        // Act
        IActionResult result = await _controller.SellItem(itemId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockSaleRepository.Verify(r => r.RecordSaleAsync(
            _testHouseholdId, itemId,
            It.IsAny<string>(),
            request.Quantity, request.Unit,
            It.IsAny<DateOnly>(),
            request.Buyer, request.Notes,
            request.AutoRemoveOnZero), Times.Once);
    }

    [Fact]
    public async Task SellItem_ReturnsUnprocessableEntity_WhenInsufficientQuantity()
    {
        // Arrange
        Guid itemId = Guid.NewGuid();

        InventoryItemDto item = TestDataFactory.CreateInventoryItemDto(id: itemId, quantity: 1.0m);
        item.HouseholdId = _testHouseholdId;

        SellItemRequest request = new SellItemRequest
        {
            Quantity = 5.0m,
            Unit     = "lb",
        };

        _mockRepository
            .Setup(r => r.GetInventoryItemAsync(itemId, _testUserId))
            .ReturnsAsync(item);

        _mockRepository
            .Setup(r => r.IsUserMemberOfHouseholdAsync(_testHouseholdId, _testUserId))
            .ReturnsAsync(true);

        _mockSaleRepository
            .Setup(r => r.RecordSaleAsync(
                It.IsAny<Guid>(), It.IsAny<Guid?>(),
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(),
                It.IsAny<DateOnly>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>()))
            .ThrowsAsync(new InvalidOperationException("Insufficient quantity. Available: 1, requested: 5."));

        // Act
        IActionResult result = await _controller.SellItem(itemId, request);

        // Assert
        result.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    [Fact]
    public async Task SellItem_ReturnsNotFound_WhenItemDoesNotExist()
    {
        // Arrange
        Guid itemId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.GetInventoryItemAsync(itemId, _testUserId))
            .ReturnsAsync((InventoryItemDto?)null);

        // Act
        IActionResult result = await _controller.SellItem(itemId, new SellItemRequest { Quantity = 1, Unit = "lb" });

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task SellItem_ReturnsUnauthorized_WhenNoUser()
    {
        // Arrange
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        // Act
        IActionResult result = await _controller.SellItem(Guid.NewGuid(), new SellItemRequest { Quantity = 1, Unit = "lb" });

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetSales
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSales_ReturnsOk_WithSalesList()
    {
        // Arrange
        List<InventorySaleDto> sales = new List<InventorySaleDto>
        {
            new InventorySaleDto
            {
                Id          = Guid.NewGuid(),
                HouseholdId = _testHouseholdId,
                ProductName = "Hen House A Eggs",
                Quantity    = 2.5m,
                Unit        = "dozen",
                SaleDate    = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
                Buyer       = "Neighbor",
                CreatedAt   = DateTime.UtcNow.AddDays(-2),
            },
        };

        _mockRepository
            .Setup(r => r.IsUserMemberOfHouseholdAsync(_testHouseholdId, _testUserId))
            .ReturnsAsync(true);

        _mockSaleRepository
            .Setup(r => r.GetSalesAsync(_testHouseholdId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync(sales);

        // Act
        IActionResult result = await _controller.GetSales(_testHouseholdId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        okResult.Value.Should().BeEquivalentTo(sales);
    }

    [Fact]
    public async Task GetSales_FiltersByItem_WhenItemIdProvided()
    {
        // Arrange
        Guid itemId = Guid.NewGuid();
        List<InventorySaleDto> sales = new List<InventorySaleDto>
        {
            new InventorySaleDto
            {
                Id              = Guid.NewGuid(),
                HouseholdId     = _testHouseholdId,
                InventoryItemId = itemId,
                ProductName     = "Hen House A Eggs",
                Quantity        = 1m,
                Unit            = "dozen",
                SaleDate        = DateOnly.FromDateTime(DateTime.UtcNow),
                CreatedAt       = DateTime.UtcNow,
            },
        };

        _mockRepository
            .Setup(r => r.IsUserMemberOfHouseholdAsync(_testHouseholdId, _testUserId))
            .ReturnsAsync(true);

        _mockSaleRepository
            .Setup(r => r.GetSalesByItemAsync(itemId))
            .ReturnsAsync(sales);

        // Act
        IActionResult result = await _controller.GetSales(_testHouseholdId, itemId: itemId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockSaleRepository.Verify(r => r.GetSalesByItemAsync(itemId), Times.Once);
    }

    [Fact]
    public async Task GetSales_ReturnsForbid_WhenUserIsNotMember()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.IsUserMemberOfHouseholdAsync(_testHouseholdId, _testUserId))
            .ReturnsAsync(false);

        // Act
        IActionResult result = await _controller.GetSales(_testHouseholdId);

        // Assert
        result.Should().BeOfType<ForbidResult>();
    }
}
