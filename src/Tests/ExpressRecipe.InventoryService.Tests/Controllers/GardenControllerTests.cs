using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using ExpressRecipe.InventoryService.Controllers;
using ExpressRecipe.InventoryService.Data;
using ExpressRecipe.MealPlanningService.Services;
using ExpressRecipe.InventoryService.Tests.Helpers;

namespace ExpressRecipe.InventoryService.Tests.Controllers;

public class GardenControllerTests
{
    private readonly Mock<IGardenRepository> _mockGarden;
    private readonly Mock<IInventoryRepository> _mockInventory;
    private readonly Mock<ISeasonalProduceService> _mockSeasonal;
    private readonly GardenController _controller;
    private readonly Guid _testUserId;
    private readonly Guid _testHouseholdId;

    public GardenControllerTests()
    {
        _mockGarden    = new Mock<IGardenRepository>();
        _mockInventory = new Mock<IInventoryRepository>();
        _mockSeasonal  = new Mock<ISeasonalProduceService>();
        _controller    = new GardenController(_mockGarden.Object, _mockInventory.Object, _mockSeasonal.Object);
        _testUserId    = Guid.NewGuid();
        _testHouseholdId = Guid.NewGuid();
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId, _testHouseholdId);
    }

    #region GetPlantings Tests

    [Fact]
    public async Task GetPlantings_ReturnsOkWithPlantings()
    {
        // Arrange
        List<GardenPlantingDto> plantings = new()
        {
            new GardenPlantingDto { Id = Guid.NewGuid(), HouseholdId = _testHouseholdId, PlantName = "Tomato", PlantType = "Tomato", RipeStatus = "Growing" },
            new GardenPlantingDto { Id = Guid.NewGuid(), HouseholdId = _testHouseholdId, PlantName = "Basil", PlantType = "Basil", RipeStatus = "Ready" }
        };

        _mockGarden
            .Setup(r => r.GetPlantingsAsync(_testHouseholdId, default))
            .ReturnsAsync(plantings);

        // Act
        IActionResult result = await _controller.GetPlantings(default);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        (result as OkObjectResult)!.Value.Should().BeEquivalentTo(plantings);
    }

    #endregion

    #region AddPlanting Tests

    [Fact]
    public async Task AddPlanting_WithoutExpectedRipeDate_ComputesFromPlantType()
    {
        // Arrange — PlantType=Tomato → 70 days
        DateOnly planted = DateOnly.FromDateTime(DateTime.Today);
        AddPlantingRequest request = new()
        {
            PlantName    = "Beefsteak",
            PlantType    = "Tomato",
            PlantedDate  = planted,
            QuantityPlanted = 2
        };

        Guid newId = Guid.NewGuid();
        DateOnly expectedRipe = planted.AddDays(70);

        _mockGarden
            .Setup(r => r.AddPlantingAsync(_testHouseholdId, "Beefsteak", null, "Tomato",
                planted, expectedRipe, 2, default))
            .ReturnsAsync(newId);

        // Act
        IActionResult result = await _controller.AddPlanting(request, default);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockGarden.Verify(r => r.AddPlantingAsync(_testHouseholdId, "Beefsteak", null, "Tomato",
            planted, expectedRipe, 2, default), Times.Once);
    }

    [Fact]
    public async Task AddPlanting_WithExplicitExpectedRipeDate_UsesThatDate()
    {
        // Arrange
        DateOnly planted       = DateOnly.FromDateTime(DateTime.Today);
        DateOnly explicitRipe  = planted.AddDays(60);
        AddPlantingRequest request = new()
        {
            PlantName        = "Cherry Tomato",
            PlantType        = "Tomato",
            PlantedDate      = planted,
            ExpectedRipeDate = explicitRipe,
            QuantityPlanted  = 4
        };

        Guid newId = Guid.NewGuid();
        _mockGarden
            .Setup(r => r.AddPlantingAsync(_testHouseholdId, "Cherry Tomato", null, "Tomato",
                planted, explicitRipe, 4, default))
            .ReturnsAsync(newId);

        // Act
        IActionResult result = await _controller.AddPlanting(request, default);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockGarden.Verify(r => r.AddPlantingAsync(_testHouseholdId, "Cherry Tomato", null, "Tomato",
            planted, explicitRipe, 4, default), Times.Once);
    }

    #endregion

    #region RecordHarvest Tests

    [Fact]
    public async Task RecordHarvest_WithAddToInventory_CreatesInventoryItem()
    {
        // Arrange
        Guid plantingId   = Guid.NewGuid();
        Guid harvestId    = Guid.NewGuid();
        Guid inventoryId  = Guid.NewGuid();

        RecordHarvestRequest request = new()
        {
            PlantName      = "Tomato",
            Quantity       = 5.0m,
            Unit           = "lb",
            AddToInventory = true
        };

        _mockGarden.Setup(r => r.RecordHarvestAsync(plantingId, 5.0m, "lb", null, default))
            .ReturnsAsync(harvestId);
        _mockInventory.Setup(r => r.CreateFromGardenHarvestAsync(_testUserId, _testHouseholdId,
                "Tomato", 5.0m, "lb", 7, default))
            .ReturnsAsync(inventoryId);
        _mockGarden.Setup(r => r.LinkHarvestToInventoryAsync(harvestId, inventoryId, default))
            .Returns(Task.CompletedTask);

        // Act
        IActionResult result = await _controller.RecordHarvest(plantingId, request, default);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockInventory.Verify(r => r.CreateFromGardenHarvestAsync(_testUserId, _testHouseholdId,
            "Tomato", 5.0m, "lb", 7, default), Times.Once);
        _mockGarden.Verify(r => r.LinkHarvestToInventoryAsync(harvestId, inventoryId, default), Times.Once);
    }

    [Fact]
    public async Task RecordHarvest_WithoutAddToInventory_DoesNotCreateInventoryItem()
    {
        // Arrange
        Guid plantingId = Guid.NewGuid();
        Guid harvestId  = Guid.NewGuid();

        RecordHarvestRequest request = new()
        {
            PlantName      = "Basil",
            Quantity       = 0.5m,
            Unit           = "oz",
            AddToInventory = false
        };

        _mockGarden.Setup(r => r.RecordHarvestAsync(plantingId, 0.5m, "oz", null, default))
            .ReturnsAsync(harvestId);

        // Act
        IActionResult result = await _controller.RecordHarvest(plantingId, request, default);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockInventory.Verify(r => r.CreateFromGardenHarvestAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<decimal>(),
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region GetSeasonalProduce Tests

    [Fact]
    public void GetSeasonalProduce_WithMonth_UsesSpecifiedMonth()
    {
        // Arrange
        List<string> expected = new() { "Tomato", "Corn", "Pepper" };
        _mockSeasonal
            .Setup(s => s.GetInSeasonProduce("northeast", It.Is<DateOnly>(d => d.Month == 8)))
            .Returns(expected);

        // Act
        IActionResult result = _controller.GetSeasonalProduce("northeast", 8);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        (result as OkObjectResult)!.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void GetSeasonalProduce_WithoutMonth_UsesTodaysMonth()
    {
        // Arrange
        List<string> expected = new() { "Kale", "Apple" };
        _mockSeasonal
            .Setup(s => s.GetInSeasonProduce("northeast", It.IsAny<DateOnly>()))
            .Returns(expected);

        // Act
        IActionResult result = _controller.GetSeasonalProduce("northeast", null);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region UpdatePlanting Tests

    [Fact]
    public async Task UpdatePlanting_ReturnsNoContent()
    {
        // Arrange
        Guid plantingId = Guid.NewGuid();
        UpdatePlantingRequest request = new()
        {
            ExpectedRipeDate         = DateOnly.FromDateTime(DateTime.Today.AddDays(7)),
            IsActive                 = true,
            RipeCheckReminderEnabled = true
        };

        _mockGarden.Setup(r => r.UpdatePlantingAsync(plantingId, request.ExpectedRipeDate,
            true, true, default)).Returns(Task.CompletedTask);

        // Act
        IActionResult result = await _controller.UpdatePlanting(plantingId, request, default);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    #endregion
}
