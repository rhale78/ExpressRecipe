using ExpressRecipe.Shared.DTOs.User;
using ExpressRecipe.UserService.Controllers;
using ExpressRecipe.UserService.Data;
using ExpressRecipe.UserService.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ExpressRecipe.UserService.Tests.Controllers;

public class AdminUserControllerTests
{
    private readonly Mock<IUserProfileRepository> _mockUsers;
    private readonly Mock<ISubscriptionRepository> _mockSubs;
    private readonly Mock<IPointsRepository> _mockPoints;
    private readonly Mock<IAuditRepository> _mockAudit;
    private readonly Mock<IConfiguration> _mockConfig;
    private readonly Mock<ILogger<AdminUserController>> _mockLogger;
    private readonly AdminUserController _controller;
    private readonly Guid _adminUserId;
    private readonly Guid _targetUserId;

    public AdminUserControllerTests()
    {
        _mockUsers = new Mock<IUserProfileRepository>();
        _mockSubs = new Mock<ISubscriptionRepository>();
        _mockPoints = new Mock<IPointsRepository>();
        _mockAudit = new Mock<IAuditRepository>();
        _mockConfig = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<AdminUserController>>();

        _controller = new AdminUserController(
            _mockUsers.Object,
            _mockSubs.Object,
            _mockPoints.Object,
            _mockAudit.Object,
            _mockConfig.Object,
            _mockLogger.Object);

        _adminUserId = Guid.NewGuid();
        _targetUserId = Guid.NewGuid();
        ControllerTestHelpers.SetupControllerContextWithRole(_controller, _adminUserId, "Admin");
    }

    // ──────────────────────── GetUserDetail ─────────────────────────────────

    [Fact]
    public async Task GetUserDetail_WhenUserExists_ReturnsOkWithDetail()
    {
        // Arrange
        var profile = new UserProfileDto { Id = Guid.NewGuid(), UserId = _targetUserId };
        _mockUsers.Setup(r => r.GetByUserIdAsync(_targetUserId)).ReturnsAsync(profile);
        _mockSubs.Setup(r => r.GetUserSubscriptionAsync(_targetUserId)).ReturnsAsync((UserSubscriptionDto?)null);
        _mockPoints.Setup(r => r.GetUserPointsSummaryAsync(_targetUserId)).ReturnsAsync(new UserPointsSummaryDto());

        // Act
        var result = await _controller.GetUserDetail(_targetUserId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetUserDetail_WhenUserNotFound_ReturnsNotFound()
    {
        // Arrange
        _mockUsers.Setup(r => r.GetByUserIdAsync(_targetUserId)).ReturnsAsync((UserProfileDto?)null);

        // Act
        var result = await _controller.GetUserDetail(_targetUserId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ──────────────────────── GrantSubscriptionCredit ───────────────────────

    [Fact]
    public async Task GrantSubscriptionCredit_AsAdmin_ReturnsNoContentAndAuditLogs()
    {
        // Arrange
        var req = new SubscriptionCreditRequest { Tier = "Plus", DurationDays = 30, Reason = "Test credit" };
        var creditId = Guid.NewGuid();

        _mockSubs.Setup(r => r.GrantCreditAsync(_targetUserId, req.Tier, req.DurationDays, req.Reason,
                                                  _adminUserId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(creditId);
        _mockAudit.Setup(r => r.LogAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(),
                                          It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                  .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.GrantSubscriptionCredit(_targetUserId, req, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _mockAudit.Verify(r => r.LogAsync(_adminUserId, "SubscriptionCredit", _targetUserId,
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ──────────────────────── Suspend ───────────────────────────────────────

    [Fact]
    public async Task Suspend_WhenUserExists_ReturnsNoContentAndAuditLogs()
    {
        // Arrange
        var req = new SuspendRequest { Reason = "Policy violation" };
        _mockUsers.Setup(r => r.SetSuspendedAsync(_targetUserId, true, _adminUserId)).ReturnsAsync(true);
        _mockAudit.Setup(r => r.LogAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(),
                                          It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                  .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Suspend(_targetUserId, req, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _mockAudit.Verify(r => r.LogAsync(_adminUserId, "UserSuspended", _targetUserId,
            req.Reason, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Suspend_WhenUserNotFound_ReturnsNotFound()
    {
        // Arrange
        var req = new SuspendRequest { Reason = "Unknown user" };
        _mockUsers.Setup(r => r.SetSuspendedAsync(_targetUserId, true, _adminUserId)).ReturnsAsync(false);

        // Act
        var result = await _controller.Suspend(_targetUserId, req, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ──────────────────────── GetAuditHistory ───────────────────────────────

    [Fact]
    public async Task GetAuditHistory_ReturnsOkWithEntries()
    {
        // Arrange
        var entries = new List<AuditLogEntry>
        {
            new() { Id = Guid.NewGuid(), ActorId = _adminUserId, Action = "UserSuspended", TargetId = _targetUserId }
        };
        _mockAudit.Setup(r => r.GetByTargetAsync(_targetUserId, 100, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(entries);

        // Act
        var result = await _controller.GetAuditHistory(_targetUserId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(entries);
    }
}
