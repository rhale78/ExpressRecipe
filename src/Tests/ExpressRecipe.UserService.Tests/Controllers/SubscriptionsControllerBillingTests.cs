using ExpressRecipe.Shared.DTOs.User;
using ExpressRecipe.UserService.Controllers;
using ExpressRecipe.UserService.Data;
using ExpressRecipe.UserService.Services;
using ExpressRecipe.UserService.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ExpressRecipe.UserService.Tests.Controllers;

public class SubscriptionsControllerBillingTests
{
    private readonly Mock<ISubscriptionRepository> _mockSubRepo;
    private readonly Mock<IPaymentService> _mockPayment;
    private readonly Mock<ILogger<SubscriptionsController>> _mockLogger;
    private readonly SubscriptionsController _controller;
    private readonly Guid _testUserId;

    public SubscriptionsControllerBillingTests()
    {
        _mockSubRepo = new Mock<ISubscriptionRepository>();
        _mockPayment = new Mock<IPaymentService>();
        _mockLogger  = new Mock<ILogger<SubscriptionsController>>();

        _controller = new SubscriptionsController(
            _mockSubRepo.Object,
            _mockPayment.Object,
            _mockLogger.Object);

        _testUserId = Guid.NewGuid();
        ControllerTestHelpers.SetupControllerContext(_controller, _testUserId);
    }

    #region CreateCheckoutSession

    [Fact]
    public async Task CreateCheckoutSession_WhenAuthenticated_ReturnsOkWithUrl()
    {
        // Arrange
        CreateCheckoutSessionRequest request = new()
        {
            StripePriceId = "price_plus_monthly",
            WithTrial     = false,
            SuccessUrl    = "https://example.com/success",
            CancelUrl     = "https://example.com/cancel"
        };

        string expectedUrl = "https://checkout.stripe.com/session/abc123";
        _mockPayment
            .Setup(p => p.CreateCheckoutSessionAsync(
                _testUserId, request.StripePriceId, request.WithTrial,
                request.SuccessUrl, request.CancelUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUrl);

        // Act
        IActionResult result = await _controller.CreateCheckoutSession(request, CancellationToken.None);

        // Assert
        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(new { url = expectedUrl });
    }

    [Fact]
    public async Task CreateCheckoutSession_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        ControllerTestHelpers.SetupUnauthenticatedControllerContext(_controller);

        CreateCheckoutSessionRequest request = new()
        {
            StripePriceId = "price_plus_monthly",
            SuccessUrl    = "https://example.com/success",
            CancelUrl     = "https://example.com/cancel"
        };

        // Act
        IActionResult result = await _controller.CreateCheckoutSession(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
        _mockPayment.Verify(
            p => p.CreateCheckoutSessionAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateCheckoutSession_WhenPaymentServiceThrows_Returns500()
    {
        // Arrange
        CreateCheckoutSessionRequest request = new()
        {
            StripePriceId = "price_plus_monthly",
            SuccessUrl    = "https://example.com/success",
            CancelUrl     = "https://example.com/cancel"
        };

        _mockPayment
            .Setup(p => p.CreateCheckoutSessionAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Stripe unavailable"));

        // Act
        IActionResult result = await _controller.CreateCheckoutSession(request, CancellationToken.None);

        // Assert
        ObjectResult statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(500);
    }

    #endregion

    #region CreateBillingPortalSession

    [Fact]
    public async Task CreateBillingPortalSession_WhenAuthenticated_ReturnsOkWithUrl()
    {
        // Arrange
        BillingPortalSessionRequest request = new()
        {
            ReturnUrl = "https://example.com/account"
        };

        string expectedUrl = "https://billing.stripe.com/session/def456";
        _mockPayment
            .Setup(p => p.CreateBillingPortalSessionAsync(
                _testUserId, request.ReturnUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUrl);

        // Act
        IActionResult result = await _controller.CreateBillingPortalSession(request, CancellationToken.None);

        // Assert
        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(new { url = expectedUrl });
    }

    [Fact]
    public async Task CreateBillingPortalSession_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        ControllerTestHelpers.SetupUnauthenticatedControllerContext(_controller);
        BillingPortalSessionRequest request = new() { ReturnUrl = "https://example.com/account" };

        // Act
        IActionResult result = await _controller.CreateBillingPortalSession(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task CreateBillingPortalSession_WhenNoStripeCustomer_ReturnsBadRequest()
    {
        // Arrange — InvalidOperationException when no StripeCustomerId found
        BillingPortalSessionRequest request = new() { ReturnUrl = "https://example.com/account" };

        _mockPayment
            .Setup(p => p.CreateBillingPortalSessionAsync(
                _testUserId, request.ReturnUrl, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("No Stripe customer ID found for user"));

        // Act
        IActionResult result = await _controller.CreateBillingPortalSession(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateBillingPortalSession_WhenPaymentServiceThrows_Returns500()
    {
        // Arrange
        BillingPortalSessionRequest request = new() { ReturnUrl = "https://example.com/account" };

        _mockPayment
            .Setup(p => p.CreateBillingPortalSessionAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Stripe unavailable"));

        // Act
        IActionResult result = await _controller.CreateBillingPortalSession(request, CancellationToken.None);

        // Assert
        ObjectResult statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(500);
    }

    #endregion
}
