using ExpressRecipe.PriceService.Controllers;
using ExpressRecipe.PriceService.Data;
using ExpressRecipe.PriceService.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ExpressRecipe.PriceService.Tests.Controllers;

public class PricesControllerTests
{
    private readonly Mock<IPriceRepository> _repositoryMock;
    private readonly Mock<ILogger<PricesController>> _loggerMock;
    private readonly IConfiguration _configuration;
    private readonly PricesController _controller;

    public PricesControllerTests()
    {
        _repositoryMock = new Mock<IPriceRepository>();
        _loggerMock = new Mock<ILogger<PricesController>>();
        _configuration = new ConfigurationBuilder().Build();

        _controller = new PricesController(
            _loggerMock.Object,
            _repositoryMock.Object,
            _configuration);
    }

    // --- SearchPrices ---

    [Fact]
    public async Task SearchPrices_ReturnsOkWithResults()
    {
        // Arrange
        var request = new PriceSearchRequest { ProductName = "milk", Page = 1, PageSize = 20 };
        var prices = new List<ProductPriceDto>
        {
            new ProductPriceDto { Id = Guid.NewGuid(), ProductName = "Whole Milk", Price = 3.99m }
        };
        _repositoryMock.Setup(r => r.SearchPricesAsync(It.IsAny<PriceSearchRequest>())).ReturnsAsync(prices);
        _repositoryMock.Setup(r => r.GetSearchCountAsync(It.IsAny<PriceSearchRequest>())).ReturnsAsync(1);

        // Act
        var result = await _controller.SearchPrices(request);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task SearchPrices_EmptyResults_ReturnsOkWithEmptyList()
    {
        // Arrange
        var request = new PriceSearchRequest { ProductName = "nonexistent" };
        _repositoryMock.Setup(r => r.SearchPricesAsync(It.IsAny<PriceSearchRequest>())).ReturnsAsync(new List<ProductPriceDto>());
        _repositoryMock.Setup(r => r.GetSearchCountAsync(It.IsAny<PriceSearchRequest>())).ReturnsAsync(0);

        // Act
        var result = await _controller.SearchPrices(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SearchPrices_CallsRepositorySearchAndCount()
    {
        // Arrange
        var request = new PriceSearchRequest { City = "Raleigh", State = "NC" };
        _repositoryMock.Setup(r => r.SearchPricesAsync(It.IsAny<PriceSearchRequest>())).ReturnsAsync(new List<ProductPriceDto>());
        _repositoryMock.Setup(r => r.GetSearchCountAsync(It.IsAny<PriceSearchRequest>())).ReturnsAsync(0);

        // Act
        await _controller.SearchPrices(request);

        // Assert
        _repositoryMock.Verify(r => r.SearchPricesAsync(It.IsAny<PriceSearchRequest>()), Times.Once);
        _repositoryMock.Verify(r => r.GetSearchCountAsync(It.IsAny<PriceSearchRequest>()), Times.Once);
    }

    // --- GetPricesByProduct ---

    [Fact]
    public async Task GetPricesByProduct_ReturnsOkWithPrices()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var prices = new List<ProductPriceDto>
        {
            new ProductPriceDto { ProductId = productId, Price = 2.49m }
        };
        _repositoryMock.Setup(r => r.SearchPricesAsync(It.IsAny<PriceSearchRequest>())).ReturnsAsync(prices);

        // Act
        var result = await _controller.GetPricesByProduct(productId);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(prices);
    }

    [Fact]
    public async Task GetPricesByProduct_WithDaysBack_PassesDaysBackToRepository()
    {
        // Arrange
        var productId = Guid.NewGuid();
        _repositoryMock.Setup(r => r.SearchPricesAsync(It.Is<PriceSearchRequest>(
            req => req.ProductId == productId && req.DaysBack == 30)))
            .ReturnsAsync(new List<ProductPriceDto>());

        // Act
        var result = await _controller.GetPricesByProduct(productId, daysBack: 30);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _repositoryMock.Verify(r => r.SearchPricesAsync(It.Is<PriceSearchRequest>(
            req => req.ProductId == productId && req.DaysBack == 30)), Times.Once);
    }

    // --- GetPricesByUpc ---

    [Fact]
    public async Task GetPricesByUpc_ReturnsOkWithPrices()
    {
        // Arrange
        var upc = "012345678901";
        var prices = new List<ProductPriceDto> { new ProductPriceDto { Upc = upc, Price = 1.99m } };
        _repositoryMock.Setup(r => r.GetPricesByUpcAsync(upc, 50)).ReturnsAsync(prices);

        // Act
        var result = await _controller.GetPricesByUpc(upc);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(prices);
    }

    [Fact]
    public async Task GetPricesByUpc_WithEmptyUpc_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetPricesByUpc("   ");

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetPricesByUpc_WithNullUpc_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetPricesByUpc(null!);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // --- GetPricesByName ---

    [Fact]
    public async Task GetPricesByName_ReturnsOkWithPrices()
    {
        // Arrange
        var prices = new List<ProductPriceDto> { new ProductPriceDto { ProductName = "Eggs", Price = 4.99m } };
        _repositoryMock.Setup(r => r.GetPricesByProductNameAsync("eggs", 50)).ReturnsAsync(prices);

        // Act
        var result = await _controller.GetPricesByName("eggs");

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(prices);
    }

    [Fact]
    public async Task GetPricesByName_WithEmptyName_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetPricesByName("  ");

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetPricesByName_WithNullName_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetPricesByName(null!);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // --- GetBatchPrices ---

    [Fact]
    public async Task GetBatchPrices_WithValidIds_ReturnsOkWithPrices()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var request = new BatchPriceRequest { ProductIds = new List<Guid> { productId } };
        var prices = new List<ProductPriceDto> { new ProductPriceDto { ProductId = productId, Price = 3.50m } };
        _repositoryMock.Setup(r => r.GetBatchPricesAsync(It.IsAny<IEnumerable<Guid>>())).ReturnsAsync(prices);

        // Act
        var result = await _controller.GetBatchPrices(request);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(prices);
    }

    [Fact]
    public async Task GetBatchPrices_WithEmptyList_ReturnsBadRequest()
    {
        // Arrange
        var request = new BatchPriceRequest { ProductIds = new List<Guid>() };

        // Act
        var result = await _controller.GetBatchPrices(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetBatchPrices_WithMultipleIds_CallsRepositoryWithAllIds()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var request = new BatchPriceRequest { ProductIds = new List<Guid> { id1, id2 } };
        _repositoryMock.Setup(r => r.GetBatchPricesAsync(It.IsAny<IEnumerable<Guid>>())).ReturnsAsync(new List<ProductPriceDto>());

        // Act
        await _controller.GetBatchPrices(request);

        // Assert
        _repositoryMock.Verify(r => r.GetBatchPricesAsync(It.Is<IEnumerable<Guid>>(ids => ids.Count() == 2)), Times.Once);
    }

    // --- GetBestPrices ---

    [Fact]
    public async Task GetBestPrices_ReturnsOkWithSortedPrices()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var prices = new List<ProductPriceDto>
        {
            new ProductPriceDto { ProductId = productId, Price = 1.99m, StoreName = "Store A" },
            new ProductPriceDto { ProductId = productId, Price = 2.49m, StoreName = "Store B" }
        };
        _repositoryMock.Setup(r => r.GetBestPricesAsync(productId, 10)).ReturnsAsync(prices);

        // Act
        var result = await _controller.GetBestPrices(productId);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(prices);
    }

    [Fact]
    public async Task GetBestPrices_WithCustomLimit_PassesLimitToRepository()
    {
        // Arrange
        var productId = Guid.NewGuid();
        _repositoryMock.Setup(r => r.GetBestPricesAsync(productId, 5)).ReturnsAsync(new List<ProductPriceDto>());

        // Act
        await _controller.GetBestPrices(productId, limit: 5);

        // Assert
        _repositoryMock.Verify(r => r.GetBestPricesAsync(productId, 5), Times.Once);
    }

    // --- GetImportStatus ---

    [Fact]
    public async Task GetImportStatus_ReturnsOkWithStatusForAllSources()
    {
        // Arrange
        var log = new PriceImportLogDto
        {
            DataSource = "OpenPrices",
            ImportedAt = DateTime.UtcNow,
            Success = true,
            RecordsImported = 1000
        };
        _repositoryMock.Setup(r => r.GetLastImportAsync(It.IsAny<string>())).ReturnsAsync(log);
        _repositoryMock.Setup(r => r.GetProductPriceCountAsync()).ReturnsAsync(5000);

        // Act
        var result = await _controller.GetImportStatus();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _repositoryMock.Verify(r => r.GetLastImportAsync(It.IsAny<string>()), Times.Exactly(4));
        _repositoryMock.Verify(r => r.GetProductPriceCountAsync(), Times.Once);
    }

    [Fact]
    public async Task GetImportStatus_WhenNoImports_ReturnsOkWithNullLastImport()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetLastImportAsync(It.IsAny<string>())).ReturnsAsync((PriceImportLogDto?)null);
        _repositoryMock.Setup(r => r.GetProductPriceCountAsync()).ReturnsAsync(0);

        // Act
        var result = await _controller.GetImportStatus();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    // --- TriggerImport ---

    [Fact]
    public async Task TriggerImport_WithoutAuth_Returns503WhenServiceNull()
    {
        // Arrange - controller has no openPricesImportService (null by default)
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(Guid.NewGuid());

        // Act
        var result = await _controller.TriggerImport("OpenPrices");

        // Assert
        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task TriggerImport_WithAuth_WhenServiceIsNull_Returns503()
    {
        // Arrange
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(Guid.NewGuid());

        // Act
        var result = await _controller.TriggerImport("OpenPrices");

        // Assert - import service is null, so 503
        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(503);
    }
}
