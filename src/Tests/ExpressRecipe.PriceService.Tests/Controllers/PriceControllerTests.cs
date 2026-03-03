using ExpressRecipe.PriceService.Controllers;
using ExpressRecipe.PriceService.Data;
using ExpressRecipe.PriceService.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ExpressRecipe.PriceService.Tests.Controllers;

public class PriceControllerTests
{
    private readonly Mock<IPriceRepository> _repositoryMock;
    private readonly Mock<ILogger<PriceController>> _loggerMock;
    private readonly PriceController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public PriceControllerTests()
    {
        _repositoryMock = new Mock<IPriceRepository>();
        _loggerMock = new Mock<ILogger<PriceController>>();

        _controller = new PriceController(_loggerMock.Object, _repositoryMock.Object);
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_userId);
    }

    // --- GetStores ---

    [Fact]
    public async Task GetStores_ReturnsOkWithStores()
    {
        // Arrange
        var stores = new List<StoreDto>
        {
            new StoreDto { Id = Guid.NewGuid(), Name = "Whole Foods", City = "Raleigh", State = "NC" }
        };
        _repositoryMock.Setup(r => r.GetStoresAsync(null, null, null)).ReturnsAsync(stores);

        // Act
        var result = await _controller.GetStores(null, null, null);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(stores);
    }

    [Fact]
    public async Task GetStores_WithFilters_PassesFiltersToRepository()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetStoresAsync("Raleigh", "NC", "Kroger")).ReturnsAsync(new List<StoreDto>());

        // Act
        var result = await _controller.GetStores("Raleigh", "NC", "Kroger");

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _repositoryMock.Verify(r => r.GetStoresAsync("Raleigh", "NC", "Kroger"), Times.Once);
    }

    [Fact]
    public async Task GetStores_EmptyResults_ReturnsOkWithEmptyList()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetStoresAsync(null, null, null)).ReturnsAsync(new List<StoreDto>());

        // Act
        var result = await _controller.GetStores(null, null, null);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        (ok.Value as List<StoreDto>).Should().BeEmpty();
    }

    // --- RecordPrice ---

    [Fact]
    public async Task RecordPrice_ValidRequest_ReturnsOkWithId()
    {
        // Arrange
        var priceId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var request = new RecordPriceRequest
        {
            ProductId = productId,
            StoreId = storeId,
            Price = 3.99m,
            ObservedAt = DateTime.UtcNow
        };
        _repositoryMock.Setup(r => r.RecordPriceAsync(productId, storeId, 3.99m, _userId, It.IsAny<DateTime?>()))
            .ReturnsAsync(priceId);

        // Act
        var result = await _controller.RecordPrice(request);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task RecordPrice_CallsRepositoryWithCorrectUserId()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var request = new RecordPriceRequest { ProductId = productId, StoreId = storeId, Price = 5.00m };
        _repositoryMock.Setup(r => r.RecordPriceAsync(productId, storeId, 5.00m, _userId, null)).ReturnsAsync(Guid.NewGuid());

        // Act
        await _controller.RecordPrice(request);

        // Assert
        _repositoryMock.Verify(r => r.RecordPriceAsync(productId, storeId, 5.00m, _userId, null), Times.Once);
    }

    // --- GetProductPrices ---

    [Fact]
    public async Task GetProductPrices_ReturnsOkWithObservations()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var observations = new List<PriceObservationDto>
        {
            new PriceObservationDto { ProductId = productId, Price = 2.99m }
        };
        _repositoryMock.Setup(r => r.GetProductPricesAsync(productId, null, 90)).ReturnsAsync(observations);

        // Act
        var result = await _controller.GetProductPrices(productId, null, 90);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(observations);
    }

    [Fact]
    public async Task GetProductPrices_WithStoreFilter_PassesStoreIdToRepository()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        _repositoryMock.Setup(r => r.GetProductPricesAsync(productId, storeId, 90)).ReturnsAsync(new List<PriceObservationDto>());

        // Act
        await _controller.GetProductPrices(productId, storeId, 90);

        // Assert
        _repositoryMock.Verify(r => r.GetProductPricesAsync(productId, storeId, 90), Times.Once);
    }

    // --- GetPriceTrend ---

    [Fact]
    public async Task GetPriceTrend_ReturnsOkWithTrend()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var trend = new PriceTrendDto
        {
            ProductId = productId,
            CurrentPrice = 3.99m,
            AveragePrice = 3.75m,
            Trend = "stable"
        };
        _repositoryMock.Setup(r => r.GetPriceTrendAsync(productId, null)).ReturnsAsync(trend);

        // Act
        var result = await _controller.GetPriceTrend(productId, null);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(trend);
    }

    // --- GetDeals ---

    [Fact]
    public async Task GetDeals_ReturnsOkWithDeals()
    {
        // Arrange
        var deals = new List<DealDto>
        {
            new DealDto { Id = Guid.NewGuid(), DealType = "BOGO", SalePrice = 2.50m }
        };
        _repositoryMock.Setup(r => r.GetActiveDealsAsync(null, null)).ReturnsAsync(deals);

        // Act
        var result = await _controller.GetDeals(null, null);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(deals);
    }

    [Fact]
    public async Task GetDeals_WithFilters_PassesFiltersToRepository()
    {
        // Arrange
        var storeId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        _repositoryMock.Setup(r => r.GetActiveDealsAsync(storeId, productId)).ReturnsAsync(new List<DealDto>());

        // Act
        await _controller.GetDeals(storeId, productId);

        // Assert
        _repositoryMock.Verify(r => r.GetActiveDealsAsync(storeId, productId), Times.Once);
    }

    // --- CreateDeal ---

    [Fact]
    public async Task CreateDeal_ValidRequest_ReturnsOkWithId()
    {
        // Arrange
        var dealId = Guid.NewGuid();
        var request = new CreateDealRequest
        {
            ProductId = Guid.NewGuid(),
            StoreId = Guid.NewGuid(),
            DealType = "SALE",
            OriginalPrice = 5.00m,
            SalePrice = 3.00m,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(7)
        };
        _repositoryMock.Setup(r => r.CreateDealAsync(
            request.ProductId, request.StoreId, request.DealType,
            request.OriginalPrice, request.SalePrice, request.StartDate, request.EndDate))
            .ReturnsAsync(dealId);

        // Act
        var result = await _controller.CreateDeal(request);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();
    }

    // --- ComparePrices ---

    [Fact]
    public async Task ComparePrices_ReturnsOkWithComparison()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var storeId1 = Guid.NewGuid();
        var storeId2 = Guid.NewGuid();
        var comparison = new List<StorePriceComparisonDto>
        {
            new StorePriceComparisonDto { ProductId = productId, BestPriceStoreId = storeId1 }
        };
        var request = new ComparePricesRequest
        {
            ProductIds = new List<Guid> { productId },
            StoreIds = new List<Guid> { storeId1, storeId2 }
        };
        _repositoryMock.Setup(r => r.ComparePricesAsync(request.ProductIds, request.StoreIds)).ReturnsAsync(comparison);

        // Act
        var result = await _controller.ComparePrices(request);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(comparison);
    }

    [Fact]
    public async Task ComparePrices_WithEmptyLists_ReturnsOkWithEmptyComparison()
    {
        // Arrange
        var request = new ComparePricesRequest
        {
            ProductIds = new List<Guid>(),
            StoreIds = new List<Guid>()
        };
        _repositoryMock.Setup(r => r.ComparePricesAsync(request.ProductIds, request.StoreIds))
            .ReturnsAsync(new List<StorePriceComparisonDto>());

        // Act
        var result = await _controller.ComparePrices(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }
}
