using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ExpressRecipe.InventoryService.Controllers;
using ExpressRecipe.InventoryService.Data;
using ExpressRecipe.InventoryService.Tests.Helpers;

namespace ExpressRecipe.InventoryService.Tests.Controllers;

public class HouseholdControllerTests
{
    private readonly Mock<IInventoryRepository> _mockRepository;
    private readonly Mock<ILogger<HouseholdController>> _mockLogger;
    private readonly HouseholdController _controller;
    private readonly Guid _testUserId;

    public HouseholdControllerTests()
    {
        _mockRepository = new Mock<IInventoryRepository>();
        _mockLogger = new Mock<ILogger<HouseholdController>>();
        _controller = new HouseholdController(_mockLogger.Object, _mockRepository.Object);
        _testUserId = Guid.NewGuid();
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);
    }

    #region CreateHousehold Tests

    [Fact]
    public async Task CreateHousehold_WithValidRequest_ReturnsCreatedAtAction()
    {
        // Arrange
        var request = TestDataFactory.CreateHouseholdRequest("My Household", "Test description");
        var householdId = Guid.NewGuid();
        var householdDto = TestDataFactory.CreateHouseholdDto(householdId, request.Name);

        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);

        _mockRepository
            .Setup(r => r.CreateHouseholdAsync(_testUserId, request.Name, request.Description))
            .ReturnsAsync(householdId);

        _mockRepository
            .Setup(r => r.GetHouseholdByIdAsync(householdId))
            .ReturnsAsync(householdDto);

        // Act
        var result = await _controller.CreateHousehold(request);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
        var createdAtResult = result as CreatedAtActionResult;
        createdAtResult!.Value.Should().BeEquivalentTo(householdDto);
        createdAtResult.ActionName.Should().Be(nameof(HouseholdController.GetHousehold));

        _mockRepository.Verify(r => r.CreateHouseholdAsync(_testUserId, request.Name, request.Description), Times.Once);
        _mockRepository.Verify(r => r.GetHouseholdByIdAsync(householdId), Times.Once);
    }

    [Fact]
    public async Task CreateHousehold_WithEmptyName_ShouldStillCallRepository()
    {
        // Arrange
        var request = TestDataFactory.CreateHouseholdRequest("", null);
        var householdId = Guid.NewGuid();
        var householdDto = TestDataFactory.CreateHouseholdDto(householdId, "");

        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);

        _mockRepository
            .Setup(r => r.CreateHouseholdAsync(_testUserId, "", null))
            .ReturnsAsync(householdId);

        _mockRepository
            .Setup(r => r.GetHouseholdByIdAsync(householdId))
            .ReturnsAsync(householdDto);

        // Act
        var result = await _controller.CreateHousehold(request);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
        _mockRepository.Verify(r => r.CreateHouseholdAsync(_testUserId, "", null), Times.Once);
    }

    #endregion

    #region GetHouseholds Tests

    [Fact]
    public async Task GetHouseholds_ReturnsOkWithHouseholdsList()
    {
        // Arrange
        var households = new List<HouseholdDto>
        {
            TestDataFactory.CreateHouseholdDto(name: "Household 1"),
            TestDataFactory.CreateHouseholdDto(name: "Household 2")
        };

        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);

        _mockRepository
            .Setup(r => r.GetUserHouseholdsAsync(_testUserId))
            .ReturnsAsync(households);

        // Act
        var result = await _controller.GetHouseholds();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(households);

        _mockRepository.Verify(r => r.GetUserHouseholdsAsync(_testUserId), Times.Once);
    }

    [Fact]
    public async Task GetHouseholds_WhenNoHouseholds_ReturnsEmptyList()
    {
        // Arrange
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);

        _mockRepository
            .Setup(r => r.GetUserHouseholdsAsync(_testUserId))
            .ReturnsAsync(new List<HouseholdDto>());

        // Act
        var result = await _controller.GetHouseholds();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var households = okResult!.Value as List<HouseholdDto>;
        households.Should().NotBeNull();
        households!.Count.Should().Be(0);
    }

    #endregion

    #region GetHousehold Tests

    [Fact]
    public async Task GetHousehold_WithValidId_ReturnsOkWithHousehold()
    {
        // Arrange
        var householdId = Guid.NewGuid();
        var household = TestDataFactory.CreateHouseholdDto(householdId, "Test Household");

        _mockRepository
            .Setup(r => r.IsUserMemberOfHouseholdAsync(householdId, _testUserId))
            .ReturnsAsync(true);
        _mockRepository
            .Setup(r => r.GetHouseholdByIdAsync(householdId))
            .ReturnsAsync(household);

        // Act
        var result = await _controller.GetHousehold(householdId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(household);

        _mockRepository.Verify(r => r.GetHouseholdByIdAsync(householdId), Times.Once);
    }

    [Fact]
    public async Task GetHousehold_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var householdId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.IsUserMemberOfHouseholdAsync(householdId, _testUserId))
            .ReturnsAsync(true);
        _mockRepository
            .Setup(r => r.GetHouseholdByIdAsync(householdId))
            .ReturnsAsync((HouseholdDto?)null);

        // Act
        var result = await _controller.GetHousehold(householdId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region AddMember Tests

    [Fact]
    public async Task AddMember_WithValidRequest_ReturnsOkWithMemberId()
    {
        // Arrange
        var householdId = Guid.NewGuid();
        var newUserId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var request = TestDataFactory.CreateAddMemberRequest(newUserId, "Member");

        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);

        _mockRepository
            .Setup(r => r.IsUserMemberOfHouseholdAsync(householdId, _testUserId))
            .ReturnsAsync(true);
        _mockRepository
            .Setup(r => r.AddHouseholdMemberAsync(householdId, newUserId, "Member", _testUserId))
            .ReturnsAsync(memberId);

        // Act
        var result = await _controller.AddMember(householdId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var response = okResult!.Value;
        response.Should().NotBeNull();

        _mockRepository.Verify(r => r.AddHouseholdMemberAsync(householdId, newUserId, "Member", _testUserId), Times.Once);
    }

    [Fact]
    public async Task AddMember_WithAdminRole_CallsRepositoryWithAdminRole()
    {
        // Arrange
        var householdId = Guid.NewGuid();
        var newUserId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var request = TestDataFactory.CreateAddMemberRequest(newUserId, "Admin");

        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);

        _mockRepository
            .Setup(r => r.IsUserMemberOfHouseholdAsync(householdId, _testUserId))
            .ReturnsAsync(true);
        _mockRepository
            .Setup(r => r.AddHouseholdMemberAsync(householdId, newUserId, "Admin", _testUserId))
            .ReturnsAsync(memberId);

        // Act
        var result = await _controller.AddMember(householdId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRepository.Verify(r => r.AddHouseholdMemberAsync(householdId, newUserId, "Admin", _testUserId), Times.Once);
    }

    #endregion

    #region Address Management Tests

    [Fact]
    public async Task CreateAddress_WithValidRequest_ReturnsCreatedAtAction()
    {
        // Arrange
        var householdId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var request = TestDataFactory.CreateAddressRequest("Home", "123 Main St");
        var addressDto = TestDataFactory.CreateAddressDto(addressId, householdId, "Home");

        _mockRepository
            .Setup(r => r.IsUserMemberOfHouseholdAsync(householdId, _testUserId))
            .ReturnsAsync(true);
        _mockRepository
            .Setup(r => r.CreateAddressAsync(householdId, request.Name, request.Street, request.City, 
                request.State, request.ZipCode, request.Country ?? "", request.Latitude, request.Longitude, request.IsPrimary))
            .ReturnsAsync(addressId);

        _mockRepository
            .Setup(r => r.GetAddressByIdAsync(addressId))
            .ReturnsAsync(addressDto);

        // Act
        var result = await _controller.CreateAddress(householdId, request);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
        var createdAtResult = result as CreatedAtActionResult;
        createdAtResult!.Value.Should().BeEquivalentTo(addressDto);
    }

    [Fact]
    public async Task GetHouseholdAddresses_ReturnsOkWithAddressList()
    {
        // Arrange
        var householdId = Guid.NewGuid();
        var addresses = new List<AddressDto>
        {
            TestDataFactory.CreateAddressDto(householdId: householdId, name: "Main House"),
            TestDataFactory.CreateAddressDto(householdId: householdId, name: "Vacation Home")
        };

        _mockRepository
            .Setup(r => r.IsUserMemberOfHouseholdAsync(householdId, _testUserId))
            .ReturnsAsync(true);
        _mockRepository
            .Setup(r => r.GetHouseholdAddressesAsync(householdId))
            .ReturnsAsync(addresses);

        // Act
        var result = await _controller.GetAddresses(householdId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(addresses);
    }

    [Fact]
    public async Task DetectNearestAddress_WithNearbyAddress_ReturnsOkWithAddress()
    {
        // Arrange
        var householdId = Guid.NewGuid();
        var request = new DetectAddressRequest
        {
            Latitude = 40.7128m,
            Longitude = -74.0060m,
            MaxDistanceKm = 1.0
        };
        var addressDto = TestDataFactory.CreateAddressDto(householdId: householdId);
        addressDto.DistanceKm = 0.5;

        _mockRepository
            .Setup(r => r.IsUserMemberOfHouseholdAsync(householdId, _testUserId))
            .ReturnsAsync(true);
        _mockRepository
            .Setup(r => r.DetectNearestAddressAsync(householdId, request.Latitude, request.Longitude, 1.0))
            .ReturnsAsync(addressDto);

        // Act
        var result = await _controller.DetectNearestAddress(householdId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(addressDto);
    }

    [Fact]
    public async Task DetectNearestAddress_WithNoNearbyAddress_ReturnsNotFound()
    {
        // Arrange
        var householdId = Guid.NewGuid();
        var request = new DetectAddressRequest
        {
            Latitude = 40.7128m,
            Longitude = -74.0060m,
            MaxDistanceKm = 1.0
        };

        _mockRepository
            .Setup(r => r.IsUserMemberOfHouseholdAsync(householdId, _testUserId))
            .ReturnsAsync(true);
        _mockRepository
            .Setup(r => r.DetectNearestAddressAsync(householdId, request.Latitude, request.Longitude, 1.0))
            .ReturnsAsync((AddressDto?)null);

        // Act
        var result = await _controller.DetectNearestAddress(householdId, request);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region Storage Location Tests

    [Fact]
    public async Task GetStorageLocationsByAddress_ReturnsOkWithLocationsList()
    {
        // Arrange
        var addressId = Guid.NewGuid();
        var householdId = Guid.NewGuid();
        var locations = new List<StorageLocationDto>
        {
            TestDataFactory.CreateStorageLocationDto(addressId: addressId, name: "Fridge"),
            TestDataFactory.CreateStorageLocationDto(addressId: addressId, name: "Freezer")
        };
        var addressDto = TestDataFactory.CreateAddressDto(addressId, householdId);

        _mockRepository
            .Setup(r => r.GetAddressByIdAsync(addressId))
            .ReturnsAsync(addressDto);
        _mockRepository
            .Setup(r => r.IsUserMemberOfHouseholdAsync(householdId, _testUserId))
            .ReturnsAsync(true);
        _mockRepository
            .Setup(r => r.GetStorageLocationsByAddressAsync(addressId))
            .ReturnsAsync(locations);

        var inventoryController = new InventoryController(
            new Mock<ILogger<InventoryController>>().Object,
            _mockRepository.Object);
        inventoryController.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);

        // Act
        var result = await inventoryController.GetLocationsByAddress(addressId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(locations);

        _mockRepository.Verify(r => r.GetStorageLocationsByAddressAsync(addressId), Times.Once);
    }

    [Fact]
    public async Task GetStorageLocationsByHousehold_ReturnsExpectedLocations()
    {
        // Arrange
        var householdId = Guid.NewGuid();
        var locations = new List<StorageLocationDto>
        {
            TestDataFactory.CreateStorageLocationDto(name: "Kitchen Pantry"),
            TestDataFactory.CreateStorageLocationDto(name: "Garage Freezer")
        };

        _mockRepository
            .Setup(r => r.IsUserMemberOfHouseholdAsync(householdId, _testUserId))
            .ReturnsAsync(true);
        _mockRepository
            .Setup(r => r.GetStorageLocationsByHouseholdAsync(householdId))
            .ReturnsAsync(locations);

        var inventoryController = new InventoryController(
            new Mock<ILogger<InventoryController>>().Object,
            _mockRepository.Object);
        inventoryController.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);

        // Act
        var result = await inventoryController.GetLocationsByHousehold(householdId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(locations);

        _mockRepository.Verify(r => r.GetStorageLocationsByHouseholdAsync(householdId), Times.Once);
    }

    #endregion
}
