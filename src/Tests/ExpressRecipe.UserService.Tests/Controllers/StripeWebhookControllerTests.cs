using ExpressRecipe.UserService.Controllers;
using ExpressRecipe.UserService.Data;
using ExpressRecipe.UserService.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Stripe;
using System.Text;
using Xunit;

namespace ExpressRecipe.UserService.Tests.Controllers;

public class StripeWebhookControllerTests
{
    // Test webhook secret used in all tests
    private const string TestWebhookSecret = "whsec_test_secret";

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
                { "Stripe:WebhookSecret", TestWebhookSecret }
            })
            .Build();
    }

    /// <summary>Creates a minimal Stripe.Event with the given id and type using direct object construction.</summary>
    private static Stripe.Event CreateTestEvent(string id, string type)
    {
        var evt = new Stripe.Event();
        // Use reflection to set the JsonProperty-backed fields since Stripe.Event
        // uses a custom JsonConverter that prevents standard deserialization in tests.
        var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
        typeof(Stripe.Event).GetProperty("Id",   flags)?.SetValue(evt, id);
        typeof(Stripe.Event).GetProperty("Type", flags)?.SetValue(evt, type);
        // Ensure the event has the expected values (guard against property rename)
        if (evt.Id != id || evt.Type != type)
        {
            throw new InvalidOperationException(
                "Failed to set test event Id/Type via reflection. Stripe.net API may have changed.");
        }
        return evt;
    }

    private StripeWebhookController CreateController(
        string body,
        string? stripeSignature = null,
        Func<string, string, string, Stripe.Event>? constructEvent = null)
    {
        // Default constructor delegate: always throw StripeException (simulates invalid sig)
        // so tests that don't provide constructEvent still test the "bad signature" path.
        var eventConstructor = constructEvent
            ?? ((_, _, _) => throw new Stripe.StripeException("test: no valid sig provided"));

        StripeWebhookController controller = new(
            _mockPayment.Object,
            _mockSubs.Object,
            _config,
            _mockLogger.Object,
            eventConstructor);

        DefaultHttpContext httpContext = new();
        httpContext.Request.Body        = new MemoryStream(Encoding.UTF8.GetBytes(body));
        httpContext.Request.ContentType = "application/json";

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
        // Arrange — no Stripe-Signature header; controller should return 400 before even parsing
        StripeWebhookController controller = CreateController("{}", stripeSignature: null);

        // Act
        IActionResult result = await controller.HandleWebhook(CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestResult>();
    }

    [Fact]
    public async Task HandleWebhook_InvalidStripeSignature_ReturnsBadRequest()
    {
        // Arrange — the default constructEvent delegate throws StripeException
        StripeWebhookController controller = CreateController("{}", stripeSignature: "invalid_sig");

        // Act
        IActionResult result = await controller.HandleWebhook(CancellationToken.None);

        // Assert — StripeException is caught and returns 400
        result.Should().BeOfType<BadRequestResult>();
    }

    [Fact]
    public void Constructor_MissingWebhookSecret_ThrowsInvalidOperationException()
    {
        // Arrange — config without webhook secret
        IConfiguration emptyConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act & Assert
        Action act = () => new StripeWebhookController(
            _mockPayment.Object,
            _mockSubs.Object,
            emptyConfig,
            _mockLogger.Object,
            (_, _, _) => throw new NotImplementedException());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Stripe:WebhookSecret*");
    }

    [Fact]
    public async Task HandleWebhook_AlreadyProcessedEvent_ReturnsOkWithAlreadyProcessedStatus()
    {
        // Arrange — inject a mock event constructor that returns a pre-built event
        Stripe.Event testEvent = CreateTestEvent("evt_already_001", EventTypes.CustomerCreated);

        _mockPayment
            .Setup(p => p.EventAlreadyProcessedAsync("evt_already_001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        StripeWebhookController controller = CreateController(
            body: "{}",
            stripeSignature: "sig",
            constructEvent: (_, _, _) => testEvent);

        // Act
        IActionResult result = await controller.HandleWebhook(CancellationToken.None);

        // Assert — returns 200 with already_processed; MarkEventProcessed never called
        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(new { status = "already_processed" });
        _mockPayment.Verify(
            p => p.MarkEventProcessedAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleWebhook_NewUnhandledEvent_ProcessesAndMarksEventProcessed()
    {
        // Arrange — unhandled event type (customer.created) hits the default case
        Stripe.Event testEvent = CreateTestEvent("evt_new_001", EventTypes.CustomerCreated);

        _mockPayment
            .Setup(p => p.EventAlreadyProcessedAsync("evt_new_001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockPayment
            .Setup(p => p.MarkEventProcessedAsync("evt_new_001", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        StripeWebhookController controller = CreateController(
            body: "{}",
            stripeSignature: "sig",
            constructEvent: (_, _, _) => testEvent);

        // Act
        IActionResult result = await controller.HandleWebhook(CancellationToken.None);

        // Assert — returns 200 and records the event as processed
        result.Should().BeOfType<OkResult>();
        _mockPayment.Verify(
            p => p.MarkEventProcessedAsync("evt_new_001", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
