using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using ExpressRecipe.InventoryService.Controllers;
using ExpressRecipe.InventoryService.Data;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace ExpressRecipe.InventoryService.Tests.Controllers;

public class StorageControllerTests
{
    private readonly Mock<IStorageLocationExtendedRepository> _mockStorage;
    private readonly Mock<IInventoryRepository> _mockInventory;
    private readonly StorageController _controller;
    private readonly Guid _householdId;

    public StorageControllerTests()
    {
        _mockStorage = new Mock<IStorageLocationExtendedRepository>();
        _mockInventory = new Mock<IInventoryRepository>();
        _controller = new StorageController(_mockStorage.Object, _mockInventory.Object);
        _householdId = Guid.NewGuid();
        _controller.ControllerContext = CreateContextWithHousehold(_householdId);
    }

    private static ControllerContext CreateContextWithHousehold(Guid householdId)
    {
        List<Claim> claims = new()
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim("household_id", householdId.ToString())
        };
        ClaimsIdentity identity = new(claims, "TestAuthentication");
        ClaimsPrincipal principal = new(identity);
        DefaultHttpContext httpContext = new() { User = principal };
        return new ControllerContext { HttpContext = httpContext };
    }

    private static ControllerContext CreateUnauthenticatedContext()
    {
        DefaultHttpContext httpContext = new() { User = new ClaimsPrincipal() };
        return new ControllerContext { HttpContext = httpContext };
    }

    private StorageLocationExtendedDto CreateLocation(
        Guid? id = null, Guid? householdId = null, string name = "Pantry",
        string? storageType = null, bool outageActive = false)
    {
        return new StorageLocationExtendedDto
        {
            Id = id ?? Guid.NewGuid(),
            HouseholdId = householdId ?? _householdId,
            Name = name,
            StorageType = storageType,
            OutageActive = outageActive,
            FoodCategories = new List<string>()
        };
    }

    #region GetLocations Tests

    [Fact]
    public async Task GetLocations_MissingHouseholdClaim_ReturnsUnauthorized()
    {
        // Arrange
        _controller.ControllerContext = CreateUnauthenticatedContext();

        // Act
        IActionResult result = await _controller.GetLocations(null, default);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetLocations_NoStorageAreas_ReturnsEmptyList()
    {
        // Arrange
        _mockStorage.Setup(r => r.GetLocationsAsync(_householdId, null, default))
                    .ReturnsAsync(new List<StorageLocationExtendedDto>());

        // Act
        IActionResult result = await _controller.GetLocations(null, default);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)result).Value.Should().BeEquivalentTo(new List<StorageLocationExtendedDto>());
    }

    [Fact]
    public async Task GetLocations_FilteredByAddressId_ReturnsFilteredList()
    {
        // Arrange
        Guid addressId = Guid.NewGuid();
        List<StorageLocationExtendedDto> locations = new()
        {
            CreateLocation(householdId: _householdId, name: "Kitchen Pantry")
        };
        _mockStorage.Setup(r => r.GetLocationsAsync(_householdId, addressId, default))
                    .ReturnsAsync(locations);

        // Act
        IActionResult result = await _controller.GetLocations(addressId, default);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)result).Value.Should().BeEquivalentTo(locations);
    }

    #endregion

    #region SetStorageType Tests

    [Fact]
    public async Task SetStorageType_LocationNotFound_ReturnsNotFound()
    {
        // Arrange
        Guid locationId = Guid.NewGuid();
        _mockStorage.Setup(r => r.GetLocationByIdAsync(locationId, default)).ReturnsAsync((StorageLocationExtendedDto?)null);

        // Act
        IActionResult result = await _controller.SetStorageType(locationId, new SetStorageTypeRequest(), default);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task SetStorageType_WrongHousehold_ReturnsForbid()
    {
        // Arrange
        Guid locationId = Guid.NewGuid();
        _mockStorage.Setup(r => r.GetLocationByIdAsync(locationId, default))
                    .ReturnsAsync(CreateLocation(id: locationId, householdId: Guid.NewGuid()));

        // Act
        IActionResult result = await _controller.SetStorageType(locationId, new SetStorageTypeRequest(), default);

        // Assert
        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task SetStorageType_UpdatesTypeAndEquipment_ReturnsNoContent()
    {
        // Arrange
        Guid locationId = Guid.NewGuid();
        Guid equipmentId = Guid.NewGuid();
        SetStorageTypeRequest req = new() { StorageType = "Freezer", EquipmentInstanceId = equipmentId };
        _mockStorage.Setup(r => r.GetLocationByIdAsync(locationId, default)).ReturnsAsync(CreateLocation(id: locationId));
        _mockStorage.Setup(r => r.UpdateStorageTypeAsync(locationId, "Freezer", equipmentId, default))
                    .Returns(Task.CompletedTask);

        // Act
        IActionResult result = await _controller.SetStorageType(locationId, req, default);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _mockStorage.Verify(r => r.UpdateStorageTypeAsync(locationId, "Freezer", equipmentId, default), Times.Once);
    }

    #endregion

    #region SetFoodCategories Tests

    [Fact]
    public async Task SetFoodCategories_LocationNotFound_ReturnsNotFound()
    {
        // Arrange
        Guid locationId = Guid.NewGuid();
        _mockStorage.Setup(r => r.GetLocationByIdAsync(locationId, default)).ReturnsAsync((StorageLocationExtendedDto?)null);

        // Act
        IActionResult result = await _controller.SetFoodCategories(locationId, new SetFoodCategoriesRequest(), default);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task SetFoodCategories_WrongHousehold_ReturnsForbid()
    {
        // Arrange
        Guid locationId = Guid.NewGuid();
        _mockStorage.Setup(r => r.GetLocationByIdAsync(locationId, default))
                    .ReturnsAsync(CreateLocation(id: locationId, householdId: Guid.NewGuid()));

        // Act
        IActionResult result = await _controller.SetFoodCategories(locationId, new SetFoodCategoriesRequest(), default);

        // Assert
        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task SetFoodCategories_ValidOwnership_ReturnsNoContent()
    {
        // Arrange
        Guid locationId = Guid.NewGuid();
        SetFoodCategoriesRequest req = new() { Categories = new List<string> { "Meat", "Dairy" } };
        _mockStorage.Setup(r => r.GetLocationByIdAsync(locationId, default)).ReturnsAsync(CreateLocation(id: locationId));
        _mockStorage.Setup(r => r.SetFoodCategoriesAsync(locationId, req.Categories, default))
                    .Returns(Task.CompletedTask);

        // Act
        IActionResult result = await _controller.SetFoodCategories(locationId, req, default);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _mockStorage.Verify(r => r.SetFoodCategoriesAsync(locationId, req.Categories, default), Times.Once);
    }

    #endregion

    #region SuggestLocations Tests

    [Fact]
    public async Task Suggest_MissingHouseholdClaim_ReturnsUnauthorized()
    {
        // Arrange
        _controller.ControllerContext = CreateUnauthenticatedContext();

        // Act
        IActionResult result = await _controller.Suggest("Meat", default);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Suggest_ForMeat_FreezerFirstPantrySecond()
    {
        // Arrange
        List<StorageLocationSuggestionDto> suggestions = new()
        {
            new StorageLocationSuggestionDto { StorageLocationId = Guid.NewGuid(), Name = "Chest Freezer", StorageType = "Freezer", MatchScore = 1 },
            new StorageLocationSuggestionDto { StorageLocationId = Guid.NewGuid(), Name = "Main Pantry", StorageType = "Pantry", MatchScore = 0 }
        };
        _mockStorage.Setup(r => r.SuggestLocationsAsync(_householdId, "Meat", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(suggestions);

        // Act
        IActionResult result = await _controller.Suggest("Meat", default);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        List<StorageLocationSuggestionDto> list = (List<StorageLocationSuggestionDto>)((OkObjectResult)result).Value!;
        list[0].MatchScore.Should().BeGreaterThan(list[1].MatchScore);
        list[0].StorageType.Should().Be("Freezer");
    }

    [Fact]
    public async Task Suggest_FreezerWithMeatCategory_MatchScoreIsTwo()
    {
        // Arrange
        List<StorageLocationSuggestionDto> suggestions = new()
        {
            new StorageLocationSuggestionDto { StorageLocationId = Guid.NewGuid(), Name = "Chest Freezer", StorageType = "Freezer", MatchScore = 2 }
        };
        _mockStorage.Setup(r => r.SuggestLocationsAsync(_householdId, "Meat", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(suggestions);

        // Act
        IActionResult result = await _controller.Suggest("Meat", default);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        List<StorageLocationSuggestionDto> list = (List<StorageLocationSuggestionDto>)((OkObjectResult)result).Value!;
        list[0].MatchScore.Should().Be(2);
    }

    #endregion

    #region Outage Tests

    [Fact]
    public async Task SetOutage_LocationNotFound_ReturnsNotFound()
    {
        // Arrange
        Guid locationId = Guid.NewGuid();
        _mockStorage.Setup(r => r.GetLocationByIdAsync(locationId, default)).ReturnsAsync((StorageLocationExtendedDto?)null);

        // Act
        IActionResult result = await _controller.SetOutage(locationId, new SetOutageRequest { OutageType = "PowerOutage" }, default);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task SetOutage_WrongHousehold_ReturnsForbid()
    {
        // Arrange
        Guid locationId = Guid.NewGuid();
        _mockStorage.Setup(r => r.GetLocationByIdAsync(locationId, default))
                    .ReturnsAsync(CreateLocation(id: locationId, householdId: Guid.NewGuid()));

        // Act
        IActionResult result = await _controller.SetOutage(locationId, new SetOutageRequest { OutageType = "PowerOutage" }, default);

        // Assert
        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task SetOutage_SetsOutageFields_ReturnsNoContent()
    {
        // Arrange
        Guid locationId = Guid.NewGuid();
        SetOutageRequest req = new() { OutageType = "PowerOutage", Notes = "Storm damage" };
        _mockStorage.Setup(r => r.GetLocationByIdAsync(locationId, default)).ReturnsAsync(CreateLocation(id: locationId));
        _mockStorage.Setup(r => r.SetOutageAsync(locationId, "PowerOutage", "Storm damage", default))
                    .Returns(Task.CompletedTask);

        // Act
        IActionResult result = await _controller.SetOutage(locationId, req, default);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _mockStorage.Verify(r => r.SetOutageAsync(locationId, "PowerOutage", "Storm damage", default), Times.Once);
    }

    [Fact]
    public async Task ClearOutage_LocationNotFound_ReturnsNotFound()
    {
        // Arrange
        Guid locationId = Guid.NewGuid();
        _mockStorage.Setup(r => r.GetLocationByIdAsync(locationId, default)).ReturnsAsync((StorageLocationExtendedDto?)null);

        // Act
        IActionResult result = await _controller.ClearOutage(locationId, default);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ClearOutage_WrongHousehold_ReturnsForbid()
    {
        // Arrange
        Guid locationId = Guid.NewGuid();
        _mockStorage.Setup(r => r.GetLocationByIdAsync(locationId, default))
                    .ReturnsAsync(CreateLocation(id: locationId, householdId: Guid.NewGuid()));

        // Act
        IActionResult result = await _controller.ClearOutage(locationId, default);

        // Assert
        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task ClearOutage_ValidOwnership_ReturnsNoContent()
    {
        // Arrange
        Guid locationId = Guid.NewGuid();
        _mockStorage.Setup(r => r.GetLocationByIdAsync(locationId, default)).ReturnsAsync(CreateLocation(id: locationId));
        _mockStorage.Setup(r => r.ClearOutageAsync(locationId, default)).Returns(Task.CompletedTask);

        // Act
        IActionResult result = await _controller.ClearOutage(locationId, default);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _mockStorage.Verify(r => r.ClearOutageAsync(locationId, default), Times.Once);
    }

    #endregion

    #region GetItemsInStorage Tests

    [Fact]
    public async Task GetItemsInStorage_DelegatesToInventoryRepository()
    {
        // Arrange
        Guid locationId = Guid.NewGuid();
        List<InventoryItemDto> items = new()
        {
            new InventoryItemDto { Id = Guid.NewGuid(), StorageLocationId = locationId, StorageLocationName = "Pantry" }
        };
        _mockInventory.Setup(r => r.GetInventoryByStorageLocationAsync(locationId)).ReturnsAsync(items);

        // Act
        IActionResult result = await _controller.GetItemsInStorage(locationId, default);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)result).Value.Should().BeEquivalentTo(items);
    }

    #endregion
}
