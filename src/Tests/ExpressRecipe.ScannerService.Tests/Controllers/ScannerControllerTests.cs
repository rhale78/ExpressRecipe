using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ExpressRecipe.ScannerService.Controllers;
using ExpressRecipe.ScannerService.Data;
using ExpressRecipe.ScannerService.Tests.Helpers;

namespace ExpressRecipe.ScannerService.Tests.Controllers;

public class ScannerControllerTests
{
    private readonly Mock<IScannerRepository> _mockRepository;
    private readonly Mock<ILogger<ScannerController>> _mockLogger;
    private readonly ScannerController _controller;
    private readonly Guid _testUserId;

    public ScannerControllerTests()
    {
        _mockRepository = new Mock<IScannerRepository>();
        _mockLogger = new Mock<ILogger<ScannerController>>();
        _controller = new ScannerController(_mockLogger.Object, _mockRepository.Object);
        _testUserId = Guid.NewGuid();
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);
    }

    #region ScanBarcode Tests

    [Fact]
    public async Task ScanBarcode_WhenAuthenticated_ReturnsOkWithScanId()
    {
        // Arrange
        var request = new ScanBarcodeRequest
        {
            Barcode = "012345678901",
            WasRecognized = false,
            ScanType = "Barcode"
        };
        var scanId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.CreateScanAsync(_testUserId, request.Barcode, request.ProductId, request.WasRecognized, request.ScanType))
            .ReturnsAsync(scanId);

        // Act
        var result = await _controller.ScanBarcode(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRepository.Verify(r => r.CreateScanAsync(_testUserId, request.Barcode, request.ProductId, request.WasRecognized, request.ScanType), Times.Once);
    }

    [Fact]
    public async Task ScanBarcode_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();
        var request = new ScanBarcodeRequest { Barcode = "012345678901", ScanType = "Barcode" };

        // Act
        var result = await _controller.ScanBarcode(request);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
        _mockRepository.Verify(r => r.CreateScanAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ScanBarcode_WithRecognizedProduct_ChecksAllergens()
    {
        // Arrange
        var productId = Guid.NewGuid().ToString();
        var request = new ScanBarcodeRequest
        {
            Barcode = "012345678901",
            ProductId = productId,
            WasRecognized = true,
            ScanType = "Barcode"
        };
        var scanId = Guid.NewGuid();
        var alerts = new List<ScanAlertDto>
        {
            new ScanAlertDto { Message = "Contains peanuts - allergen detected!", Severity = "High" }
        };

        _mockRepository
            .Setup(r => r.CreateScanAsync(_testUserId, request.Barcode, request.ProductId, request.WasRecognized, request.ScanType))
            .ReturnsAsync(scanId);
        _mockRepository
            .Setup(r => r.CheckAllergensAsync(_testUserId, Guid.Parse(productId)))
            .ReturnsAsync(alerts);

        // Act
        var result = await _controller.ScanBarcode(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRepository.Verify(r => r.CheckAllergensAsync(_testUserId, Guid.Parse(productId)), Times.Once);
    }

    #endregion

    #region GetScanHistory Tests

    [Fact]
    public async Task GetScanHistory_WhenAuthenticated_ReturnsScanHistory()
    {
        // Arrange
        var scans = new List<ScanHistoryDto>
        {
            new ScanHistoryDto { Id = Guid.NewGuid(), UserId = _testUserId, Barcode = "012345678901", WasRecognized = true },
            new ScanHistoryDto { Id = Guid.NewGuid(), UserId = _testUserId, Barcode = "987654321098", WasRecognized = false }
        };

        _mockRepository
            .Setup(r => r.GetUserScansAsync(_testUserId, 50))
            .ReturnsAsync(scans);

        // Act
        var result = await _controller.GetScanHistory(50);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        (okResult!.Value as List<ScanHistoryDto>)!.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetScanHistory_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        // Act
        var result = await _controller.GetScanHistory(50);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    #endregion

    #region GetAlerts Tests

    [Fact]
    public async Task GetAlerts_WhenAuthenticated_ReturnsAlerts()
    {
        // Arrange
        var alerts = new List<ScanAlertDto>
        {
            new ScanAlertDto { Id = Guid.NewGuid(), UserId = _testUserId, AlertType = "Allergen", IsRead = false }
        };

        _mockRepository
            .Setup(r => r.GetUserAlertsAsync(_testUserId, true))
            .ReturnsAsync(alerts);

        // Act
        var result = await _controller.GetAlerts(true);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        (okResult!.Value as List<ScanAlertDto>)!.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAlerts_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        // Act
        var result = await _controller.GetAlerts(true);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    #endregion
}
