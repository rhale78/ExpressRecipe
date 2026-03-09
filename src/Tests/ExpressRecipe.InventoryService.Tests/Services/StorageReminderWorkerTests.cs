using FluentAssertions;
using ExpressRecipe.InventoryService.Data;
using ExpressRecipe.InventoryService.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;

namespace ExpressRecipe.InventoryService.Tests.Services;

/// <summary>
/// Unit tests for StorageReminderWorker freezer-burn and outage-safety logic.
/// </summary>
public class StorageReminderWorkerTests
{
    private static StorageReminderWorker CreateWorker(
        IInventoryStorageReminderQuery query,
        HttpClient? notificationClient = null)
    {
        Mock<IHttpClientFactory> httpFactory = new Mock<IHttpClientFactory>();
        httpFactory
            .Setup(f => f.CreateClient("notificationservice"))
            .Returns(notificationClient ?? CreateFakeHttpClient(HttpStatusCode.OK));

        ServiceCollection services = new ServiceCollection();
        services.AddScoped<IInventoryStorageReminderQuery>(_ => query);
        ServiceProvider sp = services.BuildServiceProvider();

        // CreateAsyncScope() is an extension method — mock the underlying CreateScope() interface method
        Mock<IServiceScopeFactory> scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(() => sp.CreateScope());

        ILogger<StorageReminderWorker> logger =
            new Mock<ILogger<StorageReminderWorker>>().Object;

        return new StorageReminderWorker(scopeFactory.Object, httpFactory.Object, logger);
    }

    private static HttpClient CreateFakeHttpClient(HttpStatusCode status)
    {
        FakeHttpMessageHandler handler = new FakeHttpMessageHandler(status);
        return new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
    }

    // ── Freezer burn tests ────────────────────────────────────────────────────

    [Fact]
    public async Task CheckFreezerBurnRisk_ChickenIn190Days_SendsNotification()
    {
        // Arrange
        Guid householdId = Guid.NewGuid();
        FreezerBurnRiskItem chicken = new FreezerBurnRiskItem
        {
            HouseholdId = householdId,
            ItemName = "Chicken Breast",
            LocationName = "Main Freezer",
            DaysInFreezer = 190
        };

        Mock<IInventoryStorageReminderQuery> query = new Mock<IInventoryStorageReminderQuery>();
        query.Setup(q => q.GetFreezerBurnRiskItemsAsync(It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<FreezerBurnRiskItem> { chicken });

        FakeHttpMessageHandler handler = new FakeHttpMessageHandler(HttpStatusCode.OK);
        HttpClient client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

        StorageReminderWorker worker = CreateWorker(query.Object, client);

        // Act
        await worker.CheckFreezerBurnRiskAsync(query.Object, CancellationToken.None);

        // Assert — notification was sent
        handler.RequestCount.Should().Be(1);
        handler.LastRequestBody.Should().Contain("FreezerBurnRisk");
    }

    [Fact]
    public async Task CheckFreezerBurnRisk_ItemIn45Days_NoNotification()
    {
        // Arrange — 45 days in freezer, no matching threshold; DB returns nothing
        Mock<IInventoryStorageReminderQuery> query = new Mock<IInventoryStorageReminderQuery>();
        query.Setup(q => q.GetFreezerBurnRiskItemsAsync(It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<FreezerBurnRiskItem>());

        FakeHttpMessageHandler handler = new FakeHttpMessageHandler(HttpStatusCode.OK);
        HttpClient client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

        StorageReminderWorker worker = CreateWorker(query.Object, client);

        // Act
        await worker.CheckFreezerBurnRiskAsync(query.Object, CancellationToken.None);

        // Assert — no notification
        handler.RequestCount.Should().Be(0);
    }

    // ── Outage safety window tests ────────────────────────────────────────────

    [Fact]
    public async Task CheckOutageSafety_RefrigeratorAt3_1Hours_SendsNotification()
    {
        // Arrange — 75% of 4h = 3h; 3.1h >= 3h → send
        Guid locationId = Guid.NewGuid();
        Guid householdId = Guid.NewGuid();
        OutageStorageLocation outage = new OutageStorageLocation
        {
            LocationId = locationId,
            HouseholdId = householdId,
            LocationName = "Main Fridge",
            StorageType = "Refrigerator",
            OutageType = "PowerOutage",
            OutageStartedAt = DateTime.UtcNow.AddHours(-3.1),
            WarningSent = false
        };

        Mock<IInventoryStorageReminderQuery> query = new Mock<IInventoryStorageReminderQuery>();
        query.Setup(q => q.GetActiveOutagesAsync(It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<OutageStorageLocation> { outage });
        query.Setup(q => q.GetItemCountInStorageAsync(locationId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(5);
        query.Setup(q => q.MarkOutageWarningSentAsync(locationId, It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);

        FakeHttpMessageHandler handler = new FakeHttpMessageHandler(HttpStatusCode.OK);
        HttpClient client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

        StorageReminderWorker worker = CreateWorker(query.Object, client);

        // Act
        await worker.CheckOutageItemSafetyAsync(query.Object, CancellationToken.None);

        // Assert
        handler.RequestCount.Should().Be(1);
        handler.LastRequestBody.Should().Contain("OutageSafetyWarning");
        query.Verify(q => q.MarkOutageWarningSentAsync(locationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CheckOutageSafety_RefrigeratorAt2_9Hours_NoNotification()
    {
        // Arrange — 2.9h < 75% of 4h (3h) → no notification
        Guid locationId = Guid.NewGuid();
        OutageStorageLocation outage = new OutageStorageLocation
        {
            LocationId = locationId,
            HouseholdId = Guid.NewGuid(),
            LocationName = "Main Fridge",
            StorageType = "Refrigerator",
            OutageType = "PowerOutage",
            OutageStartedAt = DateTime.UtcNow.AddHours(-2.9),
            WarningSent = false
        };

        Mock<IInventoryStorageReminderQuery> query = new Mock<IInventoryStorageReminderQuery>();
        query.Setup(q => q.GetActiveOutagesAsync(It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<OutageStorageLocation> { outage });

        FakeHttpMessageHandler handler = new FakeHttpMessageHandler(HttpStatusCode.OK);
        HttpClient client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

        StorageReminderWorker worker = CreateWorker(query.Object, client);

        // Act
        await worker.CheckOutageItemSafetyAsync(query.Object, CancellationToken.None);

        // Assert
        handler.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task CheckOutageSafety_WarningSentAlready_NoSecondNotification()
    {
        // Arrange — warning already sent, should not send again
        Guid locationId = Guid.NewGuid();
        OutageStorageLocation outage = new OutageStorageLocation
        {
            LocationId = locationId,
            HouseholdId = Guid.NewGuid(),
            LocationName = "Chest Freezer",
            StorageType = "Freezer",
            OutageType = "PowerOutage",
            OutageStartedAt = DateTime.UtcNow.AddHours(-30), // well past 75% of 36h = 27h
            WarningSent = true
        };

        Mock<IInventoryStorageReminderQuery> query = new Mock<IInventoryStorageReminderQuery>();
        query.Setup(q => q.GetActiveOutagesAsync(It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<OutageStorageLocation> { outage });

        FakeHttpMessageHandler handler = new FakeHttpMessageHandler(HttpStatusCode.OK);
        HttpClient client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

        StorageReminderWorker worker = CreateWorker(query.Object, client);

        // Act
        await worker.CheckOutageItemSafetyAsync(query.Object, CancellationToken.None);

        // Assert
        handler.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task CheckOutageSafety_OutageClearedThenReset_CanSendAgain()
    {
        // Simulate outage cleared (WarningSent reset to false) then new outage
        Guid locationId = Guid.NewGuid();
        OutageStorageLocation outage = new OutageStorageLocation
        {
            LocationId = locationId,
            HouseholdId = Guid.NewGuid(),
            LocationName = "Main Fridge",
            StorageType = "Refrigerator",
            OutageType = "PowerOutage",
            OutageStartedAt = DateTime.UtcNow.AddHours(-3.5), // past 75% threshold
            WarningSent = false // reset by ClearOutageAsync
        };

        Mock<IInventoryStorageReminderQuery> query = new Mock<IInventoryStorageReminderQuery>();
        query.Setup(q => q.GetActiveOutagesAsync(It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<OutageStorageLocation> { outage });
        query.Setup(q => q.GetItemCountInStorageAsync(locationId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(3);
        query.Setup(q => q.MarkOutageWarningSentAsync(locationId, It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);

        FakeHttpMessageHandler handler = new FakeHttpMessageHandler(HttpStatusCode.OK);
        HttpClient client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

        StorageReminderWorker worker = CreateWorker(query.Object, client);

        // Act
        await worker.CheckOutageItemSafetyAsync(query.Object, CancellationToken.None);

        // Assert — notification sent again after reset
        handler.RequestCount.Should().Be(1);
        query.Verify(q => q.MarkOutageWarningSentAsync(locationId, It.IsAny<CancellationToken>()), Times.Once);
    }
}

/// <summary>Fake HTTP handler to capture outgoing requests.</summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    public int RequestCount { get; private set; }
    public string LastRequestBody { get; private set; } = string.Empty;

    public FakeHttpMessageHandler(HttpStatusCode statusCode)
    {
        _statusCode = statusCode;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        if (request.Content != null)
        {
            LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
        }
        return new HttpResponseMessage(_statusCode);
    }
}
