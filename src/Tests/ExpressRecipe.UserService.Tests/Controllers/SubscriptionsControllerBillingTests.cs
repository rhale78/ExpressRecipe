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
    private readonly Mock<ISubscriptionRepository>  _mockSubRepo;
    private readonly Mock<IUserProfileRepository>   _mockProfileRepo;
    private readonly Mock<IPaymentService>          _mockPayment;
    private readonly Mock<ILogger<SubscriptionsController>> _mockLogger;
    private readonly SubscriptionsController _controller;
    private readonly Guid   _testUserId;
    private readonly string _stripeCustomerId = "cus_test123";

    public SubscriptionsControllerBillingTests()
    {
        _mockSubRepo     = new Mock<ISubscriptionRepository>();
        _mockProfileRepo = new Mock<IUserProfileRepository>();
        _mockPayment     = new Mock<IPaymentService>();
        _mockLogger      = new Mock<ILogger<SubscriptionsController>>();

        _controller = new SubscriptionsController(
            _mockSubRepo.Object,
            _mockProfileRepo.Object,
            _mockPayment.Object,
            _mockLogger.Object);

        _testUserId = Guid.NewGuid();
        ControllerTestHelpers.SetupControllerContext(_controller, _testUserId);

        // Default profile with a Stripe customer ID
        _mockProfileRepo
            .Setup(r => r.GetByUserIdAsync(_testUserId))
            .ReturnsAsync(new UserProfileDto { UserId = _testUserId, StripeCustomerId = _stripeCustomerId });
    }

    #region StartCheckout

    [Fact]
    public async Task StartCheckout_WhenAuthenticated_ReturnsOkWithUrl()
    {
        // Arrange
        CheckoutRequest request = new()
        {
            StripePriceId = "price_plus_monthly",
            WithTrial     = false,
            BaseUrl       = "https://example.com"
        };

        string expectedUrl = "https://checkout.stripe.com/session/abc123";
        _mockPayment
            .Setup(p => p.CreateCheckoutSessionAsync(
                _testUserId, request.StripePriceId, request.WithTrial,
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUrl);

        // Act
        IActionResult result = await _controller.StartCheckout(request, CancellationToken.None);

        // Assert
        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(new { url = expectedUrl });
    }

    [Fact]
    public async Task StartCheckout_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        ControllerTestHelpers.SetupUnauthenticatedControllerContext(_controller);

        CheckoutRequest request = new()
        {
            StripePriceId = "price_plus_monthly",
            BaseUrl       = "https://example.com"
        };

        // Act
        IActionResult result = await _controller.StartCheckout(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
        _mockPayment.Verify(
            p => p.CreateCheckoutSessionAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task StartCheckout_WhenPaymentServiceThrows_Returns500()
    {
        // Arrange
        CheckoutRequest request = new()
        {
            StripePriceId = "price_plus_monthly",
            BaseUrl       = "https://example.com"
        };

        _mockPayment
            .Setup(p => p.CreateCheckoutSessionAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Stripe unavailable"));

        // Act
        IActionResult result = await _controller.StartCheckout(request, CancellationToken.None);

        // Assert
        ObjectResult statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(500);
    }

    #endregion

    #region BillingPortal

    [Fact]
    public async Task BillingPortal_WhenAuthenticated_ReturnsOkWithUrl()
    {
        // Arrange
        BillingPortalRequest request = new()
        {
            ReturnUrl = "https://example.com/account"
        };

        string expectedUrl = "https://billing.stripe.com/session/def456";
        _mockPayment
            .Setup(p => p.CreateBillingPortalSessionAsync(
                _testUserId, _stripeCustomerId, request.ReturnUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUrl);

        // Act
        IActionResult result = await _controller.BillingPortal(request, CancellationToken.None);

        // Assert
        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(new { url = expectedUrl });
    }

    [Fact]
    public async Task BillingPortal_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        ControllerTestHelpers.SetupUnauthenticatedControllerContext(_controller);
        BillingPortalRequest request = new() { ReturnUrl = "https://example.com/account" };

        // Act
        IActionResult result = await _controller.BillingPortal(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task BillingPortal_WhenNoStripeCustomer_ReturnsBadRequest()
    {
        // Arrange — profile has no StripeCustomerId
        _mockProfileRepo
            .Setup(r => r.GetByUserIdAsync(_testUserId))
            .ReturnsAsync(new UserProfileDto { UserId = _testUserId, StripeCustomerId = null });

        BillingPortalRequest request = new() { ReturnUrl = "https://example.com/account" };

        // Act
        IActionResult result = await _controller.BillingPortal(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task BillingPortal_WhenPaymentServiceThrows_Returns500()
    {
        // Arrange
        BillingPortalRequest request = new() { ReturnUrl = "https://example.com/account" };

        _mockPayment
            .Setup(p => p.CreateBillingPortalSessionAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Stripe unavailable"));

        // Act
        IActionResult result = await _controller.BillingPortal(request, CancellationToken.None);

        // Assert
        ObjectResult statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(500);
    }

    #endregion
}
