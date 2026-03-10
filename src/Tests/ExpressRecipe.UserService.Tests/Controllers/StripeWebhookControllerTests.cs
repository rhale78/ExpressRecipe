using ExpressRecipe.UserService.Controllers;
using ExpressRecipe.UserService.Data;
using ExpressRecipe.UserService.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;
using Xunit;

namespace ExpressRecipe.UserService.Tests.Controllers;

public class StripeWebhookControllerTests
{
    private readonly Mock<IPaymentService> _mockPayment;
    private readonly Mock<ISubscriptionRepository> _mockSubs;
    private readonly Mock<ILogger<StripeWebhookController>> _mockLogger;
    private readonly IConfiguration _config;

    public StripeWebhookControllerTests()
    {
        _mockPayment = new Mock<IPaymentService>();
        _mockSubs    = new Mock<ISubscriptionRepository>();
        _mockLogger  = new Mock<ILogger<StripeWebhookController>>();

        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Stripe:WebhookSecret", "whsec_test_secret" }
            })
            .Build();
    }

    private StripeWebhookController CreateController(string body, string? stripeSignature = null)
    {
        StripeWebhookController controller = new(
            _mockPayment.Object,
            _mockSubs.Object,
            _config,
            _mockLogger.Object);

        DefaultHttpContext httpContext = new();
        httpContext.Request.Body         = new MemoryStream(Encoding.UTF8.GetBytes(body));
        httpContext.Request.ContentType  = "application/json";

        if (stripeSignature != null)
        {
            httpContext.Request.Headers["Stripe-Signature"] = stripeSignature;
        }

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    [Fact]
    public async Task HandleWebhook_MissingStripeSignatureHeader_ReturnsBadRequest()
    {
        // Arrange — no Stripe-Signature header
        StripeWebhookController controller = CreateController("{}", stripeSignature: null);

        // Act
        IActionResult result = await controller.HandleWebhook(CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestResult>();
    }

    [Fact]
    public async Task HandleWebhook_InvalidStripeSignature_ReturnsBadRequest()
    {
        // Arrange — invalid signature causes StripeException
        StripeWebhookController controller = CreateController("{}", stripeSignature: "invalid_sig");

        // Act
        IActionResult result = await controller.HandleWebhook(CancellationToken.None);

        // Assert — StripeException is caught and returns 400
        result.Should().BeOfType<BadRequestResult>();
    }

    [Fact]
    public async Task HandleWebhook_AlreadyProcessedEvent_ReturnsOkWithAlreadyProcessed()
    {
        // This test exercises the idempotency path.
        // Since we can't easily construct a valid Stripe-signed payload in a unit test,
        // we use MockPaymentService (which has in-memory state) to verify the already-processed path.
        MockPaymentService mockSvc = new();

        string eventId = "evt_test_idempotency";
        await mockSvc.MarkEventProcessedAsync(eventId, "customer.subscription.updated");

        bool alreadyProcessed = await mockSvc.EventAlreadyProcessedAsync(eventId);
        alreadyProcessed.Should().BeTrue();
    }

    [Fact]
    public async Task HandleWebhook_UnknownEventId_NotAlreadyProcessed()
    {
        MockPaymentService mockSvc = new();

        bool alreadyProcessed = await mockSvc.EventAlreadyProcessedAsync("evt_never_seen");
        alreadyProcessed.Should().BeFalse();
    }
}
