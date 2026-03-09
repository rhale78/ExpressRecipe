using FluentAssertions;
using ExpressRecipe.InventoryService.Data;
using ExpressRecipe.InventoryService.Services;
using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.Shared.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;

namespace ExpressRecipe.InventoryService.Tests.Services;

/// <summary>
/// Unit tests for MealDelayStorageSubscriber filtering and notification logic.
/// </summary>
public class MealDelayStorageSubscriberTests
{
    private static MealDelayStorageSubscriber CreateSubscriber(
        IInventoryStorageReminderQuery query,
        IStorageLocationExtendedRepository storage,
        HttpClient? notificationClient = null)
    {
        Mock<IHttpClientFactory> httpFactory = new Mock<IHttpClientFactory>();
        httpFactory
            .Setup(f => f.CreateClient("notificationservice"))
            .Returns(notificationClient ?? CreateFakeHttpClient(HttpStatusCode.OK));

        ServiceCollection services = new ServiceCollection();
        services.AddScoped<IInventoryStorageReminderQuery>(_ => query);
        services.AddScoped<IStorageLocationExtendedRepository>(_ => storage);
        ServiceProvider sp = services.BuildServiceProvider();

        // CreateAsyncScope() is an extension method — mock the underlying CreateScope() interface method
        Mock<IServiceScopeFactory> scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(() => sp.CreateScope());

        Mock<IMessageBus> bus = new Mock<IMessageBus>();
        ILogger<MealDelayStorageSubscriber> logger =
            new Mock<ILogger<MealDelayStorageSubscriber>>().Object;

        return new MealDelayStorageSubscriber(bus.Object, scopeFactory.Object, httpFactory.Object, logger);
    }

    private static HttpClient CreateFakeHttpClient(HttpStatusCode status)
    {
        FakeHttpMessageHandler handler = new FakeHttpMessageHandler(status);
        return new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
    }

    [Fact]
    public async Task HandleEvent_MealDelayed3Days_ChickenInFridge_SendsReminder()
    {
        // Arrange
        Guid householdId = Guid.NewGuid();
        DateTime oldDate = DateTime.UtcNow;
        DateTime newDate = oldDate.AddDays(3);

        MealPlanUpdatedEvent evt = new MealPlanUpdatedEvent(
            householdId, Guid.NewGuid(), oldDate, newDate, "Rescheduled", DateTimeOffset.UtcNow);

        PerishableInventoryItem chicken = new PerishableInventoryItem
        {
            Id = Guid.NewGuid(),
            ItemName = "Chicken Breast",
            StorageType = "Refrigerator",
            FoodCategory = "Poultry"
        };

        Mock<IInventoryStorageReminderQuery> query = new Mock<IInventoryStorageReminderQuery>();
        query.Setup(q => q.GetPerishableItemsForRecipeAsync(householdId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<PerishableInventoryItem> { chicken });

        Mock<IStorageLocationExtendedRepository> storage = new Mock<IStorageLocationExtendedRepository>();
        storage.Setup(s => s.SuggestLocationsAsync(householdId, "Frozen", It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<StorageLocationSuggestionDto>
               {
                   new StorageLocationSuggestionDto { Id = Guid.NewGuid(), Name = "Chest Freezer", StorageType = "Freezer", Score = 100 }
               });

        FakeHttpMessageHandler handler = new FakeHttpMessageHandler(HttpStatusCode.OK);
        HttpClient client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

        MealDelayStorageSubscriber subscriber = CreateSubscriber(query.Object, storage.Object, client);

        // Act
        await subscriber.HandleEventAsync(evt, new MessageContext { MessageId = Guid.NewGuid().ToString(), MessageType = nameof(MealPlanUpdatedEvent) }, CancellationToken.None);

        // Assert
        handler.RequestCount.Should().Be(1);
        handler.LastRequestBody.Should().Contain("MealDelayStorageReminder");
        handler.LastRequestBody.Should().Contain("Chicken Breast");
    }

    [Fact]
    public async Task HandleEvent_MealDelayed1Day_NoNotification()
    {
        // Arrange — delta < 2 days, should skip
        Guid householdId = Guid.NewGuid();
        DateTime oldDate = DateTime.UtcNow;
        DateTime newDate = oldDate.AddDays(1);

        MealPlanUpdatedEvent evt = new MealPlanUpdatedEvent(
            householdId, Guid.NewGuid(), oldDate, newDate, "Rescheduled", DateTimeOffset.UtcNow);

        Mock<IInventoryStorageReminderQuery> query = new Mock<IInventoryStorageReminderQuery>();
        Mock<IStorageLocationExtendedRepository> storage = new Mock<IStorageLocationExtendedRepository>();

        FakeHttpMessageHandler handler = new FakeHttpMessageHandler(HttpStatusCode.OK);
        HttpClient client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

        MealDelayStorageSubscriber subscriber = CreateSubscriber(query.Object, storage.Object, client);

        // Act
        await subscriber.HandleEventAsync(evt, new MessageContext { MessageId = Guid.NewGuid().ToString(), MessageType = nameof(MealPlanUpdatedEvent) }, CancellationToken.None);

        // Assert
        handler.RequestCount.Should().Be(0);
        query.Verify(q => q.GetPerishableItemsForRecipeAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleEvent_NoPerishables_NoNotification()
    {
        // Arrange — meal delayed 3 days but no perishables in fridge/counter
        Guid householdId = Guid.NewGuid();
        DateTime oldDate = DateTime.UtcNow;
        DateTime newDate = oldDate.AddDays(3);

        MealPlanUpdatedEvent evt = new MealPlanUpdatedEvent(
            householdId, Guid.NewGuid(), oldDate, newDate, "Rescheduled", DateTimeOffset.UtcNow);

        Mock<IInventoryStorageReminderQuery> query = new Mock<IInventoryStorageReminderQuery>();
        query.Setup(q => q.GetPerishableItemsForRecipeAsync(householdId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<PerishableInventoryItem>());

        Mock<IStorageLocationExtendedRepository> storage = new Mock<IStorageLocationExtendedRepository>();

        FakeHttpMessageHandler handler = new FakeHttpMessageHandler(HttpStatusCode.OK);
        HttpClient client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

        MealDelayStorageSubscriber subscriber = CreateSubscriber(query.Object, storage.Object, client);

        // Act
        await subscriber.HandleEventAsync(evt, new MessageContext { MessageId = Guid.NewGuid().ToString(), MessageType = nameof(MealPlanUpdatedEvent) }, CancellationToken.None);

        // Assert
        handler.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task HandleEvent_PerishableAlreadyInFreezer_NoNotification()
    {
        // Arrange — item is already in Freezer, should not trigger reminder
        Guid householdId = Guid.NewGuid();
        DateTime oldDate = DateTime.UtcNow;
        DateTime newDate = oldDate.AddDays(3);

        MealPlanUpdatedEvent evt = new MealPlanUpdatedEvent(
            householdId, Guid.NewGuid(), oldDate, newDate, "Rescheduled", DateTimeOffset.UtcNow);

        PerishableInventoryItem chicken = new PerishableInventoryItem
        {
            Id = Guid.NewGuid(),
            ItemName = "Chicken Breast",
            StorageType = "Freezer", // already frozen
            FoodCategory = "Poultry"
        };

        Mock<IInventoryStorageReminderQuery> query = new Mock<IInventoryStorageReminderQuery>();
        query.Setup(q => q.GetPerishableItemsForRecipeAsync(householdId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<PerishableInventoryItem> { chicken });

        Mock<IStorageLocationExtendedRepository> storage = new Mock<IStorageLocationExtendedRepository>();

        FakeHttpMessageHandler handler = new FakeHttpMessageHandler(HttpStatusCode.OK);
        HttpClient client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

        MealDelayStorageSubscriber subscriber = CreateSubscriber(query.Object, storage.Object, client);

        // Act
        await subscriber.HandleEventAsync(evt, new MessageContext { MessageId = Guid.NewGuid().ToString(), MessageType = nameof(MealPlanUpdatedEvent) }, CancellationToken.None);

        // Assert — item in Freezer not in ("Refrigerator" or "Counter" or null) filter, so no notification
        handler.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task HandleEvent_NullDates_NoNotification()
    {
        // Arrange — missing dates
        MealPlanUpdatedEvent evt = new MealPlanUpdatedEvent(
            Guid.NewGuid(), Guid.NewGuid(), null, null, "Removed", DateTimeOffset.UtcNow);

        Mock<IInventoryStorageReminderQuery> query = new Mock<IInventoryStorageReminderQuery>();
        Mock<IStorageLocationExtendedRepository> storage = new Mock<IStorageLocationExtendedRepository>();
        FakeHttpMessageHandler handler = new FakeHttpMessageHandler(HttpStatusCode.OK);
        HttpClient client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

        MealDelayStorageSubscriber subscriber = CreateSubscriber(query.Object, storage.Object, client);

        // Act
        await subscriber.HandleEventAsync(evt, new MessageContext { MessageId = Guid.NewGuid().ToString(), MessageType = nameof(MealPlanUpdatedEvent) }, CancellationToken.None);

        // Assert
        handler.RequestCount.Should().Be(0);
    }
}
