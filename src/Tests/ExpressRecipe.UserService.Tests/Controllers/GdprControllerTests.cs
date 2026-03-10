using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.Messaging.Core.Options;
using ExpressRecipe.UserService.Controllers;
using ExpressRecipe.UserService.Data;
using ExpressRecipe.UserService.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ExpressRecipe.UserService.Tests.Controllers;

public class GdprControllerTests
{
    private readonly Mock<IGdprRepository> _mockGdpr;
    private readonly Mock<IMessageBus> _mockBus;
    private readonly Mock<ILogger<GdprController>> _mockLogger;
    private readonly GdprController _controller;
    private readonly Guid _testUserId;

    public GdprControllerTests()
    {
        _mockGdpr = new Mock<IGdprRepository>();
        _mockBus = new Mock<IMessageBus>();
        _mockLogger = new Mock<ILogger<GdprController>>();

        _controller = new GdprController(_mockGdpr.Object, _mockBus.Object, _mockLogger.Object);

        _testUserId = Guid.NewGuid();
        ControllerTestHelpers.SetupControllerContext(_controller, _testUserId);
    }

    // ──────────────────────── Export ────────────────────────────────────────

    [Fact]
    public async Task RequestExport_WhenAuthenticated_ReturnsOkWithRequestId()
    {
        // Arrange
        var expectedRequestId = Guid.NewGuid();
        _mockGdpr.Setup(r => r.CreateRequestAsync(_testUserId, "Export", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(expectedRequestId);

        // Act
        var result = await _controller.RequestExport(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
        // Verify repository was called
        _mockGdpr.Verify(r => r.CreateRequestAsync(_testUserId, "Export", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestExport_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        ControllerTestHelpers.SetupUnauthenticatedControllerContext(_controller);

        // Act
        var result = await _controller.RequestExport(CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
        _mockGdpr.Verify(r => r.CreateRequestAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ──────────────────────── Delete ────────────────────────────────────────

    [Fact]
    public async Task RequestDelete_WhenAuthenticated_ReturnsOkAndPublishesEvent()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        _mockGdpr.Setup(r => r.CreateRequestAsync(_testUserId, "Delete", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(requestId);
        _mockBus.Setup(b => b.PublishAsync(It.IsAny<ExpressRecipe.Shared.Messages.GdprDeleteEvent>(),
                                            It.IsAny<PublishOptions?>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.RequestDelete(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockBus.Verify(b => b.PublishAsync(
            It.Is<ExpressRecipe.Shared.Messages.GdprDeleteEvent>(e => e.UserId == _testUserId && e.RequestId == requestId),
            It.IsAny<PublishOptions?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestDelete_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        ControllerTestHelpers.SetupUnauthenticatedControllerContext(_controller);

        // Act
        var result = await _controller.RequestDelete(CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
        _mockBus.Verify(b => b.PublishAsync(It.IsAny<IMessage>(), It.IsAny<PublishOptions?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ──────────────────────── Forget ────────────────────────────────────────

    [Fact]
    public async Task RequestAnonymize_WhenAuthenticated_AnonymizesAndPublishesForgetEvent()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        _mockGdpr.Setup(r => r.CreateRequestAsync(_testUserId, "Forget", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(requestId);
        _mockGdpr.Setup(r => r.AnonymizeUserAsync(_testUserId, It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);
        _mockGdpr.Setup(r => r.SetStatusAsync(requestId, "Completed", null, null, It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);
        _mockBus.Setup(b => b.PublishAsync(It.IsAny<ExpressRecipe.Shared.Messages.GdprForgetEvent>(),
                                            It.IsAny<PublishOptions?>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.RequestAnonymize(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockGdpr.Verify(r => r.AnonymizeUserAsync(_testUserId, It.IsAny<CancellationToken>()), Times.Once);
        _mockBus.Verify(b => b.PublishAsync(
            It.Is<ExpressRecipe.Shared.Messages.GdprForgetEvent>(e => e.UserId == _testUserId),
            It.IsAny<PublishOptions?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestAnonymize_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        ControllerTestHelpers.SetupUnauthenticatedControllerContext(_controller);

        // Act
        var result = await _controller.RequestAnonymize(CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
        _mockGdpr.Verify(r => r.AnonymizeUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ──────────────────────── Get requests ──────────────────────────────────

    [Fact]
    public async Task GetMyRequests_WhenAuthenticated_ReturnsOkWithList()
    {
        // Arrange
        var requests = new List<GdprRequestDto>
        {
            new() { Id = Guid.NewGuid(), UserId = _testUserId, RequestType = "Export", Status = "Pending", RequestedAt = DateTime.UtcNow }
        };
        _mockGdpr.Setup(r => r.GetRequestsByUserAsync(_testUserId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(requests);

        // Act
        var result = await _controller.GetMyRequests(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(requests);
    }
}
