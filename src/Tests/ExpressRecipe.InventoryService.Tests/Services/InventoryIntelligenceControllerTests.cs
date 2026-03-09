using FluentAssertions;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using ExpressRecipe.InventoryService.Controllers;
using ExpressRecipe.InventoryService.Data;
using ExpressRecipe.InventoryService.Tests.Helpers;

namespace ExpressRecipe.InventoryService.Tests.Services;

/// <summary>
/// Tests for inventory intelligence endpoints — purchase events, patterns, price watch, inquiries.
/// </summary>
public class InventoryIntelligenceControllerTests
{
    private readonly Mock<IInventoryRepository> _mockRepository;
    private readonly Mock<ILogger<InventoryController>> _mockLogger;
    private readonly InventoryController _controller;
    private readonly Guid _testUserId;

    public InventoryIntelligenceControllerTests()
    {
        _mockRepository = new Mock<IInventoryRepository>();
        _mockLogger = new Mock<ILogger<InventoryController>>();
        _controller = new InventoryController(_mockLogger.Object, _mockRepository.Object);
        _testUserId = Guid.NewGuid();
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);
    }

    #region GetConsumptionPatterns

    [Fact]
    public async Task GetConsumptionPatterns_ReturnsOkWithPatterns()
    {
        // Arrange
        List<ProductConsumptionPatternDto> patterns = new List<ProductConsumptionPatternDto>
        {
            new ProductConsumptionPatternDto { Id = Guid.NewGuid(), UserId = _testUserId, PurchaseCount = 5, AvgDaysBetweenPurchases = 7m }
        };
        _mockRepository
            .Setup(r => r.GetConsumptionPatternsAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(patterns);

        // Act
        IActionResult result = await _controller.GetConsumptionPatterns();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        OkObjectResult ok = (OkObjectResult)result;
        ok.Value.Should().BeEquivalentTo(patterns);
    }

    [Fact]
    public async Task GetAbandonedProducts_ReturnsOkWithAbandonedItems()
    {
        // Arrange
        List<ProductConsumptionPatternDto> abandoned = new List<ProductConsumptionPatternDto>
        {
            new ProductConsumptionPatternDto { Id = Guid.NewGuid(), UserId = _testUserId, IsAbandoned = true, PurchaseCount = 1 }
        };
        _mockRepository
            .Setup(r => r.GetAbandonedProductsAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(abandoned);

        // Act
        IActionResult result = await _controller.GetAbandonedProducts();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        OkObjectResult ok = (OkObjectResult)result;
        (ok.Value as List<ProductConsumptionPatternDto>)!.Should().HaveCount(1);
        (ok.Value as List<ProductConsumptionPatternDto>)![0].IsAbandoned.Should().BeTrue();
    }

    [Fact]
    public async Task GetLowStockByPrediction_ReturnsOkWithPredictions()
    {
        // Arrange
        List<ProductConsumptionPatternDto> predictions = new List<ProductConsumptionPatternDto>
        {
            new ProductConsumptionPatternDto
            {
                Id = Guid.NewGuid(),
                UserId = _testUserId,
                ProductId = Guid.NewGuid(),
                EstimatedNextPurchaseDate = DateTime.UtcNow.AddDays(2)
            }
        };
        _mockRepository
            .Setup(r => r.GetLowStockByPredictionAsync(_testUserId, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(predictions);

        // Act
        IActionResult result = await _controller.GetLowStockByPrediction(3);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        OkObjectResult ok = (OkObjectResult)result;
        ok.Value.Should().BeEquivalentTo(predictions);
    }

    #endregion

    #region RecordPurchaseEvent

    [Fact]
    public async Task RecordPurchaseEvent_WithValidRequest_ReturnsOkWithId()
    {
        // Arrange
        Guid eventId = Guid.NewGuid();
        RecordPurchaseEventRequest request = new RecordPurchaseEventRequest
        {
            ProductId = Guid.NewGuid(),
            Quantity = 2,
            Unit = "units",
            Price = 5.99m,
            StoreName = "Whole Foods",
            Source = "ManualAdd"
        };
        _mockRepository
            .Setup(r => r.RecordPurchaseEventAsync(It.IsAny<PurchaseEventRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(eventId);

        // Act
        IActionResult result = await _controller.RecordPurchaseEvent(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRepository.Verify(r => r.RecordPurchaseEventAsync(
            It.Is<PurchaseEventRecord>(e =>
                e.UserId == _testUserId &&
                e.ProductId == request.ProductId &&
                e.Quantity == request.Quantity),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Price Watch

    [Fact]
    public async Task GetPriceWatchAlerts_ReturnsOkWithAlerts()
    {
        // Arrange
        List<PriceWatchAlertDto> alerts = new List<PriceWatchAlertDto>
        {
            new PriceWatchAlertDto { Id = Guid.NewGuid(), UserId = _testUserId, DealFound = true, DealPrice = 2.99m }
        };
        _mockRepository
            .Setup(r => r.GetActiveWatchAlertsByUserAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alerts);

        // Act
        IActionResult result = await _controller.GetPriceWatchAlerts();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        OkObjectResult ok = (OkObjectResult)result;
        (ok.Value as List<PriceWatchAlertDto>)!.Should().HaveCount(1);
        (ok.Value as List<PriceWatchAlertDto>)![0].DealFound.Should().BeTrue();
    }

    [Fact]
    public async Task SetPriceWatchTarget_CallsRepository()
    {
        // Arrange
        Guid alertId = Guid.NewGuid();
        SetTargetPriceRequest request = new SetTargetPriceRequest { TargetPrice = 3.49m };
        _mockRepository
            .Setup(r => r.SetPriceWatchTargetPriceAsync(_testUserId, alertId, request.TargetPrice, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        IActionResult result = await _controller.SetPriceWatchTarget(alertId, request);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _mockRepository.Verify(r => r.SetPriceWatchTargetPriceAsync(_testUserId, alertId, 3.49m, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Abandoned Product Inquiry

    [Fact]
    public async Task GetPendingInquiries_ReturnsOkWithInquiries()
    {
        // Arrange
        List<AbandonedProductInquiryDto> inquiries = new List<AbandonedProductInquiryDto>
        {
            new AbandonedProductInquiryDto { Id = Guid.NewGuid(), UserId = _testUserId, CustomName = "Turmeric", Response = null }
        };
        _mockRepository
            .Setup(r => r.GetPendingInquiriesAsync(_testUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inquiries);

        // Act
        IActionResult result = await _controller.GetPendingInquiries();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        OkObjectResult ok = (OkObjectResult)result;
        (ok.Value as List<AbandonedProductInquiryDto>)!.Should().HaveCount(1);
    }

    [Fact]
    public async Task RespondToInquiry_WithAllergyResponse_RecordsWithoutActioning()
    {
        // Arrange
        Guid inquiryId = Guid.NewGuid();
        InquiryResponseRequest request = new InquiryResponseRequest { Response = "Allergy", Note = "Got rash" };
        _mockRepository
            .Setup(r => r.RecordInquiryResponseAsync(_testUserId, inquiryId, "Allergy", "Got rash", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        IActionResult result = await _controller.RespondToInquiry(inquiryId, request);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        // IsActioned stays false — Blazor navigation handles the allergy route
        _mockRepository.Verify(r => r.RecordInquiryResponseAsync(_testUserId, inquiryId, "Allergy", "Got rash", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Waste Report

    [Fact]
    public async Task GetWasteReport_ReturnsOkWithMonthlyData()
    {
        // Arrange
        List<WasteReportMonthDto> report = new List<WasteReportMonthDto>
        {
            new WasteReportMonthDto { Year = 2026, Month = 2, ExpiredItemsDisposed = 3, TotalDisposedValue = 12.50m }
        };
        _mockRepository
            .Setup(r => r.GetWasteReportAsync(_testUserId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        // Act
        IActionResult result = await _controller.GetWasteReport();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        OkObjectResult ok = (OkObjectResult)result;
        ok.Value.Should().BeEquivalentTo(report);
    }

    #endregion
}
