using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ExpressRecipe.MealPlanningService.Data;
using ExpressRecipe.MealPlanningService.Workers;
using ExpressRecipe.Messaging.Core.Abstractions;
using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.Messaging.Core.Options;
using ExpressRecipe.Shared.Messages;

namespace ExpressRecipe.MealPlanningService.Tests.Workers;

public class RecipeCookedEventSubscriberTests
{
    private readonly Mock<IMessageBus> _mockBus;
    private readonly Mock<IWorkQueueRepository> _mockQueue;
    private readonly RecipeCookedEventSubscriber _subscriber;
    private readonly Guid _testHouseholdId;

    public RecipeCookedEventSubscriberTests()
    {
        _mockBus        = new Mock<IMessageBus>();
        _mockQueue      = new Mock<IWorkQueueRepository>();
        _testHouseholdId = Guid.NewGuid();
        _subscriber     = new RecipeCookedEventSubscriber(
            _mockBus.Object,
            _mockQueue.Object,
            NullLogger<RecipeCookedEventSubscriber>.Instance);
    }

    private static MessageContext CreateContext() => new MessageContext
    {
        MessageId   = Guid.NewGuid().ToString(),
        MessageType = nameof(RecipeCookedEvent)
    };

    [Fact]
    public async Task HandleAsync_WhenHasRatingIsTrue_DoesNotCreateQueueItem()
    {
        RecipeCookedEvent evt = new(
            RecipeId:         Guid.NewGuid(),
            UserId:           Guid.NewGuid(),
            HouseholdId:      _testHouseholdId,
            Servings:         2,
            CookedAt:         DateTimeOffset.UtcNow,
            CookingHistoryId: Guid.NewGuid())
        {
            HasRating = true
        };

        await _subscriber.HandleAsync(evt, CreateContext(), default);

        _mockQueue.Verify(q => q.UpsertItemAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<DateTime?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenHasRatingIsFalse_CreatesRateRecipeQueueItem()
    {
        Guid cookingHistoryId = Guid.NewGuid();
        RecipeCookedEvent evt = new(
            RecipeId:         Guid.NewGuid(),
            UserId:           Guid.NewGuid(),
            HouseholdId:      _testHouseholdId,
            Servings:         2,
            CookedAt:         DateTimeOffset.UtcNow,
            CookingHistoryId: cookingHistoryId)
        {
            HasRating = false
        };

        _mockQueue
            .Setup(q => q.UpsertItemAsync(
                _testHouseholdId, "RateRecipe", WorkQueuePriority.RateRecipe,
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                cookingHistoryId, "Recipe", It.IsAny<DateTime?>(), default))
            .Returns(Task.CompletedTask);

        await _subscriber.HandleAsync(evt, CreateContext(), default);

        _mockQueue.Verify(q => q.UpsertItemAsync(
            _testHouseholdId, "RateRecipe", WorkQueuePriority.RateRecipe,
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
            cookingHistoryId, "Recipe",
            It.Is<DateTime?>(d => d.HasValue && d.Value > DateTime.UtcNow),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenNoHouseholdId_DoesNotCreateQueueItem()
    {
        RecipeCookedEvent evt = new(
            RecipeId:         Guid.NewGuid(),
            UserId:           Guid.NewGuid(),
            HouseholdId:      null,
            Servings:         1,
            CookedAt:         DateTimeOffset.UtcNow,
            CookingHistoryId: Guid.NewGuid())
        {
            HasRating = false
        };

        await _subscriber.HandleAsync(evt, CreateContext(), default);

        _mockQueue.Verify(q => q.UpsertItemAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<DateTime?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SetsExpiresAtToSevenDaysFromNow()
    {
        Guid cookingHistoryId = Guid.NewGuid();
        RecipeCookedEvent evt = new(
            RecipeId:         Guid.NewGuid(),
            UserId:           Guid.NewGuid(),
            HouseholdId:      _testHouseholdId,
            Servings:         1,
            CookedAt:         DateTimeOffset.UtcNow,
            CookingHistoryId: cookingHistoryId)
        {
            HasRating = false
        };

        _mockQueue
            .Setup(q => q.UpsertItemAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), default))
            .Returns(Task.CompletedTask);

        await _subscriber.HandleAsync(evt, CreateContext(), default);

        _mockQueue.Verify(q => q.UpsertItemAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<Guid?>(), It.IsAny<string?>(),
            It.Is<DateTime?>(d => d.HasValue
                && d.Value >= DateTime.UtcNow.AddDays(6)
                && d.Value <= DateTime.UtcNow.AddDays(8)),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_UsesCookingHistoryIdAsSourceEntityId()
    {
        Guid cookingHistoryId = Guid.NewGuid();
        RecipeCookedEvent evt = new(
            RecipeId:         Guid.NewGuid(),
            UserId:           Guid.NewGuid(),
            HouseholdId:      _testHouseholdId,
            Servings:         1,
            CookedAt:         DateTimeOffset.UtcNow,
            CookingHistoryId: cookingHistoryId)
        {
            HasRating = false
        };

        _mockQueue
            .Setup(q => q.UpsertItemAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                cookingHistoryId, It.IsAny<string?>(), It.IsAny<DateTime?>(), default))
            .Returns(Task.CompletedTask);

        await _subscriber.HandleAsync(evt, CreateContext(), default);

        _mockQueue.Verify(q => q.UpsertItemAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
            cookingHistoryId,  // sourceEntityId = cookingHistoryId for deduplication
            "Recipe",
            It.IsAny<DateTime?>(), default), Times.Once);
    }

    [Fact]
    public async Task StartAsync_SubscribesToBus()
    {
        _mockBus
            .Setup(b => b.SubscribeAsync<RecipeCookedEvent>(
                It.IsAny<Func<RecipeCookedEvent, MessageContext, CancellationToken, Task>>(),
                It.IsAny<SubscribeOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _subscriber.StartAsync(CancellationToken.None);

        _mockBus.Verify(b => b.SubscribeAsync<RecipeCookedEvent>(
            It.IsAny<Func<RecipeCookedEvent, MessageContext, CancellationToken, Task>>(),
            It.IsAny<SubscribeOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
