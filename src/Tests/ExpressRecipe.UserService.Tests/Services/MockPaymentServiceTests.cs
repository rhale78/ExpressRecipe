using ExpressRecipe.UserService.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ExpressRecipe.UserService.Tests.Services;

public class MockPaymentServiceTests
{
    private static MockPaymentService CreateService() =>
        new(NullLogger<MockPaymentService>.Instance);

    [Fact]
    public async Task CreateCheckoutSessionAsync_ReturnsMockUrlWithSuccessUrlBase()
    {
        MockPaymentService svc    = CreateService();
        string successUrl         = "https://example.com/success";
        string priceId            = "price_plus_monthly";

        string url = await svc.CreateCheckoutSessionAsync(
            Guid.NewGuid(), priceId, withTrial: false,
            successUrl, "https://example.com/cancel");

        url.Should().StartWith(successUrl);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_WithTrial_ReturnsMockUrl()
    {
        MockPaymentService svc = CreateService();
        string successUrl      = "https://example.com/done";

        string url = await svc.CreateCheckoutSessionAsync(
            Guid.NewGuid(), "price_premium_yearly", withTrial: true,
            successUrl, "https://example.com/cancel");

        url.Should().StartWith(successUrl);
    }

    [Fact]
    public async Task CreateBillingPortalSessionAsync_ReturnsReturnUrl()
    {
        MockPaymentService svc = CreateService();
        string returnUrl       = "https://example.com/account";
        string customerId      = "cus_mock_123";

        string url = await svc.CreateBillingPortalSessionAsync(Guid.NewGuid(), customerId, returnUrl);

        url.Should().Be(returnUrl);
    }

    [Fact]
    public async Task CancelSubscriptionAsync_DoesNotThrow()
    {
        MockPaymentService svc = CreateService();
        Func<Task> act = () => svc.CancelSubscriptionAsync("sub_mock_123");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EventAlreadyProcessedAsync_NewEvent_ReturnsFalse()
    {
        MockPaymentService svc = CreateService();
        bool result = await svc.EventAlreadyProcessedAsync("evt_new_" + Guid.NewGuid());
        result.Should().BeFalse();
    }

    [Fact]
    public async Task MarkEventProcessedAsync_ThenEventAlreadyProcessed_ReturnsTrue()
    {
        MockPaymentService svc = CreateService();
        string eventId         = "evt_idempotent_" + Guid.NewGuid();

        await svc.MarkEventProcessedAsync(eventId, "customer.subscription.updated");
        bool result = await svc.EventAlreadyProcessedAsync(eventId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task MarkEventProcessedAsync_CalledTwice_DoesNotThrow()
    {
        MockPaymentService svc = CreateService();
        string eventId         = "evt_dup_" + Guid.NewGuid();

        await svc.MarkEventProcessedAsync(eventId, "customer.subscription.created");
        Func<Task> act = () => svc.MarkEventProcessedAsync(eventId, "customer.subscription.created");

        await act.Should().NotThrowAsync();
    }
}
