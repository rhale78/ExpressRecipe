using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ExpressRecipe.InventoryService.Controllers;
using ExpressRecipe.InventoryService.Data;
using ExpressRecipe.InventoryService.Tests.Helpers;

namespace ExpressRecipe.InventoryService.Tests.Controllers;

public class LivestockControllerTests
{
    private readonly Mock<ILivestockRepository> _mockLivestock;
    private readonly Mock<IInventoryRepository> _mockInventory;
    private readonly Mock<ILogger<LivestockController>> _mockLogger;
    private readonly LivestockController _controller;
    private readonly Guid _testUserId;
    private readonly Guid _testHouseholdId;

    public LivestockControllerTests()
    {
        _mockLivestock = new Mock<ILivestockRepository>();
        _mockInventory = new Mock<IInventoryRepository>();
        _mockLogger = new Mock<ILogger<LivestockController>>();
        _controller = new LivestockController(_mockLogger.Object, _mockLivestock.Object, _mockInventory.Object);
        _testUserId = Guid.NewGuid();
        _testHouseholdId = Guid.NewGuid();
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetAnimals
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAnimals_ReturnsOkWithAnimals_WhenUserIsMember()
    {
        // Arrange
        List<LivestockAnimalDto> animals = new List<LivestockAnimalDto>
        {
            CreateAnimalDto("Hen House A", "Chicken", "Egg"),
            CreateAnimalDto("Bessie", "Cow", "Dairy"),
        };

        _mockInventory
            .Setup(r => r.IsUserMemberOfHouseholdAsync(_testHouseholdId, _testUserId))
            .ReturnsAsync(true);

        _mockLivestock
            .Setup(r => r.GetAnimalsAsync(_testHouseholdId, true))
            .ReturnsAsync(animals);

        // Act
        IActionResult result = await _controller.GetAnimals(_testHouseholdId, true);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        okResult.Value.Should().BeEquivalentTo(animals);
    }

    [Fact]
    public async Task GetAnimals_ReturnsForbid_WhenUserIsNotMember()
    {
        // Arrange
        _mockInventory
            .Setup(r => r.IsUserMemberOfHouseholdAsync(_testHouseholdId, _testUserId))
            .ReturnsAsync(false);

        // Act
        IActionResult result = await _controller.GetAnimals(_testHouseholdId, true);

        // Assert
        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetAnimals_ReturnsUnauthorized_WhenNoUser()
    {
        // Arrange
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        // Act
        IActionResult result = await _controller.GetAnimals(_testHouseholdId, true);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AddAnimal
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAnimal_ReturnsCreated_WhenValid()
    {
        // Arrange
        Guid newId = Guid.NewGuid();
        AddAnimalRequest request = new AddAnimalRequest
        {
            HouseholdId        = _testHouseholdId,
            Name               = "Meat Rabbits",
            AnimalType         = "Rabbit",
            ProductionCategory = "Meat",
            IsFlockOrHerd      = true,
            Count              = 8,
        };

        LivestockAnimalDto created = CreateAnimalDto("Meat Rabbits", "Rabbit", "Meat", id: newId);

        _mockInventory
            .Setup(r => r.IsUserMemberOfHouseholdAsync(_testHouseholdId, _testUserId))
            .ReturnsAsync(true);

        _mockLivestock
            .Setup(r => r.AddAnimalAsync(_testHouseholdId, request.Name, request.AnimalType,
                request.ProductionCategory, request.IsFlockOrHerd, request.Count,
                request.AcquiredDate, request.BreedNotes, request.Notes))
            .ReturnsAsync(newId);

        _mockLivestock
            .Setup(r => r.GetAnimalByIdAsync(newId))
            .ReturnsAsync(created);

        // Act
        IActionResult result = await _controller.AddAnimal(request);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
        CreatedAtActionResult createdResult = (CreatedAtActionResult)result;
        createdResult.Value.Should().BeEquivalentTo(created);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UpdateAnimal
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAnimal_ReturnsOk_WhenValid()
    {
        // Arrange
        Guid animalId = Guid.NewGuid();
        LivestockAnimalDto existing = CreateAnimalDto("Old Name", "Chicken", "Egg", id: animalId);
        LivestockAnimalDto updated  = CreateAnimalDto("New Name", "Chicken", "Egg", id: animalId);

        UpdateAnimalRequest request = new UpdateAnimalRequest
        {
            Name     = "New Name",
            Count    = 12,
            IsActive = true,
        };

        _mockLivestock.Setup(r => r.GetAnimalByIdAsync(animalId)).ReturnsAsync(existing);
        _mockInventory
            .Setup(r => r.IsUserMemberOfHouseholdAsync(existing.HouseholdId, _testUserId))
            .ReturnsAsync(true);
        _mockLivestock
            .Setup(r => r.UpdateAnimalAsync(animalId, request.Name, request.Count,
                request.IsActive, request.BreedNotes, request.Notes))
            .Returns(Task.CompletedTask);
        _mockLivestock.SetupSequence(r => r.GetAnimalByIdAsync(animalId))
            .ReturnsAsync(existing)
            .ReturnsAsync(updated);

        // Act
        IActionResult result = await _controller.UpdateAnimal(animalId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdateAnimal_ReturnsNotFound_WhenAnimalDoesNotExist()
    {
        // Arrange
        Guid animalId = Guid.NewGuid();
        _mockLivestock.Setup(r => r.GetAnimalByIdAsync(animalId)).ReturnsAsync((LivestockAnimalDto?)null);

        // Act
        IActionResult result = await _controller.UpdateAnimal(animalId, new UpdateAnimalRequest { Name = "x", Count = 1, IsActive = true });

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DeleteAnimal
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAnimal_ReturnsNoContent_WhenValid()
    {
        // Arrange
        Guid animalId = Guid.NewGuid();
        LivestockAnimalDto existing = CreateAnimalDto("Bessie", "Cow", "Dairy", id: animalId);

        _mockLivestock.Setup(r => r.GetAnimalByIdAsync(animalId)).ReturnsAsync(existing);
        _mockInventory
            .Setup(r => r.IsUserMemberOfHouseholdAsync(existing.HouseholdId, _testUserId))
            .ReturnsAsync(true);
        _mockLivestock
            .Setup(r => r.SoftDeleteAnimalAsync(animalId))
            .Returns(Task.CompletedTask);

        // Act
        IActionResult result = await _controller.DeleteAnimal(animalId);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _mockLivestock.Verify(r => r.SoftDeleteAnimalAsync(animalId), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // LogProduction
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LogProduction_ReturnsOk_WithProductionId()
    {
        // Arrange
        Guid animalId    = Guid.NewGuid();
        Guid productionId = Guid.NewGuid();
        LivestockAnimalDto animal = CreateAnimalDto("Hen House A", "Chicken", "Egg", id: animalId);

        LogProductionRequest request = new LogProductionRequest
        {
            ProductionDate   = DateOnly.FromDateTime(DateTime.UtcNow),
            ProductType      = "Eggs",
            Quantity         = 12,
            Unit             = "count",
            AddToInventory   = false,
        };

        _mockLivestock.Setup(r => r.GetAnimalByIdAsync(animalId)).ReturnsAsync(animal);
        _mockInventory
            .Setup(r => r.IsUserMemberOfHouseholdAsync(animal.HouseholdId, _testUserId))
            .ReturnsAsync(true);
        _mockLivestock
            .Setup(r => r.LogProductionAsync(animalId, _testUserId, request.ProductionDate, request.ProductType,
                request.Quantity, request.Unit, request.AddToInventory,
                request.StorageLocationId.HasValue ? request.StorageLocationId.Value.ToString() : null,
                request.Notes))
            .ReturnsAsync(productionId);

        // Act
        IActionResult result = await _controller.LogProduction(animalId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetProductionSummary
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetProductionSummary_ReturnsOk_WithSummary()
    {
        // Arrange
        List<LivestockProductionSummaryDto> summary = new List<LivestockProductionSummaryDto>
        {
            new LivestockProductionSummaryDto
            {
                ProductType   = "Eggs",
                TotalQuantity = 210m,
                Unit          = "count",
                DaysRecorded  = 30,
                DailyAverage  = 7m,
            },
        };

        _mockInventory
            .Setup(r => r.IsUserMemberOfHouseholdAsync(_testHouseholdId, _testUserId))
            .ReturnsAsync(true);

        _mockLivestock
            .Setup(r => r.GetProductionSummaryAsync(
                _testHouseholdId,
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>()))
            .ReturnsAsync(summary);

        // Act
        IActionResult result = await _controller.GetProductionSummary(_testHouseholdId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        okResult.Value.Should().BeEquivalentTo(summary);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RecordHarvest
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecordHarvest_ReturnsOk_WithHarvestId()
    {
        // Arrange
        Guid animalId  = Guid.NewGuid();
        Guid harvestId = Guid.NewGuid();
        LivestockAnimalDto animal = CreateAnimalDto("Meat Rabbits", "Rabbit", "Meat", id: animalId);

        RecordLivestockHarvestRequest request = new RecordLivestockHarvestRequest
        {
            HarvestDate    = DateOnly.FromDateTime(DateTime.UtcNow),
            CountHarvested = 2,
            LiveWeightLbs  = 10m,
            AddToInventory = false,
            YieldItems     = new List<HarvestYieldItem>
            {
                new HarvestYieldItem { Cut = "Whole", WeightLbs = 4m, Unit = "lb" },
            },
        };

        _mockLivestock.Setup(r => r.GetAnimalByIdAsync(animalId)).ReturnsAsync(animal);
        _mockInventory
            .Setup(r => r.IsUserMemberOfHouseholdAsync(animal.HouseholdId, _testUserId))
            .ReturnsAsync(true);
        _mockLivestock
            .Setup(r => r.RecordHarvestAsync(
                animalId, _testUserId, request.HarvestDate, request.CountHarvested,
                request.LiveWeightLbs, request.ProcessedWeightLbs,
                request.ProcessedBy, request.AddToInventory,
                request.YieldItems,
                request.StorageLocationId.HasValue ? request.StorageLocationId.Value.ToString() : null,
                request.Notes))
            .ReturnsAsync(harvestId);

        // Act
        IActionResult result = await _controller.RecordHarvest(animalId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetHarvests
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetHarvests_ReturnsOk_WithHarvestList()
    {
        // Arrange
        Guid animalId = Guid.NewGuid();
        LivestockAnimalDto animal = CreateAnimalDto("Meat Rabbits", "Rabbit", "Meat", id: animalId);

        List<LivestockHarvestDto> harvests = new List<LivestockHarvestDto>
        {
            new LivestockHarvestDto
            {
                Id             = Guid.NewGuid(),
                AnimalId       = animalId,
                AnimalName     = "Meat Rabbits",
                HarvestDate    = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)),
                CountHarvested = 2,
                AddedToInventory = false,
                CreatedAt      = DateTime.UtcNow.AddDays(-7),
            },
        };

        _mockLivestock.Setup(r => r.GetAnimalByIdAsync(animalId)).ReturnsAsync(animal);
        _mockInventory
            .Setup(r => r.IsUserMemberOfHouseholdAsync(animal.HouseholdId, _testUserId))
            .ReturnsAsync(true);
        _mockLivestock
            .Setup(r => r.GetHarvestsAsync(animalId))
            .ReturnsAsync(harvests);

        // Act
        IActionResult result = await _controller.GetHarvests(animalId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        okResult.Value.Should().BeEquivalentTo(harvests);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private LivestockAnimalDto CreateAnimalDto(
        string name, string animalType, string productionCategory,
        Guid? id = null)
    {
        return new LivestockAnimalDto
        {
            Id                 = id ?? Guid.NewGuid(),
            HouseholdId        = _testHouseholdId,
            Name               = name,
            AnimalType         = animalType,
            ProductionCategory = productionCategory,
            IsFlockOrHerd      = true,
            Count              = 5,
            IsActive           = true,
            CreatedAt          = DateTime.UtcNow,
        };
    }
}
