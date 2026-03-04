using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ExpressRecipe.InventoryService.Controllers;
using ExpressRecipe.InventoryService.Data;
using ExpressRecipe.InventoryService.Tests.Helpers;

namespace ExpressRecipe.InventoryService.Tests.Controllers;

public class ScanControllerTests
{
    private readonly Mock<IInventoryRepository> _mockRepository;
    private readonly Mock<ILogger<ScanController>> _mockLogger;
    private readonly ScanController _controller;
    private readonly Guid _testUserId;

    public ScanControllerTests()
    {
        _mockRepository = new Mock<IInventoryRepository>();
        _mockLogger = new Mock<ILogger<ScanController>>();
        _controller = new ScanController(_mockLogger.Object, _mockRepository.Object);
        _testUserId = Guid.NewGuid();
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);
    }

    #region StartSession Tests

    [Fact]
    public async Task StartSession_WithValidRequest_ReturnsOkWithSessionId()
    {
        // Arrange
        var request = TestDataFactory.CreateStartScanSessionRequest("Adding");
        var sessionId = Guid.NewGuid();
        var sessionDto = TestDataFactory.CreateScanSessionDto(sessionId, "Adding");

        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);

        _mockRepository
            .Setup(r => r.StartScanSessionAsync(_testUserId, null, request.SessionType, request.StorageLocationId))
            .ReturnsAsync(sessionId);

        _mockRepository
            .Setup(r => r.GetScanSessionByIdAsync(sessionId))
            .ReturnsAsync(sessionDto);

        // Act
        var result = await _controller.StartSession(request);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
        var createdResult = result as CreatedAtActionResult;
        createdResult!.Value.Should().BeEquivalentTo(sessionDto);

        _mockRepository.Verify(r => r.StartScanSessionAsync(_testUserId, null, request.SessionType, request.StorageLocationId), Times.Once);
    }

    [Fact]
    public async Task StartSession_WithAddingMode_CreatesAddingSession()
    {
        // Arrange
        var request = TestDataFactory.CreateStartScanSessionRequest("Adding", Guid.NewGuid());
        var sessionId = Guid.NewGuid();
        var sessionDto = TestDataFactory.CreateScanSessionDto(sessionId, "Adding");

        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);

        _mockRepository
            .Setup(r => r.StartScanSessionAsync(_testUserId, null, "Adding", request.StorageLocationId))
            .ReturnsAsync(sessionId);

        _mockRepository
            .Setup(r => r.GetScanSessionByIdAsync(sessionId))
            .ReturnsAsync(sessionDto);

        // Act
        var result = await _controller.StartSession(request);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
        _mockRepository.Verify(r => r.StartScanSessionAsync(_testUserId, null, "Adding", request.StorageLocationId), Times.Once);
    }

    [Fact]
    public async Task StartSession_WithUsingMode_CreatesUsingSession()
    {
        // Arrange
        var request = TestDataFactory.CreateStartScanSessionRequest("Using");
        var sessionId = Guid.NewGuid();
        var sessionDto = TestDataFactory.CreateScanSessionDto(sessionId, "Using");

        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);

        _mockRepository
            .Setup(r => r.StartScanSessionAsync(_testUserId, null, "Using", request.StorageLocationId))
            .ReturnsAsync(sessionId);

        _mockRepository
            .Setup(r => r.GetScanSessionByIdAsync(sessionId))
            .ReturnsAsync(sessionDto);

        // Act
        var result = await _controller.StartSession(request);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
        var createdResult = result as CreatedAtActionResult;
        var session = createdResult!.Value as ScanSessionDto;
        session.Should().NotBeNull();
        session!.SessionType.Should().Be("Using");
    }

    [Fact]
    public async Task StartSession_WithDisposingMode_CreatesDisposingSession()
    {
        // Arrange
        var request = TestDataFactory.CreateStartScanSessionRequest("Disposing");
        var sessionId = Guid.NewGuid();
        var sessionDto = TestDataFactory.CreateScanSessionDto(sessionId, "Disposing");

        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);

        _mockRepository
            .Setup(r => r.StartScanSessionAsync(_testUserId, null, "Disposing", request.StorageLocationId))
            .ReturnsAsync(sessionId);

        _mockRepository
            .Setup(r => r.GetScanSessionByIdAsync(sessionId))
            .ReturnsAsync(sessionDto);

        // Act
        var result = await _controller.StartSession(request);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
        var createdResult = result as CreatedAtActionResult;
        var session = createdResult!.Value as ScanSessionDto;
        session!.SessionType.Should().Be("Disposing");
    }

    #endregion

    #region GetActiveSession Tests

    [Fact]
    public async Task GetActiveSession_WithActiveSession_ReturnsOkWithSession()
    {
        // Arrange
        var sessionDto = TestDataFactory.CreateScanSessionDto(itemsScanned: 5);

        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);

        _mockRepository
            .Setup(r => r.GetActiveScanSessionAsync(_testUserId))
            .ReturnsAsync(sessionDto);

        // Act
        var result = await _controller.GetActiveSession();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(sessionDto);
    }

    [Fact]
    public async Task GetActiveSession_WithNoActiveSession_ReturnsNotFound()
    {
        // Arrange
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);

        _mockRepository
            .Setup(r => r.GetActiveScanSessionAsync(_testUserId))
            .ReturnsAsync((ScanSessionDto?)null);

        // Act
        var result = await _controller.GetActiveSession();

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region ScanAdd Tests

    [Fact]
    public async Task ScanAdd_WithValidBarcode_ReturnsOkWithItemId()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var request = new ScanAddRequest
        {
            Barcode = "123456789",
            Quantity = 2.0m,
            StorageLocationId = Guid.NewGuid()
        };

        _mockRepository
            .Setup(r => r.GetScanSessionByIdAsync(sessionId))
            .ReturnsAsync(new ScanSessionDto { Id = sessionId, UserId = _testUserId });
        _mockRepository
            .Setup(r => r.ScanAddItemAsync(sessionId, request.Barcode, request.Quantity, request.StorageLocationId))
            .ReturnsAsync(itemId);

        // Act
        var result = await _controller.ScanAdd(sessionId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var response = okResult!.Value;
        response.Should().NotBeNull();

        _mockRepository.Verify(r => r.ScanAddItemAsync(sessionId, request.Barcode, request.Quantity, request.StorageLocationId), Times.Once);
    }

    [Fact]
    public async Task ScanAdd_WithMultipleQuantity_AddsCorrectQuantity()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var request = new ScanAddRequest
        {
            Barcode = "987654321",
            Quantity = 5.0m,
            StorageLocationId = Guid.NewGuid()
        };

        _mockRepository
            .Setup(r => r.GetScanSessionByIdAsync(sessionId))
            .ReturnsAsync(new ScanSessionDto { Id = sessionId, UserId = _testUserId });
        _mockRepository
            .Setup(r => r.ScanAddItemAsync(sessionId, request.Barcode, 5.0m, request.StorageLocationId))
            .ReturnsAsync(itemId);

        // Act
        var result = await _controller.ScanAdd(sessionId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRepository.Verify(r => r.ScanAddItemAsync(sessionId, request.Barcode, 5.0m, request.StorageLocationId), Times.Once);
    }

    [Fact]
    public async Task ScanAdd_WhenRepositoryThrowsInvalidOperation_ReturnsBadRequest()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var request = new ScanAddRequest { Barcode = "BAD", Quantity = 1.0m, StorageLocationId = Guid.NewGuid() };

        _mockRepository
            .Setup(r => r.GetScanSessionByIdAsync(sessionId))
            .ReturnsAsync(new ScanSessionDto { Id = sessionId, UserId = _testUserId });
        _mockRepository
            .Setup(r => r.ScanAddItemAsync(sessionId, request.Barcode, request.Quantity, request.StorageLocationId))
            .ThrowsAsync(new InvalidOperationException("Session not found"));

        // Act
        var result = await _controller.ScanAdd(sessionId, request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region ScanUse Tests

    [Fact]
    public async Task ScanUse_WithValidBarcode_ReturnsOkWithItemId()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var request = new ScanUseRequest
        {
            Barcode = "123456789",
            Quantity = 1.0m
        };

        _mockRepository
            .Setup(r => r.GetScanSessionByIdAsync(sessionId))
            .ReturnsAsync(new ScanSessionDto { Id = sessionId, UserId = _testUserId });
        _mockRepository
            .Setup(r => r.ScanUseItemAsync(sessionId, request.Barcode, request.Quantity))
            .ReturnsAsync(itemId);

        // Act
        var result = await _controller.ScanUse(sessionId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var response = okResult!.Value;
        response.Should().NotBeNull();

        _mockRepository.Verify(r => r.ScanUseItemAsync(sessionId, request.Barcode, request.Quantity), Times.Once);
    }

    [Fact]
    public async Task ScanUse_WithPartialQuantity_UsesCorrectQuantity()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var request = new ScanUseRequest
        {
            Barcode = "987654321",
            Quantity = 0.5m
        };

        _mockRepository
            .Setup(r => r.GetScanSessionByIdAsync(sessionId))
            .ReturnsAsync(new ScanSessionDto { Id = sessionId, UserId = _testUserId });
        _mockRepository
            .Setup(r => r.ScanUseItemAsync(sessionId, request.Barcode, 0.5m))
            .ReturnsAsync(itemId);

        // Act
        var result = await _controller.ScanUse(sessionId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRepository.Verify(r => r.ScanUseItemAsync(sessionId, request.Barcode, 0.5m), Times.Once);
    }

    [Fact]
    public async Task ScanUse_WhenRepositoryThrowsInvalidOperation_ReturnsBadRequest()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var request = new ScanUseRequest { Barcode = "NOTFOUND", Quantity = 1.0m };

        _mockRepository
            .Setup(r => r.GetScanSessionByIdAsync(sessionId))
            .ReturnsAsync(new ScanSessionDto { Id = sessionId, UserId = _testUserId });
        _mockRepository
            .Setup(r => r.ScanUseItemAsync(sessionId, request.Barcode, request.Quantity))
            .ThrowsAsync(new InvalidOperationException("Item not in inventory"));

        // Act
        var result = await _controller.ScanUse(sessionId, request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region ScanDispose Tests

    [Fact]
    public async Task ScanDispose_WithValidBarcode_ReturnsOkWithItemId()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var request = new ScanDisposeRequest
        {
            Barcode = "123456789",
            DisposalReason = "Expired",
            AllergenDetected = null
        };

        _mockRepository
            .Setup(r => r.GetScanSessionByIdAsync(sessionId))
            .ReturnsAsync(new ScanSessionDto { Id = sessionId, UserId = _testUserId });
        _mockRepository
            .Setup(r => r.ScanDisposeItemAsync(sessionId, request.Barcode, request.DisposalReason, request.AllergenDetected))
            .ReturnsAsync(itemId);

        // Act
        var result = await _controller.ScanDispose(sessionId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var response = okResult!.Value;
        response.Should().NotBeNull();

        _mockRepository.Verify(r => r.ScanDisposeItemAsync(sessionId, request.Barcode, request.DisposalReason, request.AllergenDetected), Times.Once);
    }

    [Fact]
    public async Task ScanDispose_WithAllergenDetected_RecordsAllergen()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var request = new ScanDisposeRequest
        {
            Barcode = "987654321",
            DisposalReason = "CausedAllergy",
            AllergenDetected = "Peanuts"
        };

        _mockRepository
            .Setup(r => r.GetScanSessionByIdAsync(sessionId))
            .ReturnsAsync(new ScanSessionDto { Id = sessionId, UserId = _testUserId });
        _mockRepository
            .Setup(r => r.ScanDisposeItemAsync(sessionId, request.Barcode, "CausedAllergy", "Peanuts"))
            .ReturnsAsync(itemId);

        // Act
        var result = await _controller.ScanDispose(sessionId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRepository.Verify(r => r.ScanDisposeItemAsync(sessionId, request.Barcode, "CausedAllergy", "Peanuts"), Times.Once);
    }

    [Fact]
    public async Task ScanDispose_WithBadReason_RecordsCorrectly()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var request = new ScanDisposeRequest
        {
            Barcode = "111222333",
            DisposalReason = "Bad",
            AllergenDetected = null
        };

        _mockRepository
            .Setup(r => r.GetScanSessionByIdAsync(sessionId))
            .ReturnsAsync(new ScanSessionDto { Id = sessionId, UserId = _testUserId });
        _mockRepository
            .Setup(r => r.ScanDisposeItemAsync(sessionId, request.Barcode, "Bad", null))
            .ReturnsAsync(itemId);

        // Act
        var result = await _controller.ScanDispose(sessionId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRepository.Verify(r => r.ScanDisposeItemAsync(sessionId, request.Barcode, "Bad", null), Times.Once);
    }

    [Fact]
    public async Task ScanDispose_WhenRepositoryThrowsInvalidOperation_ReturnsBadRequest()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var request = new ScanDisposeRequest { Barcode = "GONE", DisposalReason = "Missing", AllergenDetected = null };

        _mockRepository
            .Setup(r => r.GetScanSessionByIdAsync(sessionId))
            .ReturnsAsync(new ScanSessionDto { Id = sessionId, UserId = _testUserId });
        _mockRepository
            .Setup(r => r.ScanDisposeItemAsync(sessionId, request.Barcode, request.DisposalReason, request.AllergenDetected))
            .ThrowsAsync(new InvalidOperationException("Item not found"));

        // Act
        var result = await _controller.ScanDispose(sessionId, request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region EndSession Tests

    [Fact]
    public async Task EndSession_WithValidSessionId_ReturnsOk()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.GetScanSessionByIdAsync(sessionId))
            .ReturnsAsync(new ScanSessionDto { Id = sessionId, UserId = _testUserId });
        _mockRepository
            .Setup(r => r.EndScanSessionAsync(sessionId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.EndSession(sessionId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRepository.Verify(r => r.EndScanSessionAsync(sessionId), Times.Once);
    }

    [Fact]
    public async Task EndSession_CalledMultipleTimes_CallsRepositoryEachTime()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.GetScanSessionByIdAsync(sessionId))
            .ReturnsAsync(new ScanSessionDto { Id = sessionId, UserId = _testUserId });
        _mockRepository
            .Setup(r => r.EndScanSessionAsync(sessionId))
            .Returns(Task.CompletedTask);

        // Act
        await _controller.EndSession(sessionId);
        await _controller.EndSession(sessionId);

        // Assert
        _mockRepository.Verify(r => r.EndScanSessionAsync(sessionId), Times.Exactly(2));
    }

    #endregion

    #region Integration Scenario Tests

    [Fact]
    public async Task ScanningWorkflow_AddMultipleItems_UpdatesSessionCorrectly()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var storageLocationId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.GetScanSessionByIdAsync(sessionId))
            .ReturnsAsync(new ScanSessionDto { Id = sessionId, UserId = _testUserId });
        _mockRepository
            .Setup(r => r.ScanAddItemAsync(sessionId, It.IsAny<string>(), It.IsAny<decimal>(), storageLocationId))
            .ReturnsAsync(Guid.NewGuid());

        // Act - Scan 3 items
        await _controller.ScanAdd(sessionId, new ScanAddRequest { Barcode = "111", Quantity = 1, StorageLocationId = storageLocationId });
        await _controller.ScanAdd(sessionId, new ScanAddRequest { Barcode = "222", Quantity = 2, StorageLocationId = storageLocationId });
        await _controller.ScanAdd(sessionId, new ScanAddRequest { Barcode = "333", Quantity = 1, StorageLocationId = storageLocationId });

        // Assert
        _mockRepository.Verify(r => r.ScanAddItemAsync(sessionId, It.IsAny<string>(), It.IsAny<decimal>(), storageLocationId), Times.Exactly(3));
    }

    #endregion
}
