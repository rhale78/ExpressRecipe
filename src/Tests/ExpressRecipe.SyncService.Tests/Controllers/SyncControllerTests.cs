using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ExpressRecipe.SyncService.Controllers;
using ExpressRecipe.SyncService.Data;
using ExpressRecipe.SyncService.Tests.Helpers;

namespace ExpressRecipe.SyncService.Tests.Controllers;

public class SyncControllerTests
{
    private readonly Mock<ISyncRepository> _mockRepository;
    private readonly Mock<ILogger<SyncController>> _mockLogger;
    private readonly SyncController _controller;
    private readonly Guid _testUserId;
    private readonly Guid _testDeviceId;

    public SyncControllerTests()
    {
        _mockRepository = new Mock<ISyncRepository>();
        _mockLogger = new Mock<ILogger<SyncController>>();
        _controller = new SyncController(_mockLogger.Object, _mockRepository.Object);
        _testUserId = Guid.NewGuid();
        _testDeviceId = Guid.NewGuid();
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);
    }

    #region RegisterDevice Tests

    [Fact]
    public async Task RegisterDevice_WhenAuthenticated_ReturnsOkWithDeviceId()
    {
        // Arrange
        var request = new RegisterDeviceRequest
        {
            DeviceName = "My Phone",
            DeviceType = "Mobile",
            OsVersion = "Android 14",
            AppVersion = "1.0.0"
        };
        var deviceId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.RegisterDeviceAsync(_testUserId, request.DeviceName, request.DeviceType, request.OsVersion, request.AppVersion))
            .ReturnsAsync(deviceId);

        // Act
        var result = await _controller.RegisterDevice(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRepository.Verify(r => r.RegisterDeviceAsync(_testUserId, request.DeviceName, request.DeviceType, request.OsVersion, request.AppVersion), Times.Once);
    }

    [Fact]
    public async Task RegisterDevice_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();
        var request = new RegisterDeviceRequest { DeviceName = "Phone", DeviceType = "Mobile", OsVersion = "iOS 17", AppVersion = "1.0.0" };

        // Act
        var result = await _controller.RegisterDevice(request);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
        _mockRepository.Verify(r => r.RegisterDeviceAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region GetDevices Tests

    [Fact]
    public async Task GetDevices_WhenAuthenticated_ReturnsUserDevices()
    {
        // Arrange
        var devices = new List<DeviceRegistrationDto>
        {
            new DeviceRegistrationDto { Id = _testDeviceId, UserId = _testUserId, DeviceName = "My Phone", DeviceType = "Mobile" },
            new DeviceRegistrationDto { Id = Guid.NewGuid(), UserId = _testUserId, DeviceName = "My Laptop", DeviceType = "Desktop" }
        };

        _mockRepository
            .Setup(r => r.GetUserDevicesAsync(_testUserId))
            .ReturnsAsync(devices);

        // Act
        var result = await _controller.GetDevices();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        (okResult!.Value as List<DeviceRegistrationDto>)!.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetDevices_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        // Act
        var result = await _controller.GetDevices();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    #endregion

    #region PushChanges Tests

    [Fact]
    public async Task PushChanges_WhenAuthenticated_ReturnsSyncedCount()
    {
        // Arrange
        var syncId = Guid.NewGuid();
        var change = new SyncChange
        {
            EntityType = "Recipe",
            EntityId = Guid.NewGuid(),
            Version = 1,
            Operation = "Update",
            Data = "{}",
            ClientTimestamp = DateTime.UtcNow
        };
        var request = new PushChangesRequest
        {
            DeviceId = _testDeviceId,
            Changes = new List<SyncChange> { change }
        };

        _mockRepository
            .Setup(r => r.CreateSyncMetadataAsync(_testUserId, request.DeviceId, change.EntityType, change.EntityId, change.Version, change.Operation, change.Data, change.ClientTimestamp))
            .ReturnsAsync(syncId);

        // Act
        var result = await _controller.PushChanges(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRepository.Verify(r => r.CreateSyncMetadataAsync(_testUserId, request.DeviceId, change.EntityType, change.EntityId, change.Version, change.Operation, change.Data, change.ClientTimestamp), Times.Once);
    }

    [Fact]
    public async Task PushChanges_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();
        var request = new PushChangesRequest { DeviceId = _testDeviceId, Changes = new List<SyncChange>() };

        // Act
        var result = await _controller.PushChanges(request);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    #endregion

    #region PullChanges Tests

    [Fact]
    public async Task PullChanges_WhenAuthenticated_ReturnsPendingChanges()
    {
        // Arrange
        var since = DateTime.UtcNow.AddHours(-1);
        var changes = new List<SyncMetadataDto>
        {
            new SyncMetadataDto { Id = Guid.NewGuid(), EntityType = "Recipe", Operation = "Update" }
        };

        _mockRepository
            .Setup(r => r.GetPendingSyncsAsync(_testUserId, _testDeviceId, since))
            .ReturnsAsync(changes);

        // Act
        var result = await _controller.PullChanges(_testDeviceId, since);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRepository.Verify(r => r.GetPendingSyncsAsync(_testUserId, _testDeviceId, since), Times.Once);
    }

    [Fact]
    public async Task PullChanges_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        // Act
        var result = await _controller.PullChanges(_testDeviceId, DateTime.UtcNow);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    #endregion
}
