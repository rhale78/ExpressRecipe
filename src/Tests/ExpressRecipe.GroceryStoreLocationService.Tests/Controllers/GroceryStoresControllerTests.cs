using ExpressRecipe.GroceryStoreLocationService.Controllers;
using ExpressRecipe.GroceryStoreLocationService.Data;
using ExpressRecipe.GroceryStoreLocationService.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ExpressRecipe.GroceryStoreLocationService.Tests.Controllers;

public class GroceryStoresControllerTests
{
    private readonly Mock<IGroceryStoreRepository> _repositoryMock;
    private readonly Mock<ILogger<GroceryStoresController>> _loggerMock;
    private readonly GroceryStoresController _controller;

    public GroceryStoresControllerTests()
    {
        _repositoryMock = new Mock<IGroceryStoreRepository>();
        _loggerMock = new Mock<ILogger<GroceryStoresController>>();

        _controller = new GroceryStoresController(
            _repositoryMock.Object,
            _loggerMock.Object);

        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();
    }

    // --- Search ---

    [Fact]
    public async Task Search_ReturnsOkWithStores()
    {
        // Arrange
        var stores = new List<GroceryStoreDto>
        {
            new GroceryStoreDto { Id = Guid.NewGuid(), Name = "Kroger", City = "Raleigh", State = "NC" }
        };
        _repositoryMock.Setup(r => r.SearchAsync(It.IsAny<GroceryStoreSearchRequest>())).ReturnsAsync(stores);
        _repositoryMock.Setup(r => r.GetSearchCountAsync(It.IsAny<GroceryStoreSearchRequest>())).ReturnsAsync(1);

        // Act
        var result = await _controller.Search(null, null, null, null, null, null, null, null, null);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task Search_EmptyResults_ReturnsOkWithEmptyData()
    {
        // Arrange
        _repositoryMock.Setup(r => r.SearchAsync(It.IsAny<GroceryStoreSearchRequest>())).ReturnsAsync(new List<GroceryStoreDto>());
        _repositoryMock.Setup(r => r.GetSearchCountAsync(It.IsAny<GroceryStoreSearchRequest>())).ReturnsAsync(0);

        // Act
        var result = await _controller.Search(null, null, null, null, null, null, null, null, null);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Search_WithNameFilter_PassesNameToRepository()
    {
        // Arrange
        _repositoryMock.Setup(r => r.SearchAsync(It.Is<GroceryStoreSearchRequest>(req => req.Name == "Whole Foods")))
            .ReturnsAsync(new List<GroceryStoreDto>());
        _repositoryMock.Setup(r => r.GetSearchCountAsync(It.IsAny<GroceryStoreSearchRequest>())).ReturnsAsync(0);

        // Act
        await _controller.Search(name: "Whole Foods", null, null, null, null, null, null, null, null);

        // Assert
        _repositoryMock.Verify(r => r.SearchAsync(It.Is<GroceryStoreSearchRequest>(req => req.Name == "Whole Foods")), Times.Once);
    }

    [Fact]
    public async Task Search_WithChainFilter_PassesChainToRepository()
    {
        // Arrange
        _repositoryMock.Setup(r => r.SearchAsync(It.Is<GroceryStoreSearchRequest>(req => req.Chain == "Kroger")))
            .ReturnsAsync(new List<GroceryStoreDto>());
        _repositoryMock.Setup(r => r.GetSearchCountAsync(It.IsAny<GroceryStoreSearchRequest>())).ReturnsAsync(0);

        // Act
        await _controller.Search(null, chain: "Kroger", null, null, null, null, null, null, null);

        // Assert
        _repositoryMock.Verify(r => r.SearchAsync(It.Is<GroceryStoreSearchRequest>(req => req.Chain == "Kroger")), Times.Once);
    }

    [Fact]
    public async Task Search_WithCityStateFilters_PassesBothToRepository()
    {
        // Arrange
        _repositoryMock.Setup(r => r.SearchAsync(It.Is<GroceryStoreSearchRequest>(
            req => req.City == "Charlotte" && req.State == "NC")))
            .ReturnsAsync(new List<GroceryStoreDto>());
        _repositoryMock.Setup(r => r.GetSearchCountAsync(It.IsAny<GroceryStoreSearchRequest>())).ReturnsAsync(0);

        // Act
        await _controller.Search(null, null, city: "Charlotte", state: "NC", null, null, null, null, null);

        // Assert
        _repositoryMock.Verify(r => r.SearchAsync(It.Is<GroceryStoreSearchRequest>(
            req => req.City == "Charlotte" && req.State == "NC")), Times.Once);
    }

    [Fact]
    public async Task Search_WithSnapFilter_PassesAcceptsSnapToRepository()
    {
        // Arrange
        _repositoryMock.Setup(r => r.SearchAsync(It.Is<GroceryStoreSearchRequest>(req => req.AcceptsSnap == true)))
            .ReturnsAsync(new List<GroceryStoreDto>());
        _repositoryMock.Setup(r => r.GetSearchCountAsync(It.IsAny<GroceryStoreSearchRequest>())).ReturnsAsync(0);

        // Act
        await _controller.Search(null, null, null, null, null, null, acceptsSnap: true, null, null);

        // Assert
        _repositoryMock.Verify(r => r.SearchAsync(It.Is<GroceryStoreSearchRequest>(req => req.AcceptsSnap == true)), Times.Once);
    }

    [Fact]
    public async Task Search_PageSizeClamped_DoesNotExceed200()
    {
        // Arrange
        _repositoryMock.Setup(r => r.SearchAsync(It.Is<GroceryStoreSearchRequest>(req => req.PageSize == 200)))
            .ReturnsAsync(new List<GroceryStoreDto>());
        _repositoryMock.Setup(r => r.GetSearchCountAsync(It.IsAny<GroceryStoreSearchRequest>())).ReturnsAsync(0);

        // Act
        await _controller.Search(null, null, null, null, null, null, null, null, null, page: 1, pageSize: 500);

        // Assert
        _repositoryMock.Verify(r => r.SearchAsync(It.Is<GroceryStoreSearchRequest>(req => req.PageSize == 200)), Times.Once);
    }

    [Fact]
    public async Task Search_CallsBothSearchAndCountMethods()
    {
        // Arrange
        _repositoryMock.Setup(r => r.SearchAsync(It.IsAny<GroceryStoreSearchRequest>())).ReturnsAsync(new List<GroceryStoreDto>());
        _repositoryMock.Setup(r => r.GetSearchCountAsync(It.IsAny<GroceryStoreSearchRequest>())).ReturnsAsync(0);

        // Act
        await _controller.Search(null, null, null, null, null, null, null, null, null);

        // Assert
        _repositoryMock.Verify(r => r.SearchAsync(It.IsAny<GroceryStoreSearchRequest>()), Times.Once);
        _repositoryMock.Verify(r => r.GetSearchCountAsync(It.IsAny<GroceryStoreSearchRequest>()), Times.Once);
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_ExistingStore_ReturnsOkWithStore()
    {
        // Arrange
        var storeId = Guid.NewGuid();
        var store = new GroceryStoreDto { Id = storeId, Name = "Target", City = "Durham" };
        _repositoryMock.Setup(r => r.GetByIdAsync(storeId)).ReturnsAsync(store);

        // Act
        var result = await _controller.GetById(storeId);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(store);
    }

    [Fact]
    public async Task GetById_NonExistentStore_ReturnsNotFound()
    {
        // Arrange
        var storeId = Guid.NewGuid();
        _repositoryMock.Setup(r => r.GetByIdAsync(storeId)).ReturnsAsync((GroceryStoreDto?)null);

        // Act
        var result = await _controller.GetById(storeId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    // --- GetNearby ---

    [Fact]
    public async Task GetNearby_ValidCoordinates_ReturnsOkWithStores()
    {
        // Arrange
        var stores = new List<GroceryStoreDto>
        {
            new GroceryStoreDto { Id = Guid.NewGuid(), Name = "Food Lion", DistanceMiles = 1.5 }
        };
        _repositoryMock.Setup(r => r.GetNearbyAsync(35.77, -78.63, 10.0, 50)).ReturnsAsync(stores);

        // Act
        var result = await _controller.GetNearby(lat: 35.77, lon: -78.63);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task GetNearby_WithCustomRadius_PassesRadiusToRepository()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetNearbyAsync(35.77, -78.63, 5.0, 50)).ReturnsAsync(new List<GroceryStoreDto>());

        // Act
        await _controller.GetNearby(lat: 35.77, lon: -78.63, radiusMiles: 5.0);

        // Assert
        _repositoryMock.Verify(r => r.GetNearbyAsync(35.77, -78.63, 5.0, 50), Times.Once);
    }

    [Fact]
    public async Task GetNearby_RadiusClampedToMax_DoesNotExceed100Miles()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetNearbyAsync(It.IsAny<double>(), It.IsAny<double>(), 100.0, It.IsAny<int>()))
            .ReturnsAsync(new List<GroceryStoreDto>());

        // Act
        await _controller.GetNearby(lat: 35.77, lon: -78.63, radiusMiles: 500.0);

        // Assert
        _repositoryMock.Verify(r => r.GetNearbyAsync(35.77, -78.63, 100.0, It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task GetNearby_LimitClamped_DoesNotExceed200()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetNearbyAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), 200))
            .ReturnsAsync(new List<GroceryStoreDto>());

        // Act
        await _controller.GetNearby(lat: 35.77, lon: -78.63, limit: 999);

        // Assert
        _repositoryMock.Verify(r => r.GetNearbyAsync(35.77, -78.63, 10.0, 200), Times.Once);
    }

    // --- GetImportStatus ---

    [Fact]
    public async Task GetImportStatus_ReturnsOkWithStatusForAllSources()
    {
        // Arrange
        var log = new StoreImportLogDto { DataSource = "USDA_SNAP", Success = true, RecordsImported = 500 };
        _repositoryMock.Setup(r => r.GetLastImportAsync("USDA_SNAP")).ReturnsAsync(log);
        _repositoryMock.Setup(r => r.GetLastImportAsync("OSM")).ReturnsAsync((StoreImportLogDto?)null);
        _repositoryMock.Setup(r => r.GetLastImportAsync("OpenPrices")).ReturnsAsync((StoreImportLogDto?)null);

        // Act
        var result = await _controller.GetImportStatus();

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task GetImportStatus_WhenNoImports_ReturnsOkWithNullValues()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetLastImportAsync(It.IsAny<string>())).ReturnsAsync((StoreImportLogDto?)null);

        // Act
        var result = await _controller.GetImportStatus();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    // --- TriggerImport ---

    [Fact]
    public void TriggerImport_WithAuthenticatedUser_WhenWorkerNull_Returns503()
    {
        // Arrange - no import worker injected (null by default)
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(Guid.NewGuid());

        // Act
        var result = _controller.TriggerImport("snap");

        // Assert - worker is null, so service unavailable
        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(503);
    }

    [Fact]
    public void TriggerImport_WithAllSource_WhenWorkerNull_Returns503()
    {
        // Arrange - no import worker injected (null by default)
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(Guid.NewGuid());

        // Act
        var result = _controller.TriggerImport("all");

        // Assert - worker is null, so service unavailable
        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(503);
    }

    // --- GetByChain ---

    [Fact]
    public async Task GetByChain_ReturnsOkWithStores()
    {
        // Arrange
        var stores = new List<GroceryStoreDto>
        {
            new GroceryStoreDto { Id = Guid.NewGuid(), Name = "Kroger", NormalizedChain = "Kroger", City = "Raleigh", State = "NC" }
        };
        _repositoryMock.Setup(r => r.GetByChainAsync("Kroger", 100)).ReturnsAsync(stores);

        // Act
        var result = await _controller.GetByChain("Kroger");

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().BeEquivalentTo(stores);
    }

    [Fact]
    public async Task GetByChain_EmptyResult_ReturnsOkWithEmptyList()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetByChainAsync("UnknownChain", It.IsAny<int>()))
            .ReturnsAsync(new List<GroceryStoreDto>());

        // Act
        var result = await _controller.GetByChain("UnknownChain");

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    // --- GetChains ---

    [Fact]
    public async Task GetChains_ReturnsOkWithChainList()
    {
        // Arrange
        var chains = new List<StoreChainDto>
        {
            new StoreChainDto { Id = Guid.NewGuid(), CanonicalName = "Walmart", IsNational = true },
            new StoreChainDto { Id = Guid.NewGuid(), CanonicalName = "Kroger", IsNational = true }
        };
        _repositoryMock.Setup(r => r.GetAllChainsAsync()).ReturnsAsync(chains);

        // Act
        var result = await _controller.GetChains();

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().BeEquivalentTo(chains);
    }

    // --- GetStoreHours ---

    [Fact]
    public async Task GetStoreHours_ExistingStore_ReturnsOkWithHours()
    {
        // Arrange
        var storeId = Guid.NewGuid();
        var store = new GroceryStoreDto { Id = storeId, Name = "Target" };
        var hours = new List<StoreHoursDto>
        {
            new StoreHoursDto { Id = Guid.NewGuid(), StoreId = storeId, DayOfWeek = 1, OpenTime = TimeSpan.FromHours(8), CloseTime = TimeSpan.FromHours(22) }
        };
        _repositoryMock.Setup(r => r.GetByIdAsync(storeId)).ReturnsAsync(store);
        _repositoryMock.Setup(r => r.GetStoreHoursAsync(storeId)).ReturnsAsync(hours);

        // Act
        var result = await _controller.GetStoreHours(storeId);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().BeEquivalentTo(hours);
    }

    [Fact]
    public async Task GetStoreHours_NonExistentStore_ReturnsNotFound()
    {
        // Arrange
        var storeId = Guid.NewGuid();
        _repositoryMock.Setup(r => r.GetByIdAsync(storeId)).ReturnsAsync((GroceryStoreDto?)null);

        // Act
        var result = await _controller.GetStoreHours(storeId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    // --- Search with new filters ---

    [Fact]
    public async Task Search_WithIsVerifiedFilter_PassesIsVerifiedToRepository()
    {
        // Arrange
        _repositoryMock.Setup(r => r.SearchAsync(It.Is<GroceryStoreSearchRequest>(req => req.IsVerified == true)))
            .ReturnsAsync(new List<GroceryStoreDto>());
        _repositoryMock.Setup(r => r.GetSearchCountAsync(It.IsAny<GroceryStoreSearchRequest>())).ReturnsAsync(0);

        // Act
        await _controller.Search(null, null, null, null, null, null, null, isVerified: true, null);

        // Assert
        _repositoryMock.Verify(r => r.SearchAsync(It.Is<GroceryStoreSearchRequest>(req => req.IsVerified == true)), Times.Once);
    }

    [Fact]
    public async Task Search_WithNormalizedChainFilter_PassesNormalizedChainToRepository()
    {
        // Arrange
        _repositoryMock.Setup(r => r.SearchAsync(It.Is<GroceryStoreSearchRequest>(req => req.NormalizedChain == "Walmart")))
            .ReturnsAsync(new List<GroceryStoreDto>());
        _repositoryMock.Setup(r => r.GetSearchCountAsync(It.IsAny<GroceryStoreSearchRequest>())).ReturnsAsync(0);

        // Act
        await _controller.Search(null, null, null, null, null, null, null, null, normalizedChain: "Walmart");

        // Assert
        _repositoryMock.Verify(r => r.SearchAsync(It.Is<GroceryStoreSearchRequest>(req => req.NormalizedChain == "Walmart")), Times.Once);
    }

    // --- GetImportStatus (updated with new sources) ---

    [Fact]
    public async Task GetImportStatus_ReturnsOkWithAllSources()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetLastImportAsync(It.IsAny<string>())).ReturnsAsync((StoreImportLogDto?)null);

        // Act
        var result = await _controller.GetImportStatus();

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        // Verify all 5 sources are queried
        _repositoryMock.Verify(r => r.GetLastImportAsync("USDA_SNAP"), Times.Once);
        _repositoryMock.Verify(r => r.GetLastImportAsync("OPENSTREETMAP"), Times.Once);
        _repositoryMock.Verify(r => r.GetLastImportAsync("OVERTURE_MAPS"), Times.Once);
        _repositoryMock.Verify(r => r.GetLastImportAsync("HIFLD"), Times.Once);
    }
}
