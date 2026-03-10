using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ExpressRecipe.NotificationService.Controllers;
using ExpressRecipe.NotificationService.Data;
using ExpressRecipe.NotificationService.Tests.Helpers;

namespace ExpressRecipe.NotificationService.Tests.Controllers;

public class NotificationControllerTests
{
    private readonly Mock<INotificationRepository> _mockRepository;
    private readonly Mock<ILogger<NotificationController>> _mockLogger;
    private readonly NotificationController _controller;
    private readonly Guid _testUserId;

    public NotificationControllerTests()
    {
        _mockRepository = new Mock<INotificationRepository>();
        _mockLogger = new Mock<ILogger<NotificationController>>();
        _controller = new NotificationController(_mockLogger.Object, _mockRepository.Object);
        _testUserId = Guid.NewGuid();
        _controller.ControllerContext = ControllerTestHelpers.CreateAuthenticatedContext(_testUserId);
    }

    // ──────────── GetNotifications ────────────

    [Fact]
    public async Task GetNotifications_WhenAuthenticated_ReturnsOk()
    {
        var notifications = new List<NotificationDto>
        {
            new() { Id = Guid.NewGuid(), UserId = _testUserId, Title = "Test", Type = "ExpiringItem" }
        };
        _mockRepository.Setup(r => r.GetUserNotificationsAsync(_testUserId, false, 50)).ReturnsAsync(notifications);

        var result = await _controller.GetNotifications();

        result.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)result).Value.Should().Be(notifications);
    }

    [Fact]
    public async Task GetNotifications_WhenUnauthenticated_ReturnsUnauthorized()
    {
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();
        var result = await _controller.GetNotifications();
        result.Should().BeOfType<UnauthorizedResult>();
    }

    // ──────────── GetNotification ────────────

    [Fact]
    public async Task GetNotification_WhenExists_ReturnsOk()
    {
        var notificationId = Guid.NewGuid();
        var notification = new NotificationDto { Id = notificationId, UserId = _testUserId, Title = "Test" };
        _mockRepository.Setup(r => r.GetNotificationAsync(notificationId)).ReturnsAsync(notification);

        var result = await _controller.GetNotification(notificationId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetNotification_WhenNotFound_ReturnsNotFound()
    {
        _mockRepository.Setup(r => r.GetNotificationAsync(It.IsAny<Guid>())).ReturnsAsync((NotificationDto?)null);

        var result = await _controller.GetNotification(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetNotification_WhenOwnedByOtherUser_ReturnsForbid()
    {
        var notificationId = Guid.NewGuid();
        var notification = new NotificationDto { Id = notificationId, UserId = Guid.NewGuid(), Title = "Test" }; // different user
        _mockRepository.Setup(r => r.GetNotificationAsync(notificationId)).ReturnsAsync(notification);

        var result = await _controller.GetNotification(notificationId);

        result.Should().BeOfType<ForbidResult>();
    }

    // ──────────── MarkAsRead ────────────

    [Fact]
    public async Task MarkAsRead_WhenOwnedByUser_ReturnsNoContent()
    {
        var notificationId = Guid.NewGuid();
        var notification = new NotificationDto { Id = notificationId, UserId = _testUserId };
        _mockRepository.Setup(r => r.GetNotificationAsync(notificationId)).ReturnsAsync(notification);
        _mockRepository.Setup(r => r.MarkAsReadAsync(notificationId)).Returns(Task.CompletedTask);

        var result = await _controller.MarkAsRead(notificationId);

        result.Should().BeOfType<NoContentResult>();
        _mockRepository.Verify(r => r.MarkAsReadAsync(notificationId), Times.Once);
    }

    // ──────────── MarkAllAsRead ────────────

    [Fact]
    public async Task MarkAllAsRead_WhenAuthenticated_ReturnsNoContent()
    {
        _mockRepository.Setup(r => r.MarkAllAsReadAsync(_testUserId)).Returns(Task.CompletedTask);

        var result = await _controller.MarkAllAsRead();

        result.Should().BeOfType<NoContentResult>();
        _mockRepository.Verify(r => r.MarkAllAsReadAsync(_testUserId), Times.Once);
    }

    // ──────────── DeleteNotification ────────────

    [Fact]
    public async Task DeleteNotification_WhenOwnedByUser_ReturnsNoContent()
    {
        var notificationId = Guid.NewGuid();
        var notification = new NotificationDto { Id = notificationId, UserId = _testUserId };
        _mockRepository.Setup(r => r.GetNotificationAsync(notificationId)).ReturnsAsync(notification);
        _mockRepository.Setup(r => r.DeleteNotificationAsync(notificationId)).Returns(Task.CompletedTask);

        var result = await _controller.DeleteNotification(notificationId);

        result.Should().BeOfType<NoContentResult>();
        _mockRepository.Verify(r => r.DeleteNotificationAsync(notificationId), Times.Once);
    }

    [Fact]
    public async Task DeleteNotification_WhenOwnedByOtherUser_ReturnsForbid()
    {
        var notificationId = Guid.NewGuid();
        var notification = new NotificationDto { Id = notificationId, UserId = Guid.NewGuid() };
        _mockRepository.Setup(r => r.GetNotificationAsync(notificationId)).ReturnsAsync(notification);

        var result = await _controller.DeleteNotification(notificationId);

        result.Should().BeOfType<ForbidResult>();
        _mockRepository.Verify(r => r.DeleteNotificationAsync(It.IsAny<Guid>()), Times.Never);
    }

    // ──────────── GetUnreadCount ────────────

    [Fact]
    public async Task GetUnreadCount_WhenAuthenticated_ReturnsCount()
    {
        _mockRepository.Setup(r => r.GetUnreadCountAsync(_testUserId)).ReturnsAsync(3);

        var result = await _controller.GetUnreadCount();

        result.Should().BeOfType<OkObjectResult>();
    }

    // ──────────── SearchNotifications (new endpoint) ────────────

    [Fact]
    public async Task SearchNotifications_WithTypeFilter_ReturnsFilteredResults()
    {
        var notifications = new List<NotificationDto>
        {
            new() { Id = Guid.NewGuid(), UserId = _testUserId, Type = "ExpiringItem", IsRead = false },
            new() { Id = Guid.NewGuid(), UserId = _testUserId, Type = "LowStock", IsRead = false }
        };
        // page=1, pageSize=50 → fetches pageSize * page = 50 from repo
        _mockRepository.Setup(r => r.GetUserNotificationsAsync(_testUserId, true, 50)).ReturnsAsync(notifications);

        var result = await _controller.SearchNotifications(new NotificationSearchRequest { Type = "ExpiringItem", IsRead = false, Page = 1, PageSize = 50 });

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SearchNotifications_WhenUnauthenticated_ReturnsUnauthorized()
    {
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        var result = await _controller.SearchNotifications(new NotificationSearchRequest());

        result.Should().BeOfType<UnauthorizedResult>();
    }

    // ──────────── GetSummary (new endpoint) ────────────

    [Fact]
    public async Task GetSummary_WhenAuthenticated_ReturnsSummary()
    {
        var notifications = new List<NotificationDto>
        {
            new() { Id = Guid.NewGuid(), UserId = _testUserId, Type = "ExpiringItem", IsRead = false },
            new() { Id = Guid.NewGuid(), UserId = _testUserId, Type = "ExpiringItem", IsRead = true }
        };
        _mockRepository.Setup(r => r.GetUserNotificationsAsync(_testUserId, false, 500)).ReturnsAsync(notifications);
        _mockRepository.Setup(r => r.GetUnreadCountAsync(_testUserId)).ReturnsAsync(1);

        var result = await _controller.GetSummary();

        result.Should().BeOfType<OkObjectResult>();
    }

    // ──────────── DeleteAllRead (new endpoint) ────────────

    [Fact]
    public async Task DeleteAllRead_WhenAuthenticated_ReturnsNoContent()
    {
        _mockRepository.Setup(r => r.DeleteAllReadAsync(_testUserId)).Returns(Task.CompletedTask);

        var result = await _controller.DeleteAllRead();

        result.Should().BeOfType<NoContentResult>();
        _mockRepository.Verify(r => r.DeleteAllReadAsync(_testUserId), Times.Once);
    }

    // ──────────── MarkNotificationRead (new endpoint) ────────────

    [Fact]
    public async Task MarkNotificationRead_WhenOwnedByUser_ReturnsNoContent()
    {
        var notificationId = Guid.NewGuid();
        var notification = new NotificationDto { Id = notificationId, UserId = _testUserId };
        _mockRepository.Setup(r => r.GetNotificationAsync(notificationId)).ReturnsAsync(notification);
        _mockRepository.Setup(r => r.MarkAsReadAsync(notificationId)).Returns(Task.CompletedTask);

        var result = await _controller.MarkNotificationRead(new MarkNotificationReadRequest { NotificationId = notificationId, IsRead = true });

        result.Should().BeOfType<NoContentResult>();
        _mockRepository.Verify(r => r.MarkAsReadAsync(notificationId), Times.Once);
    }

    [Fact]
    public async Task MarkNotificationRead_WithIsReadFalse_CallsMarkAsUnread()
    {
        var notificationId = Guid.NewGuid();
        var notification = new NotificationDto { Id = notificationId, UserId = _testUserId };
        _mockRepository.Setup(r => r.GetNotificationAsync(notificationId)).ReturnsAsync(notification);
        _mockRepository.Setup(r => r.MarkAsUnreadAsync(notificationId)).Returns(Task.CompletedTask);

        var result = await _controller.MarkNotificationRead(new MarkNotificationReadRequest { NotificationId = notificationId, IsRead = false });

        result.Should().BeOfType<NoContentResult>();
        _mockRepository.Verify(r => r.MarkAsUnreadAsync(notificationId), Times.Once);
        _mockRepository.Verify(r => r.MarkAsReadAsync(It.IsAny<Guid>()), Times.Never);
    }

    // ──────────── GDPR / Permanent delete endpoints ────────────

    [Fact]
    public async Task HardDeleteNotification_WhenOwnedByUser_ReturnsNoContent()
    {
        var notificationId = Guid.NewGuid();
        var notification = new NotificationDto { Id = notificationId, UserId = _testUserId };
        _mockRepository.Setup(r => r.GetNotificationAsync(notificationId)).ReturnsAsync(notification);
        _mockRepository.Setup(r => r.HardDeleteNotificationAsync(notificationId)).Returns(Task.CompletedTask);

        var result = await _controller.HardDeleteNotification(notificationId);

        result.Should().BeOfType<NoContentResult>();
        _mockRepository.Verify(r => r.HardDeleteNotificationAsync(notificationId), Times.Once);
    }

    [Fact]
    public async Task HardDeleteNotification_WhenOwnedByOtherUser_ReturnsForbid()
    {
        var notificationId = Guid.NewGuid();
        var notification = new NotificationDto { Id = notificationId, UserId = Guid.NewGuid() };
        _mockRepository.Setup(r => r.GetNotificationAsync(notificationId)).ReturnsAsync(notification);

        var result = await _controller.HardDeleteNotification(notificationId);

        result.Should().BeOfType<ForbidResult>();
        _mockRepository.Verify(r => r.HardDeleteNotificationAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task HardDeleteAllUserData_WhenAuthenticated_ReturnsNoContent()
    {
        _mockRepository.Setup(r => r.HardDeleteAllUserNotificationsAsync(_testUserId)).Returns(Task.CompletedTask);

        var result = await _controller.HardDeleteAllUserData();

        result.Should().BeOfType<NoContentResult>();
        _mockRepository.Verify(r => r.HardDeleteAllUserNotificationsAsync(_testUserId), Times.Once);
    }

    [Fact]
    public async Task HardDeleteAllUserData_WhenUnauthenticated_ReturnsUnauthorized()
    {
        _controller.ControllerContext = ControllerTestHelpers.CreateUnauthenticatedContext();

        var result = await _controller.HardDeleteAllUserData();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    // ──────────── Preferences ────────────

    [Fact]
    public async Task GetPreferences_WhenAuthenticated_ReturnsPreferences()
    {
        var prefs = new List<NotificationPreferenceDto>
        {
            new() { Id = Guid.NewGuid(), UserId = _testUserId, NotificationType = "ExpiringItem", EmailEnabled = true }
        };
        _mockRepository.Setup(r => r.GetUserPreferencesAsync(_testUserId)).ReturnsAsync(prefs);

        var result = await _controller.GetPreferences();

        result.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)result).Value.Should().Be(prefs);
    }

    [Fact]
    public async Task UpdatePreferences_WithNullPreferences_ReturnsBadRequest()
    {
        var result = await _controller.UpdatePreferences(new UpdatePreferencesRequest { Preferences = null! });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdatePreferences_WithEmptyPreferences_ReturnsBadRequest()
    {
        var result = await _controller.UpdatePreferences(new UpdatePreferencesRequest { Preferences = new List<SavePreferenceRequest>() });

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
